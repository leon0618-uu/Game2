from __future__ import annotations

import json
from pathlib import Path
from typing import Any

from .audit_logger import utc_timestamp
from .secret_filter import redact


PASS = "PASS"
PENDING_APPROVAL = "PENDING_APPROVAL"
EXTERNAL_BLOCKED = "EXTERNAL_BLOCKED"
MISSING = "MISSING"


LOCAL_REQUIRED_PATHS = [
    ("blocked_task_schema", "schemas/blocked_task.json", "BlockedTaskPackage schema exists."),
    ("intervention_schema", "schemas/intervention_directive.json", "InterventionDirective schema exists."),
    ("approval_request_schema", "schemas/approval_request.json", "ApprovalRequest schema exists."),
    ("approval_decision_schema", "schemas/approval_decision.json", "ApprovalDecision schema exists."),
    ("final_result_schema", "schemas/final_task_result.json", "FinalTaskResult schema exists."),
    ("task_supervisor", "src/task_supervisor.py", "Task Supervisor escalation rules exist."),
    ("context_collector", "src/context_collector.py", "BlockedTaskPackage redaction and validation exists."),
    ("intervention_directive", "src/intervention_directive.py", "Local intervention fallback exists."),
    ("skill_security", "src/skill_security.py", "Skill proposal safety scanner exists."),
    ("approval_request", "src/approval_request.py", "Approval request and decision logic exists."),
    ("final_task_result", "src/final_task_result.py", "Final task result completion gate exists."),
    ("risk_plan", "src/risk_plan.py", "High-risk action plan generation exists."),
    ("risk_executor", "src/risk_executor.py", "High-risk execution gate exists."),
    ("feishu_sender", "src/feishu_sender.py", "Approval-gated Feishu sender exists."),
    ("feishu_decision_parser", "src/feishu_decision_ingest.py", "Local Feishu approval decision parser exists."),
    ("openai_intervention_gate", "src/openai_intervention_gate.py", "Approval-gated OpenAI intervention caller exists."),
    ("openclaw_security_snapshot", "src/openclaw_security_snapshot.py", "Read-only OpenClaw security snapshot exists."),
    ("v2_compliance_audit", "src/v2_compliance_audit.py", "V2.0 requirement-by-requirement compliance audit exists."),
    ("supervisor", "src/supervisor.py", "One-shot and finite supervisor scan exists."),
    ("start_script", "scripts/start-bridge.ps1", "Bridge start helper exists."),
    ("health_script", "scripts/health-check.ps1", "Bridge health helper exists."),
    ("install_script", "scripts/install-service.ps1", "Service install helper exists and is approval gated."),
    ("user_approval_policy", "policies/user_approval_policy.yaml", "User approval policy exists."),
]


PENDING_HIGH_RISK_ITEMS = [
    ("real_feishu_send", "Real Feishu message send must be approved, executed, and verified.", "Requires explicit user approval and a successful Feishu sender result."),
    ("openai_real_call", "OpenAI intervention call must be approved, executed, and verified.", "Requires OPENAI_API_KEY, quota, and an approved OpenAI intervention command."),
    ("skill_apply_install", "OpenClaw Skill apply/install has not been executed.", "Requires approved approval-decision and grey rollout validation."),
    ("persistent_service", "Persistent bridge service or Scheduled Task is not installed.", "Requires user approval before startup behavior changes."),
    ("openclaw_secretrefs", "OpenClaw plaintext secrets have not been migrated to SecretRefs.", "Requires careful OpenClaw config change and user approval."),
    ("command_owner", "OpenClaw command owner has not been configured.", "Requires the trusted operator identity."),
    ("feishu_decision_ingest", "Feishu callback approval decisions can be verified and ingested.", "Requires signed Feishu callback ingestion evidence."),
]

