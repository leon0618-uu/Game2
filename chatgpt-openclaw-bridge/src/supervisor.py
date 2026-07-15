from __future__ import annotations

import json
import time
from dataclasses import dataclass
from pathlib import Path
from typing import Any

from .audit_logger import AuditLogger
from .feishu_reporter import blocked_message, intervention_message, write_outbox_message
from .intervention_directive import build_fallback_intervention
from .openai_client import DEFAULT_MODEL, OpenAIClient
from .openclaw_client import OpenClawClient
from .task_intake import blocked_task_candidates, task_to_blocked_package


@dataclass(frozen=True)
class SupervisorPaths:
    incidents: Path
    interventions: Path
    outbox: Path
    audit: Path


@dataclass(frozen=True)
class SupervisorOptions:
    status: str = "failed"
    runtime: str | None = None
    limit: int = 5
    call_openai: bool = False
    model: str = DEFAULT_MODEL


def run_supervisor_scan(repo_root: Path, paths: SupervisorPaths, options: SupervisorOptions) -> dict[str, Any]:
    client = OpenClawClient(repo_root)
    tasks = client.parsed_tasks_list(status=options.status, runtime=options.runtime)
    candidates = blocked_task_candidates(tasks, limit=options.limit)
    incident_logger = AuditLogger(paths.incidents)
    intervention_logger = AuditLogger(paths.interventions)
    audit_logger = AuditLogger(paths.audit)
    openai_client = OpenAIClient(model=options.model)

    records: list[dict[str, Any]] = []
    environment = _minimal_environment(repo_root)
    for candidate in candidates:
        task = candidate["task"]
        decision = candidate["decision"]
        package = task_to_blocked_package(task, environment)
        incident_path = incident_logger.write_event("blocked-task", package, request_id=package["task_id"])

        openai_result = openai_client.request_intervention(package) if options.call_openai else None
        if openai_result and openai_result.ok and openai_result.payload:
            directive = openai_result.payload
        else:
            directive = build_fallback_intervention(package, reason=decision.reason.value)
            if openai_result and not openai_result.ok:
                directive["root_cause"] = f"OpenAI unavailable: {openai_result.error}. Local fallback directive only."
        intervention_path = intervention_logger.write_event("intervention-directive", directive, request_id=package["task_id"])

        blocked_outbox = write_outbox_message(
            paths.outbox,
            "blocked",
            blocked_message(
                task=package["task_id"],
                blocked_minutes=0,
                problem=package["current_problem"],
                attempts=f"{package['attempt_count']} recorded attempt(s)",
            ),
        )
        intervention_outbox = write_outbox_message(
            paths.outbox,
            "intervention",
            intervention_message(
                root_cause=directive["root_cause"],
                action=directive["decision"],
                executor=package["source_agent"],
                verifier="xingyuan-qa",
                confidence=int(float(directive.get("confidence", 0.0)) * 100),
            ),
        )
        records.append(
            {
                "task_id": package["task_id"],
                "reason": decision.reason.value,
                "incident": str(incident_path),
                "intervention": str(intervention_path),
                "outbox": [str(blocked_outbox), str(intervention_outbox)],
            }
        )

    audit_path = audit_logger.write_event("supervisor-scan", {"count": len(records), "records": records})
    return {"count": len(records), "records": records, "audit": str(audit_path)}


def run_supervisor_loop(
    repo_root: Path,
    paths: SupervisorPaths,
    options: SupervisorOptions,
    iterations: int,
    interval_seconds: int,
) -> list[dict[str, Any]]:
    results: list[dict[str, Any]] = []
    for index in range(iterations):
        results.append(run_supervisor_scan(repo_root, paths, options))
        if index < iterations - 1:
            time.sleep(interval_seconds)
    return results


def _minimal_environment(repo_root: Path) -> dict[str, str]:
    version_file = repo_root / "ProjectSettings" / "ProjectVersion.txt"
    unity_version = "unknown"
    if version_file.exists():
        for line in version_file.read_text(encoding="utf-8").splitlines():
            if line.startswith("m_EditorVersion:"):
                unity_version = line.split(":", 1)[1].strip()
                break
    return {
        "os": "Windows",
        "unity_version": unity_version,
        "dotnet_version": "not checked by supervisor scan",
        "openclaw_version": "checked by environment audit",
    }
