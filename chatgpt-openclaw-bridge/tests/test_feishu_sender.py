import unittest

from src.approval_request import build_approval_decision, build_approval_request
from src.feishu_sender import build_feishu_send_command, build_feishu_send_preview, validate_feishu_send_request


class FeishuSenderTests(unittest.TestCase):
    def test_build_command_uses_openclaw_feishu_dry_run_by_default(self):
        command = build_feishu_send_command(target="chat-id", message="hello")
        self.assertEqual(command[:5], ["openclaw", "message", "send", "--channel", "feishu"])
        self.assertIn("--dry-run", command)

    def test_preview_without_execute_does_not_require_approval(self):
        preview = build_feishu_send_preview(target="chat-id", message="hello")
        self.assertEqual(preview["mode"], "preview")
        self.assertFalse(preview["blocked"])
        self.assertFalse(preview["executed"])

    def test_execute_without_approval_is_blocked(self):
        preview = build_feishu_send_preview(target="chat-id", message="hello", execute=True)
        self.assertTrue(preview["blocked"])
        self.assertFalse(preview["can_execute"])

    def test_approval_must_match_exact_send_command(self):
        command = build_feishu_send_command(target="chat-id", message="hello", dry_run=False)
        request = build_approval_request(
            item="Send Feishu message",
            reason="Real send.",
            recommendation="Approve exact command.",
            risk="high",
            impact="Sends a message.",
            rollback="Send correction.",
            action_type="other",
            command=command,
        )
        decision = build_approval_decision(request=request, decision="approve", decided_by="Leon")
        preview = build_feishu_send_preview(target="chat-id", message="hello", execute=True, approval_decision=decision)
        self.assertTrue(preview["approved"])
        self.assertTrue(preview["can_execute"])

    def test_validation_requires_target_and_message(self):
        self.assertIn("target is required", validate_feishu_send_request("", "hello", "xingyuan"))
        self.assertIn("message is required", validate_feishu_send_request("chat-id", "", "xingyuan"))


if __name__ == "__main__":
    unittest.main()
