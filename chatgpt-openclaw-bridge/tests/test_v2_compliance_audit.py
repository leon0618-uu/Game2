import tempfile
import unittest
from pathlib import Path

from src.readiness_audit import EXTERNAL_BLOCKED, MISSING, PASS, PENDING_APPROVAL
from src.v2_compliance_audit import build_v2_compliance_audit, write_v2_compliance_audit


class V2ComplianceAuditTests(unittest.TestCase):
    def test_current_project_reports_requirement_evidence_and_pending_items(self):
        bridge_root = Path(__file__).resolve().parents[1]
        repo_root = bridge_root.parent
        audit = build_v2_compliance_audit(repo_root, bridge_root)
        statuses = {item["requirement_id"]: item["status"] for item in audit["items"]}
        self.assertEqual(statuses["v2_document"], PASS)
        self.assertEqual(statuses["github_repository_link"], PASS)
        self.assertIn(statuses["pending_real_feishu_send"], {PENDING_APPROVAL, EXTERNAL_BLOCKED, PASS})
        self.assertIn(audit["overall_status"], {"CONDITIONAL_PASS", "NOT_READY", "BLOCKED_EXTERNAL", "PASS"})

    def test_missing_project_evidence_marks_not_ready(self):
        with tempfile.TemporaryDirectory() as tmp:
            root = Path(tmp)
            bridge_root = root / "bridge"
            bridge_root.mkdir()
            audit = build_v2_compliance_audit(root, bridge_root)
            statuses = {item["requirement_id"]: item["status"] for item in audit["items"]}
            self.assertEqual(statuses["v2_document"], MISSING)
            self.assertEqual(audit["overall_status"], "NOT_READY")

    def test_write_v2_compliance_audit(self):
        bridge_root = Path(__file__).resolve().parents[1]
        audit = build_v2_compliance_audit(bridge_root.parent, bridge_root)
        with tempfile.TemporaryDirectory() as tmp:
            path = write_v2_compliance_audit(Path(tmp), audit)
            self.assertTrue(path.exists())


if __name__ == "__main__":
    unittest.main()
