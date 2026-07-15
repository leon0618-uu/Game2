from __future__ import annotations

import argparse
import json
import os
import platform
import sys
from pathlib import Path
from typing import Any

from .approval_request import (
    build_approval_decision,
    build_approval_request,
    is_approved_decision_for_command,
    validate_approval_decision,
    validate_approval_request,
    write_approval_decision,
    write_approval_outbox,
    write_approval_request,
)
from .approval_bundle_status import build_approval_bundle_status, write_approval_bundle_status
from .approval_bundle_report import build_approval_bundle_report, write_approval_bundle_report
from .approval_bundle_decision import write_bundle_approval_decision
from .approval_bundle_scope import build_scope_out_record, write_all_scope_out_records, write_scope_out_record
from .audit_logger import AuditLogger, utc_timestamp
from .command_runner import run_command
from .context_collector import sanitize_blocked_task_package, validate_blocked_task_package
from .models import TaskSnapshot
from .openclaw_client import OpenClawClient
from .readiness_audit import build_high_risk_status_items, build_readiness_audit, write_readiness_audit
from .risk_plan import RISK_PLAN_TEMPLATES, build_all_risk_plans, build_approval_bundle_manifest, build_risk_plan, write_approval_bundle_manifest, write_risk_plan
from .risk_executor import build_risk_execution_preview, execute_risk_plan, write_risk_execution
from .secret_filter import redact
from .skill_proposal import write_skill_proposal
from .skill_security import scan_skill_proposal
from .skill_workshop import apply_plan, execute_plan, inspect_plan, propose_create_plan
from .feishu_reporter import approval_message, blocked_message, intervention_message, progress_message, skill_message, write_outbox_message
from .feishu_sender import build_feishu_send_preview, execute_feishu_send
from .feishu_decision_ingest import build_feishu_approval_decision, build_feishu_callback_response, validate_feishu_approval_decision, verify_feishu_signature
from .final_task_result import build_final_task_result, validate_final_task_result, write_final_task_result
from .models import SkillCandidateInput
from .openai_client import DEFAULT_MODEL, OpenAIClient
from .openai_intervention_gate import build_openai_intervention_preview, execute_openai_intervention
from .openclaw_security_snapshot import build_openclaw_security_snapshot, write_openclaw_security_snapshot
from .intervention_directive import build_fallback_intervention, validate_intervention_directive
from .task_supervisor import evaluate_task
from .task_intake import blocked_task_candidates, task_to_blocked_package
from .supervisor import SupervisorOptions, SupervisorPaths, run_supervisor_loop, run_supervisor_scan
from .v2_compliance_audit import build_v2_compliance_audit, write_v2_compliance_audit
from .v2_goal_completion import build_v2_goal_completion_audit, write_v2_goal_completion_audit


DEFAULT_REPO_ROOT = Path(__file__).resolve().parents[2]
DEFAULT_DATA_ROOT = Path(__file__).resolve().parents[1] / "data"
DEFAULT_APPROVAL_ROOT = DEFAULT_DATA_ROOT / "approvals"


def _run_text(command: list[str], cwd: Path) -> str:
    completed = run_command(command, cwd=cwd, timeout_seconds=30)
    output = (completed.stdout or completed.stderr).strip()
    return output if output else "unknown"


def current_environment(repo_root: Path) -> dict[str, str]:
    return {
        "os": f"{platform.system()} {platform.release()}",
        "unity_version": _read_unity_version(repo_root),
        "dotnet_version": _run_text(["dotnet", "--version"], repo_root),
        "openclaw_version": _run_text(["openclaw", "--version"], repo_root),
    }


def _read_unity_version(repo_root: Path) -> str:
    version_file = repo_root / "ProjectSettings" / "ProjectVersion.txt"
    if not version_file.exists():
        return "unknown"
    for line in version_file.read_text(encoding="utf-8").splitlines():
        if line.startswith("m_EditorVersion:"):
            return line.split(":", 1)[1].strip()
    return "unknown"


def build_blocked_package(args: argparse.Namespace) -> dict[str, Any]:
    repo_root = Path(args.repo_root).resolve()
    raw = {
        "task_id": args.task_id,
        "project": args.project,
        "branch": _run_text(["git", "branch", "--show-current"], repo_root),
        "source_agent": args.source_agent,
        "task_status": args.task_status,
        "goal": args.goal,
        "acceptance_criteria": args.acceptance_criteria or [],
        "current_problem": args.current_problem,
        "first_error_time": args.first_error_time,
        "last_progress_time": args.last_progress_time,
        "attempt_count": args.attempt_count,
        "attempted_solutions": args.attempted_solutions or [],
        "commands_executed": args.commands_executed or [],
        "error_logs": args.error_logs or [],
        "stack_trace": args.stack_trace or [],
        "git_diff_summary": _run_text(["git", "diff", "--stat"], repo_root),
        "changed_files": _run_text(["git", "status", "--short"], repo_root).splitlines(),
        "environment": current_environment(repo_root),
        "agent_conclusion": args.agent_conclusion,
        "agent_confidence": args.agent_confidence,
        "requested_help": args.requested_help,
        "screenshots": args.screenshots or [],
        "sensitive_data_removed": False,
    }
    return sanitize_blocked_task_package(raw)


def command_health(args: argparse.Namespace) -> int:
    client = OpenClawClient(Path(args.repo_root).resolve())
    health = client.parsed_health()
    summary = {
        "ok": health.get("ok"),
        "defaultAgentId": health.get("defaultAgentId"),
        "agents": [agent.get("agentId") for agent in health.get("agents", [])],
        "channels": list((health.get("channels") or {}).keys()),
    }
    if args.audit:
        path = AuditLogger(Path(args.audit_dir)).write_event("openclaw-health", summary)
        print(path)
    else:
        print(json.dumps(redact(summary), ensure_ascii=False, indent=2))
    return 0


def command_tasks(args: argparse.Namespace) -> int:
    client = OpenClawClient(Path(args.repo_root).resolve())
    tasks = client.parsed_tasks_list(status=args.status, runtime=args.runtime)
    payload = redact(tasks) if args.full else summarize_tasks(tasks)
    if args.audit:
        path = AuditLogger(Path(args.audit_dir)).write_event("openclaw-tasks", {"tasks": payload})
        print(path)
    else:
        print(json.dumps(payload, ensure_ascii=False, indent=2))
    return 0


def summarize_tasks(tasks_payload: Any) -> dict[str, Any]:
    tasks = tasks_payload.get("tasks", []) if isinstance(tasks_payload, dict) else tasks_payload
    if not isinstance(tasks, list):
        tasks = []
    status_counts: dict[str, int] = {}
    runtime_counts: dict[str, int] = {}
    agent_counts: dict[str, int] = {}
    recent: list[dict[str, Any]] = []
    for task in tasks:
        if not isinstance(task, dict):
            continue
        status = str(task.get("status", "unknown"))
        runtime = str(task.get("runtime", "unknown"))
        agent = str(task.get("agentId", "unknown"))
        status_counts[status] = status_counts.get(status, 0) + 1
        runtime_counts[runtime] = runtime_counts.get(runtime, 0) + 1
        agent_counts[agent] = agent_counts.get(agent, 0) + 1
        if len(recent) < 10:
            recent.append(
                {
                    "taskId": task.get("taskId"),
                    "runtime": runtime,
                    "agentId": agent,
                    "status": status,
                    "label": compact_text(str(task.get("label") or task.get("task") or ""), max_chars=120),
                    "createdAt": task.get("createdAt"),
                    "endedAt": task.get("endedAt"),
                }
            )
    return redact(
        {
            "count": len(tasks),
            "status_counts": status_counts,
            "runtime_counts": runtime_counts,
            "agent_counts": agent_counts,
            "recent": recent,
        }
    )


def compact_text(value: str, max_chars: int = 120) -> str:
    normalized = " ".join(value.split())
    if len(normalized) <= max_chars:
        return normalized
    return f"{normalized[: max_chars - 3]}..."


