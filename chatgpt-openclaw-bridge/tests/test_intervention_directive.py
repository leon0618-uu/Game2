import unittest

from src.intervention_directive import build_fallback_intervention, validate_intervention_directive


class InterventionDirectiveTests(unittest.TestCase):
    def test_fallback_directive_is_valid(self):
        directive = build_fallback_intervention(
            {"task_id": "TASK-1", "source_agent": "xingyuan-qa", "current_problem": "Compile failed"},
            reason="FAILED",
        )
        self.assertEqual(validate_intervention_directive(directive), [])
        self.assertEqual(directive["intervention_level"], "L3")

    def test_core_rule_change_requires_user_approval(self):
        directive = build_fallback_intervention({"task_id": "TASK-2"}, reason="CORE_RULE_CHANGE_REQUIRED")
        self.assertTrue(directive["user_approval_required"])


if __name__ == "__main__":
    unittest.main()

