from __future__ import annotations

import json
import subprocess
from pathlib import Path
from typing import Any

from .audit_logger import utc_timestamp
from .readiness_audit import EXTERNAL_BLOCKED, MISSING, PASS, PENDING_APPROVAL, build_high_risk_status_items
from .secret_filter import redact


REQUIREMENTS: list[dict[str, Any]] = [
    {
        "requirement_id": "github_repository_link",
        "requirement": "Repository is linked to the requested GitHub project.",
        "repo_paths": [".git"],
        "remote_contains": "github.com/leon0618-uu/Game2",
    },
    {
        "requirement_id": "v2_document",
        "requirement": "V2.0 collaboration workflow is captured in project docs.",
        "repo_paths": ["Docs/OPENCLAW_CODEX_COLLABORATION.md", "Docs/environment-audit.md"],
    },
    {
        "requirement_id": "ai_team_rules",
        "requirement": "Project AI team roles and OpenClaw dispatch rules are documented.",
        "repo_paths": ["AGENTS.md", "README.md"],
        "terms": {"AGENTS.md": ["xingyuan-lead", "OpenClaw"], "README.md": ["xingyuan-lead", "OpenClaw"]},
    },
    {
        "requirement_id": "openclaw_inspection",
        "requirement": "Bridge can inspect OpenClaw health and task state before intervention.",
        "bridge_paths": ["src/openclaw_client.py", "tests/test_task_intake.py", "tests/test_supervisor.py"],
    },
    {
        "requirement_id": "task_supervisor",
        "requirement": "Task Supervisor escalation rules exist and are tested.",
        "bridge_paths": ["src/task_supervisor.py", "policies/escalation_policy.yaml", "tests/test_task_supervisor.py"],
    },
    {
        "requirement_id": "blocked_task_package",
        "requirement": "BlockedTaskPackage schema, redaction, collection, and tests exist.",
        "bridge_paths": [
            "schemas/blocked_task.json",
            "src/context_collector.py",
            "src/task_intake.py",
            "tests/test_context_collector.py",
            "tests/test_task_intake.py",
        ],
    },
    {
        "requirement_id": "intervention_directive",
        "requirement": "Intervention directive schema, fallback generation, and OpenAI preview gate exist.",
        "bridge_paths": [
            "schemas/intervention_directive.json",
            "src/intervention_directive.py",
            "src/openai_client.py",
            "src/openai_intervention_gate.py",
            "tests/test_intervention_directive.py",
            "tests/test_openai_intervention_gate.py",
        ],
    },
    {
        "requirement_id": "feishu_display_layer",
        "requirement": "Feishu templates, dry-run outbox, sender gate, and local decision parser exist.",
        "bridge_paths": [
            "src/feishu_reporter.py",
            "src/feishu_sender.py",
            "src/feishu_decision_ingest.py",
            "tests/test_feishu_reporter.py",
            "tests/test_feishu_sender.py",
            "tests/test_feishu_decision_ingest.py",
        ],
    },
    {
        "requirement_id": "skill_self_evolution",
        "requirement": "Skill candidate, proposal, safety scan, workshop planning, and install-risk logic exist.",
        "bridge_paths": [
            "src/skill_factory.py",
            "src/skill_proposal.py",
            "src/skill_security.py",
            "src/skill_validator.py",
            "src/skill_workshop.py",
            "policies/install_policy.yaml",
            "tests/test_skill_factory.py",
            "tests/test_skill_proposal.py",
            "tests/test_skill_security.py",
            "tests/test_skill_workshop.py",
        ],
    },
    {
        "requirement_id": "structured_approval_gate",
        "requirement": "Structured approval requests, decisions, policies, and command matching exist.",
        "bridge_paths": [
            "schemas/approval_request.json",
            "schemas/approval_decision.json",
            "src/approval_request.py",
            "src/approval_gate.py",
            "policies/user_approval_policy.yaml",
            "tests/test_approval_request.py",
            "tests/test_risk_executor.py",
        ],
    },
    {
        "requirement_id": "risk_plans",
        "requirement": "Every high-risk pending item has an approval-ready risk plan and execution gate.",
        "bridge_paths": ["src/risk_plan.py", "src/risk_executor.py", "tests/test_risk_plan.py", "tests/test_risk_executor.py"],
    },
    {
        "requirement_id": "completion_standard",
        "requirement": "Final task result gate enforces PASS evidence and caveat statuses.",
        "bridge_paths": ["schemas/final_task_result.json", "src/final_task_result.py", "tests/test_final_task_result.py"],
    },
    {
        "requirement_id": "loop_limits",
        "requirement": "Automatic rework and loop limits are represented in policy and supervisor logic.",
        "bridge_paths": ["policies/escalation_policy.yaml", "src/task_supervisor.py", "tests/test_task_supervisor.py"],
        "terms": {"bridge:policies/escalation_policy.yaml": ["automatic_rework"]},
    },
    {
        "requirement_id": "secret_redaction",
        "requirement": "Secrets are redacted from packages, logs, Feishu messages, and security evidence.",
        "bridge_paths": [
            "src/secret_filter.py",
            "src/openclaw_security_snapshot.py",
            "tests/test_secret_filter.py",
            "tests/test_openclaw_security_snapshot.py",
        ],
    },
    {
        "requirement_id": "agent_worktrees",
        "requirement": "Lead, architect, gameplay, UI/tools, and QA worktrees are present for AI team delegation.",
        "repo_paths": [
            ".",
            "D:/AI-Worktrees/Xingyuan/architect",
            "D:/AI-Worktrees/Xingyuan/gameplay",
            "D:/AI-Worktrees/Xingyuan/ui-tools",
            "D:/AI-Worktrees/Xingyuan/qa",
        ],
    },
    {
        "requirement_id": "persistent_service_gate",
        "requirement": "Persistent bridge startup is represented as an approval-gated script.",
        "bridge_paths": ["scripts/install-service.ps1", "tests/test_scripts_exist.py"],
    },
    {
        "requirement_id": "readiness_audit",
        "requirement": "Read-only readiness audit separates local evidence from approval-pending work.",
        "bridge_paths": ["src/readiness_audit.py", "tests/test_readiness_audit.py"],
    },
]


