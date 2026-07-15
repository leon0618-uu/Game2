import tempfile
import unittest
from pathlib import Path

from src.approval_bundle_report import build_approval_bundle_report, write_approval_bundle_report


class ApprovalBundleReportTests(unittest.TestCase):
    def sample_status(self):
        return {
            "bundle_id": "APPROVALBUNDLE-test",
            "created_at": "20260714T000000Z",
            "overall_status": "PENDING_DECISION",
            "status_counts": {"WAITING_FOR_DECISION": 1},
            "sensitive_data_removed": True,
            "items": [
                {
                    "requirement_id": "openai_real_call",
                    "status": "WAITING_FOR_DECISION",
                    "risk": "high",
                    "action_type": "other",
                    "can_execute": False,
                    "approval_request": "approvals/request.json",
                    "feishu_outbox": "outbox/approval.json",
                    "risk_plan_file": "audit/plan.json",
                    "approval_decision": "",
                    "approval_reason": "approval decision not provided",
                    "placeholder_commands": ["python -m src.main openai-intervention --package <incident.json>"],
                    "missing_files": [],
                }
            ],
        }

    def test_report_contains_decision_commands_and_safety(self):
        report = build_approval_bundle_report(self.sample_status(), operator="Leon", manifest_path="audit/bundle.json")
        self.assertIn("# Approval Bundle Report: APPROVALBUNDLE-test", report)
        self.assertIn("approval-bundle-decision --manifest \"audit/bundle.json\" --requirement-id openai_real_call", report)
        self.assertIn("approval-bundle-scope-out --manifest \"audit/bundle.json\" --requirement-id openai_real_call", report)
        self.assertIn("--decision approve --decided-by \"Leon\"", report)
        self.assertIn("--decision reject --decided-by \"Leon\"", report)
        self.assertIn("--decision pause_and_inspect --decided-by \"Leon\"", report)
        self.assertIn("## Bulk Scope-Out", report)
        self.assertIn("approval-bundle-scope-out-all --manifest \"audit/bundle.json\" --scoped-out-by \"Leon\"", report)
        self.assertIn("--confirm-all", report)
        self.assertIn("This report is read-only.", report)
        self.assertIn("<incident.json>", report)

    def test_write_approval_bundle_report(self):
        report = build_approval_bundle_report(self.sample_status(), operator="Leon", manifest_path="audit/bundle.json")
        with tempfile.TemporaryDirectory() as tmp:
            path = write_approval_bundle_report(Path(tmp), report, "APPROVALBUNDLE-test")
            self.assertTrue(path.exists())
            self.assertEqual(path.suffix, ".md")


if __name__ == "__main__":
    unittest.main()