AGENT_WORKTREE_PATHS = [
    ("lead_worktree", "xingyuan-lead worktree exists.", None),
    ("architect_worktree", "xingyuan-architect worktree exists.", "D:/AI-Worktrees/Xingyuan/architect"),
    ("gameplay_worktree", "xingyuan-gameplay worktree exists.", "D:/AI-Worktrees/Xingyuan/gameplay"),
    ("ui_tools_worktree", "xingyuan-ui-tools worktree exists.", "D:/AI-Worktrees/Xingyuan/ui-tools"),
    ("qa_worktree", "xingyuan-qa worktree exists.", "D:/AI-Worktrees/Xingyuan/qa"),
]


def _item(requirement_id: str, requirement: str, status: str, evidence: list[str], next_action: str = "") -> dict[str, Any]:
    return {
        "requirement_id": requirement_id,
        "requirement": requirement,
        "status": status,
        "evidence": evidence,
        "next_action": next_action,
    }


def _startup_fallback_path() -> Path:
    return Path.home() / "AppData/Roaming/Microsoft/Windows/Start Menu/Programs/Startup/XingyuanOpenClawCodexBridge.cmd"


def _openclaw_config_path() -> Path:
    return Path.home() / ".openclaw/openclaw.json"


def _latest_audit_payload(bridge_root: Path | None, event_type: str) -> tuple[Path | None, dict[str, Any] | None]:
    if bridge_root is None:
        return None, None
    audit_dir = bridge_root / "data" / "audit"
    if not audit_dir.exists():
        return None, None
    candidates = sorted(audit_dir.glob(f"*-{event_type}-*.json"), key=lambda path: path.stat().st_mtime, reverse=True)
    for path in candidates:
        try:
            record = json.loads(path.read_text(encoding="utf-8-sig"))
        except (OSError, json.JSONDecodeError):
            continue
        payload = record.get("payload", record)
        if isinstance(payload, dict):
            return path, payload
    return None, None


def _payload_text(payload: dict[str, Any]) -> str:
    return json.dumps(payload, ensure_ascii=False, sort_keys=True)


def _resolved_high_risk_item(requirement_id: str, repo_root: Path | None = None, bridge_root: Path | None = None) -> tuple[str, list[str], str | None]:
    if requirement_id == "real_feishu_send":
        audit_path, payload = _latest_audit_payload(bridge_root, "feishu-send")
        if audit_path and payload and payload.get("executed"):
            result = payload.get("result")
            if isinstance(result, dict) and result.get("returncode") == 0:
                return PASS, [str(audit_path)], None
            text = _payload_text(payload).lower()
            if "scope" in text or "access denied" in text or "99991672" in text:
                return (
                    EXTERNAL_BLOCKED,
                    [str(audit_path)],
                    "Grant the required Feishu app scope and retry the approved real send.",
                )
            return EXTERNAL_BLOCKED, [str(audit_path)], "Resolve the Feishu send failure and retry the approved command."
    if requirement_id == "openai_real_call":
        audit_path, payload = _latest_audit_payload(bridge_root, "openai-intervention")
        if audit_path and payload and payload.get("executed"):
            result = payload.get("result")
            if isinstance(result, dict) and result.get("ok") is True:
                validation_errors = payload.get("intervention_validation_errors")
                if not validation_errors:
                    return PASS, [str(audit_path)], None
            text = _payload_text(payload).lower()
            if "insufficient_quota" in text or "quota" in text or "429" in text:
                return EXTERNAL_BLOCKED, [str(audit_path)], "Resolve OpenAI API quota/billing and retry the approved intervention call."
            return EXTERNAL_BLOCKED, [str(audit_path)], "Resolve the OpenAI API failure and retry the approved intervention call."
    if requirement_id == "feishu_decision_ingest":
        audit_path, payload = _latest_audit_payload(bridge_root, "feishu-decision-ingest")
        if audit_path and payload and payload.get("valid") is True and payload.get("decision_source") == "feishu":
            if payload.get("signature_verified") is True:
                return PASS, [str(audit_path)], None
            return (
                PENDING_APPROVAL,
                [str(audit_path)],
                "Enable Feishu callback signature verification before treating real decision ingestion as complete.",
            )
    if requirement_id == "skill_apply_install" and repo_root is not None:
        skill_path = repo_root / "skills/blocked-task-escalation/SKILL.md"
        if skill_path.exists():
            return PASS, [str(skill_path)], None
    if requirement_id == "persistent_service":
        startup_path = _startup_fallback_path()
        if startup_path.exists():
            return PASS, [str(startup_path)], None
    if requirement_id == "openclaw_secretrefs":
        config_path = _openclaw_config_path()
        if config_path.exists():
            text = config_path.read_text(encoding="utf-8", errors="replace")
            required_refs = [
                "OPENCLAW_GATEWAY_TOKEN",
                "FEISHU_DEFAULT_APP_SECRET",
                "FEISHU_XINGYUAN_APP_SECRET",
            ]
            if all(ref in text for ref in required_refs):
                return PASS, [f"{config_path}: env SecretRefs configured for gateway and Feishu secrets"], None
    if requirement_id == "command_owner":
        config_path = _openclaw_config_path()
        if config_path.exists():
            text = config_path.read_text(encoding="utf-8", errors="replace")
            if '"ownerAllowFrom"' in text:
                return PASS, [f"{config_path}: commands.ownerAllowFrom configured"], None
    return PENDING_APPROVAL, [], None