def command_evaluate(args: argparse.Namespace) -> int:
    snapshot = TaskSnapshot(
        task_id=args.task_id,
        state=args.state,
        source_agent=args.source_agent,
        capability_gap=args.capability_gap,
        missing_tool_or_environment=args.missing_tool_or_environment,
        compile_failed=args.compile_failed,
        test_failed=args.test_failed,
        build_failed=args.build_failed,
        conflicting_agent_conclusions=args.conflicting_agent_conclusions,
        destructive_risk=args.destructive_risk,
        core_rule_change_required=args.core_rule_change_required,
        consecutive_no_progress_checks=args.consecutive_no_progress_checks,
        same_error_count=args.same_error_count,
        same_repair_failure_count=args.same_repair_failure_count,
        plan_cycle_count=args.plan_cycle_count,
        automatic_rework_count=args.automatic_rework_count,
        repeated_file_churn=args.repeated_file_churn,
        flaky_tests=args.flaky_tests,
    )
    decision = evaluate_task(snapshot)
    payload = {"escalate": decision.escalate, "reason": decision.reason.value, "details": decision.details}
    print(json.dumps(payload, ensure_ascii=False, indent=2))
    return 0


def command_blocked_package(args: argparse.Namespace) -> int:
    package = build_blocked_package(args)
    errors = validate_blocked_task_package(package)
    if errors:
        print(json.dumps({"valid": False, "errors": errors}, ensure_ascii=False, indent=2))
        return 2
    output_dir = Path(args.output_dir)
    path = AuditLogger(output_dir).write_event("blocked-task", package, request_id=args.task_id)
    print(path)
    return 0


def command_collect_blocked(args: argparse.Namespace) -> int:
    repo_root = Path(args.repo_root).resolve()
    client = OpenClawClient(repo_root)
    tasks = client.parsed_tasks_list(status=args.status, runtime=args.runtime)
    environment = current_environment(repo_root)
    candidates = blocked_task_candidates(tasks, limit=args.limit)
    incident_logger = AuditLogger(Path(args.output_dir))
    audit_logger = AuditLogger(Path(args.audit_dir))
    written: list[str] = []
    dry_run_messages: list[str] = []
    for candidate in candidates:
        task = candidate["task"]
        package = task_to_blocked_package(task, environment)
        incident_path = incident_logger.write_event("blocked-task", package, request_id=package["task_id"])
        written.append(str(incident_path))
        dry_run_messages.append(
            blocked_message(
                task=package["task_id"],
                blocked_minutes=0,
                problem=package["current_problem"],
                attempts=f"{package['attempt_count']} recorded attempt(s)",
            )
        )
    audit_path = audit_logger.write_event(
        "collect-blocked-summary",
        {"candidate_count": len(candidates), "incident_files": written, "feishu_dry_run_messages": dry_run_messages},
    )
    print(json.dumps({"candidate_count": len(candidates), "incident_files": written, "audit_file": str(audit_path)}, ensure_ascii=False, indent=2))
    return 0


def command_propose_skill(args: argparse.Namespace) -> int:
    candidate = SkillCandidateInput(
        name=args.name,
        same_issue_count=args.same_issue_count,
        successful_workflow_count=args.successful_workflow_count,
        complex_and_reusable=args.complex_and_reusable,
        project_hard_constraint=args.project_hard_constraint,
        could_damage_project=args.could_damage_project,
        repeated_validation_miss=args.repeated_validation_miss,
        shared_handoff_format=args.shared_handoff_format,
        one_off=args.one_off,
        validated=args.validated,
        file_specific_patch_only=args.file_specific_patch_only,
        contains_secret_or_private_data=args.contains_secret_or_private_data,
        unclear_source_scripts=args.unclear_source_scripts,
        overbroad_permissions=args.overbroad_permissions,
        has_validation_method=not args.no_validation_method,
    )
    proposal_dir = write_skill_proposal(candidate, Path(args.output_dir), args.description, args.install_agent)
    print(proposal_dir)
    return 0


def command_intervention(args: argparse.Namespace) -> int:
    package_path = Path(args.package)
    package = json.loads(package_path.read_text(encoding="utf-8"))
    payload = package.get("payload", package)
    directive: dict[str, Any]
    openai_result = None
    if args.call_openai:
        openai_result = OpenAIClient(model=args.model).request_intervention(payload)
    if openai_result and openai_result.ok and openai_result.payload:
        directive = openai_result.payload
    else:
        directive = build_fallback_intervention(payload, reason=args.reason)
        if openai_result and not openai_result.ok:
            directive["root_cause"] = f"OpenAI unavailable: {openai_result.error}. Local fallback directive only."
    errors = validate_intervention_directive(directive)
    if errors:
        print(json.dumps({"valid": False, "errors": errors}, ensure_ascii=False, indent=2))
        return 2
    output_dir = Path(args.output_dir)
    path = AuditLogger(output_dir).write_event("intervention-directive", directive, request_id=directive["task_id"])
    print(path)
    return 0


def command_openai_intervention(args: argparse.Namespace) -> int:
    package_path = Path(args.package)
    package_event = json.loads(package_path.read_text(encoding="utf-8-sig"))
    package = package_event.get("payload", package_event)
    decision = None
    if args.approval_decision:
        decision_event = json.loads(Path(args.approval_decision).read_text(encoding="utf-8-sig"))
        decision = decision_event.get("payload", decision_event)
    if args.execute:
        if decision is None:
            payload = build_openai_intervention_preview(
                package_path=str(package_path),
                package=package,
                model=args.model,
                execute=True,
            )
            payload["approval_reason"] = "approval decision is required for --execute"
        else:
            payload = execute_openai_intervention(
                package_path=str(package_path),
                package=package,
                model=args.model,
                approval_decision=decision,
                timeout_seconds=args.timeout_seconds,
            )
    else:
        payload = build_openai_intervention_preview(package_path=str(package_path), package=package, model=args.model)
    if args.write:
        path = AuditLogger(Path(args.output_dir)).write_event(
            "openai-intervention",
            payload,
            request_id=str(package.get("task_id") or package.get("id") or "openai-intervention"),
        )
        payload["audit_path"] = str(path)
    print(json.dumps(payload, ensure_ascii=False, indent=2))
    if payload.get("blocked"):
        return 2
    result = payload.get("result")
    if isinstance(result, dict) and result.get("ok") is False:
        return 1
    validation_errors = payload.get("intervention_validation_errors")
    if isinstance(validation_errors, list) and validation_errors:
        return 1
    return 0


def command_feishu_dry_run(args: argparse.Namespace) -> int:
    if args.type == "progress":
        message = progress_message(args.task, args.status, args.owner, args.stage, args.text)
    elif args.type == "blocked":
        message = blocked_message(args.task, args.blocked_minutes, args.text, args.attempts)
    elif args.type == "intervention":
        message = intervention_message(args.root_cause, args.text, args.owner, args.verifier, args.confidence)
    elif args.type == "skill":
        message = skill_message(args.skill, args.task, args.owner, args.test_result, args.status)
    elif args.type == "approval":
        message = approval_message(args.task, args.text, args.recommendation, args.risk, args.impact, args.rollback)
    else:
        raise ValueError(f"Unsupported message type: {args.type}")
    path = write_outbox_message(Path(args.output_dir), args.type, message)
    print(path)
    return 0


def command_feishu_send(args: argparse.Namespace) -> int:
    decision = None
    if args.approval_decision:
        decision_event = json.loads(Path(args.approval_decision).read_text(encoding="utf-8"))
        decision = decision_event.get("payload", decision_event)
    if args.execute:
        if decision is None:
            payload = build_feishu_send_preview(target=args.target, message=args.message, account=args.account, execute=True)
            payload["approval_reason"] = "approval decision is required for --execute"
        else:
            payload = execute_feishu_send(
                target=args.target,
                message=args.message,
                account=args.account,
                approval_decision=decision,
                cwd=Path(args.repo_root).resolve(),
                timeout_seconds=args.timeout_seconds,
            )
    else:
        payload = build_feishu_send_preview(target=args.target, message=args.message, account=args.account, execute=False, approval_decision=decision)
    if args.write:
        path = AuditLogger(Path(args.output_dir)).write_event("feishu-send", payload, request_id="feishu-send")
        payload["audit_path"] = str(path)
    print(json.dumps(payload, ensure_ascii=False, indent=2))
    if payload.get("blocked"):
        return 2
    result = payload.get("result")
    if isinstance(result, dict):
        returncode = result.get("returncode")
        if isinstance(returncode, int) and returncode != 0:
            return returncode if returncode > 0 else 1
    return 0


