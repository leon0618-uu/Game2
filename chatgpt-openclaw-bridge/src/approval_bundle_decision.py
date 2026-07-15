from __future__ import annotations

import json
from pathlib import Path
from typing import Any

from .approval_request import build_approval_decision, validate_approval_decision, write_approval_decision
from .risk_plan import validate_approval_bundle_manifest
from .secret_filter import redact


def load_json_object(path: Path) -> dict[str, Any]:
    data = json.loads(path.read_text(encoding="utf-8-sig"))
    if not isinstance(data, dict):
        raise ValueError(f"{path} must contain a JSON object")
    payload = data.get("payload", data)
    if not isinstance(payload, dict):
        raise ValueError(f"{path} payload must be a JSON object")
    return payload


def find_manifest_item(manifest: dict[str, Any], requirement_id: str) -> dict[str, Any] | None:
    for item in manifest.get("items", []):
        if isinstance(item, dict) and item.get("requirement_id") == requirement_id:
            return item
    return None


def build_bundle_approval_decision(
    *,
    manifest: dict[str, Any],
    requirement_id: str,
    decision: str,
    decided_by: str,
    notes: str = "",
) -> tuple[dict[str, Any], dict[str, Any], dict[str, Any]]:
    manifest_errors = validate_approval_bundle_manifest(manifest)
    if manifest_errors:
        raise ValueError("; ".join(manifest_errors))
    item = find_manifest_item(manifest, requirement_id)
    if item is None:
        raise KeyError(f"requirement_id not found in approval bundle: {requirement_id}")
    request_path = Path(str(item.get("approval_request", "")))
    request = load_json_object(request_path)
    decision_record = build_approval_decision(request=request, decision=decision, decided_by=decided_by, notes=notes)
    decision_record["approval_bundle_id"] = manifest.get("bundle_id", "")
    decision_record["requirement_id"] = requirement_id
    errors = validate_approval_decision(decision_record, request=request)
    if errors:
        raise ValueError("; ".join(errors))
    return redact(decision_record), request, item


def write_bundle_approval_decision(
    *,
    output_dir: Path,
    manifest: dict[str, Any],
    requirement_id: str,
    decision: str,
    decided_by: str,
    notes: str = "",
) -> tuple[Path, dict[str, Any], dict[str, Any]]:
    decision_record, request, item = build_bundle_approval_decision(
        manifest=manifest,
        requirement_id=requirement_id,
        decision=decision,
        decided_by=decided_by,
        notes=notes,
    )
    path = write_approval_decision(output_dir, decision_record, request)
    return path, decision_record, item