def build_high_risk_status_items(prefix: str = "", repo_root: Path | None = None, bridge_root: Path | None = None) -> list[dict[str, Any]]:
    items: list[dict[str, Any]] = []
    for requirement_id, requirement, next_action in PENDING_HIGH_RISK_ITEMS:
        status, evidence, resolved_next_action = _resolved_high_risk_item(requirement_id, repo_root=repo_root, bridge_root=bridge_root)
        items.append(_item(f"{prefix}{requirement_id}", requirement, status, evidence, "" if status == PASS else (resolved_next_action or next_action)))
    return items


def build_readiness_audit(repo_root: Path, bridge_root: Path) -> dict[str, Any]:
    items: list[dict[str, Any]] = []
    for requirement_id, relative_path, requirement in LOCAL_REQUIRED_PATHS:
        path = bridge_root / relative_path
        if path.exists():
            items.append(_item(requirement_id, requirement, PASS, [str(path)]))
        else:
            items.append(_item(requirement_id, requirement, MISSING, [], f"Create {relative_path}."))

    for requirement_id, requirement, configured_path in AGENT_WORKTREE_PATHS:
        path = repo_root if configured_path is None else Path(configured_path)
        if path.exists():
            items.append(_item(requirement_id, requirement, PASS, [str(path)]))
        else:
            items.append(_item(requirement_id, requirement, MISSING, [], f"Restore or recreate {path} before agent delegation depends on it."))

    items.extend(build_high_risk_status_items(repo_root=repo_root, bridge_root=bridge_root))

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

    audit = {
        "audit_id": f"READINESS-{utc_timestamp()}",
        "created_at": utc_timestamp(),
        "project": "Xingyuan Covenant",
        "repo_root": str(repo_root),
        "bridge_root": str(bridge_root),
        "overall_status": overall_status,
        "status_counts": status_counts,
        "items": items,
        "sensitive_data_removed": True,
    }
    return redact(audit)


def write_readiness_audit(output_dir: Path, audit: dict[str, Any]) -> Path:
    output_dir.mkdir(parents=True, exist_ok=True)
    safe_id = "".join(ch if ch.isalnum() or ch in ("-", "_") else "-" for ch in audit["audit_id"])
    path = output_dir / f"{utc_timestamp()}-readiness-audit-{safe_id}.json"
    path.write_text(json.dumps(redact(audit), ensure_ascii=False, indent=2), encoding="utf-8")
    return path