def command_approval_request(args: argparse.Namespace) -> int:
    request = build_approval_request(
        item=args.item,
        reason=args.reason,
        recommendation=args.recommendation,
        risk=args.risk,
        impact=args.impact,
        rollback=args.rollback,
        action_type=args.action_type,
        task_id=args.task_id,
        requested_by=args.requested_by,
        command=args.requested_command or [],
        evidence=args.evidence or [],
    )
    errors = validate_approval_request(request)
    if errors:
        print(json.dumps({"valid": False, "errors": errors}, ensure_ascii=False, indent=2))
        return 2
    approval_path = write_approval_request(Path(args.output_dir), request)
    payload = {"valid": True, "approval_request": str(approval_path)}
    if not args.no_feishu_outbox:
        payload["feishu_outbox"] = str(write_approval_outbox(Path(args.outbox_dir), request))
    print(json.dumps(payload, ensure_ascii=False, indent=2))
    return 0


def command_approval_decision(args: argparse.Namespace) -> int:
    request_path = Path(args.request)
    request_event = json.loads(request_path.read_text(encoding="utf-8"))
    request = request_event.get("payload", request_event)
    decision_record = build_approval_decision(
        request=request,
        decision=args.decision,
        decided_by=args.decided_by,
        notes=args.notes,
    )
    errors = validate_approval_decision(decision_record, request=request)
    if errors:
        print(json.dumps({"valid": False, "errors": errors}, ensure_ascii=False, indent=2))
        return 2
    decision_path = write_approval_decision(Path(args.output_dir), decision_record, request)
    print(json.dumps({"valid": True, "approval_decision": str(decision_path), "decision": args.decision}, ensure_ascii=False, indent=2))
    return 0


def command_feishu_decision_ingest(args: argparse.Namespace) -> int:
    payload_path = Path(args.payload)
    raw_body = payload_path.read_bytes()
    feishu_event = json.loads(raw_body.decode("utf-8-sig"))
    feishu_payload = feishu_event.get("payload", feishu_event)
    if not isinstance(feishu_payload, dict):
        print(json.dumps({"valid": False, "errors": ["feishu payload must be an object"]}, ensure_ascii=False, indent=2))
        return 2

    signature_verified = False
    if args.headers_json or args.require_signature:
        headers = {}
        if args.headers_json:
            headers_event = json.loads(Path(args.headers_json).read_text(encoding="utf-8-sig"))
            headers = {str(key).lower(): str(value) for key, value in headers_event.items()}
        encrypt_key = args.encrypt_key or (os.environ.get(args.encrypt_key_env) if args.encrypt_key_env else "")
        signature_verified = verify_feishu_signature(
            timestamp=headers.get("x-lark-request-timestamp", ""),
            nonce=headers.get("x-lark-request-nonce", ""),
            encrypt_key=encrypt_key or "",
            body=raw_body,
            signature=headers.get("x-lark-signature", ""),
        )
        if not signature_verified:
            print(json.dumps({"valid": False, "errors": ["feishu callback signature verification failed"]}, ensure_ascii=False, indent=2))
            return 2

    callback_response = build_feishu_callback_response(feishu_payload, verification_token=os.environ.get(args.verification_token_env) if args.verification_token_env else None)
    if callback_response is not None:
        if args.write:
            path = AuditLogger(Path(args.audit_dir)).write_event("feishu-decision-ingest", {**callback_response, "signature_verified": signature_verified}, request_id="challenge")
            callback_response["audit_path"] = str(path)
        print(json.dumps(callback_response, ensure_ascii=False, indent=2))
        return 0 if callback_response.get("valid") else 2

    if not args.request:
        print(json.dumps({"valid": False, "errors": ["--request is required for approval decision callbacks"]}, ensure_ascii=False, indent=2))
        return 2
    request_path = Path(args.request)
    request_event = json.loads(request_path.read_text(encoding="utf-8-sig"))
    request = request_event.get("payload", request_event)
    decision_record = build_feishu_approval_decision(request, feishu_payload)
    errors = validate_feishu_approval_decision(request, feishu_payload, decision_record)
    if errors:
        print(json.dumps({"valid": False, "errors": errors}, ensure_ascii=False, indent=2))
        return 2
    decision_path = write_approval_decision(Path(args.output_dir), decision_record, request)
    payload = {
        "valid": True,
        "approval_decision": str(decision_path),
        "decision": decision_record["decision"],
        "request_id": decision_record["request_id"],
        "decision_source": "feishu",
        "signature_verified": signature_verified,
    }
    if args.write:
        path = AuditLogger(Path(args.audit_dir)).write_event("feishu-decision-ingest", payload, request_id=decision_record["request_id"])
        payload["audit_path"] = str(path)
    print(json.dumps(payload, ensure_ascii=False, indent=2))
    return 0


def command_final_result(args: argparse.Namespace) -> int:
    result = build_final_task_result(
        task_id=args.task_id,
        status=args.status,
        summary=args.summary,
        evidence=args.evidence or [],
        qa_result=args.qa_result,
        compile_passed=args.compile_passed,
        tests_passed=args.tests_passed,
        regression_passed=args.regression_passed,
        severe_new_issue=args.severe_new_issue,
        git_diff_reviewed=args.git_diff_reviewed,
        evidence_archived=args.evidence_archived,
        skill_review_passed=not args.skill_review_failed,
        feishu_summary_sent=args.feishu_summary_sent,
        caveats=args.caveat or [],
        blockers=args.blocker or [],
        next_actions=args.next_action or [],
    )
    errors = validate_final_task_result(result)
    if errors:
        print(json.dumps({"valid": False, "errors": errors}, ensure_ascii=False, indent=2))
        return 2
    path = write_final_task_result(Path(args.output_dir), result)
    print(json.dumps({"valid": True, "final_task_result": str(path), "status": args.status}, ensure_ascii=False, indent=2))
    return 0


def command_readiness_audit(args: argparse.Namespace) -> int:
    repo_root = Path(args.repo_root).resolve()
    bridge_root = Path(args.bridge_root).resolve()
    audit = build_readiness_audit(repo_root, bridge_root)
    if args.write:
        path = write_readiness_audit(Path(args.output_dir), audit)
        print(json.dumps({"readiness_audit": str(path), "overall_status": audit["overall_status"], "status_counts": audit["status_counts"]}, ensure_ascii=False, indent=2))
    else:
        print(json.dumps(audit, ensure_ascii=False, indent=2))
    return 0 if audit["overall_status"] in {"PASS", "CONDITIONAL_PASS"} else 2


def command_compliance_audit(args: argparse.Namespace) -> int:
    repo_root = Path(args.repo_root).resolve()
    bridge_root = Path(args.bridge_root).resolve()
    audit = build_v2_compliance_audit(repo_root, bridge_root)
    if args.write:
        path = write_v2_compliance_audit(Path(args.output_dir), audit)
        print(json.dumps({"v2_compliance_audit": str(path), "overall_status": audit["overall_status"], "status_counts": audit["status_counts"]}, ensure_ascii=False, indent=2))
    else:
        print(json.dumps(audit, ensure_ascii=False, indent=2))
    return 0 if audit["overall_status"] in {"PASS", "CONDITIONAL_PASS"} else 2


def command_goal_completion_audit(args: argparse.Namespace) -> int:
    repo_root = Path(args.repo_root).resolve()
    bridge_root = Path(args.bridge_root).resolve()
    readiness = build_readiness_audit(repo_root, bridge_root)
    compliance = build_v2_compliance_audit(repo_root, bridge_root)
    bundle_status = None
    if args.manifest:
        manifest_path = Path(args.manifest).resolve()
        manifest_event = json.loads(manifest_path.read_text(encoding="utf-8-sig"))
        manifest = manifest_event.get("payload", manifest_event)
        dynamic_items: dict[str, dict[str, Any]] = {}
        if not args.no_dynamic_evidence and manifest_path.is_relative_to(bridge_root):
            dynamic_items = {
                str(item["requirement_id"]): item
                for item in build_high_risk_status_items(repo_root=repo_root, bridge_root=bridge_root)
            }
        bundle_status = build_approval_bundle_status(
            manifest,
            decision_dir=Path(args.decision_dir) if args.decision_dir else None,
            scope_dir=Path(args.scope_dir) if args.scope_dir else None,
            dynamic_items=dynamic_items,
        )
    audit = build_v2_goal_completion_audit(
        readiness_audit=readiness,
        compliance_audit=compliance,
        approval_bundle_status=bundle_status,
    )
    payload: dict[str, Any] = {"goal_completion_audit": audit}
    if args.write:
        payload["goal_completion_audit_file"] = str(write_v2_goal_completion_audit(Path(args.output_dir), audit))
    print(json.dumps(payload, ensure_ascii=False, indent=2))
    return 0 if audit["complete"] else 2


