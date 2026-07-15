import json
import tempfile
import unittest
from pathlib import Path

from src.approval_request import (
    build_approval_decision,
    build_approval_request,
    is_approved_decision_for_command,
    validate_approval_decision,
    validate_approval_request,
    write_approval_decision,
    write_approval_outbox,
    write_approval_request,
)


class ApprovalRequestTests(unittest.TestCase):
    def test_builds_valid_pending_request(self):
        request = build_approval_request(
            item="Apply Skill proposal",
            reason="Skill apply changes agent behavior.",
            recommendation="Approve only after safety scan passes.",
            risk="high",
            impact="One agent may receive a new workflow.",
            rollback="Remove the applied Skill and rerun OpenClaw skills check.",
            action_type="skill_apply",
            command=["openclaw", "skills", "workshop", "apply", "proposal-1"],
        )
        self.assertEqual(validate_approval_request(request), [])
        self.assertEqual(request["status"], "pending")
        self.assertTrue(request["requires_user_approval"])

    def test_invalid_action_is_rejected(self):
        request = build_approval_request(
            item="Unknown",
            reason="Needs approval.",
            recommendation="Review.",
            risk="high",
            impact="Unknown.",
            rollback="Stop.",
            action_type="unknown",
        )
        self.assertIn("action_type must be one of", "\n".join(validate_approval_request(request)))

    def test_write_request_and_outbox_redact_secrets(self):
        with tempfile.TemporaryDirectory() as tmp:
            root = Path(tmp)
            request = build_approval_request(
                item="Configure secret",
                reason="token=FAKE_TEST_TOKEN_FOR_REDACTION must not leak",
                recommendation="Use SecretRefs.",
                risk="high",
                impact="Local OpenClaw config changes.",
                rollback="Restore prior config backup.",
                action_type="secret_config",
            )
            request_path = write_approval_request(root / "approvals", request)
            outbox_path = write_approval_outbox(root / "outbox", request)
            encoded = request_path.read_text(encoding="utf-8") + outbox_path.read_text(encoding="utf-8")
            self.assertNotIn("FAKE_TEST_TOKEN_FOR_REDACTION", encoded)
            self.assertTrue(json.loads(request_path.read_text(encoding="utf-8"))["sensitive_data_removed"])

    def test_builds_valid_approval_decision(self):
        request = build_approval_request(
            item="Apply Skill proposal",
            reason="Skill apply changes agent behavior.",
            recommendation="Approve only after safety scan passes.",
            risk="high",
            impact="One agent may receive a new workflow.",
            rollback="Remove the applied Skill and rerun OpenClaw skills check.",
            action_type="skill_apply",
            command=["openclaw", "skills", "workshop", "apply", "proposal-1"],
        )
        decision = build_approval_decision(request=request, decision="approve", decided_by="Leon", notes="Reviewed.")
        self.assertEqual(validate_approval_decision(decision, request=request), [])
        approved, reason = is_approved_decision_for_command(
            decision,
            action_type="skill_apply",
            command=["openclaw", "skills", "workshop", "apply", "proposal-1"],
        )
        self.assertTrue(approved, reason)

    def test_rejected_decision_does_not_approve_command(self):
        request = build_approval_request(
            item="Apply Skill proposal",
            reason="Needs approval.",
            recommendation="Review.",
            risk="high",
            impact="Agent behavior changes.",
            rollback="Remove Skill.",
            action_type="skill_apply",
            command=["openclaw", "skills", "workshop", "apply", "proposal-1"],
        )
        decision = build_approval_decision(request=request, decision="reject", decided_by="Leon")
        approved, reason = is_approved_decision_for_command(
            decision,
            action_type="skill_apply",
            command=["openclaw", "skills", "workshop", "apply", "proposal-1"],
        )
        self.assertFalse(approved)
        self.assertIn("reject", reason)

    def test_approval_decision_command_must_match(self):
        request = build_approval_request(
            item="Apply Skill proposal",
            reason="Needs approval.",
            recommendation="Review.",
            risk="high",
            impact="Agent behavior changes.",
            rollback="Remove Skill.",
            action_type="skill_apply",
            command=["openclaw", "skills", "workshop", "apply", "proposal-1"],
        )
        decision = build_approval_decision(request=request, decision="approve", decided_by="Leon")
        approved, reason = is_approved_decision_for_command(
            decision,
            action_type="skill_apply",
            command=["openclaw", "skills", "workshop", "apply", "proposal-2"],
        )
        self.assertFalse(approved)
        self.assertIn("does not match", reason)

    def test_approval_decision_command_can_match_after_redaction(self):
        sensitive_target = "ou_" + "d868b0926f8dec8e104d2bee7b0bd7d7"
        command = ["openclaw", "message", "send", "--target", sensitive_target]
        request = build_approval_request(
            item="Send Feishu message",
            reason="Needs approval.",
            recommendation="Review.",
            risk="high",
            impact="One Feishu message.",
            rollback="Return to dry-run.",
            action_type="other",
            command=command,
        )
        decision = build_approval_decision(request=request, decision="approve", decided_by="Leon")
        approved, reason = is_approved_decision_for_command(decision, action_type="other", command=command)
        self.assertTrue(approved, reason)

    def test_write_approval_decision(self):
        with tempfile.TemporaryDirectory() as tmp:
            request = build_approval_request(
                item="Apply Skill proposal",
                reason="Needs approval.",
                recommendation="Review.",
                risk="high",
                impact="Agent behavior changes.",
                rollback="Remove Skill.",
                action_type="skill_apply",
                command=["openclaw", "skills", "workshop", "apply", "proposal-1"],
            )
            decision = build_approval_decision(request=request, decision="approve", decided_by="Leon")
            path = write_approval_decision(Path(tmp), decision, request)
            self.assertTrue(path.exists())
            self.assertEqual(json.loads(path.read_text(encoding="utf-8"))["decision"], "approve")


if __name__ == "__main__":
    unittest.main()
