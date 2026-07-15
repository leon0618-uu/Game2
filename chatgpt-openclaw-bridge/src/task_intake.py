from __future__ import annotations

from typing import Any

from .context_collector import sanitize_blocked_task_package
from .models import TaskSnapshot
from .task_supervisor import evaluate_task


BLOCKED_STATUSES = {"failed", "timed_out", "lost"}


def iter_tasks(tasks_payload: Any) -> list[dict[str, Any]]:
    tasks = tasks_payload.get("tasks", []) if isinstance(tasks_payload, dict) else tasks_payload
    if not isinstance(tasks, list):
        return []
    return [task for task in tasks if isinstance(task, dict)]


def task_to_snapshot(task: dict[str, Any]) -> TaskSnapshot:
    status = str(task.get("status", "unknown"))
    error_text = " ".join(
        str(task.get(field, ""))
        for field in ["error", "terminalSummary", "progressSummary", "task", "label"]
    )
    return TaskSnapshot(
        task_id=str(task.get("taskId", "")),
        state=status,
        source_agent=str(task.get("agentId", "")),
        capability_gap="CAPABILITY_GAP" in error_text,
        missing_tool_or_environment=any(term in error_text.lower() for term in ["tool missing", "unavailable", "not found"]),
        compile_failed="compile" in error_text.lower() and "failed" in error_text.lower(),
        test_failed="test" in error_text.lower() and "failed" in error_text.lower(),
        build_failed="build" in error_text.lower() and "failed" in error_text.lower(),
    )


def blocked_task_candidates(tasks_payload: Any, limit: int | None = None) -> list[dict[str, Any]]:
    candidates: list[dict[str, Any]] = []
    for task in iter_tasks(tasks_payload):
        snapshot = task_to_snapshot(task)
        decision = evaluate_task(snapshot)
        if decision.escalate:
            candidates.append({"task": task, "decision": decision})
            if limit is not None and len(candidates) >= limit:
                break
    return candidates


def task_to_blocked_package(task: dict[str, Any], environment: dict[str, str]) -> dict[str, Any]:
    task_id = str(task.get("taskId", ""))
    status = str(task.get("status", "unknown"))
    label = str(task.get("label") or task.get("task") or "")
    error = str(task.get("error") or "")
    terminal_summary = str(task.get("terminalSummary") or "")
    progress_summary = str(task.get("progressSummary") or "")
    command_evidence = [f"openclaw tasks show {task_id} --json"] if task_id else ["openclaw tasks list --json"]
    logs = [value for value in [error, terminal_summary, progress_summary] if value]
    raw_package = {
        "task_id": task_id,
        "project": "Xingyuan Covenant",
        "branch": "",
        "source_agent": str(task.get("agentId", "")),
        "task_status": status,
        "goal": label[:500],
        "acceptance_criteria": [],
        "current_problem": error or terminal_summary[:500] or progress_summary[:500] or f"OpenClaw task status is {status}",
        "first_error_time": str(task.get("startedAt") or task.get("createdAt") or ""),
        "last_progress_time": str(task.get("lastEventAt") or task.get("endedAt") or ""),
        "attempt_count": 1,
        "attempted_solutions": [],
        "commands_executed": command_evidence,
        "error_logs": logs[:3],
        "stack_trace": [],
        "git_diff_summary": "",
        "changed_files": [],
        "environment": environment,
        "agent_conclusion": f"OpenClaw task ended with status {status}.",
        "agent_confidence": 0.5,
        "requested_help": "Diagnose blocked OpenClaw task and provide next intervention directive.",
        "screenshots": [],
        "sensitive_data_removed": False,
    }
    return sanitize_blocked_task_package(raw_package)

