from __future__ import annotations

import json
from dataclasses import dataclass
from pathlib import Path
from typing import Any

from .command_runner import run_command


@dataclass(frozen=True)
class OpenClawCommandResult:
    returncode: int
    stdout: str
    stderr: str


class OpenClawClient:
    def __init__(self, repo_root: Path, timeout_seconds: int = 600) -> None:
        self.repo_root = repo_root
        self.timeout_seconds = timeout_seconds

    def run_agent(self, agent_id: str, message_file: Path, session_key: str | None = None) -> OpenClawCommandResult:
        command = [
            "openclaw",
            "agent",
            "--agent",
            agent_id,
            "--message-file",
            str(message_file),
            "--json",
            "--timeout",
            str(self.timeout_seconds),
        ]
        if session_key:
            command.extend(["--session-key", session_key])
        completed = run_command(command, cwd=self.repo_root, timeout_seconds=self.timeout_seconds + 30)
        return OpenClawCommandResult(completed.returncode, completed.stdout, completed.stderr)

    @staticmethod
    def parse_json_stdout(result: OpenClawCommandResult) -> dict:
        return json.loads(result.stdout)

    def health(self) -> OpenClawCommandResult:
        return self._run(["openclaw", "gateway", "call", "health"], timeout_seconds=60)

    def tasks_list(self, status: str | None = None, runtime: str | None = None) -> OpenClawCommandResult:
        command = ["openclaw", "tasks", "list", "--json"]
        if status:
            command.extend(["--status", status])
        if runtime:
            command.extend(["--runtime", runtime])
        return self._run(command, timeout_seconds=60)

    def parsed_health(self) -> dict[str, Any]:
        result = self.health()
        if result.returncode != 0:
            raise RuntimeError(result.stderr or result.stdout)
        return self._extract_json_object(result.stdout)

    def parsed_tasks_list(self, status: str | None = None, runtime: str | None = None) -> Any:
        result = self.tasks_list(status=status, runtime=runtime)
        if result.returncode != 0:
            raise RuntimeError(result.stderr or result.stdout)
        return self._extract_json_object_or_array(result.stdout)

    def _run(self, command: list[str], timeout_seconds: int) -> OpenClawCommandResult:
        completed = run_command(command, cwd=self.repo_root, timeout_seconds=timeout_seconds)
        return OpenClawCommandResult(completed.returncode, completed.stdout, completed.stderr)

    @staticmethod
    def _extract_json_object(stdout: str) -> dict[str, Any]:
        value = OpenClawClient._extract_json_object_or_array(stdout)
        if not isinstance(value, dict):
            raise ValueError("Expected JSON object")
        return value

    @staticmethod
    def _extract_json_object_or_array(stdout: str) -> Any:
        stripped = stdout.strip()
        if not stripped:
            raise ValueError("No JSON output")
        for index, char in enumerate(stripped):
            if char in "{[":
                return json.loads(stripped[index:])
        raise ValueError("No JSON object or array found")
