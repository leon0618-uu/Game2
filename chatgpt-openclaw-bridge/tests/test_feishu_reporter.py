import json
import tempfile
import unittest
from pathlib import Path

from src.feishu_reporter import approval_message, blocked_message, progress_message, skill_message, write_outbox_message


class FeishuReporterTests(unittest.TestCase):
    def test_templates_include_expected_headers(self):
        self.assertIn("【任务进度】", progress_message("T1", "running", "lead", "test", "ok"))
        self.assertIn("【任务阻塞】", blocked_message("T1", 15, "problem", "attempts"))
        self.assertIn("【能力增强】", skill_message("skill", "T1", "lead", "pass", "grey"))
        self.assertIn("【需要用户审批】", approval_message("item", "reason", "approve", "high", "impact", "rollback"))

    def test_outbox_write_redacts_message(self):
        fake_token = "abc12345" + "678901234567890"
        with tempfile.TemporaryDirectory() as tmp:
            path = write_outbox_message(Path(tmp), "blocked", f"token={fake_token}")
            data = json.loads(path.read_text(encoding="utf-8"))
            self.assertEqual(data["message_type"], "blocked")
            self.assertNotIn(fake_token, data["message"])

    def test_outbox_write_does_not_overwrite_same_second_messages(self):
        with tempfile.TemporaryDirectory() as tmp:
            root = Path(tmp)
            first = write_outbox_message(root, "approval", "first")
            second = write_outbox_message(root, "approval", "second")
            self.assertNotEqual(first, second)
            self.assertTrue(first.exists())
            self.assertTrue(second.exists())


if __name__ == "__main__":
    unittest.main()
