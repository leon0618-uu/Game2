from __future__ import annotations

from dataclasses import dataclass, field
from enum import Enum
from typing import Any


class EscalationReason(str, Enum):
    NONE = "NONE"
    FAILED = "FAILED"
    TIMED_OUT = "TIMED_OUT"
    LOST = "LOST"
    CAPABILITY_GAP = "CAPABILITY_GAP"
    TOOL_OR_ENVIRONMENT_MISSING = "TOOL_OR_ENVIRONMENT_MISSING"
    BLOCKING_VALIDATION_ERROR = "BLOCKING_VALIDATION_ERROR"
    CONFLICTING_AGENT_CONCLUSIONS = "CONFLICTING_AGENT_CONCLUSIONS"
    DESTRUCTIVE_RISK = "DESTRUCTIVE_RISK"
    CORE_RULE_CHANGE_REQUIRED = "CORE_RULE_CHANGE_REQUIRED"
    STALLED = "STALLED"
    REPEATED_ERROR = "REPEATED_ERROR"
    REPEATED_REPAIR_FAILURE = "REPEATED_REPAIR_FAILURE"
    PLAN_CYCLE = "PLAN_CYCLE"
    REWORK_LIMIT = "REWORK_LIMIT"
    FILE_CHURN = "FILE_CHURN"
    FLAKY_TESTS = "FLAKY_TESTS"


class SkillRisk(str, Enum):
    LOW = "low"
    MEDIUM = "medium"
    HIGH = "high"


class SkillCandidateDecision(str, Enum):
    CREATE = "CREATE"
    DO_NOT_CREATE = "DO_NOT_CREATE"


@dataclass(frozen=True)
class ProgressSnapshot:
    git_diff_hash: str = ""
    changed_file_count: int = 0
    log_size: int = 0
    test_total: int = 0
    test_passed: int = 0
    test_failed: int = 0
    tool_call_count: int = 0
    diagnostic_hash: str = ""
    subtask_state: str = ""


@dataclass(frozen=True)
class TaskSnapshot:
    task_id: str
    state: str
    source_agent: str
    capability_gap: bool = False
    missing_tool_or_environment: bool = False
    compile_failed: bool = False
    test_failed: bool = False
    build_failed: bool = False
    conflicting_agent_conclusions: bool = False
    destructive_risk: bool = False
    core_rule_change_required: bool = False
    consecutive_no_progress_checks: int = 0
    same_error_count: int = 0
    same_repair_failure_count: int = 0
    plan_cycle_count: int = 0
    automatic_rework_count: int = 0
    repeated_file_churn: bool = False
    flaky_tests: bool = False


@dataclass(frozen=True)
class SupervisorDecision:
    escalate: bool
    reason: EscalationReason = EscalationReason.NONE
    details: str = ""


@dataclass(frozen=True)
class SkillCandidateInput:
    name: str
    same_issue_count: int = 0
    successful_workflow_count: int = 0
    complex_and_reusable: bool = False
    project_hard_constraint: bool = False
    could_damage_project: bool = False
    repeated_validation_miss: bool = False
    shared_handoff_format: bool = False
    one_off: bool = False
    validated: bool = False
    file_specific_patch_only: bool = False
    contains_secret_or_private_data: bool = False
    unclear_source_scripts: bool = False
    overbroad_permissions: bool = False
    has_validation_method: bool = True


@dataclass(frozen=True)
class SkillCandidateResult:
    decision: SkillCandidateDecision
    reasons: list[str] = field(default_factory=list)


JsonObject = dict[str, Any]

