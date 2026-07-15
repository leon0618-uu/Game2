from __future__ import annotations

import re
from typing import Any


REDACTION = "[REDACTED]"

SENSITIVE_KEYS = {
    "api_key",
    "apikey",
    "appsecret",
    "app_secret",
    "authorization",
    "cookie",
    "gateway_token",
    "openai_api_key",
    "password",
    "secret",
    "token",
}

SECRET_PATTERNS = [
    re.compile(r"(?i)(bearer\s+)[A-Za-z0-9._~+/=-]{16,}"),
    re.compile(r"(?i)(api[_-]?key\s*[:=]\s*)[A-Za-z0-9._~+/=-]{16,}"),
    re.compile(r"(?i)(appsecret\s*[:=]\s*)[A-Za-z0-9._~+/=-]{16,}"),
    re.compile(r"(?i)(token\s*[:=]\s*)[A-Za-z0-9._~+/=-]{16,}"),
    re.compile(r"(?i)(password\s*[:=]\s*)[^,\s;]+"),
    re.compile(r"sk-[A-Za-z0-9]{20,}"),
    re.compile(r"ou_[A-Za-z0-9]{12,}"),
    re.compile(r"cli_[A-Za-z0-9]{12,}"),
]


def redact_text(value: str) -> str:
    redacted = value
    for pattern in SECRET_PATTERNS:
        if pattern.groups:
            redacted = pattern.sub(lambda m: f"{m.group(1)}{REDACTION}", redacted)
        else:
            redacted = pattern.sub(REDACTION, redacted)
    return redacted


def _is_sensitive_key(key: str) -> bool:
    normalized = key.replace("-", "_").lower()
    return normalized in SENSITIVE_KEYS or normalized.endswith("_secret") or normalized.endswith("_token")


def redact(value: Any) -> Any:
    if isinstance(value, str):
        return redact_text(value)
    if isinstance(value, list):
        return [redact(item) for item in value]
    if isinstance(value, tuple):
        return tuple(redact(item) for item in value)
    if isinstance(value, dict):
        result: dict[Any, Any] = {}
        for key, item in value.items():
            if isinstance(key, str) and _is_sensitive_key(key):
                result[key] = REDACTION
            else:
                result[key] = redact(item)
        return result
    return value

