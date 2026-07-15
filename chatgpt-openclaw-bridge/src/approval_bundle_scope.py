from __future__ import annotations

import json
from pathlib import Path
from typing import Any

from .audit_logger import utc_timestamp
from .risk_plan import validate_approval_bundle_manifest
from .secret_filter import redact


def find_manifest_item(manifest: dict[str, Any], requirement_id: str) -> dict[str, Any] | None:
    for item in manifest.get("items", []):
        if isinstance(item, dict) and item.get("requirement_id") == requirement_id:
            return item
    return None


def build_scope_out_record(*, manifest: dict[str, Any], requirement_id: str, scoped_out_by: str, reason: str) -> dict[str, Any]:
    manifest_errors = validate_approval_bundle_manifest(manifest)
    if manifest_errors:
        raise ValueError("; ".join(manifest_errors))
    item = find_manifest_item(manifest, requirement_id)
    if item is None:
        raise KeyError(f"requirement_id not found in approval bundle: {requirement_id}")
    created_at = utc_timestamp()
    record = {
        "scope_out_id": f"SCOPEOUT-{created_at}-{requirement_id}",
        "bundle_id": manifest.get("bundle_id", ""),
        "requirement_id": requirement_id,
        "scoped_out_at": created_at,
        "scoped_out_by": scoped_out_by,
        "reason": reason,
        "item": item.get("item", ""),
        "risk": item.get("risk", ""),
        "action_type": item.get("action_type", ""),
        "external_actions_executed": False,
        "sensitive_data_removed": True,
    }
    return redact(record)


def validate_scope_out_record(record: dict[str, Any], manifest: dict[str, Any] | None = None) -> list[str]:
    errors: list[str] = []
    for field in ("scope_out_id", "bundle_id", "requirement_id", "scoped_out_at", "scoped_out_by", "reason", "item", "risk", "action_type"):
        value = record.get(field)
        if not isinstance(value, str) or not value.strip():
            errors.append(f"{field} must be a non-empty string")
    if record.get("external_actions_executed") is not False:
        errors.append("external_actions_executed must be false")
    if record.get("sensitive_data_removed") is not True:
        errors.append("sensitive_data_removed must be true")
    if manifest is not None:
        manifest_errors = validate_approval_bundle_manifest(manifest)
        if manifest_errors:
            errors.extend(f"manifest.{error}" for error in manifest_errors)
        if record.get("bundle_id") != manifest.get("bundle_id"):
            errors.append("bundle_id must match approval bundle")
        if find_manifest_item(manifest, str(record.get("requirement_id", ""))) is None:
            errors.append("requirement_id must exist in approval bundle")
    return errors


def write_scope_out_record(output_dir: Path, record: dict[str, Any], manifest: dict[str, Any]) -> Path:
    errors = validate_scope_out_record(record, manifest)
    if errors:
        raise ValueError("; ".join(errors))
    output_dir.mkdir(parents=True, exist_ok=True)
    safe_id = "".join(ch if ch.isalnum() or ch in ("-", "_") else "-" for ch in record["scope_out_id"])
    path = output_dir / f"{utc_timestamp()}-scope-out-{safe_id}.json"
    path.write_text(json.dumps(redact(record), ensure_ascii=False, indent=2), encoding="utf-8")
    return path


def write_all_scope_out_records(
    *,
    output_dir: Path,
    manifest: dict[str, Any],
    scoped_out_by: str,
    reason: str,
) -> list[tuple[Path, dict[str, Any]]]:
    manifest_errors = validate_approval_bundle_manifest(manifest)
    if manifest_errors:
        raise ValueError("; ".join(manifest_errors))
    written: list[tuple[Path, dict[str, Any]]] = []
    for item in manifest.get("items", []):
        requirement_id = str(item.get("requirement_id", ""))
        record = build_scope_out_record(
            manifest=manifest,
            requirement_id=requirement_id,
            scoped_out_by=scoped_out_by,
            reason=reason,
        )
        path = write_scope_out_record(output_dir, record, manifest)
        written.append((path, record))
    return written


def load_json_object(path: Path) -> dict[str, Any] | None:
    if not path.exists():
        return None
    data = json.loads(path.read_text(encoding="utf-8-sig"))
    if not isinstance(data, dict):
        return None
    payload = data.get("payload", data)
    return payload if isinstance(payload, dict) else None


def find_scope_out_record(scope_dir: Path, bundle_id: str, requirement_id: str) -> tuple[dict[str, Any] | None, str]:
    if not scope_dir.exists():
        return None, ""
    matches: list[tuple[float, Path, dict[str, Any]]] = []
    for path in scope_dir.glob("*-scope-out-SCOPEOUT-*.json"):
        record = load_json_object(path)
        if record and record.get("bundle_id") == bundle_id and record.get("requirement_id") == requirement_id:
            matches.append((path.stat().st_mtime, path, record))
    if not matches:
        return None, ""
    _, path, record = sorted(matches, key=lambda item: item[0])[-1]
    return record, str(path)
