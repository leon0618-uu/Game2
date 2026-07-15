from __future__ import annotations

import shutil
import subprocess
from pathlib import Path


def resolve_command(command: list[str]) -> list[str]:
    if not command:
        raise ValueError("command cannot be empty")
    executable = command[0]
    if executable.lower() != "openclaw":
        return command
    ps1 = shutil.which("openclaw.ps1")
    if ps1:
        return ["powershell", "-NoProfile", "-ExecutionPolicy", "Bypass", "-File", ps1, *command[1:]]
    resolved = shutil.which("openclaw")
    if resolved:
        return [resolved, *command[1:]]
    cmd = shutil.which("openclaw.cmd")
    if cmd:
        return [cmd, *command[1:]]
    return command


def run_command(command: list[str], cwd: Path, timeout_seconds: int) -> subprocess.CompletedProcess[str]:
    return subprocess.run(
        resolve_command(command),
        cwd=cwd,
        text=True,
        encoding="utf-8",
        capture_output=True,
        timeout=timeout_seconds,
        check=False,
    )

