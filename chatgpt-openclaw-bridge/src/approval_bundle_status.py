from __future__ import annotations

import json
from pathlib import Path
from typing import Any

from .approval_request import validate_approval_decision, validate_approval_request
from .approval_bundle_scope import find_scope_out_record, validate_scope_out_record
from .audit_logger import utc_timestamp
from .risk_executor import build_risk_execution_preview, validate_risk_plan
from .risk_plan import validate_approval_bundle_manifest
from .secret_filter import redact


def _load_json(path: Path) -> dict[str, Any] | None:
    if not path.exists():
        return None
    data = json.loads(path.read_text(encoding="utf-8-sig"))
    return data.get("payload", data) if isinstance(data, dict) else None


def _find_decision_for_request(decision_dir: Path, request_id: str) -> tuple[dict[str, Any] | None, str]:
    if not decision_dir.exists():
        return None, ""
    matches: list[tuple[float, Path, dict[str, Any]]] = []
    for path in decision_dir.glob("*-approval-decision-DECISION-*.json"):
        record = _load_json(path)
        if record and record.get("request_id") == request_id:
            matches.append((path.stat().st_mtime, path, record))
    if not matches:
        return None, ""
    _, path, record = sorted(matches, key=lambda item: item[0])[-1]
    return record, str(path)


def build_approval_bundle_status(
    manifest: dict[str, Any],
    decision_dir: Path | None = None,
    scope_dir: Path | None = None,
    dynamic_items: dict[str, dict[str, Any]] | None = None,
) -> dict[str, Any]:
    manifest_errors = validate_approval_bundle_manifest(manifest)
    items: list[dict[str, Any]] = []
    status_counts: dict[str, int] = {}
    decision_root = decision_dir

    for item in manifest.get("items", []):
        requirement_id = str(item.get("requirement_id", "unknown"))
        risk_plan_path = Path(str(item.get("risk_plan_file", "")))
        request_path = Path(str(item.get("approval_request", "")))
        outbox_path = Path(str(item.get("feishu_outbox", "")))
        file_status = {
            "risk_plan_file": risk_plan_path.exists(),
            "approval_request": request_path.exists(),
            "feishu_outbox": outbox_path.exists(),
        }

        plan = _load_json(risk_plan_path)
        request = _load_json(request_path)
        plan_errors = validate_risk_plan(plan or {}) if plan else ["risk plan file is missing or invalid JSON"]
        request_errors = validate_approval_request(request or {}) if request else ["approval request file is missing or invalid JSON"]

        decision = None
        decision_path = ""
        if request and request.get("request_id"):
            search_dir = decision_root or request_path.parent
            decision, decision_path = _find_decision_for_request(search_dir, str(request["request_id"]))

        scope_root = scope_dir or decision_root or request_path.parent
        scope_out, scope_out_path = find_scope_out_record(scope_root, str(manifest.get("bundle_id", "")), requirement_id)
        scope_out_errors = validate_scope_out_record(scope_out, manifest) if scope_out else []

        decision_errors: list[str] = []
        decision_value = "none"
        if decision:
            decision_errors = validate_approval_decision(decision, request=request)
            decision_value = str(decision.get("decision", "unknown"))

        execution_preview = build_risk_execution_preview(plan or {}, decision)
        missing_files = [name for name, exists in file_status.items() if not exists]

        dynamic_item = (dynamic_items or {}).get(requirement_id)
        dynamic_status = str(dynamic_item.get("status", "")) if dynamic_item else ""

        if manifest_errors or missing_files or plan_errors or request_errors or decision_errors or scope_out_errors:
            item_status = "INVALID_OR_MISSING_EVIDENCE"
        elif scope_out:
            item_status = "SCOPED_OUT"
        elif dynamic_status == "PASS":
            item_status = "COMPLETE"
        elif dynamic_status == "EXTERNAL_BLOCKED":
            item_status = "EXTERNAL_BLOCKED"
        elif not decision:
            item_status = "WAITING_FOR_DECISION"
        elif decision_value == "reject":
            item_status = "DECISION_REJECTED"
        elif decision_value == "pause_and_inspect":
            item_status = "PAUSE_AND_INSPECT"
        elif execution_preview.get("approved") and execution_preview.get("placeholder_commands"):
            item_status = "APPROVED_BLOCKED_PLACEHOLDER"
        elif execution_preview.get("can_execute"):
            item_status = "READY_TO_EXECUTE"
        else:
            item_status = "APPROVED_BUT_BLOCKED"

        status_counts[item_status] = status_counts.get(item_status, 0) + 1
        items.append(
            redact(
                {
                    "requirement_id": requirement_id,
                    "status": item_status,
                    "risk": item.get("risk", ""),
                    "action_type": item.get("action_type", ""),
                    "file_status": file_status,
                    "missing_files": missing_files,
                    "risk_plan_file": str(risk_plan_path),
                    "approval_request": str(request_path),
                    "feishu_outbox": str(outbox_path),
                    "approval_decision": decision_path,
                    "scope_out": scope_out_path,
                    "decision": decision_value,
                    "approval_reason": execution_preview.get("approval_reason", ""),
                    "can_execute": execution_preview.get("can_execute", False),
                    "placeholder_commands": execution_preview.get("placeholder_commands", []),
                    "dynamic_evidence": dynamic_item.get("evidence", []) if dynamic_item else [],
                    "dynamic_next_action": dynamic_item.get("next_action", "") if dynamic_item else "",
                    "plan_errors": plan_errors,
                    "request_errors": request_errors,
                    "decision_errors": decision_errors,
                    "scope_out_errors": scope_out_errors,
                }
            )
        )

    if manifest_errors:
        overall_status = "INVALID"
    elif status_counts.get("INVALID_OR_MISSING_EVIDENCE"):
        overall_status = "INCOMPLETE"
    elif status_counts.get("WAITING_FOR_DECISION") or status_counts.get("PAUSE_AND_INSPECT"):
        overall_status = "PENDING_DECISION"
    elif status_counts.get("DECISION_REJECTED"):
        overall_status = "REJECTED"
    elif status_counts.get("EXTERNAL_BLOCKED"):
        overall_status = "BLOCKED_EXTERNAL"
    elif status_counts.get("APPROVED_BLOCKED_PLACEHOLDER") or status_counts.get("APPROVED_BUT_BLOCKED"):
        overall_status = "APPROVED_BUT_BLOCKED"
    elif status_counts.get("READY_TO_EXECUTE") == len(items):
        overall_status = "READY_TO_EXECUTE"
    elif status_counts.get("SCOPED_OUT", 0) + status_counts.get("COMPLETE", 0) == len(items):
        overall_status = "COMPLETE"
    else:
        overall_status = "UNKNOWN"

    return redact(
        {
            "status_id": f"APPROVALBUNDLESTATUS-{utc_timestamp()}",
            "created_at": utc_timestamp(),
            "bundle_id": manifest.get("bundle_id", ""),
            "overall_status": overall_status,
            "status_counts": status_counts,
            "manifest_errors": manifest_errors,
            "items": items,
            "sensitive_data_removed": True,
        }
    )


def write_approval_bundle_status(output_dir: Path, status: dict[str, Any]) -> Path:
    output_dir.mkdir(parents=True, exist_ok=True)
    safe_id = "".join(ch if ch.isalnum() or ch in ("-", "_") else "-" for ch in status["status_id"])
    path = output_dir / f"{utc_timestamp()}-approval-bundle-status-{safe_id}.json"
    path.write_text(json.dumps(redact(status), ensure_ascii=False, indent=2), encoding="utf-8")
    return path
