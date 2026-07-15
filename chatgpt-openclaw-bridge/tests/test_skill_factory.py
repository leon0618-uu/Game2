import unittest

from src.approval_gate import requires_user_approval
from src.models import SkillCandidateDecision, SkillCandidateInput, SkillRisk
from src.skill_factory import evaluate_skill_candidate
from src.skill_validator import classify_install_risk


class SkillFactoryTests(unittest.TestCase):
    def test_creates_candidate_for_repeated_validated_workflow(self):
        result = evaluate_skill_candidate(
            SkillCandidateInput(name="compile-error-triage", successful_workflow_count=2, validated=True)
        )
        self.assertEqual(result.decision, SkillCandidateDecision.CREATE)

    def test_rejects_unvalidated_candidate_even_with_trigger(self):
        result = evaluate_skill_candidate(SkillCandidateInput(name="bad-skill", same_issue_count=2, validated=False))
        self.assertEqual(result.decision, SkillCandidateDecision.DO_NOT_CREATE)
        self.assertIn("workflow is not validated", result.reasons)

    def test_high_risk_install_requires_user_approval(self):
        risk = classify_install_risk("plugin install that opens a network port")
        self.assertEqual(risk, SkillRisk.HIGH)
        self.assertTrue(requires_user_approval("install skill", risk))

    def test_low_risk_markdown_does_not_require_user_approval(self):
        risk = classify_install_risk("markdown checklist for agent handoff")
        self.assertEqual(risk, SkillRisk.LOW)
        self.assertFalse(requires_user_approval("install low-risk skill", risk))


if __name__ == "__main__":
    unittest.main()

