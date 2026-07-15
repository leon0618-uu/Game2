from __future__ import annotations

import json
from pathlib import Path
from typing import Any

from .audit_logger import utc_timestamp
from .readiness_audit import EXTERNAL_BLOCKED, MISSING, PENDING_APPROVAL
from .secret_filter import redact


def _status_count(audit: dict[str, Any], status: str) -> int:
    counts = audit.get("status_counts") or {}
    value = counts.get(status, 0)
    return int(value) if isinstance(value, int) else 0


def _audit_is_pass_or_scoped_pending(audit: dict[str, Any], bundle_complete: bool) -> bool:
    if audit.get("overall_status") == "PASS":
        return True
    return (
        bundle_complete
        and audit.get("overall_status") == "CONDITIONAL_PASS"
        and _status_count(audit, MISSING) == 0
        and _status_count(audit, PENDING_APPROVAL) > 0
    )


def build_v2_goal_completion_audit(
    *,
    readiness_audit: dict[str, Any],
    compliance_audit: dict[str, Any],
    approval_bundle_status: dict[str, Any] | None = None,
) -> dict[str, Any]:
    blockers: list[dict[str, Any]] = []
    bundle_overall = str(approval_bundle_status.get("overall_status", "missing")) if approval_bundle_status else "missing"
    bundle_complete = bundle_overall == "COMPLETE"

    if not _audit_is_pass_or_scoped_pending(readiness_audit, bundle_complete):
        blockers.append(
            {
                "blocker_id": "readiness_not_pass",
                "status": readiness_audit.get("overall_status", "unknown"),
                "reason": "Readiness audit must be PASS, or all pending high-risk items must be explicitly scoped out, before completion.",
                "missing": _status_count(readiness_audit, MISSING),
                "pending_approval": _status_count(readiness_audit, PENDING_APPROVAL),
                "external_blocked": _status_count(readiness_audit, EXTERNAL_BLOCKED),
            }
        )

    if not _audit_is_pass_or_scoped_pending(compliance_audit, bundle_complete):
        blockers.append(
            {
                "blocker_id": "compliance_not_pass",
                "status": compliance_audit.get("overall_status", "unknown"),
                "reason": "V2.0 compliance must be PASS, or all pending high-risk items must be explicitly scoped out, before completion.",
                "missing": _status_count(compliance_audit, MISSING),
                "pending_approval": _status_count(compliance_audit, PENDING_APPROVAL),
                "external_blocked": _status_count(compliance_audit, EXTERNAL_BLOCKED),
            }
        )

    if approval_bundle_status is None:
        blockers.append(
            {
                "blocker_id": "approval_bundle_status_missing",
                "status": "MISSING",
                "reason": "Approval bundle status is required to prove high-risk pending items are resolved or explicitly scoped out.",
            }
        )
    elif not bundle_complete:
        blockers.append(
            {
                "blocker_id": "approval_bundle_not_complete",
                "status": bundle_overall,
                "reason": "High-risk pending items still require user decisions, concrete commands, execution evidence, verification, or explicit scope-out.",
                "status_counts": approval_bundle_status.get("status_counts", {}),
            }
        )

    overall_status = "COMPLETE" if not blockers else "NOT_COMPLETE"
    return redact(
        {
            "completion_audit_id": f"V2GOALCOMPLETION-{utc_timestamp()}",
            "created_at": utc_timestamp(),
            "objective": "Execute the V2.0 OpenClaw/Codex collaboration workflow.",
            "overall_status": overall_status,
            "complete": overall_status == "COMPLETE",
            "blockers": blockers,
            "source_status": {
                "readiness": readiness_audit.get("overall_status", "unknown"),
                "compliance": compliance_audit.get("overall_status", "unknown"),
                "approval_bundle": bundle_overall,
            },
            "sensitive_data_removed": True,
        }
    )


def write_v2_goal_completion_audit(output_dir: Path, audit: dict[str, Any]) -> Path:
    output_dir.mkdir(parents=True, exist_ok=True)
    safe_id = "".join(ch if ch.isalnum() or ch in ("-", "_") else "-" for ch in audit["completion_audit_id"])
    path = output_dir / f"{utc_timestamp()}-v2-goal-completion-{safe_id}.json"
    path.write_text(json.dumps(redact(audit), ensure_ascii=False, indent=2), encoding="utf-8")
    return path