def _latest_approval_bundle_manifest(bridge_root: Path) -> Path | None:
    audit_dir = bridge_root / "data" / "audit"
    if not audit_dir.exists():
        return None
    matches = sorted(audit_dir.glob("*-approval-bundle-APPROVALBUNDLE-*.json"), key=lambda path: path.stat().st_mtime, reverse=True)
    return matches[0] if matches else None


def _write_v2_unblock_report(output_dir: Path, payload: dict[str, Any]) -> Path:
    output_dir.mkdir(parents=True, exist_ok=True)
    path = output_dir / f"{utc_timestamp()}-v2-unblock-check-report.md"
    lines = [
        "# V2.0 Unblock Check",
        "",
        f"- Overall status: `{payload.get('overall_status', '')}`",
        f"- Complete: `{str(payload.get('complete', False)).lower()}`",
        f"- External blockers: `{payload.get('external_blocker_count', 0)}`",
        "",
        "## Gate Status",
        "",
        f"- Readiness: `{payload.get('readiness', {}).get('overall_status', '')}` {payload.get('readiness', {}).get('status_counts', {})}",
        f"- Compliance: `{payload.get('compliance', {}).get('overall_status', '')}` {payload.get('compliance', {}).get('status_counts', {})}",
        f"- Approval bundle: `{payload.get('approval_bundle', {}).get('overall_status', '')}` {payload.get('approval_bundle', {}).get('status_counts', {})}",
        f"- Goal completion: `{payload.get('goal_completion', {}).get('overall_status', '')}`",
        "",
        "## External Blockers",
        "",
    ]
    blockers = payload.get("external_blockers", [])
    if isinstance(blockers, list) and blockers:
        lines.append("| Requirement | Status | Next action | Evidence |")
        lines.append("| --- | --- | --- | --- |")
        for blocker in blockers:
            if not isinstance(blocker, dict):
                continue
            evidence = "<br>".join(str(item) for item in blocker.get("evidence", []))
            lines.append(
                f"| `{blocker.get('requirement_id', '')}` | `{blocker.get('status', '')}` | {blocker.get('next_action', '')} | {evidence} |"
            )
    else:
        lines.append("No external blockers reported.")
    remediation = payload.get("remediation", {}) if isinstance(payload.get("remediation"), dict) else {}
    lines.extend(
        [
            "",
            "## Remediation",
            "",
            f"- Runbook: `{remediation.get('runbook', '')}`",
            f"- Preview retry: `{remediation.get('preview_retry_command', '')}`",
            f"- Approved retry: `{remediation.get('approved_retry_command', '')}`",
            f"- Completion gate: `{remediation.get('completion_gate_command', '')}`",
            "",
        ]
    )
    path.write_text("\n".join(lines), encoding="utf-8")
    return path


def command_v2_unblock_check(args: argparse.Namespace) -> int:
    repo_root = Path(args.repo_root).resolve()
    bridge_root = Path(args.bridge_root).resolve()
    readiness = build_readiness_audit(repo_root, bridge_root)
    compliance = build_v2_compliance_audit(repo_root, bridge_root)
    high_risk_items = build_high_risk_status_items(repo_root=repo_root, bridge_root=bridge_root)
    external_blockers = [
        {
            "requirement_id": item["requirement_id"],
            "requirement": item["requirement"],
            "status": item["status"],
            "evidence": item.get("evidence", []),
            "next_action": item.get("next_action", ""),
        }
        for item in high_risk_items
        if item.get("status") == "EXTERNAL_BLOCKED"
    ]

    manifest_path = Path(args.manifest).resolve() if args.manifest else _latest_approval_bundle_manifest(bridge_root)
    bundle_status = None
    if manifest_path:
        manifest_event = json.loads(manifest_path.read_text(encoding="utf-8-sig"))
        manifest = manifest_event.get("payload", manifest_event)
        dynamic_items = {str(item["requirement_id"]): item for item in high_risk_items}
        bundle_status = build_approval_bundle_status(
            manifest,
            decision_dir=Path(args.decision_dir) if args.decision_dir else None,
            scope_dir=Path(args.scope_dir) if args.scope_dir else None,
            dynamic_items=dynamic_items,
        )

    completion = build_v2_goal_completion_audit(
        readiness_audit=readiness,
        compliance_audit=compliance,
        approval_bundle_status=bundle_status,
    )
    payload: dict[str, Any] = {
        "overall_status": "COMPLETE" if completion.get("complete") else ("BLOCKED_EXTERNAL" if external_blockers else "NOT_COMPLETE"),
        "complete": completion.get("complete", False),
        "external_blocker_count": len(external_blockers),
        "external_blockers": external_blockers,
        "remediation": {
            "runbook": str(repo_root / "Docs" / "OPENCLAW_EXTERNAL_BLOCKER_RUNBOOK.md"),
            "preview_retry_command": r".\chatgpt-openclaw-bridge\scripts\retry-external-blockers.ps1",
            "approved_retry_command": r".\chatgpt-openclaw-bridge\scripts\retry-external-blockers.ps1 -Execute -Approved",
            "completion_gate_command": r".\chatgpt-openclaw-bridge\scripts\bridge.ps1 goal-completion-audit --manifest chatgpt-openclaw-bridge\data\audit\20260714T163415Z-approval-bundle-APPROVALBUNDLE-20260714T163415Z.json --write",
        },
        "readiness": {"overall_status": readiness["overall_status"], "status_counts": readiness["status_counts"]},
        "compliance": {"overall_status": compliance["overall_status"], "status_counts": compliance["status_counts"]},
        "approval_bundle": {
            "manifest": str(manifest_path) if manifest_path else "",
            "overall_status": bundle_status.get("overall_status", "MISSING") if bundle_status else "MISSING",
            "status_counts": bundle_status.get("status_counts", {}) if bundle_status else {},
        },
        "goal_completion": {
            "overall_status": completion["overall_status"],
            "blockers": completion["blockers"],
        },
        "sensitive_data_removed": True,
    }
    payload = redact(payload)
    if args.write:
        path = AuditLogger(Path(args.output_dir)).write_event("v2-unblock-check", payload, request_id="v2-unblock-check")
        payload["audit_path"] = str(path)
    if args.write_report:
        payload["report_path"] = str(_write_v2_unblock_report(Path(args.output_dir), payload))
    print(json.dumps(payload, ensure_ascii=False, indent=2))
    return 0 if payload.get("complete") else 2


def command_security_snapshot(args: argparse.Namespace) -> int:
    snapshot = build_openclaw_security_snapshot(Path(args.repo_root).resolve(), timeout_seconds=args.timeout_seconds)
    if args.write:
        path = write_openclaw_security_snapshot(Path(args.output_dir), snapshot)
        print(json.dumps({"security_snapshot": str(path), "result_keys": list(snapshot["results"].keys())}, ensure_ascii=False, indent=2))
    else:
        print(json.dumps(snapshot, ensure_ascii=False, indent=2))
    return 0


def command_risk_plan(args: argparse.Namespace) -> int:
    plans = build_all_risk_plans() if args.requirement_id == "all" else [build_risk_plan(args.requirement_id)]
    payload: dict[str, Any] = {"plans": plans}
    risk_plan_files: list[str] = []
    if args.write:
        risk_plan_files = [str(write_risk_plan(Path(args.output_dir), plan)) for plan in plans]
        payload["risk_plan_files"] = risk_plan_files
    approvals: list[dict[str, str]] = []
    if args.write_approval_request:
        for plan in plans:
            request = build_approval_request(
                item=str(plan["item"]),
                reason=str(plan["reason"]),
                recommendation=str(plan["recommendation"]),
                risk=str(plan["risk"]),
                impact=str(plan["impact"]),
                rollback=str(plan["rollback"]),
                action_type=str(plan["action_type"]),
                task_id=args.task_id,
                requested_by=args.requested_by,
                command=list(plan["planned_commands"]),
                evidence=list(plan["required_evidence"]),
            )
            approval_path = write_approval_request(Path(args.approval_dir), request)
            outbox_path = write_approval_outbox(Path(args.outbox_dir), request)
            approvals.append({"approval_request": str(approval_path), "feishu_outbox": str(outbox_path)})
        payload["approval_requests"] = approvals
    if args.write_bundle_manifest:
        if not args.write or not args.write_approval_request:
            print(json.dumps({"valid": False, "errors": ["--write-bundle-manifest requires --write and --write-approval-request"]}, ensure_ascii=False, indent=2))
            return 2
        manifest = build_approval_bundle_manifest(
            plans=plans,
            risk_plan_files=risk_plan_files,
            approval_requests=approvals,
            task_id=args.task_id,
            requested_by=args.requested_by,
        )
        payload["approval_bundle_manifest"] = str(write_approval_bundle_manifest(Path(args.output_dir), manifest))
    print(json.dumps(payload, ensure_ascii=False, indent=2))
    return 0


