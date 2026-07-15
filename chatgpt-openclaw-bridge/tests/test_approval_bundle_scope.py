import tempfile
import unittest
from pathlib import Path

from src.approval_bundle_scope import build_scope_out_record, find_scope_out_record, validate_scope_out_record, write_all_scope_out_records, write_scope_out_record
from src.approval_request import build_approval_request, write_approval_request
from src.feishu_reporter import write_outbox_message
from src.risk_plan import build_approval_bundle_manifest, build_risk_plan, write_risk_plan


class ApprovalBundleScopeTests(unittest.TestCase):
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

    def test_builds_valid_scope_out_record(self):
        with tempfile.TemporaryDirectory() as tmp:
            manifest = self.build_bundle(Path(tmp))
            record = build_scope_out_record(
                manifest=manifest,
                requirement_id="openai_real_call",
                scoped_out_by="Leon",
                reason="Not part of this phase.",
            )
            self.assertFalse(validate_scope_out_record(record, manifest))
            self.assertEqual(record["external_actions_executed"], False)

    def test_unknown_requirement_is_rejected(self):
        with tempfile.TemporaryDirectory() as tmp:
            manifest = self.build_bundle(Path(tmp))
            with self.assertRaises(KeyError):
                build_scope_out_record(manifest=manifest, requirement_id="missing", scoped_out_by="Leon", reason="No.")

    def test_write_and_find_scope_out_record(self):
        with tempfile.TemporaryDirectory() as tmp:
            root = Path(tmp)
            manifest = self.build_bundle(root)
            record = build_scope_out_record(
                manifest=manifest,
                requirement_id="openai_real_call",
                scoped_out_by="Leon",
                reason="Not part of this phase.",
            )
            path = write_scope_out_record(root / "approvals", record, manifest)
            self.assertTrue(path.exists())
            found, found_path = find_scope_out_record(root / "approvals", manifest["bundle_id"], "openai_real_call")
            self.assertEqual(found["scope_out_id"], record["scope_out_id"])
            self.assertEqual(found_path, str(path))

    def test_write_all_scope_out_records(self):
        with tempfile.TemporaryDirectory() as tmp:
            root = Path(tmp)
            first = self.build_bundle(root / "one")
            second = self.build_bundle(root / "two")
            manifest = {
                **first,
                "item_count": 2,
                "items": [first["items"][0], {**second["items"][0], "requirement_id": "real_feishu_send"}],
            }
            written = write_all_scope_out_records(
                output_dir=root / "approvals",
                manifest=manifest,
                scoped_out_by="Leon",
                reason="Not part of this phase.",
            )
            self.assertEqual(len(written), 2)
            self.assertTrue(all(path.exists() for path, _ in written))


if __name__ == "__main__":
    unittest.main()
