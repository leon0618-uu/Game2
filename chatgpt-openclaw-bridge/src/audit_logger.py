from __future__ import annotations

import json
from datetime import datetime, timezone
from pathlib import Path
from typing import Any

from .secret_filter import redact


def utc_timestamp() -> str:
    return datetime.now(timezone.utc).strftime("%Y%m%dT%H%M%SZ")


class AuditLogger:
    def __init__(self, root: Path) -> None:
        self.root = root
        self.root.mkdir(parents=True, exist_ok=True)

    def write_event(self, event_type: str, payload: dict[str, Any], request_id: str | None = None) -> Path:
        safe_event_type = "".join(ch if ch.isalnum() or ch in ("-", "_") else "-" for ch in event_type)
        safe_request_id = request_id or payload.get("request_id") or payload.get("task_id") or "event"
        safe_request_id = "".join(ch if ch.isalnum() or ch in ("-", "_") else "-" for ch in str(safe_request_id))
        path = self.root / f"{utc_timestamp()}-{safe_event_type}-{safe_request_id}.json"
        record = {
            "event_type": event_type,
            "written_at": utc_timestamp(),
            "payload": redact(payload),
        }
        path.write_text(json.dumps(record, ensure_ascii=False, indent=2), encoding="utf-8")
        return path

