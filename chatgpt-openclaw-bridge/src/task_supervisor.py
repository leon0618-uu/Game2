from __future__ import annotations

from .models import EscalationReason, SupervisorDecision, TaskSnapshot


IMMEDIATE_STATE_REASONS = {
    "failed": EscalationReason.FAILED,
    "timed_out": EscalationReason.TIMED_OUT,
    "lost": EscalationReason.LOST,
}


def evaluate_task(snapshot: TaskSnapshot) -> SupervisorDecision:
    state = snapshot.state.lower()
    if state in IMMEDIATE_STATE_REASONS:
        return SupervisorDecision(True, IMMEDIATE_STATE_REASONS[state], f"Task state is {snapshot.state}.")
    if snapshot.capability_gap:
        return SupervisorDecision(True, EscalationReason.CAPABILITY_GAP, "Agent reported CAPABILITY_GAP.")
    if snapshot.missing_tool_or_environment:
        return SupervisorDecision(True, EscalationReason.TOOL_OR_ENVIRONMENT_MISSING, "Required tool or environment is missing.")
    if snapshot.compile_failed or snapshot.test_failed or snapshot.build_failed:
        return SupervisorDecision(True, EscalationReason.BLOCKING_VALIDATION_ERROR, "Compile, test, or build failed.")
    if snapshot.conflicting_agent_conclusions:
        return SupervisorDecision(True, EscalationReason.CONFLICTING_AGENT_CONCLUSIONS, "Agents disagree on a core technical conclusion.")
    if snapshot.destructive_risk:
        return SupervisorDecision(True, EscalationReason.DESTRUCTIVE_RISK, "Current operation may damage project, data, or Git history.")
    if snapshot.core_rule_change_required:
        return SupervisorDecision(True, EscalationReason.CORE_RULE_CHANGE_REQUIRED, "Fix requires changing confirmed gameplay rules.")
    if snapshot.consecutive_no_progress_checks >= 3:
        return SupervisorDecision(True, EscalationReason.STALLED, "No substantive progress for 3 consecutive checks.")
    if snapshot.same_error_count >= 2:
        return SupervisorDecision(True, EscalationReason.REPEATED_ERROR, "Same error repeated at least twice.")
    if snapshot.same_repair_failure_count >= 2:
        return SupervisorDecision(True, EscalationReason.REPEATED_REPAIR_FAILURE, "Same repair plan failed at least twice.")
    if snapshot.plan_cycle_count >= 3:
        return SupervisorDecision(True, EscalationReason.PLAN_CYCLE, "Agent is cycling among plans.")
    if snapshot.automatic_rework_count >= 3:
        return SupervisorDecision(True, EscalationReason.REWORK_LIMIT, "Automatic rework limit reached.")
    if snapshot.repeated_file_churn:
        return SupervisorDecision(True, EscalationReason.FILE_CHURN, "Repeated file churn without resolution.")
    if snapshot.flaky_tests:
        return SupervisorDecision(True, EscalationReason.FLAKY_TESTS, "Tests oscillate between pass and fail.")
    return SupervisorDecision(False)