def command_risk_execute(args: argparse.Namespace) -> int:
    plan_event = json.loads(Path(args.plan).read_text(encoding="utf-8"))
    plan = plan_event.get("payload", plan_event)
    decision = None
    if args.approval_decision:
        decision_event = json.loads(Path(args.approval_decision).read_text(encoding="utf-8"))
        decision = decision_event.get("payload", decision_event)
    if args.execute:
        if decision is None:
            execution = build_risk_execution_preview(plan, None, mode="execute")
            execution["blocked"] = True
            execution["approval_reason"] = "approval decision is required for --execute"
        else:
            execution = execute_risk_plan(plan, decision, cwd=Path(args.repo_root).resolve(), timeout_seconds=args.timeout_seconds)
    else:
        execution = build_risk_execution_preview(plan, decision, mode="preview")
    payload: dict[str, Any] = {"execution": execution}
    if args.write:
        payload["risk_execution"] = str(write_risk_execution(Path(args.output_dir), execution))
    print(json.dumps(payload, ensure_ascii=False, indent=2))
    return 0 if not execution.get("blocked") else 2


def command_approval_bundle_status(args: argparse.Namespace) -> int:
    manifest_path = Path(args.manifest).resolve()
    manifest_event = json.loads(manifest_path.read_text(encoding="utf-8-sig"))
    manifest = manifest_event.get("payload", manifest_event)
    repo_root = Path(args.repo_root).resolve()
    bridge_root = Path(args.bridge_root).resolve()
    dynamic_items: dict[str, dict[str, Any]] = {}
    if not args.no_dynamic_evidence and manifest_path.is_relative_to(bridge_root):
        dynamic_items = {
            str(item["requirement_id"]): item
            for item in build_high_risk_status_items(repo_root=repo_root, bridge_root=bridge_root)
        }
    status = build_approval_bundle_status(
        manifest,
        decision_dir=Path(args.decision_dir) if args.decision_dir else None,
        scope_dir=Path(args.scope_dir) if args.scope_dir else None,
        dynamic_items=dynamic_items,
    )
    payload: dict[str, Any] = {"approval_bundle_status": status}
    if args.write:
        payload["approval_bundle_status_file"] = str(write_approval_bundle_status(Path(args.output_dir), status))
    if args.write_report:
        report = build_approval_bundle_report(status, operator=args.operator, manifest_path=str(Path(args.manifest).resolve()))
        payload["approval_bundle_report"] = str(write_approval_bundle_report(Path(args.output_dir), report, str(status.get("bundle_id", ""))))
    print(json.dumps(payload, ensure_ascii=False, indent=2))
    return 0 if status["overall_status"] not in {"INVALID", "INCOMPLETE"} else 2


def command_approval_bundle_decision(args: argparse.Namespace) -> int:
    manifest_event = json.loads(Path(args.manifest).read_text(encoding="utf-8-sig"))
    manifest = manifest_event.get("payload", manifest_event)
    try:
        decision_path, decision_record, item = write_bundle_approval_decision(
            output_dir=Path(args.output_dir),
            manifest=manifest,
            requirement_id=args.requirement_id,
            decision=args.decision,
            decided_by=args.decided_by,
            notes=args.notes,
        )
    except (KeyError, ValueError, FileNotFoundError) as exc:
        print(json.dumps({"valid": False, "errors": [str(exc)]}, ensure_ascii=False, indent=2))
        return 2
    print(
        json.dumps(
            {
                "valid": True,
                "approval_decision": str(decision_path),
                "decision": decision_record["decision"],
                "requirement_id": item["requirement_id"],
                "approval_bundle_id": decision_record.get("approval_bundle_id", ""),
                "executed": False,
            },
            ensure_ascii=False,
            indent=2,
        )
    )
    return 0


def command_approval_bundle_scope_out(args: argparse.Namespace) -> int:
    manifest_event = json.loads(Path(args.manifest).read_text(encoding="utf-8-sig"))
    manifest = manifest_event.get("payload", manifest_event)
    try:
        record = build_scope_out_record(
            manifest=manifest,
            requirement_id=args.requirement_id,
            scoped_out_by=args.scoped_out_by,
            reason=args.reason,
        )
        path = write_scope_out_record(Path(args.output_dir), record, manifest)
    except (KeyError, ValueError) as exc:
        print(json.dumps({"valid": False, "errors": [str(exc)]}, ensure_ascii=False, indent=2))
        return 2
    print(
        json.dumps(
            {
                "valid": True,
                "scope_out": str(path),
                "requirement_id": record["requirement_id"],
                "approval_bundle_id": record["bundle_id"],
                "external_actions_executed": False,
            },
            ensure_ascii=False,
            indent=2,
        )
    )
    return 0


def command_approval_bundle_scope_out_all(args: argparse.Namespace) -> int:
    if not args.confirm_all:
        print(json.dumps({"valid": False, "errors": ["--confirm-all is required to scope out every approval-bundle item"]}, ensure_ascii=False, indent=2))
        return 2
    manifest_event = json.loads(Path(args.manifest).read_text(encoding="utf-8-sig"))
    manifest = manifest_event.get("payload", manifest_event)
    try:
        written = write_all_scope_out_records(
            output_dir=Path(args.output_dir),
            manifest=manifest,
            scoped_out_by=args.scoped_out_by,
            reason=args.reason,
        )
    except (KeyError, ValueError) as exc:
        print(json.dumps({"valid": False, "errors": [str(exc)]}, ensure_ascii=False, indent=2))
        return 2
    print(
        json.dumps(
            {
                "valid": True,
                "scope_out_count": len(written),
                "scope_out_files": [str(path) for path, _ in written],
                "requirement_ids": [record["requirement_id"] for _, record in written],
                "external_actions_executed": False,
            },
            ensure_ascii=False,
            indent=2,
        )
    )
    return 0


def command_skill_scan(args: argparse.Namespace) -> int:
    report = scan_skill_proposal(Path(args.proposal_dir), workspace_root=Path(args.workspace_root) if args.workspace_root else None)
    payload = {
        "proposal_dir": report.proposal_dir,
        "status": report.status,
        "findings": [finding.__dict__ for finding in report.findings],
    }
    print(json.dumps(payload, ensure_ascii=False, indent=2))
    return 0 if report.status == "PASS" else 2


def command_workshop_plan(args: argparse.Namespace) -> int:
    if args.action == "propose-create":
        plan = propose_create_plan(args.name, args.description, Path(args.proposal_dir), agent=args.agent)
    elif args.action == "inspect":
        plan = inspect_plan(args.proposal_id, agent=args.agent)
    elif args.action == "apply":
        plan = apply_plan(args.proposal_id, agent=args.agent)
    else:
        raise ValueError(f"Unsupported action: {args.action}")
    payload = {"command": plan.command, "risk_note": plan.risk_note, "executed": False}
    if args.execute:
        approved_by_file = False
        if args.action == "apply" and args.approval_decision:
            decision_record = json.loads(Path(args.approval_decision).read_text(encoding="utf-8"))
            approved_by_file, approval_reason = is_approved_decision_for_command(decision_record, action_type="skill_apply", command=plan.command)
            if not approved_by_file:
                print(json.dumps({"blocked": True, "reason": approval_reason}, ensure_ascii=False, indent=2))
                return 2
        if args.action == "apply" and not args.approved and not approved_by_file:
            blocked_payload: dict[str, Any] = {"blocked": True, "reason": "apply requires --approved"}
            if args.write_approval_request:
                request = build_approval_request(
                    item=f"Apply OpenClaw Skill proposal {args.proposal_id}",
                    reason="OpenClaw Skill apply can change agent behavior and must pass the V2.0 user approval gate.",
                    recommendation="Approve only after proposal inspection, safety scan, and rollback plan are reviewed.",
                    risk="high",
                    impact=f"Target agent: {args.agent or 'default'}; command would run: {' '.join(plan.command)}",
                    rollback="Inspect the installed Skill, remove or roll back it for the target agent, then run OpenClaw skills check.",
                    action_type="skill_apply",
                    command=plan.command,
                    evidence=[
                        "workshop-plan blocked apply execution before approval",
                        "run skill-scan and workshop inspect before approving",
                    ],
                )
                approval_path = write_approval_request(Path(args.approval_dir), request)
                outbox_path = write_approval_outbox(Path(args.outbox_dir), request)
                blocked_payload.update({"approval_request": str(approval_path), "feishu_outbox": str(outbox_path)})
            print(json.dumps(blocked_payload, ensure_ascii=False, indent=2))
            return 2
        code, stdout, stderr = execute_plan(plan, cwd=Path(args.repo_root).resolve())
        payload.update({"executed": True, "returncode": code, "stdout": stdout[-4000:], "stderr": stderr[-4000:]})
        print(json.dumps(payload, ensure_ascii=False, indent=2))
        return code
    print(json.dumps(payload, ensure_ascii=False, indent=2))
    return 0