def _resolve(repo_root: Path, bridge_root: Path, path_text: str, bridge_path: bool) -> Path:
    path = Path(path_text)
    if path.is_absolute():
        return path
    return (bridge_root if bridge_path else repo_root) / path


def _path_result(path: Path) -> tuple[bool, str]:
    return path.exists(), str(path)


def _read_text(path: Path) -> str:
    if not path.exists() or not path.is_file():
        return ""
    return path.read_text(encoding="utf-8", errors="replace")


def _remote_origin(repo_root: Path) -> str:
    try:
        completed = subprocess.run(
            ["git", "remote", "get-url", "origin"],
            cwd=repo_root,
            text=True,
            encoding="utf-8",
            capture_output=True,
            timeout=10,
            check=False,
        )
    except (OSError, subprocess.TimeoutExpired):
        return ""
    if completed.returncode != 0:
        return ""
    return completed.stdout.strip()


def _evaluate_requirement(requirement: dict[str, Any], repo_root: Path, bridge_root: Path) -> dict[str, Any]:
    evidence: list[str] = []
    missing: list[str] = []

    for path_text in requirement.get("repo_paths", []):
        exists, evidence_path = _path_result(_resolve(repo_root, bridge_root, path_text, bridge_path=False))
        (evidence if exists else missing).append(evidence_path)

    for path_text in requirement.get("bridge_paths", []):
        exists, evidence_path = _path_result(_resolve(repo_root, bridge_root, path_text, bridge_path=True))
        (evidence if exists else missing).append(evidence_path)

    for path_text, terms in requirement.get("terms", {}).items():
        if path_text.startswith("bridge:"):
            path = _resolve(repo_root, bridge_root, path_text.removeprefix("bridge:"), bridge_path=True)
        else:
            repo_path = _resolve(repo_root, bridge_root, path_text, bridge_path=False)
            path = repo_path if repo_path.exists() else _resolve(repo_root, bridge_root, path_text, bridge_path=True)
        text = _read_text(path)
        for term in terms:
            if term in text:
                evidence.append(f"{path}: contains {term}")
            else:
                missing.append(f"{path}: missing {term}")

    remote_contains = requirement.get("remote_contains")
    if remote_contains:
        remote = _remote_origin(repo_root)
        if remote_contains in remote:
            evidence.append(f"origin={remote}")
        else:
            missing.append(f"origin does not contain {remote_contains}")

    status = MISSING if missing else PASS
    return {
        "requirement_id": requirement["requirement_id"],
        "requirement": requirement["requirement"],
        "status": status,
        "evidence": evidence,
        "missing": missing,
    }


def build_v2_compliance_audit(repo_root: Path, bridge_root: Path) -> dict[str, Any]:
    items = [_evaluate_requirement(requirement, repo_root, bridge_root) for requirement in REQUIREMENTS]
    for item in build_high_risk_status_items(prefix="pending_", repo_root=repo_root, bridge_root=bridge_root):
        item["missing"] = []
        items.append(item)

    status_counts: dict[str, int] = {}
    for item in items:
        status = str(item["status"])
        status_counts[status] = status_counts.get(status, 0) + 1

    if status_counts.get(MISSING):
        overall_status = "NOT_READY"
    elif status_counts.get(EXTERNAL_BLOCKED):
        overall_status = "BLOCKED_EXTERNAL"
    elif status_counts.get(PENDING_APPROVAL):
        overall_status = "CONDITIONAL_PASS"
    else:
        overall_status = "PASS"

    return redact(
        {
            "audit_id": f"V2COMPLIANCE-{utc_timestamp()}",
            "created_at": utc_timestamp(),
            "project": "Xingyuan Covenant",
            "repo_root": str(repo_root),
            "bridge_root": str(bridge_root),
            "overall_status": overall_status,
            "status_counts": status_counts,
            "items": items,
            "sensitive_data_removed": True,
        }
    )


def write_v2_compliance_audit(output_dir: Path, audit: dict[str, Any]) -> Path:
    output_dir.mkdir(parents=True, exist_ok=True)
    safe_id = "".join(ch if ch.isalnum() or ch in ("-", "_") else "-" for ch in audit["audit_id"])
    path = output_dir / f"{utc_timestamp()}-v2-compliance-audit-{safe_id}.json"
    path.write_text(json.dumps(redact(audit), ensure_ascii=False, indent=2), encoding="utf-8")
    return path
