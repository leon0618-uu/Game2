from __future__ import annotations

from pathlib import Path
from typing import Any

from .approval_request import is_approved_decision_for_command
from .intervention_directive import validate_intervention_directive
from .openai_client import OpenAIClient
from .secret_filter import redact


def build_openai_intervention_command(package_path: str, model: str, execute: bool = True) -> list[str]:
    command = [
        "python",
        "-m",
        "src.main",
        "openai-intervention",
        "--package",
        package_path,
        "--model",
        model,
    ]
    if execute:
        command.append("--execute")
    return command


def build_openai_intervention_preview(
    *,
    package_path: str,
    package: dict[str, Any],
    model: str,
    execute: bool = False,
    approval_decision: dict[str, Any] | None = None,
) -> dict[str, Any]:
    command = build_openai_intervention_command(package_path, model, execute=True)
    approved = False
    approval_reason = "approval decision not provided"
    if approval_decision is not None:
        approved, approval_reason = is_approved_decision_for_command(approval_decision, action_type="other", command=command)
    validation_errors: list[str] = []
    if not package_path.strip():
        validation_errors.append("package path is required")
    if not package:
        validation_errors.append("package is empty")
    request_preview = OpenAIClient(model=model).build_intervention_request(package)
    can_execute = execute and approved and not validation_errors
    return redact(
        {
            "provider": "openai",
            "operation": "intervention",
            "model": model,
            "package_path": package_path,
            "mode": "execute" if execute else "preview",
            "command": command,
            "validation_errors": validation_errors,
            "approved": approved,
            "approval_reason": approval_reason,
            "can_execute": can_execute,
            "blocked": execute and not can_execute,
            "executed": False,
            "request_preview": request_preview,
            "result": None,
            "sensitive_data_removed": True,
        }
    )


def execute_openai_intervention(
    *,
    package_path: str,
    package: dict[str, Any],
    model: str,
    approval_decision: dict[str, Any],
    timeout_seconds: int = 60,
) -> dict[str, Any]:
    preview = build_openai_intervention_preview(
        package_path=package_path,
        package=package,
        model=model,
        execute=True,
        approval_decision=approval_decision,
    )
    if not preview["can_execute"]:
        return preview
    result = OpenAIClient(model=model, timeout_seconds=timeout_seconds).request_intervention(package)
    payload = {
        "ok": result.ok,
        "payload": result.payload,
        "error": result.error,
    }
    preview["executed"] = True
    preview["result"] = redact(payload)
    if result.ok and result.payload:
        errors = validate_intervention_directive(result.payload)
        preview["intervention_validation_errors"] = errors
    return redact(preview)
