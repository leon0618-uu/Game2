from __future__ import annotations

from dataclasses import dataclass
from pathlib import Path

from .command_runner import run_command


@dataclass(frozen=True)
class WorkshopPlan:
    command: list[str]
    risk_note: str


def propose_create_plan(name: str, description: str, proposal_dir: Path, agent: str | None = None) -> WorkshopPlan:
    command = [
        "openclaw",
        "skills",
        "workshop",
    ]
    if agent:
        command.extend(["--agent", agent])
    command.extend(
        [
            "propose-create",
            "--name",
            name,
            "--description",
            description,
            "--proposal-dir",
            str(proposal_dir),
        ]
    )
    return WorkshopPlan(command, "Creates a pending proposal; does not apply or install the Skill.")


def inspect_plan(proposal_id: str, agent: str | None = None) -> WorkshopPlan:
    command = ["openclaw", "skills", "workshop"]
    if agent:
        command.extend(["--agent", agent])
    command.extend(["inspect", proposal_id])
    return WorkshopPlan(command, "Read-only proposal inspection.")


def apply_plan(proposal_id: str, agent: str | None = None) -> WorkshopPlan:
    command = ["openclaw", "skills", "workshop"]
    if agent:
        command.extend(["--agent", agent])
    command.extend(["apply", proposal_id])
    return WorkshopPlan(command, "Applies a Skill proposal; requires prior safety review and approval gate.")


def execute_plan(plan: WorkshopPlan, cwd: Path, timeout_seconds: int = 120) -> tuple[int, str, str]:
    completed = run_command(plan.command, cwd=cwd, timeout_seconds=timeout_seconds)
    return completed.returncode, completed.stdout, completed.stderr

