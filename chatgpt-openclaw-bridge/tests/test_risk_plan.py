import json
import tempfile
import unittest
from pathlib import Path

from src.risk_plan import (
    RISK_PLAN_TEMPLATES,
    build_all_risk_plans,
    build_approval_bundle_manifest,
    build_risk_plan,
    validate_approval_bundle_manifest,
    write_approval_bundle_manifest,
    write_risk_plan,
)
from src.readiness_audit import PENDING_HIGH_RISK_ITEMS


class RiskPlanTests(unittest.TestCase):
    def test_builds_plan_for_each_pending_requirement(self):
        plans = build_all_risk_plans()
        self.assertEqual({plan["requirement_id"] for plan in plans}, set(RISK_PLAN_TEMPLATES))
        self.assertTrue(all(plan["status"] == "PENDING_USER_APPROVAL" for plan in plans))

    def test_templates_match_readiness_pending_items(self):
        readiness_ids = {requirement_id for requirement_id, _, _ in PENDING_HIGH_RISK_ITEMS}
        self.assertEqual(set(RISK_PLAN_TEMPLATES), readiness_ids)

    def test_unknown_requirement_raises(self):
        with self.assertRaises(KeyError):
            build_risk_plan("unknown")

    def test_write_risk_plan(self):
        with tempfile.TemporaryDirectory() as tmp:
            plan = build_risk_plan("openai_real_call")
            path = write_risk_plan(Path(tmp), plan)
            self.assertTrue(path.exists())
            payload = json.loads(path.read_text(encoding="utf-8"))
            self.assertEqual(payload["requirement_id"], "openai_real_call")

    def test_persistent_service_plan_requires_approval_flag(self):
        plan = build_risk_plan("persistent_service")
        command = plan["planned_commands"][0]
        self.assertIn("-Execute", command)
        self.assertIn("-Approved", command)

    def test_builds_valid_approval_bundle_manifest(self):
        plans = build_all_risk_plans()
        risk_plan_files = [f"audit/{plan['requirement_id']}.json" for plan in plans]
        approvals = [
            {"approval_request": f"approvals/{plan['requirement_id']}.json", "feishu_outbox": f"outbox/{plan['requirement_id']}.json"}
            for plan in plans
        ]
        manifest = build_approval_bundle_manifest(
            plans=plans,
            risk_plan_files=risk_plan_files,
            approval_requests=approvals,
            task_id="V2-APPROVAL-BUNDLE",
            requested_by="codex",
        )
        self.assertFalse(validate_approval_bundle_manifest(manifest))
        self.assertEqual(manifest["item_count"], 7)
        self.assertTrue(manifest["execution_safety"]["local_files_only"])
        self.assertFalse(manifest["execution_safety"]["external_actions_executed"])

    def test_approval_bundle_manifest_rejects_duplicate_paths(self):
        plans = build_all_risk_plans()
        risk_plan_files = ["same.json" for _ in plans]
        approvals = [{"approval_request": f"approval-{index}.json", "feishu_outbox": f"outbox-{index}.json"} for index, _ in enumerate(plans)]
        manifest = build_approval_bundle_manifest(
            plans=plans,
            risk_plan_files=risk_plan_files,
            approval_requests=approvals,
            task_id="V2-APPROVAL-BUNDLE",
            requested_by="codex",
        )
        self.assertIn("path is duplicated", "\n".join(validate_approval_bundle_manifest(manifest)))

    def test_write_approval_bundle_manifest(self):
        plans = build_all_risk_plans()
        risk_plan_files = [f"audit/{plan['requirement_id']}.json" for plan in plans]
        approvals = [
            {"approval_request": f"approvals/{plan['requirement_id']}.json", "feishu_outbox": f"outbox/{plan['requirement_id']}.json"}
            for plan in plans
        ]
        manifest = build_approval_bundle_manifest(
            plans=plans,
            risk_plan_files=risk_plan_files,
            approval_requests=approvals,
            task_id="V2-APPROVAL-BUNDLE",
            requested_by="codex",
        )
        with tempfile.TemporaryDirectory() as tmp:
            path = write_approval_bundle_manifest(Path(tmp), manifest)
            self.assertTrue(path.exists())


if __name__ == "__main__":
    unittest.main()
