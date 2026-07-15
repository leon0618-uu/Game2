import json
import tempfile
import unittest
from pathlib import Path

from src.approval_request import build_approval_decision, build_approval_request
from src.openai_intervention_gate import build_openai_intervention_command, build_openai_intervention_preview


class OpenAIInterventionGateTests(unittest.TestCase):
    def test_preview_redacts_blocked_package(self):
        fake_token = "abc12345" + "678901234567890"
        preview = build_openai_intervention_preview(
            package_path="incident.json",
            package={"task_id": "T1", "error": f"token={fake_token}"},
            model="test-model",
        )
        encoded = json.dumps(preview)
        self.assertNotIn(fake_token, encoded)
        self.assertEqual(preview["mode"], "preview")
        self.assertFalse(preview["executed"])

    def test_execute_without_approval_is_blocked(self):
        preview = build_openai_intervention_preview(
            package_path="incident.json",
            package={"task_id": "T1"},
            model="test-model",
            execute=True,
        )
        self.assertTrue(preview["blocked"])
        self.assertFalse(preview["can_execute"])

    def test_approval_must_match_exact_command(self):
        command = build_openai_intervention_command("incident.json", "test-model", execute=True)
        request = build_approval_request(
            item="Call OpenAI",
            reason="Blocked task needs intervention.",
            recommendation="Approve exact command.",
            risk="high",
            impact="Sends redacted package to OpenAI.",
            rollback="Use fallback intervention.",
            action_type="other",
            command=command,
        )
        decision = build_approval_decision(request=request, decision="approve", decided_by="Leon")
        preview = build_openai_intervention_preview(
            package_path="incident.json",
            package={"task_id": "T1"},
            model="test-model",
            execute=True,
            approval_decision=decision,
        )
        self.assertTrue(preview["approved"])
        self.assertTrue(preview["can_execute"])


if __name__ == "__main__":
    unittest.main()
