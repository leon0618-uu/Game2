import tempfile
import unittest
from pathlib import Path

from src.readiness_audit import EXTERNAL_BLOCKED, MISSING, PASS, PENDING_APPROVAL, build_high_risk_status_items, build_readiness_audit, write_readiness_audit


class ReadinessAuditTests(unittest.TestCase):
    def test_current_bridge_reports_pending_high_risk_items(self):
        bridge_root = Path(__file__).resolve().parents[1]
        audit = build_readiness_audit(bridge_root.parent, bridge_root)
        statuses = {item["requirement_id"]: item["status"] for item in audit["items"]}
        self.assertEqual(statuses["blocked_task_schema"], PASS)
        self.assertIn(statuses["real_feishu_send"], {PENDING_APPROVAL, EXTERNAL_BLOCKED, PASS})
        self.assertIn(statuses["lead_worktree"], {PASS, MISSING})
        self.assertIn(audit["overall_status"], {"CONDITIONAL_PASS", "NOT_READY", "BLOCKED_EXTERNAL", "PASS"})

    def test_missing_local_file_marks_not_ready(self):
        with tempfile.TemporaryDirectory() as tmp:
            bridge_root = Path(tmp)
            audit = build_readiness_audit(bridge_root.parent, bridge_root)
            statuses = {item["requirement_id"]: item["status"] for item in audit["items"]}
            self.assertEqual(statuses["blocked_task_schema"], MISSING)
            self.assertEqual(audit["overall_status"], "NOT_READY")

    def test_write_readiness_audit(self):
        bridge_root = Path(__file__).resolve().parents[1]
        audit = build_readiness_audit(bridge_root.parent, bridge_root)
        with tempfile.TemporaryDirectory() as tmp:
            path = write_readiness_audit(Path(tmp), audit)
            self.assertTrue(path.exists())

    def test_high_risk_items_detect_external_feishu_scope_blocker(self):
        with tempfile.TemporaryDirectory() as tmp:
            bridge_root = Path(tmp)
            audit_dir = bridge_root / "data" / "audit"
            audit_dir.mkdir(parents=True)
            (audit_dir / "20260714T000000Z-feishu-send-feishu-send.json").write_text(
                '{"payload":{"executed":true,"result":{"returncode":1,"stderr":"code 99991672 missing scope"}}}',
                encoding="utf-8",
            )
            items = build_high_risk_status_items(repo_root=bridge_root.parent, bridge_root=bridge_root)
            statuses = {item["requirement_id"]: item for item in items}
            self.assertEqual(statuses["real_feishu_send"]["status"], EXTERNAL_BLOCKED)

    def test_high_risk_items_detect_openai_quota_blocker(self):
        with tempfile.TemporaryDirectory() as tmp:
            bridge_root = Path(tmp)
            audit_dir = bridge_root / "data" / "audit"
            audit_dir.mkdir(parents=True)
            (audit_dir / "20260714T000000Z-openai-intervention-TASK-1.json").write_text(
                '{"payload":{"executed":true,"result":{"ok":false,"error":"OpenAI HTTP 429 insufficient_quota"}}}',
                encoding="utf-8",
            )
            items = build_high_risk_status_items(repo_root=bridge_root.parent, bridge_root=bridge_root)
            statuses = {item["requirement_id"]: item for item in items}
            self.assertEqual(statuses["openai_real_call"]["status"], EXTERNAL_BLOCKED)


if __name__ == "__main__":
    unittest.main()
