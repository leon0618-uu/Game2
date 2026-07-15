from __future__ import annotations

import json
from pathlib import Path
from typing import Any

from .audit_logger import utc_timestamp
from .feishu_reporter import approval_message, write_outbox_message
from .secret_filter import redact, redact_text


RISK_LEVELS = {"low", "medium", "high"}
ACTION_TYPES = {
    "push",
    "create_pr",
    "merge_pr",
    "release",
    "skill_apply",
    "skill_install",
    "plugin_install",
    "system_config",
    "secret_config",
    "open_port",
    "paid_service",
    "other",
}
ALLOWED_DECISIONS = ["approve", "reject", "pause_and_inspect"]


def build_approval_request(
    *,
    item: str,
    reason: str,
    recommendation: str,
    risk: str,
    impact: str,
    rollback: str,
    action_type: str = "other",
    task_id: str = "",
    requested_by: str = "codex",
    command: list[str] | None = None,
    evidence: list[str] | None = None,
) -> dict[str, Any]:
    created_at = utc_timestamp()
    safe_item = "".join(ch if ch.isalnum() or ch in ("-", "_") else "-" for ch in item)[:40] or "request"
    request = {
        "request_id": f"APPROVAL-{created_at}-{safe_item}",
        "created_at": created_at,
        "status": "pending",
        "action_type": action_type,
        "task_id": task_id,
        "item": item,
        "reason": reason,
        "recommendation": recommendation,
        "risk": risk.lower(),
        "impact": impact,
        "rollback": rollback,
        "command": command or [],
        "evidence": evidence or [],
        "requested_by": requested_by,
        "requires_user_approval": True,
        "allowed_decisions": ALLOWED_DECISIONS,
        "sensitive_data_removed": True,
    }
    return redact(request)


def validate_approval_request(request: dict[str, Any]) -> list[str]:
    errors: list[str] = []
    required_text_fields = [
        "request_id",
        "created_at",
        "status",
        "action_type",
        "item",
        "reason",
        "recommendation",
        "risk",
        "impact",
        "rollback",
        "requested_by",
    ]
    for field in required_text_fields:
        value = request.get(field)
        if not isinstance(value, str) or not value.strip():
            errors.append(f"{field} must be a non-empty string")
    if request.get("status") != "pending":
        errors.append("status must be pending when the request is created")
    if request.get("risk") not in RISK_LEVELS:
        errors.append(f"risk must be one of {sorted(RISK_LEVELS)}")
    if request.get("action_type") not in ACTION_TYPES:
        errors.append(f"action_type must be one of {sorted(ACTION_TYPES)}")
    if request.get("requires_user_approval") is not True:
        errors.append("requires_user_approval must be true")
    if request.get("sensitive_data_removed") is not True:
        errors.append("sensitive_data_removed must be true")
    if request.get("allowed_decisions") != ALLOWED_DECISIONS:
        errors.append("allowed_decisions must match the standard approval choices")
    for field in ("command", "evidence"):
        value = request.get(field)
        if not isinstance(value, list) or not all(isinstance(item, str) for item in value):
            errors.append(f"{field} must be a list of strings")
    return errors


def write_approval_request(output_dir: Path, request: dict[str, Any]) -> Path:
    errors = validate_approval_request(request)
    if errors:
        raise ValueError("; ".join(errors))
    output_dir.mkdir(parents=True, exist_ok=True)
    safe_id = "".join(ch if ch.isalnum() or ch in ("-", "_") else "-" for ch in request["request_id"])
    path = output_dir / f"{utc_timestamp()}-approval-request-{safe_id}.json"
    path.write_text(json.dumps(redact(request), ensure_ascii=False, indent=2), encoding="utf-8")
    return path


def write_approval_outbox(outbox_dir: Path, request: dict[str, Any]) -> Path:
    message = approval_message(
        item=redact_text(str(request["item"])),
        reason=redact_text(str(request["reason"])),
        recommendation=redact_text(str(request["recommendation"])),
        risk=redact_text(str(request["risk"])),
        impact=redact_text(str(request["impact"])),
        rollback=redact_text(str(request["rollback"])),
    )
    return write_outbox_message(outbox_dir, "approval", message)


def build_approval_decision(
    *,
    request: dict[str, Any],
    decision: str,
    decided_by: str,
    notes: str = "",
) -> dict[str, Any]:
    decided_at = utc_timestamp()
    record = {
        "decision_id": f"DECISION-{decided_at}-{request.get('request_id', 'unknown')}",
        "request_id": request.get("request_id", ""),
        "decided_at": decided_at,
        "decision": decision,
        "decided_by": decided_by,
        "notes": notes,
        "request_action_type": request.get("action_type", ""),
        "request_item": request.get("item", ""),
        "request_command": request.get("command", []),
        "sensitive_data_removed": True,
    }
    return redact(record)


def validate_approval_decision(decision_record: dict[str, Any], request: dict[str, Any] | None = None) -> list[str]:
    errors: list[str] = []
    for field in ("decision_id", "request_id", "decided_at", "decision", "decided_by", "request_action_type", "request_item"):
        value = decision_record.get(field)
        if not isinstance(value, str) or not value.strip():
            errors.append(f"{field} must be a non-empty string")
    if decision_record.get("decision") not in ALLOWED_DECISIONS:
        errors.append(f"decision must be one of {ALLOWED_DECISIONS}")
    if decision_record.get("request_action_type") not in ACTION_TYPES:
        errors.append(f"request_action_type must be one of {sorted(ACTION_TYPES)}")
    if decision_record.get("sensitive_data_removed") is not True:
        errors.append("sensitive_data_removed must be true")
    command = decision_record.get("request_command")
    if not isinstance(command, list) or not all(isinstance(item, str) for item in command):
        errors.append("request_command must be a list of strings")
    if request is not None:
        request_errors = validate_approval_request(request)
        if request_errors:
            errors.extend(f"request.{error}" for error in request_errors)
        if decision_record.get("request_id") != request.get("request_id"):
            errors.append("decision request_id must match approval request")
        if decision_record.get("request_action_type") != request.get("action_type"):
            errors.append("decision action_type must match approval request")
        if decision_record.get("request_command") != request.get("command", []):
            errors.append("decision command must match approval request")
    return errors


def is_approved_decision_for_command(
    decision_record: dict[str, Any],
    *,
    action_type: str,
    command: list[str],
) -> tuple[bool, str]:
    errors = validate_approval_decision(decision_record)
    if errors:
        return False, "; ".join(errors)
    if decision_record.get("decision") != "approve":
        return False, f"approval decision is {decision_record.get('decision')}"
    if decision_record.get("request_action_type") != action_type:
        return False, f"approval action_type is {decision_record.get('request_action_type')}, expected {action_type}"
    request_command = decision_record.get("request_command")
    redacted_request_command = redact({"command": request_command}).get("command")
    redacted_command = redact({"command": command}).get("command")
    if request_command != command and redacted_request_command != redacted_command:
        return False, "approval command does not match requested command"
    return True, "approved"


def write_approval_decision(output_dir: Path, decision_record: dict[str, Any], request: dict[str, Any]) -> Path:
    errors = validate_approval_decision(decision_record, request=request)
    if errors:
        raise ValueError("; ".join(errors))
    output_dir.mkdir(parents=True, exist_ok=True)
    safe_id = "".join(ch if ch.isalnum() or ch in ("-", "_") else "-" for ch in decision_record["decision_id"])
    path = output_dir / f"{utc_timestamp()}-approval-decision-{safe_id}.json"
    path.write_text(json.dumps(redact(decision_record), ensure_ascii=False, indent=2), encoding="utf-8")
    return path
