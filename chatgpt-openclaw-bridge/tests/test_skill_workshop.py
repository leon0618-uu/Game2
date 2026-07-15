import unittest
from pathlib import Path

from src.skill_workshop import apply_plan, inspect_plan, propose_create_plan


class SkillWorkshopTests(unittest.TestCase):
    def test_propose_create_plan_contains_expected_command(self):
        plan = propose_create_plan("skill-a", "desc", Path("proposal"), agent="xingyuan-lead")
        self.assertIn("propose-create", plan.command)
        self.assertIn("--agent", plan.command)
        self.assertIn("xingyuan-lead", plan.command)

    def test_inspect_plan_is_read_only(self):
        plan = inspect_plan("proposal-1")
        self.assertEqual(plan.command[-2:], ["inspect", "proposal-1"])

    def test_apply_plan_mentions_approval(self):
        plan = apply_plan("proposal-1")
        self.assertIn("requires", plan.risk_note)


if __name__ == "__main__":
    unittest.main()

