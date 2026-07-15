from __future__ import annotations

import json
import subprocess
from pathlib import Path
from typing import Any, Callable

from .audit_logger import utc_timestamp
from .command_runner import run_command
from .secret_filter import redact


SECURITY_COMMANDS = {
    "secrets_audit": ["openclaw", "secrets", "audit", "--check", "--json"],
    "security_audit": ["openclaw", "security", "audit", "--json"],
    "config_validate": ["openclaw", "config", "validate", "--json"],
}


Runner = Callable[[list[str], Path, int], subprocess.CompletedProcess[str]]


def _parse_json_or_text(value: str) -> Any:
    stripped = value.strip()
    if not stripped:
        return ""
    try:
        return json.loads(stripped)
    except json.JSONDecodeError:
        return stripped[-4000:]


def build_openclaw_security_snapshot(
    repo_root: Path,
    timeout_seconds: int = 60,
    runner: Runner = run_command,
) -> dict[str, Any]:
    results: dict[str, Any] = {}
    for name, command in SECURITY_COMMANDS.items():
        try:
            completed = runner(command, repo_root, timeout_seconds)
            results[name] = {
                "command": command,
                "returncode": completed.returncode,
                "stdout": _parse_json_or_text(completed.stdout),
                "stderr": _parse_json_or_text(completed.stderr),
            }
        except Exception as exc:  # pragma: no cover - defensive external CLI boundary
            results[name] = {
                "command": command,
                "returncode": -1,
                "stdout": "",
                "stderr": f"{type(exc).__name__}: {exc}",
            }
    snapshot = {
        "snapshot_id": f"OPENCLAW-SECURITY-{utc_timestamp()}",
        "created_at": utc_timestamp(),
        "repo_root": str(repo_root),
        "read_only": True,
        "commands": SECURITY_COMMANDS,
        "results": results,
        "sensitive_data_removed": True,
    }
    return redact(snapshot)


def write_openclaw_security_snapshot(output_dir: Path, snapshot: dict[str, Any]) -> Path:
    output_dir.mkdir(parents=True, exist_ok=True)
    safe_id = "".join(ch if ch.isalnum() or ch in ("-", "_") else "-" for ch in snapshot["snapshot_id"])
    path = output_dir / f"{utc_timestamp()}-openclaw-security-snapshot-{safe_id}.json"
    path.write_text(json.dumps(redact(snapshot), ensure_ascii=False, indent=2), encoding="utf-8")
    return path
