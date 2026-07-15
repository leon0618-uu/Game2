from __future__ import annotations

from typing import Any

from .intervention_engine import default_intervention_level
from .models import EscalationReason


def build_fallback_intervention(package: dict[str, Any], reason: str = "FAILED") -> dict[str, Any]:
    escalation_reason = _parse_reason(reason)
    level = default_intervention_level(escalation_reason)
    task_id = str(package.get("task_id", ""))
    source_agent = str(package.get("source_agent", "xingyuan-lead"))
    current_problem = str(package.get("current_problem", ""))
    return {
        "intervention_id": f"INT-LOCAL-{task_id or 'UNKNOWN'}",
        "task_id": task_id,
        "root_cause": "Unknown. Local fallback directive requires ChatGPT/OpenAI review before repair.",
        "confidence": 0.0,
        "intervention_level": level,
        "decision": "REWORK" if level in {"L2", "L3"} else "REPLAN",
        "stop_current_attempts": ["Do not repeat the same failed command without new evidence."],
        "instructions": [
            {
                "agent": source_agent,
                "action": "Collect missing evidence, isolate the first failing command, and return updated BlockedTaskPackage.",
                "files": list(package.get("changed_files", []))[:20],
                "commands": list(package.get("commands_executed", []))[:10],
            }
        ],
        "patches": [],
        "tests_required": [],
        "acceptance_criteria": [
            "Root cause is identified.",
            "A repair or replan directive is reviewed before implementation.",
            "QA evidence is attached before closing the task.",
        ],
        "rollback": ["Do not apply code changes from this fallback directive."],
        "skill_candidate": "same failure" in current_problem.lower() or escalation_reason == EscalationReason.REPEATED_ERROR,
        "user_approval_required": escalation_reason == EscalationReason.CORE_RULE_CHANGE_REQUIRED,
    }


def _parse_reason(reason: str) -> EscalationReason:
    try:
        return EscalationReason[reason]
    except KeyError:
        try:
            return EscalationReason(reason)
        except ValueError:
            return EscalationReason.FAILED


def validate_intervention_directive(directive: dict[str, Any]) -> list[str]:
    required = [
        "intervention_id",
        "task_id",
        "root_cause",
        "confidence",
        "intervention_level",
        "decision",
        "stop_current_attempts",
        "instructions",
        "patches",
        "tests_required",
        "acceptance_criteria",
        "rollback",
        "skill_candidate",
        "user_approval_required",
    ]
    errors: list[str] = []
    for field in required:
        if field not in directive:
            errors.append(f"missing field: {field}")
    if directive.get("intervention_level") not in {"L1", "L2", "L3", "L4", "L5"}:
        errors.append("invalid intervention_level")
    if directive.get("decision") not in {
        "CONTINUE",
        "REPAIR",
        "REWORK",
        "REPLAN",
        "CREATE_SKILL",
        "INSTALL_TOOL",
        "USER_APPROVAL_REQUIRED",
        "BLOCKED_EXTERNAL",
        "STOP",
        "PASS",
        "CONDITIONAL_PASS",
    }:
        errors.append("invalid decision")
    return errors

