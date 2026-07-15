import tempfile
import unittest
from pathlib import Path

from src.approval_request import build_approval_decision, build_approval_request
from src.risk_executor import approval_matches_plan, build_risk_execution_preview, command_has_placeholder, validate_risk_plan, write_risk_execution
from src.risk_plan import build_risk_plan


class RiskExecutorTests(unittest.TestCase):
    def test_validates_risk_plan(self):
        plan = build_risk_plan("openai_real_call")
        self.assertEqual(validate_risk_plan(plan), [])

    def test_placeholder_detection(self):
        self.assertTrue(command_has_placeholder("python -m src.main intervention --package <incident.json> --call-openai"))
        self.assertTrue(command_has_placeholder("python -m src.main feishu-dry-run ..."))
        self.assertFalse(command_has_placeholder("python -m src.main readiness-audit"))

    def test_preview_without_decision_is_not_executable(self):
        plan = build_risk_plan("openai_real_call")
        preview = build_risk_execution_preview(plan)
        self.assertEqual(preview["mode"], "preview")
        self.assertFalse(preview["approved"])
        self.assertFalse(preview["can_execute"])
        self.assertFalse(preview["blocked"])

    def test_approval_must_match_plan_commands(self):
        plan = build_risk_plan("openai_real_call")
        request = build_approval_request(
            item=plan["item"],
            reason=plan["reason"],
            recommendation=plan["recommendation"],
            risk=plan["risk"],
            impact=plan["impact"],
            rollback=plan["rollback"],
            action_type=plan["action_type"],
            command=["different command"],
        )
        decision = build_approval_decision(request=request, decision="approve", decided_by="Leon")
        approved, reason = approval_matches_plan(decision, plan)
        self.assertFalse(approved)
        self.assertIn("does not match", reason)

    def test_approved_placeholder_plan_still_cannot_execute(self):
        plan = build_risk_plan("openai_real_call")
        request = build_approval_request(
            item=plan["item"],
            reason=plan["reason"],
            recommendation=plan["recommendation"],
            risk=plan["risk"],
            impact=plan["impact"],
            rollback=plan["rollback"],
            action_type=plan["action_type"],
            command=plan["planned_commands"],
        )
        decision = build_approval_decision(request=request, decision="approve", decided_by="Leon")
        preview = build_risk_execution_preview(plan, decision, mode="execute")
        self.assertEqual(preview["mode"], "execute")
        self.assertTrue(preview["approved"])
        self.assertFalse(preview["can_execute"])
        self.assertTrue(preview["blocked"])
        self.assertTrue(preview["placeholder_commands"])

    def test_write_risk_execution(self):
        plan = build_risk_plan("openai_real_call")
        preview = build_risk_execution_preview(plan)
        with tempfile.TemporaryDirectory() as tmp:
            path = write_risk_execution(Path(tmp), preview)
            self.assertTrue(path.exists())


if __name__ == "__main__":
    unittest.main()
