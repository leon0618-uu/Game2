from __future__ import annotations

import json
import subprocess
from pathlib import Path
from typing import Any

from .approval_request import validate_approval_decision
from .audit_logger import utc_timestamp
from .secret_filter import redact


REQUIRED_PLAN_FIELDS = [
    "plan_id",
    "requirement_id",
    "status",
    "action_type",
    "item",
    "risk",
    "planned_commands",
    "required_evidence",
    "sensitive_data_removed",
]


def validate_risk_plan(plan: dict[str, Any]) -> list[str]:
    errors: list[str] = []
    for field in REQUIRED_PLAN_FIELDS:
        if field not in plan:
            errors.append(f"{field} is required")
    for field in ("plan_id", "requirement_id", "status", "action_type", "item", "risk"):
        value = plan.get(field)
        if not isinstance(value, str) or not value.strip():
            errors.append(f"{field} must be a non-empty string")
    for field in ("planned_commands", "required_evidence"):
        value = plan.get(field)
        if not isinstance(value, list) or not all(isinstance(item, str) and item.strip() for item in value):
            errors.append(f"{field} must be a list of non-empty strings")
    if plan.get("sensitive_data_removed") is not True:
        errors.append("sensitive_data_removed must be true")
    return errors


def command_has_placeholder(command: str) -> bool:
    return "<" in command or ">" in command or "..." in command


def approval_matches_plan(decision: dict[str, Any], plan: dict[str, Any]) -> tuple[bool, str]:
    decision_errors = validate_approval_decision(decision)
    if decision_errors:
        return False, "; ".join(decision_errors)
    plan_errors = validate_risk_plan(plan)
    if plan_errors:
        return False, "; ".join(plan_errors)
    if decision.get("decision") != "approve":
        return False, f"approval decision is {decision.get('decision')}"
    if decision.get("request_action_type") != plan.get("action_type"):
        return False, f"approval action_type is {decision.get('request_action_type')}, expected {plan.get('action_type')}"
    if decision.get("request_command") != plan.get("planned_commands"):
        return False, "approval command does not match risk plan commands"
    return True, "approved"


def build_risk_execution_preview(plan: dict[str, Any], decision: dict[str, Any] | None = None, mode: str = "preview") -> dict[str, Any]:
    plan_errors = validate_risk_plan(plan)
    approved = False
    approval_reason = "approval decision not provided"
    if decision is not None:
        approved, approval_reason = approval_matches_plan(decision, plan)
    placeholder_commands = [command for command in plan.get("planned_commands", []) if command_has_placeholder(command)]
    can_execute = approved and not plan_errors and not placeholder_commands
    preview = {
        "execution_id": f"RISKEXEC-{utc_timestamp()}-{plan.get('requirement_id', 'unknown')}",
        "created_at": utc_timestamp(),
        "mode": mode,
        "plan_id": plan.get("plan_id", ""),
        "requirement_id": plan.get("requirement_id", ""),
        "approved": approved,
        "approval_reason": approval_reason,
        "can_execute": can_execute,
        "blocked": mode == "execute" and not can_execute,
        "plan_errors": plan_errors,
        "commands": plan.get("planned_commands", []),
        "placeholder_commands": placeholder_commands,
        "executed": False,
        "results": [],
        "sensitive_data_removed": True,
    }
    return redact(preview)


def execute_risk_plan(plan: dict[str, Any], decision: dict[str, Any], cwd: Path, timeout_seconds: int = 120) -> dict[str, Any]:
    preview = build_risk_execution_preview(plan, decision, mode="execute")
    if not preview["can_execute"]:
        return preview
    results: list[dict[str, Any]] = []
    for command in plan["planned_commands"]:
        completed = subprocess.run(
            command,
            cwd=cwd,
            shell=True,
            text=True,
            encoding="utf-8",
            capture_output=True,
            timeout=timeout_seconds,
            check=False,
        )
        results.append(
            {
                "command": command,
                "returncode": completed.returncode,
                "stdout": completed.stdout[-4000:],
                "stderr": completed.stderr[-4000:],
            }
        )
        if completed.returncode != 0:
            break
    preview["executed"] = True
    preview["results"] = redact(results)
    return redact(preview)


def write_risk_execution(output_dir: Path, execution: dict[str, Any]) -> Path:
    output_dir.mkdir(parents=True, exist_ok=True)
    safe_id = "".join(ch if ch.isalnum() or ch in ("-", "_") else "-" for ch in execution["execution_id"])
    path = output_dir / f"{utc_timestamp()}-risk-execution-{safe_id}.json"
    path.write_text(json.dumps(redact(execution), ensure_ascii=False, indent=2), encoding="utf-8")
    return path
