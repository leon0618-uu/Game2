from __future__ import annotations

from .models import EscalationReason


def default_intervention_level(reason: EscalationReason) -> str:
    if reason in {EscalationReason.CAPABILITY_GAP, EscalationReason.TOOL_OR_ENVIRONMENT_MISSING}:
        return "L5"
    if reason in {EscalationReason.STALLED, EscalationReason.CONFLICTING_AGENT_CONCLUSIONS}:
        return "L2"
    if reason in {EscalationReason.REWORK_LIMIT, EscalationReason.PLAN_CYCLE, EscalationReason.CORE_RULE_CHANGE_REQUIRED}:
        return "L4"
    if reason in {EscalationReason.FAILED, EscalationReason.TIMED_OUT, EscalationReason.LOST, EscalationReason.BLOCKING_VALIDATION_ERROR}:
        return "L3"
    return "L1"

