import json
import unittest

from src.context_collector import validate_blocked_task_package
from src.task_intake import blocked_task_candidates, task_to_blocked_package


class TaskIntakeTests(unittest.TestCase):
    def test_failed_task_becomes_candidate(self):
        payload = {"tasks": [{"taskId": "T1", "status": "failed", "agentId": "xingyuan-qa"}]}
        candidates = blocked_task_candidates(payload)
        self.assertEqual(len(candidates), 1)

    def test_succeeded_task_is_not_candidate(self):
        payload = {"tasks": [{"taskId": "T1", "status": "succeeded", "agentId": "xingyuan-qa"}]}
        self.assertEqual(blocked_task_candidates(payload), [])

    def test_task_to_blocked_package_is_valid_and_redacted(self):
        fake_token = "abc12345" + "678901234567890"
        task = {
            "taskId": "T1",
            "status": "failed",
            "agentId": "xingyuan-qa",
            "error": f"token={fake_token}",
            "label": "Compile failure",
        }
        environment = {"os": "Windows", "unity_version": "6000.5.3f1", "dotnet_version": "missing", "openclaw_version": "2026.7.1"}
        package = task_to_blocked_package(task, environment)
        self.assertEqual(validate_blocked_task_package(package), [])
        self.assertNotIn(fake_token, json.dumps(package))


if __name__ == "__main__":
    unittest.main()