def command_supervisor_run(args: argparse.Namespace) -> int:
    paths = SupervisorPaths(
        incidents=Path(args.incidents_dir),
        interventions=Path(args.interventions_dir),
        outbox=Path(args.outbox_dir),
        audit=Path(args.audit_dir),
    )
    options = SupervisorOptions(
        status=args.status,
        runtime=args.runtime,
        limit=args.limit,
        call_openai=args.call_openai,
        model=args.model,
    )
    if args.once:
        result = run_supervisor_scan(Path(args.repo_root).resolve(), paths, options)
    else:
        result = {
            "runs": run_supervisor_loop(
                Path(args.repo_root).resolve(),
                paths,
                options,
                iterations=args.iterations,
                interval_seconds=args.interval_seconds,
            )
        }
    print(json.dumps(result, ensure_ascii=False, indent=2))
    return 0


def build_parser() -> argparse.ArgumentParser:
    parser = argparse.ArgumentParser(description="Xingyuan ChatGPT/OpenClaw bridge")
    parser.set_defaults(func=lambda args: parser.print_help() or 0)
    parser.add_argument("--repo-root", default=str(DEFAULT_REPO_ROOT))

    subparsers = parser.add_subparsers(dest="command")

    health = subparsers.add_parser("health", help="Read OpenClaw gateway health")
    health.add_argument("--audit", action="store_true")
    health.add_argument("--audit-dir", default=str(DEFAULT_DATA_ROOT / "audit"))
    health.set_defaults(func=command_health)

    tasks = subparsers.add_parser("tasks", help="Read OpenClaw tracked tasks")
    tasks.add_argument("--status")
    tasks.add_argument("--runtime")
    tasks.add_argument("--full", action="store_true", help="Print redacted full task payload instead of summary")
    tasks.add_argument("--audit", action="store_true")
    tasks.add_argument("--audit-dir", default=str(DEFAULT_DATA_ROOT / "audit"))
    tasks.set_defaults(func=command_tasks)

    evaluate = subparsers.add_parser("evaluate", help="Evaluate a task snapshot for escalation")
    evaluate.add_argument("--task-id", required=True)
    evaluate.add_argument("--state", required=True)
    evaluate.add_argument("--source-agent", required=True)
    evaluate.add_argument("--capability-gap", action="store_true")
    evaluate.add_argument("--missing-tool-or-environment", action="store_true")
    evaluate.add_argument("--compile-failed", action="store_true")
    evaluate.add_argument("--test-failed", action="store_true")
    evaluate.add_argument("--build-failed", action="store_true")
    evaluate.add_argument("--conflicting-agent-conclusions", action="store_true")
    evaluate.add_argument("--destructive-risk", action="store_true")
    evaluate.add_argument("--core-rule-change-required", action="store_true")
    evaluate.add_argument("--consecutive-no-progress-checks", type=int, default=0)
    evaluate.add_argument("--same-error-count", type=int, default=0)
    evaluate.add_argument("--same-repair-failure-count", type=int, default=0)
    evaluate.add_argument("--plan-cycle-count", type=int, default=0)
    evaluate.add_argument("--automatic-rework-count", type=int, default=0)
    evaluate.add_argument("--repeated-file-churn", action="store_true")
    evaluate.add_argument("--flaky-tests", action="store_true")
    evaluate.set_defaults(func=command_evaluate)

    blocked = subparsers.add_parser("blocked-package", help="Create a redacted BlockedTaskPackage audit file")
    blocked.add_argument("--task-id", required=True)
    blocked.add_argument("--project", default="Xingyuan Covenant")
    blocked.add_argument("--source-agent", required=True)
    blocked.add_argument("--task-status", required=True)
    blocked.add_argument("--goal", required=True)
    blocked.add_argument("--acceptance-criteria", action="append")
    blocked.add_argument("--current-problem", required=True)
    blocked.add_argument("--first-error-time", default="")
    blocked.add_argument("--last-progress-time", default="")
    blocked.add_argument("--attempt-count", type=int, default=0)
    blocked.add_argument("--attempted-solutions", action="append")
    blocked.add_argument("--commands-executed", action="append")
    blocked.add_argument("--error-logs", action="append")
    blocked.add_argument("--stack-trace", action="append")
    blocked.add_argument("--agent-conclusion", default="")
    blocked.add_argument("--agent-confidence", type=float, default=0.0)
    blocked.add_argument("--requested-help", default="")
    blocked.add_argument("--screenshots", action="append")
    blocked.add_argument("--output-dir", default=str(DEFAULT_DATA_ROOT / "incidents"))
    blocked.set_defaults(func=command_blocked_package)

    collect = subparsers.add_parser("collect-blocked", help="Create local incidents for real failed/timed_out/lost OpenClaw tasks")
    collect.add_argument("--status", default="failed")
    collect.add_argument("--runtime")
    collect.add_argument("--limit", type=int, default=5)
    collect.add_argument("--output-dir", default=str(DEFAULT_DATA_ROOT / "incidents"))
    collect.add_argument("--audit-dir", default=str(DEFAULT_DATA_ROOT / "audit"))
    collect.set_defaults(func=command_collect_blocked)

    propose = subparsers.add_parser("propose-skill", help="Write a local Skill Workshop-style proposal directory")
    propose.add_argument("--name", required=True)
    propose.add_argument("--description", required=True)
    propose.add_argument("--install-agent", required=True)
    propose.add_argument("--output-dir", default=str(DEFAULT_DATA_ROOT / "skills"))
    propose.add_argument("--same-issue-count", type=int, default=0)
    propose.add_argument("--successful-workflow-count", type=int, default=0)
    propose.add_argument("--complex-and-reusable", action="store_true")
    propose.add_argument("--project-hard-constraint", action="store_true")
    propose.add_argument("--could-damage-project", action="store_true")
    propose.add_argument("--repeated-validation-miss", action="store_true")
    propose.add_argument("--shared-handoff-format", action="store_true")
    propose.add_argument("--one-off", action="store_true")
    propose.add_argument("--validated", action="store_true")
    propose.add_argument("--file-specific-patch-only", action="store_true")
    propose.add_argument("--contains-secret-or-private-data", action="store_true")
    propose.add_argument("--unclear-source-scripts", action="store_true")
    propose.add_argument("--overbroad-permissions", action="store_true")
    propose.add_argument("--no-validation-method", action="store_true")
    propose.set_defaults(func=command_propose_skill)

    intervention = subparsers.add_parser("intervention", help="Create an intervention directive from a BlockedTaskPackage")
    intervention.add_argument("--package", required=True, help="Path to a blocked task JSON event or raw package")
    intervention.add_argument("--reason", default="FAILED")
    intervention.add_argument("--output-dir", default=str(DEFAULT_DATA_ROOT / "interventions"))
    intervention.add_argument("--call-openai", action="store_true", help="Actually call OpenAI using OPENAI_API_KEY")
    intervention.add_argument("--model", default=DEFAULT_MODEL)
    intervention.set_defaults(func=command_intervention)

    openai_intervention = subparsers.add_parser("openai-intervention", help="Preview or execute an approval-gated OpenAI intervention call")
    openai_intervention.add_argument("--package", required=True, help="Path to a blocked task JSON event or raw package")
    openai_intervention.add_argument("--model", default=DEFAULT_MODEL)
    openai_intervention.add_argument("--approval-decision")
    openai_intervention.add_argument("--execute", action="store_true")
    openai_intervention.add_argument("--timeout-seconds", type=int, default=60)
    openai_intervention.add_argument("--write", action="store_true", help="Write the preview/execution result to the audit log")
    openai_intervention.add_argument("--output-dir", default=str(DEFAULT_DATA_ROOT / "audit"))
    openai_intervention.set_defaults(func=command_openai_intervention)

    feishu = subparsers.add_parser("feishu-dry-run", help="Write a Feishu message to local outbox without sending")
    feishu.add_argument("--type", required=True, choices=["progress", "blocked", "intervention", "skill", "approval"])
    feishu.add_argument("--task", required=True)
    feishu.add_argument("--text", required=True)
    feishu.add_argument("--owner", default="xingyuan-lead")
    feishu.add_argument("--status", default="dry-run")
    feishu.add_argument("--stage", default="local")
    feishu.add_argument("--blocked-minutes", type=int, default=0)
    feishu.add_argument("--attempts", default="0")
    feishu.add_argument("--root-cause", default="Unknown")
    feishu.add_argument("--verifier", default="xingyuan-qa")
    feishu.add_argument("--confidence", type=int, default=0)
    feishu.add_argument("--skill", default="")
    feishu.add_argument("--test-result", default="not run")
    feishu.add_argument("--recommendation", default="review")
    feishu.add_argument("--risk", default="low")
    feishu.add_argument("--impact", default="none")
    feishu.add_argument("--rollback", default="not applicable")
    feishu.add_argument("--output-dir", default=str(DEFAULT_DATA_ROOT / "outbox"))
    feishu.set_defaults(func=command_feishu_dry_run)

    feishu_send = subparsers.add_parser("feishu-send", help="Preview or execute an approval-gated Feishu send through OpenClaw")
    feishu_send.add_argument("--target", required=True)
    feishu_send.add_argument("--message", required=True)
    feishu_send.add_argument("--account", default="xingyuan")
    feishu_send.add_argument("--approval-decision")
    feishu_send.add_argument("--execute", action="store_true")
    feishu_send.add_argument("--timeout-seconds", type=int, default=60)
    feishu_send.add_argument("--write", action="store_true", help="Write the preview/execution result to the audit log")
    feishu_send.add_argument("--output-dir", default=str(DEFAULT_DATA_ROOT / "audit"))
    feishu_send.set_defaults(func=command_feishu_send)

    approval = subparsers.add_parser("approval-request", help="Write a structured user approval request and optional Feishu dry-run message")
    approval.add_argument("--item", required=True)
    approval.add_argument("--reason", required=True)
    approval.add_argument("--recommendation", required=True)
    approval.add_argument("--risk", choices=["low", "medium", "high"], default="high")
    approval.add_argument("--impact", required=True)
    approval.add_argument("--rollback", required=True)
    approval.add_argument("--action-type", default="other")
    approval.add_argument("--task-id", default="")
    approval.add_argument("--requested-by", default="codex")
    approval.add_argument("--requested-command", action="append")
    approval.add_argument("--evidence", action="append")
    approval.add_argument("--output-dir", default=str(DEFAULT_APPROVAL_ROOT))
    approval.add_argument("--outbox-dir", default=str(DEFAULT_DATA_ROOT / "outbox"))
    approval.add_argument("--no-feishu-outbox", action="store_true")
    approval.set_defaults(func=command_approval_request)

    approval_decision = subparsers.add_parser("approval-decision", help="Record a local user decision for an approval request")
    approval_decision.add_argument("--request", required=True, help="Path to an approval request JSON file")
    approval_decision.add_argument("--decision", required=True, choices=["approve", "reject", "pause_and_inspect"])
    approval_decision.add_argument("--decided-by", required=True)
    approval_decision.add_argument("--notes", default="")
    approval_decision.add_argument("--output-dir", default=str(DEFAULT_APPROVAL_ROOT))
    approval_decision.set_defaults(func=command_approval_decision)

    feishu_decision = subparsers.add_parser("feishu-decision-ingest", help="Convert a Feishu approval callback payload into an approval-decision JSON file")
    feishu_decision.add_argument("--request", help="Path to an approval request JSON file; required for decision callbacks")
    feishu_decision.add_argument("--payload", required=True, help="Path to a Feishu callback or saved Feishu-style decision payload JSON file")
    feishu_decision.add_argument("--headers-json", help="Optional JSON file containing Feishu callback request headers")
    feishu_decision.add_argument("--require-signature", action="store_true", help="Require X-Lark-Signature verification before accepting the payload")
    feishu_decision.add_argument("--encrypt-key", default="", help="Feishu callback encrypt key for local verification tests; prefer --encrypt-key-env for real use")
    feishu_decision.add_argument("--encrypt-key-env", default="FEISHU_XINGYUAN_ENCRYPT_KEY", help="Environment variable containing the Feishu callback encrypt key")
    feishu_decision.add_argument("--verification-token-env", default="", help="Optional environment variable containing the Feishu verification token")
    feishu_decision.add_argument("--output-dir", default=str(DEFAULT_APPROVAL_ROOT))
    feishu_decision.add_argument("--write", action="store_true", help="Write callback ingestion evidence to the audit log")
    feishu_decision.add_argument("--audit-dir", default=str(DEFAULT_DATA_ROOT / "audit"))
    feishu_decision.set_defaults(func=command_feishu_decision_ingest)

    final_result = subparsers.add_parser("final-result", help="Write and validate a V2.0 final task result")
    final_result.add_argument("--task-id", required=True)
    final_result.add_argument("--status", required=True, choices=["PASS", "CONDITIONAL_PASS", "REWORK", "BLOCKED"])
    final_result.add_argument("--summary", required=True)
    final_result.add_argument("--evidence", action="append")
    final_result.add_argument("--qa-result", required=True)
    final_result.add_argument("--compile-passed", action="store_true")
    final_result.add_argument("--tests-passed", action="store_true")
    final_result.add_argument("--regression-passed", action="store_true")
    final_result.add_argument("--severe-new-issue", action="store_true")
    final_result.add_argument("--git-diff-reviewed", action="store_true")
    final_result.add_argument("--evidence-archived", action="store_true")
    final_result.add_argument("--skill-review-failed", action="store_true")
    final_result.add_argument("--feishu-summary-sent", action="store_true")
    final_result.add_argument("--caveat", action="append")
    final_result.add_argument("--blocker", action="append")
    final_result.add_argument("--next-action", action="append")
    final_result.add_argument("--output-dir", default=str(DEFAULT_DATA_ROOT / "final-results"))
    final_result.set_defaults(func=command_final_result)

    readiness = subparsers.add_parser("readiness-audit", help="Inspect local V2.0 bridge readiness without changing external state")
    readiness.add_argument("--bridge-root", default=str(Path(__file__).resolve().parents[1]))
    readiness.add_argument("--write", action="store_true")
    readiness.add_argument("--output-dir", default=str(DEFAULT_DATA_ROOT / "audit"))
    readiness.set_defaults(func=command_readiness_audit)

    compliance = subparsers.add_parser("compliance-audit", help="Inspect V2.0 requirement-by-requirement evidence without changing external state")
    compliance.add_argument("--bridge-root", default=str(Path(__file__).resolve().parents[1]))
    compliance.add_argument("--write", action="store_true")
    compliance.add_argument("--output-dir", default=str(DEFAULT_DATA_ROOT / "audit"))
    compliance.set_defaults(func=command_compliance_audit)

    completion = subparsers.add_parser("goal-completion-audit", help="Conservatively decide whether the V2.0 objective is complete")
    completion.add_argument("--bridge-root", default=str(Path(__file__).resolve().parents[1]))
    completion.add_argument("--manifest", help="Optional approval-bundle manifest JSON file")
    completion.add_argument("--decision-dir", help="Directory containing approval-decision JSON files")
    completion.add_argument("--scope-dir", help="Directory containing approval-bundle scope-out JSON files")
    completion.add_argument("--no-dynamic-evidence", action="store_true", help="Ignore readiness-derived execution evidence")
    completion.add_argument("--write", action="store_true")
    completion.add_argument("--output-dir", default=str(DEFAULT_DATA_ROOT / "audit"))
    completion.set_defaults(func=command_goal_completion_audit)

    unblock_check = subparsers.add_parser("v2-unblock-check", help="Summarize remaining V2.0 external blockers without executing external actions")
    unblock_check.add_argument("--bridge-root", default=str(Path(__file__).resolve().parents[1]))
    unblock_check.add_argument("--manifest", help="Optional approval-bundle manifest JSON file; defaults to the latest bridge manifest")
    unblock_check.add_argument("--decision-dir", help="Directory containing approval-decision JSON files")
    unblock_check.add_argument("--scope-dir", help="Directory containing approval-bundle scope-out JSON files")
    unblock_check.add_argument("--write", action="store_true")
    unblock_check.add_argument("--write-report", action="store_true", help="Write a Markdown unblock summary next to the JSON audit")
    unblock_check.add_argument("--output-dir", default=str(DEFAULT_DATA_ROOT / "audit"))
    unblock_check.set_defaults(func=command_v2_unblock_check)

    security_snapshot = subparsers.add_parser("security-snapshot", help="Capture read-only redacted OpenClaw security evidence")
    security_snapshot.add_argument("--write", action="store_true")
    security_snapshot.add_argument("--timeout-seconds", type=int, default=60)
    security_snapshot.add_argument("--output-dir", default=str(DEFAULT_DATA_ROOT / "audit"))
    security_snapshot.set_defaults(func=command_security_snapshot)

    risk_plan = subparsers.add_parser("risk-plan", help="Build approval-ready plans for V2.0 high-risk pending items")
    risk_plan.add_argument("--requirement-id", required=True, choices=["all", *sorted(RISK_PLAN_TEMPLATES.keys())])
    risk_plan.add_argument("--write", action="store_true")
    risk_plan.add_argument("--write-approval-request", action="store_true")
    risk_plan.add_argument("--write-bundle-manifest", action="store_true")
    risk_plan.add_argument("--task-id", default="")
    risk_plan.add_argument("--requested-by", default="codex")
    risk_plan.add_argument("--output-dir", default=str(DEFAULT_DATA_ROOT / "audit"))
    risk_plan.add_argument("--approval-dir", default=str(DEFAULT_APPROVAL_ROOT))
    risk_plan.add_argument("--outbox-dir", default=str(DEFAULT_DATA_ROOT / "outbox"))
    risk_plan.set_defaults(func=command_risk_plan)

    risk_execute = subparsers.add_parser("risk-execute", help="Preview or execute an approved V2.0 high-risk plan")
    risk_execute.add_argument("--plan", required=True, help="Path to a risk plan JSON file")
    risk_execute.add_argument("--approval-decision", help="Path to an approval decision JSON file")
    risk_execute.add_argument("--execute", action="store_true")
    risk_execute.add_argument("--write", action="store_true")
    risk_execute.add_argument("--timeout-seconds", type=int, default=120)
    risk_execute.add_argument("--output-dir", default=str(DEFAULT_DATA_ROOT / "audit"))
    risk_execute.set_defaults(func=command_risk_execute)

    bundle_status = subparsers.add_parser("approval-bundle-status", help="Inspect approval-bundle decisions and execution readiness without executing")
    bundle_status.add_argument("--manifest", required=True, help="Path to an approval-bundle manifest JSON file")
    bundle_status.add_argument("--decision-dir", help="Directory containing approval-decision JSON files")
    bundle_status.add_argument("--scope-dir", help="Directory containing approval-bundle scope-out JSON files")
    bundle_status.add_argument("--bridge-root", default=str(Path(__file__).resolve().parents[1]))
    bundle_status.add_argument("--no-dynamic-evidence", action="store_true", help="Ignore readiness-derived execution evidence")
    bundle_status.add_argument("--write", action="store_true")
    bundle_status.add_argument("--write-report", action="store_true")
    bundle_status.add_argument("--operator", default="<operator>")
    bundle_status.add_argument("--output-dir", default=str(DEFAULT_DATA_ROOT / "audit"))
    bundle_status.set_defaults(func=command_approval_bundle_status)

    bundle_decision = subparsers.add_parser("approval-bundle-decision", help="Record a local approval decision for one approval-bundle requirement without executing")
    bundle_decision.add_argument("--manifest", required=True, help="Path to an approval-bundle manifest JSON file")
    bundle_decision.add_argument("--requirement-id", required=True)
    bundle_decision.add_argument("--decision", required=True, choices=["approve", "reject", "pause_and_inspect"])
    bundle_decision.add_argument("--decided-by", required=True)
    bundle_decision.add_argument("--notes", default="")
    bundle_decision.add_argument("--output-dir", default=str(DEFAULT_APPROVAL_ROOT))
    bundle_decision.set_defaults(func=command_approval_bundle_decision)

    bundle_scope = subparsers.add_parser("approval-bundle-scope-out", help="Explicitly scope one approval-bundle requirement out of the current V2.0 objective")
    bundle_scope.add_argument("--manifest", required=True, help="Path to an approval-bundle manifest JSON file")
    bundle_scope.add_argument("--requirement-id", required=True)
    bundle_scope.add_argument("--scoped-out-by", required=True)
    bundle_scope.add_argument("--reason", required=True)
    bundle_scope.add_argument("--output-dir", default=str(DEFAULT_APPROVAL_ROOT))
    bundle_scope.set_defaults(func=command_approval_bundle_scope_out)

    bundle_scope_all = subparsers.add_parser("approval-bundle-scope-out-all", help="Explicitly scope every approval-bundle requirement out of the current V2.0 objective")
    bundle_scope_all.add_argument("--manifest", required=True, help="Path to an approval-bundle manifest JSON file")
    bundle_scope_all.add_argument("--scoped-out-by", required=True)
    bundle_scope_all.add_argument("--reason", required=True)
    bundle_scope_all.add_argument("--confirm-all", action="store_true")
    bundle_scope_all.add_argument("--output-dir", default=str(DEFAULT_APPROVAL_ROOT))
    bundle_scope_all.set_defaults(func=command_approval_bundle_scope_out_all)

    supervisor = subparsers.add_parser("supervisor-run", help="Run one or more local supervisor scans")
    supervisor.add_argument("--once", action="store_true", help="Run a single scan")
    supervisor.add_argument("--iterations", type=int, default=1)
    supervisor.add_argument("--interval-seconds", type=int, default=300)
    supervisor.add_argument("--status", default="failed")
    supervisor.add_argument("--runtime")
    supervisor.add_argument("--limit", type=int, default=5)
    supervisor.add_argument("--call-openai", action="store_true")
    supervisor.add_argument("--model", default=DEFAULT_MODEL)
    supervisor.add_argument("--incidents-dir", default=str(DEFAULT_DATA_ROOT / "incidents"))
    supervisor.add_argument("--interventions-dir", default=str(DEFAULT_DATA_ROOT / "interventions"))
    supervisor.add_argument("--outbox-dir", default=str(DEFAULT_DATA_ROOT / "outbox"))
    supervisor.add_argument("--audit-dir", default=str(DEFAULT_DATA_ROOT / "audit"))
    supervisor.set_defaults(func=command_supervisor_run)

    skill_scan = subparsers.add_parser("skill-scan", help="Scan a local Skill Proposal directory for safety risks")
    skill_scan.add_argument("--proposal-dir", required=True)
    skill_scan.add_argument("--workspace-root", default=str(DEFAULT_REPO_ROOT))
    skill_scan.set_defaults(func=command_skill_scan)

    workshop = subparsers.add_parser("workshop-plan", help="Print or explicitly execute OpenClaw Skill Workshop commands")
    workshop.add_argument("--action", required=True, choices=["propose-create", "inspect", "apply"])
    workshop.add_argument("--agent")
    workshop.add_argument("--name", default="")
    workshop.add_argument("--description", default="")
    workshop.add_argument("--proposal-dir", default="")
    workshop.add_argument("--proposal-id", default="")
    workshop.add_argument("--execute", action="store_true")
    workshop.add_argument("--approved", action="store_true", help="Required for apply execution")
    workshop.add_argument("--approval-decision", help="Path to an approved approval-decision JSON file")
    workshop.add_argument("--write-approval-request", action="store_true", help="Write a local approval request when apply execution is blocked")
    workshop.add_argument("--approval-dir", default=str(DEFAULT_APPROVAL_ROOT))
    workshop.add_argument("--outbox-dir", default=str(DEFAULT_DATA_ROOT / "outbox"))
    workshop.set_defaults(func=command_workshop_plan)

    return parser


def main() -> int:
    if hasattr(sys.stdout, "reconfigure"):
        sys.stdout.reconfigure(encoding="utf-8")
    if hasattr(sys.stderr, "reconfigure"):
        sys.stderr.reconfigure(encoding="utf-8")
    parser = build_parser()
    args = parser.parse_args()
    return int(args.func(args) or 0)


if __name__ == "__main__":
    raise SystemExit(main())
