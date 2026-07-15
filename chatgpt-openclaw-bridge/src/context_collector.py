from __future__ import annotations

from typing import Any

from .secret_filter import redact


REQUIRED_FIELDS = {
    "task_id": str,
    "project": str,
    "branch": str,
    "source_agent": str,
    "task_status": str,
    "goal": str,
    "acceptance_criteria": list,
    "current_problem": str,
    "first_error_time": str,
    "last_progress_time": str,
    "attempt_count": int,
    "attempted_solutions": list,
    "commands_executed": list,
    "error_logs": list,
    "stack_trace": list,
    "git_diff_summary": str,
    "changed_files": list,
    "environment": dict,
    "agent_conclusion": str,
    "agent_confidence": (int, float),
    "requested_help": str,
    "screenshots": list,
    "sensitive_data_removed": bool,
}

REQUIRED_ENVIRONMENT_FIELDS = {"os", "unity_version", "dotnet_version", "openclaw_version"}


def validate_blocked_task_package(package: dict[str, Any]) -> list[str]:
    errors: list[str] = []
    for field, expected_type in REQUIRED_FIELDS.items():
        if field not in package:
            errors.append(f"missing field: {field}")
            continue
        if not isinstance(package[field], expected_type):
            errors.append(f"invalid type for {field}")
    environment = package.get("environment")
    if isinstance(environment, dict):
        missing_env = REQUIRED_ENVIRONMENT_FIELDS.difference(environment)
        for field in sorted(missing_env):
            errors.append(f"missing environment field: {field}")
    if package.get("sensitive_data_removed") is not True:
        errors.append("sensitive_data_removed must be true")
    if not package.get("commands_executed"):
        errors.append("commands_executed must include actual commands")
    if not package.get("error_logs") and not package.get("stack_trace"):
        errors.append("error_logs or stack_trace must include evidence")
    return errors


def sanitize_blocked_task_package(package: dict[str, Any]) -> dict[str, Any]:
    sanitized = redact(package)
    if isinstance(sanitized, dict):
        sanitized["sensitive_data_removed"] = True
    return sanitized

