from __future__ import annotations

from .models import SkillCandidateDecision, SkillCandidateInput, SkillCandidateResult


def evaluate_skill_candidate(candidate: SkillCandidateInput) -> SkillCandidateResult:
    blockers: list[str] = []
    if candidate.one_off:
        blockers.append("one-off operation")
    if not candidate.validated:
        blockers.append("workflow is not validated")
    if candidate.file_specific_patch_only:
        blockers.append("file-specific patch only")
    if candidate.contains_secret_or_private_data:
        blockers.append("contains secret or private data")
    if candidate.unclear_source_scripts:
        blockers.append("uses unclear-source scripts")
    if candidate.overbroad_permissions:
        blockers.append("requests overbroad permissions")
    if not candidate.has_validation_method:
        blockers.append("no validation method")
    if blockers:
        return SkillCandidateResult(SkillCandidateDecision.DO_NOT_CREATE, blockers)

    triggers: list[str] = []
    if candidate.same_issue_count >= 2:
        triggers.append("same issue appeared at least twice")
    if candidate.successful_workflow_count >= 2:
        triggers.append("workflow succeeded at least twice")
    if candidate.complex_and_reusable:
        triggers.append("complex and reusable")
    if candidate.project_hard_constraint:
        triggers.append("project hard constraint")
    if candidate.could_damage_project:
        triggers.append("mistake could damage project")
    if candidate.repeated_validation_miss:
        triggers.append("repeated validation miss")
    if candidate.shared_handoff_format:
        triggers.append("shared handoff format")
    if triggers:
        return SkillCandidateResult(SkillCandidateDecision.CREATE, triggers)
    return SkillCandidateResult(SkillCandidateDecision.DO_NOT_CREATE, ["no creation trigger matched"])

