import json
import tempfile
import unittest
from pathlib import Path

from src.final_task_result import build_final_task_result, validate_final_task_result, write_final_task_result


class FinalTaskResultTests(unittest.TestCase):
    def test_pass_requires_all_completion_evidence(self):
        result = build_final_task_result(
            task_id="TASK-1",
            status="PASS",
            summary="Task completed.",
            evidence=["compile: passed", "tests: passed"],
            qa_result="QA passed.",
            compile_passed=True,
            tests_passed=True,
            regression_passed=True,
            git_diff_reviewed=True,
            evidence_archived=True,
            skill_review_passed=True,
            feishu_summary_sent=True,
        )
        self.assertEqual(validate_final_task_result(result), [])

    def test_pass_rejects_missing_feishu_summary(self):
        result = build_final_task_result(
            task_id="TASK-1",
            status="PASS",
            summary="Task completed.",
            evidence=["compile: passed"],
            qa_result="QA passed.",
            compile_passed=True,
            tests_passed=True,
            regression_passed=True,
            git_diff_reviewed=True,
            evidence_archived=True,
            skill_review_passed=True,
            feishu_summary_sent=False,
        )
        self.assertIn("PASS requires feishu_summary_sent=true", validate_final_task_result(result))

    def test_conditional_pass_requires_caveat(self):
        result = build_final_task_result(
            task_id="TASK-1",
            status="CONDITIONAL_PASS",
            summary="Task mostly completed.",
            evidence=["manual check passed"],
            qa_result="QA accepted with caveat.",
            caveats=[],
        )
        self.assertIn("CONDITIONAL_PASS requires at least one caveat", validate_final_task_result(result))

    def test_blocked_requires_blocker_or_next_action(self):
        result = build_final_task_result(
            task_id="TASK-1",
            status="BLOCKED",
            summary="Task blocked.",
            qa_result="QA not run.",
        )
        self.assertIn("BLOCKED requires blockers or next_actions", validate_final_task_result(result))

    def test_write_final_task_result_redacts_secret_like_text(self):
        with tempfile.TemporaryDirectory() as tmp:
            result = build_final_task_result(
                task_id="TASK-1",
                status="REWORK",
                summary="Need rework.",
                evidence=["token=FAKE_TEST_TOKEN_FOR_REDACTION"],
                qa_result="QA failed.",
                blockers=["Compile failed."],
            )
            path = write_final_task_result(Path(tmp), result)
            encoded = path.read_text(encoding="utf-8")
            self.assertNotIn("FAKE_TEST_TOKEN_FOR_REDACTION", encoded)
            self.assertEqual(json.loads(encoded)["status"], "REWORK")


if __name__ == "__main__":
    unittest.main()
