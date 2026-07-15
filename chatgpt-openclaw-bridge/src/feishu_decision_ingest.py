from __future__ import annotations

import hashlib
import hmac
import json
from typing import Any

from .approval_request import ALLOWED_DECISIONS, build_approval_decision, validate_approval_decision
from .secret_filter import redact


DECISION_ALIASES = {
    "approve": "approve",
    "approved": "approve",
    "同意": "approve",
    "批准": "approve",
    "reject": "reject",
    "rejected": "reject",
    "拒绝": "reject",
    "驳回": "reject",
    "pause": "pause_and_inspect",
    "inspect": "pause_and_inspect",
    "pause_and_inspect": "pause_and_inspect",
    "pause-and-inspect": "pause_and_inspect",
    "暂停检查": "pause_and_inspect",
    "暂停并检查": "pause_and_inspect",
}


def verify_feishu_signature(*, timestamp: str, nonce: str, encrypt_key: str, body: bytes, signature: str) -> bool:
    if not timestamp or not nonce or not encrypt_key or not signature:
        return False
    seed = f"{timestamp}{nonce}{encrypt_key}".encode("utf-8")
    digest = hashlib.sha256(seed + body).hexdigest()
    return hmac.compare_digest(digest, signature)


def _coerce_json_object(value: Any) -> Any:
    if isinstance(value, str):
        text = value.strip()
        if text.startswith("{") and text.endswith("}"):
            try:
                return json.loads(text)
            except json.JSONDecodeError:
                return value
    return value


def _nested_any(payload: dict[str, Any], path: tuple[str, ...]) -> Any:
    current: Any = payload
    for key in path:
        current = _coerce_json_object(current)
        if not isinstance(current, dict):
            return None
        current = current.get(key)
    return _coerce_json_object(current)


def _nested_value(payload: dict[str, Any], path: tuple[str, ...]) -> Any:
    return _nested_any(payload, path)


def _first_text(payload: dict[str, Any], paths: list[tuple[str, ...]]) -> str:
    for path in paths:
        value = _nested_any(payload, path)
        if isinstance(value, str) and value.strip():
            return value.strip()
    return ""


def normalize_decision(value: str) -> str:
    normalized = " ".join(value.strip().lower().replace("-", "_").split())
    return DECISION_ALIASES.get(normalized, normalized)


def normalize_feishu_callback_payload(payload: dict[str, Any]) -> dict[str, Any]:
    normalized = dict(payload)
    action_value = _nested_any(payload, ("event", "action", "value"))
    if action_value is None:
        action_value = _nested_any(payload, ("action", "value"))
    if isinstance(action_value, dict):
        normalized.setdefault("source", "feishu")
        for key in ("request_id", "approval_request_id", "decision", "action", "notes", "comment"):
            if key in action_value and key not in normalized:
                normalized[key] = action_value[key]

    if "schema" in payload or "header" in payload or "event" in payload:
        normalized.setdefault("source", "feishu")
    return normalized


def build_feishu_callback_response(payload: dict[str, Any], verification_token: str | None = None) -> dict[str, Any] | None:
    token = _first_text(payload, [("token",), ("event", "token")])
    if verification_token is not None and token and token != verification_token:
        return {"valid": False, "errors": ["feishu verification token does not match"]}
    challenge = _first_text(payload, [("challenge",), ("event", "challenge")])
    request_type = _first_text(payload, [("type",), ("event", "type")])
    if challenge and request_type in {"url_verification", "challenge"}:
        return {"valid": True, "challenge": challenge, "decision_source": "feishu"}
    return None


def extract_feishu_payload_fields(payload: dict[str, Any]) -> dict[str, str]:
    payload = normalize_feishu_callback_payload(payload)
    return {
        "source": _first_text(payload, [("source",), ("channel",), ("event", "source"), ("event", "channel"), ("event", "app"), ("header", "event_type")]),
        "request_id": _first_text(
            payload,
            [
                ("request_id",),
                ("approval_request_id",),
                ("event", "request_id"),
                ("event", "approval_request_id"),
                ("event", "action", "value", "request_id"),
                ("event", "action", "value", "approval_request_id"),
                ("action", "value", "request_id"),
                ("action", "value", "approval_request_id"),
            ],
        ),
        "decision": normalize_decision(
            _first_text(
                payload,
                [
                    ("decision",),
                    ("action",),
                    ("event", "decision"),
                    ("event", "action"),
                    ("button", "value"),
                    ("event", "action", "value", "decision"),
                    ("event", "action", "value", "action"),
                    ("action", "value", "decision"),
                    ("action", "value", "action"),
                ],
            )
        ),
        "decided_by": _first_text(
            payload,
            [
                ("decided_by",),
                ("operator",),
                ("user", "name"),
                ("user", "id"),
                ("event", "operator"),
                ("event", "operator", "name"),
                ("event", "operator", "open_id"),
                ("event", "operator", "user_id"),
                ("event", "operator", "union_id"),
                ("event", "operator", "operator_id", "open_id"),
                ("event", "operator", "operator_id", "user_id"),
                ("event", "operator", "operator_id", "union_id"),
                ("event", "user", "name"),
                ("event", "user", "id"),
                ("event", "user", "open_id"),
                ("event", "user", "user_id"),
                ("event", "user", "union_id"),
            ],
        ),
        "notes": _first_text(
            payload,
            [
                ("notes",),
                ("comment",),
                ("reason",),
                ("event", "notes"),
                ("event", "comment"),
                ("event", "action", "value", "notes"),
                ("event", "action", "value", "comment"),
                ("action", "value", "notes"),
                ("action", "value", "comment"),
            ],
        ),
        "message_id": _first_text(
            payload,
            [
                ("message_id",),
                ("open_message_id",),
                ("event", "message_id"),
                ("event", "open_message_id"),
                ("event", "context", "open_message_id"),
            ],
        ),
    }


def build_feishu_approval_decision(request: dict[str, Any], payload: dict[str, Any]) -> dict[str, Any]:
    fields = extract_feishu_payload_fields(payload)
    decision_record = build_approval_decision(
        request=request,
        decision=fields["decision"],
        decided_by=fields["decided_by"],
        notes=fields["notes"],
    )
    decision_record.update(
        {
            "decision_source": "feishu",
            "source_request_id": fields["request_id"],
            "source_message_id": fields["message_id"],
        }
    )
    return redact(decision_record)


def validate_feishu_approval_decision(request: dict[str, Any], payload: dict[str, Any], decision_record: dict[str, Any]) -> list[str]:
    errors: list[str] = []
    if not isinstance(payload, dict):
        return ["feishu payload must be an object"]

    fields = extract_feishu_payload_fields(payload)
    if "feishu" not in fields["source"].lower():
        errors.append("feishu payload source must identify Feishu")
    if fields["request_id"] != request.get("request_id"):
        errors.append("feishu payload request_id must match approval request")
    if fields["decision"] not in ALLOWED_DECISIONS:
        errors.append(f"feishu payload decision must be one of {ALLOWED_DECISIONS}")
    if not fields["decided_by"]:
        errors.append("feishu payload decided_by/operator/user is required")

    errors.extend(validate_approval_decision(decision_record, request=request))
    if decision_record.get("decision_source") != "feishu":
        errors.append("decision_source must be feishu")
    return errors
