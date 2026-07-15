import tempfile
import unittest
from pathlib import Path

from src.approval_bundle_decision import build_bundle_approval_decision, write_bundle_approval_decision
from src.approval_request import build_approval_request, write_approval_request
from src.feishu_reporter import write_outbox_message
from src.risk_plan import build_approval_bundle_manifest, build_risk_plan, write_risk_plan


class ApprovalBundleDecisionTests(unittest.TestCase):
    def build_bundle(self, root: Path):
        plan = build_risk_plan("openai_real_call")
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
        outbox_path = write_outbox_message(root / "outbox", "approval", "approval")
        return build_approval_bundle_manifest(
            plans=[plan],
            risk_plan_files=[str(plan_path)],
            approval_requests=[{"approval_request": str(request_path), "feishu_outbox": str(outbox_path)}],
            task_id="V2-APPROVAL-BUNDLE",
            requested_by="codex",
        )

    def test_builds_decision_for_requirement_id(self):
        with tempfile.TemporaryDirectory() as tmp:
            manifest = self.build_bundle(Path(tmp))
            decision, _, item = build_bundle_approval_decision(
                manifest=manifest,
                requirement_id="openai_real_call",
                decision="approve",
                decided_by="Leon",
                notes="reviewed",
            )
            self.assertEqual(decision["decision"], "approve")
            self.assertEqual(decision["approval_bundle_id"], manifest["bundle_id"])
            self.assertEqual(decision["requirement_id"], "openai_real_call")
            self.assertEqual(item["requirement_id"], "openai_real_call")

    def test_unknown_requirement_is_rejected(self):
        with tempfile.TemporaryDirectory() as tmp:
            manifest = self.build_bundle(Path(tmp))
            with self.assertRaises(KeyError):
                build_bundle_approval_decision(
                    manifest=manifest,
                    requirement_id="missing",
                    decision="approve",
                    decided_by="Leon",
                )

    def test_write_bundle_approval_decision(self):
        with tempfile.TemporaryDirectory() as tmp:
            root = Path(tmp)
            manifest = self.build_bundle(root)
            path, decision, _ = write_bundle_approval_decision(
                output_dir=root / "approvals",
                manifest=manifest,
                requirement_id="openai_real_call",
                decision="pause_and_inspect",
                decided_by="Leon",
            )
            self.assertTrue(path.exists())
            self.assertEqual(decision["decision"], "pause_and_inspect")


if __name__ == "__main__":
    unittest.main()
