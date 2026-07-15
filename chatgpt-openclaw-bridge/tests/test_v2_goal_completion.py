import tempfile
import unittest
from pathlib import Path

from src.v2_goal_completion import build_v2_goal_completion_audit, write_v2_goal_completion_audit


class V2GoalCompletionTests(unittest.TestCase):
    def test_not_complete_when_audits_are_conditional(self):
        audit = build_v2_goal_completion_audit(
            readiness_audit={"overall_status": "CONDITIONAL_PASS", "status_counts": {"PENDING_APPROVAL": 7}},
            compliance_audit={"overall_status": "CONDITIONAL_PASS", "status_counts": {"PENDING_APPROVAL": 7}},
            approval_bundle_status={"overall_status": "PENDING_DECISION", "status_counts": {"WAITING_FOR_DECISION": 7}},
        )
        self.assertFalse(audit["complete"])
        self.assertEqual(audit["overall_status"], "NOT_COMPLETE")
        self.assertEqual({blocker["blocker_id"] for blocker in audit["blockers"]}, {"readiness_not_pass", "compliance_not_pass", "approval_bundle_not_complete"})

    def test_complete_only_when_all_sources_pass_and_bundle_complete(self):
        audit = build_v2_goal_completion_audit(
            readiness_audit={"overall_status": "PASS", "status_counts": {}},
            compliance_audit={"overall_status": "PASS", "status_counts": {}},
            approval_bundle_status={"overall_status": "COMPLETE", "status_counts": {}},
        )
        self.assertTrue(audit["complete"])
        self.assertEqual(audit["overall_status"], "COMPLETE")

    def test_scoped_out_bundle_resolves_conditional_pending_approval(self):
        audit = build_v2_goal_completion_audit(
            readiness_audit={"overall_status": "CONDITIONAL_PASS", "status_counts": {"PENDING_APPROVAL": 7}},
            compliance_audit={"overall_status": "CONDITIONAL_PASS", "status_counts": {"PENDING_APPROVAL": 7}},
            approval_bundle_status={"overall_status": "COMPLETE", "status_counts": {"SCOPED_OUT": 7}},
        )
        self.assertTrue(audit["complete"])
        self.assertEqual(audit["overall_status"], "COMPLETE")

    def test_missing_items_still_block_even_when_bundle_complete(self):
        audit = build_v2_goal_completion_audit(
            readiness_audit={"overall_status": "NOT_READY", "status_counts": {"MISSING": 1}},
            compliance_audit={"overall_status": "CONDITIONAL_PASS", "status_counts": {"PENDING_APPROVAL": 7}},
            approval_bundle_status={"overall_status": "COMPLETE", "status_counts": {"SCOPED_OUT": 7}},
        )
        self.assertFalse(audit["complete"])
        self.assertEqual(audit["blockers"][0]["blocker_id"], "readiness_not_pass")

    def test_missing_bundle_status_blocks_completion(self):
        audit = build_v2_goal_completion_audit(
            readiness_audit={"overall_status": "PASS", "status_counts": {}},
            compliance_audit={"overall_status": "PASS", "status_counts": {}},
            approval_bundle_status=None,
        )
        self.assertFalse(audit["complete"])
        self.assertEqual(audit["blockers"][0]["blocker_id"], "approval_bundle_status_missing")

    def test_write_v2_goal_completion_audit(self):
        audit = build_v2_goal_completion_audit(
            readiness_audit={"overall_status": "PASS", "status_counts": {}},
            compliance_audit={"overall_status": "PASS", "status_counts": {}},
            approval_bundle_status={"overall_status": "COMPLETE", "status_counts": {}},
        )
        with tempfile.TemporaryDirectory() as tmp:
            path = write_v2_goal_completion_audit(Path(tmp), audit)
            self.assertTrue(path.exists())


if __name__ == "__main__":
    unittest.main()
