import unittest

from src.context_collector import sanitize_blocked_task_package, validate_blocked_task_package


def valid_package():
    return {
        "task_id": "TASK-1",
        "project": "Xingyuan Covenant",
        "branch": "agent/test",
        "source_agent": "xingyuan-gameplay",
        "task_status": "failed",
        "goal": "Reproduce failure",
        "acceptance_criteria": ["test passes"],
        "current_problem": "Compile failed",
        "first_error_time": "2026-07-14T10:00:00Z",
        "last_progress_time": "2026-07-14T10:05:00Z",
        "attempt_count": 1,
        "attempted_solutions": ["rerun test"],
        "commands_executed": ["Unity.exe -batchmode"],
        "error_logs": ["error CS1002"],
        "stack_trace": [],
        "git_diff_summary": "1 file changed",
        "changed_files": ["Assets/Test.cs"],
        "environment": {
            "os": "Windows",
            "unity_version": "6000.5.3f1",
            "dotnet_version": "missing",
            "openclaw_version": "2026.7.1",
        },
        "agent_conclusion": "Need repair",
        "agent_confidence": 0.5,
        "requested_help": "Find root cause",
        "screenshots": [],
        "sensitive_data_removed": True,
    }


class ContextCollectorTests(unittest.TestCase):
    def test_valid_package_has_no_errors(self):
        self.assertEqual(validate_blocked_task_package(valid_package()), [])

    def test_missing_command_is_invalid(self):
        package = valid_package()
        package["commands_executed"] = []
        self.assertIn("commands_executed must include actual commands", validate_blocked_task_package(package))

    def test_sanitize_sets_sensitive_flag_and_redacts(self):
        package = valid_package()
        fake_token = "abc12345" + "678901234567890"
        package["error_logs"] = [f"token={fake_token}"]
        package["sensitive_data_removed"] = False
        sanitized = sanitize_blocked_task_package(package)
        self.assertTrue(sanitized["sensitive_data_removed"])
        self.assertNotIn(fake_token, sanitized["error_logs"][0])


if __name__ == "__main__":
    unittest.main()
