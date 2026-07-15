from __future__ import annotations

from pathlib import Path
from typing import Any

from .approval_request import is_approved_decision_for_command
from .command_runner import run_command
from .secret_filter import redact, redact_text


def build_feishu_send_command(
    *,
    target: str,
    message: str,
    account: str = "xingyuan",
    dry_run: bool = True,
) -> list[str]:
    command = [
        "openclaw",
        "message",
        "send",
        "--channel",
        "feishu",
        "--account",
        account,
        "--target",
        target,
        "--message",
        message,
        "--json",
    ]
    if dry_run:
        command.append("--dry-run")
    return command


def validate_feishu_send_request(target: str, message: str, account: str) -> list[str]:
    errors: list[str] = []
    if not target.strip():
        errors.append("target is required")
    if not message.strip():
        errors.append("message is required")
    if not account.strip():
        errors.append("account is required")
    return errors


def build_feishu_send_preview(
    *,
    target: str,
    message: str,
    account: str = "xingyuan",
    execute: bool = False,
    approval_decision: dict[str, Any] | None = None,
) -> dict[str, Any]:
    validation_errors = validate_feishu_send_request(target, message, account)
    command = build_feishu_send_command(target=target, message=message, account=account, dry_run=not execute)
    approved = False
    approval_reason = "approval decision not provided"
    if approval_decision is not None:
        approved, approval_reason = is_approved_decision_for_command(approval_decision, action_type="other", command=command)
    can_execute = execute and not validation_errors and approved
    return redact(
        {
            "channel": "feishu",
            "account": account,
            "target": target,
            "message_preview": redact_text(message[:500]),
            "mode": "execute" if execute else "preview",
            "command": command,
            "validation_errors": validation_errors,
            "approved": approved,
            "approval_reason": approval_reason,
            "can_execute": can_execute,
            "blocked": execute and not can_execute,
            "executed": False,
            "result": None,
            "sensitive_data_removed": True,
        }
    )


def execute_feishu_send(
    *,
    target: str,
    message: str,
    account: str,
    approval_decision: dict[str, Any],
    cwd: Path,
    timeout_seconds: int = 60,
) -> dict[str, Any]:
    preview = build_feishu_send_preview(target=target, message=message, account=account, execute=True, approval_decision=approval_decision)
    if not preview["can_execute"]:
        return preview
    completed = run_command(preview["command"], cwd=cwd, timeout_seconds=timeout_seconds)
    preview["executed"] = True
    preview["result"] = redact(
        {
            "returncode": completed.returncode,
            "stdout": completed.stdout[-4000:],
            "stderr": completed.stderr[-4000:],
        }
    )
    return redact(preview)
