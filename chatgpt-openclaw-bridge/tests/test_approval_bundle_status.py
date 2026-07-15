import json
import tempfile
import unittest
from pathlib import Path

from src.approval_bundle_status import build_approval_bundle_status, write_approval_bundle_status
from src.approval_bundle_scope import build_scope_out_record, write_scope_out_record
from src.approval_request import build_approval_decision, build_approval_request, write_approval_decision, write_approval_request
from src.feishu_reporter import write_outbox_message
from src.risk_plan import build_approval_bundle_manifest, build_risk_plan, write_approval_bundle_manifest, write_risk_plan


class ApprovalBundleStatusTests(unittest.TestCase):
    def write_single_item_bundle(self, root: Path, requirement_id: str = "openai_real_call"):
        plan = build_risk_plan(requirement_id)
        plan_path = write_risk_plan(root / "audit", plan)
        request = build_approval_request(
            item=plan["item"],
            reason=plan["reason"],
            recommendation=plan["recommendation"],
            risk=plan["risk"],
            impact=plan["impact"],
            rollback=plan["rollback"],
            action_type=plan["action_type"],
            command=list(plan["planned_commands"]),
            evidence=list(plan["required_evidence"]),
        )
        request_path = write_approval_request(root / "approvals", request)
        outbox_path = write_outbox_message(root / "outbox", "approval", "approval message")
        manifest = build_approval_bundle_manifest(
            plans=[plan],
            risk_plan_files=[str(plan_path)],
            approval_requests=[{"approval_request": str(request_path), "feishu_outbox": str(outbox_path)}],
            task_id="V2-APPROVAL-BUNDLE",
            requested_by="codex",
        )
        manifest_path = write_approval_bundle_manifest(root / "audit", manifest)
        return manifest, manifest_path, request

    def test_waits_for_decision_when_no_decision_exists(self):
        with tempfile.TemporaryDirectory() as tmp:
            root = Path(tmp)
            manifest, _, _ = self.write_single_item_bundle(root)
            status = build_approval_bundle_status(manifest, decision_dir=root / "approvals")
            self.assertEqual(status["overall_status"], "PENDING_DECISION")
            self.assertEqual(status["items"][0]["status"], "WAITING_FOR_DECISION")

    def test_approved_placeholder_command_remains_blocked(self):
        with tempfile.TemporaryDirectory() as tmp:
            root = Path(tmp)
            manifest, _, request = self.write_single_item_bundle(root)
            decision = build_approval_decision(request=request, decision="approve", decided_by="Leon")
            write_approval_decision(root / "approvals", decision, request)
            status = build_approval_bundle_status(manifest, decision_dir=root / "approvals")
            self.assertEqual(status["overall_status"], "APPROVED_BUT_BLOCKED")
            self.assertEqual(status["items"][0]["status"], "APPROVED_BLOCKED_PLACEHOLDER")
            self.assertFalse(status["items"][0]["can_execute"])

    def test_dynamic_pass_marks_approved_placeholder_complete(self):
        with tempfile.TemporaryDirectory() as tmp:
            root = Path(tmp)
            manifest, _, request = self.write_single_item_bundle(root)
            decision = build_approval_decision(request=request, decision="approve", decided_by="Leon")
            write_approval_decision(root / "approvals", decision, request)
            status = build_approval_bundle_status(
                manifest,
                decision_dir=root / "approvals",
                dynamic_items={"openai_real_call": {"status": "PASS", "evidence": ["audit.json"]}},
            )
            self.assertEqual(status["overall_status"], "COMPLETE")
            self.assertEqual(status["items"][0]["status"], "COMPLETE")
            self.assertEqual(status["items"][0]["dynamic_evidence"], ["audit.json"])

    def test_dynamic_external_blocker_marks_bundle_blocked_external(self):
        with tempfile.TemporaryDirectory() as tmp:
            root = Path(tmp)
            manifest, _, request = self.write_single_item_bundle(root)
            decision = build_approval_decision(request=request, decision="approve", decided_by="Leon")
            write_approval_decision(root / "approvals", decision, request)
            status = build_approval_bundle_status(
                manifest,
                decision_dir=root / "approvals",
                dynamic_items={"openai_real_call": {"status": "EXTERNAL_BLOCKED", "next_action": "Resolve quota."}},
            )
            self.assertEqual(status["overall_status"], "BLOCKED_EXTERNAL")
            self.assertEqual(status["items"][0]["status"], "EXTERNAL_BLOCKED")

    def test_missing_files_mark_incomplete(self):
        with tempfile.TemporaryDirectory() as tmp:
            root = Path(tmp)
            manifest, _, _ = self.write_single_item_bundle(root)
            Path(manifest["items"][0]["risk_plan_file"]).unlink()
            status = build_approval_bundle_status(manifest, decision_dir=root / "approvals")
            self.assertEqual(status["overall_status"], "INCOMPLETE")
            self.assertEqual(status["items"][0]["status"], "INVALID_OR_MISSING_EVIDENCE")

    def test_scoped_out_item_marks_bundle_complete(self):
        with tempfile.TemporaryDirectory() as tmp:
            root = Path(tmp)
            manifest, _, _ = self.write_single_item_bundle(root)
            record = build_scope_out_record(
                manifest=manifest,
                requirement_id="openai_real_call",
                scoped_out_by="Leon",
                reason="Not part of this phase.",
            )
            write_scope_out_record(root / "approvals", record, manifest)
            status = build_approval_bundle_status(manifest, decision_dir=root / "approvals", scope_dir=root / "approvals")
            self.assertEqual(status["overall_status"], "COMPLETE")
            self.assertEqual(status["items"][0]["status"], "SCOPED_OUT")
            self.assertFalse(status["items"][0]["can_execute"])

    def test_write_approval_bundle_status(self):
        with tempfile.TemporaryDirectory() as tmp:
            root = Path(tmp)
            manifest, _, _ = self.write_single_item_bundle(root)
            status = build_approval_bundle_status(manifest, decision_dir=root / "approvals")
            path = write_approval_bundle_status(root / "audit", status)
            self.assertTrue(path.exists())
            payload = json.loads(path.read_text(encoding="utf-8"))
            self.assertEqual(payload["overall_status"], "PENDING_DECISION")


if __name__ == "__main__":
    unittest.main()
