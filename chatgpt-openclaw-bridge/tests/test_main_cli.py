import json
import subprocess
import sys
import unittest
from pathlib import Path

from src.main import compact_text, summarize_tasks


ROOT = Path(__file__).resolve().parents[1]


class MainCliTests(unittest.TestCase):
    def run_cli(self, *args):
        return subprocess.run(
            [sys.executable, "-m", "src.main", *args],
            cwd=ROOT,
            text=True,
            encoding="utf-8",
            capture_output=True,
            check=False,
        )

    def test_evaluate_failed_outputs_escalation_json(self):
        result = self.run_cli("evaluate", "--task-id", "TASK-1", "--state", "failed", "--source-agent", "xingyuan-qa")
        self.assertEqual(result.returncode, 0, result.stderr)
        payload = json.loads(result.stdout)
        self.assertTrue(payload["escalate"])
        self.assertEqual(payload["reason"], "FAILED")

    def test_summarize_tasks_redacts_private_session_keys(self):
        payload = {
            "tasks": [
                {
                    "taskId": "T1",
                    "runtime": "cron",
                    "agentId": "xingyuan-lead",
                    "status": "failed",
                    "childSessionKey": "agent:xingyuan-lead:feishu:direct:ou_FAKEPRIVATEID123456",
                }
            ]
        }
        summary = summarize_tasks(payload)
        encoded = json.dumps(summary)
        self.assertNotIn("ou_FAKEPRIVATEID123456", encoded)
        self.assertEqual(summary["status_counts"]["failed"], 1)

    def test_summarize_tasks_compacts_long_labels(self):
        summary = summarize_tasks(
            {"tasks": [{"taskId": "T1", "runtime": "cli", "agentId": "xingyuan-gameplay", "status": "failed", "task": "x" * 200}]}
        )
        self.assertLessEqual(len(summary["recent"][0]["label"]), 120)

    def test_compact_text_normalizes_whitespace(self):
        self.assertEqual(compact_text("a\n\nb\tc"), "a b c")

    def test_propose_skill_cli_writes_proposal(self):
        import tempfile

        with tempfile.TemporaryDirectory() as tmp:
            result = self.run_cli(
                "propose-skill",
                "--name",
                "agent-handoff",
                "--description",
                "Standardize agent handoff.",
                "--install-agent",
                "xingyuan-lead",
                "--successful-workflow-count",
                "2",
                "--validated",
                "--output-dir",
                tmp,
            )
            self.assertEqual(result.returncode, 0, result.stderr)
            self.assertTrue((Path(tmp) / "agent-handoff" / "PROPOSAL.md").exists())

    def test_intervention_cli_writes_fallback_directive(self):
        import json
        import tempfile

        with tempfile.TemporaryDirectory() as tmp:
            package = Path(tmp) / "package.json"
            output_dir = Path(tmp) / "interventions"
            package.write_text(
                json.dumps(
                    {
                        "payload": {
                            "task_id": "TASK-1",
                            "source_agent": "xingyuan-qa",
                            "current_problem": "Compile failed",
                            "changed_files": [],
                            "commands_executed": ["test"],
                        }
                    }
                ),
                encoding="utf-8",
            )
            result = self.run_cli("intervention", "--package", str(package), "--output-dir", str(output_dir))
            self.assertEqual(result.returncode, 0, result.stderr)
            self.assertTrue(list(output_dir.glob("*.json")))

    def test_approval_request_cli_writes_request_and_outbox(self):
        import tempfile

        with tempfile.TemporaryDirectory() as tmp:
            root = Path(tmp)
            result = self.run_cli(
                "approval-request",
                "--item",
                "Apply Skill proposal",
                "--reason",
                "OpenClaw Skill apply changes agent behavior.",
                "--recommendation",
                "Approve after safety scan.",
                "--risk",
                "high",
                "--impact",
                "One agent receives the Skill.",
                "--rollback",
                "Remove the Skill and rerun checks.",
                "--action-type",
                "skill_apply",
                "--output-dir",
                str(root / "approvals"),
                "--outbox-dir",
                str(root / "outbox"),
            )
            self.assertEqual(result.returncode, 0, result.stderr)
            payload = json.loads(result.stdout)
            self.assertTrue(Path(payload["approval_request"]).exists())
            self.assertTrue(Path(payload["feishu_outbox"]).exists())

    def test_workshop_apply_without_approval_can_write_approval_request(self):
        import tempfile

        with tempfile.TemporaryDirectory() as tmp:
            root = Path(tmp)
            result = self.run_cli(
                "workshop-plan",
                "--action",
                "apply",
                "--proposal-id",
                "proposal-123",
                "--execute",
                "--write-approval-request",
                "--approval-dir",
                str(root / "approvals"),
                "--outbox-dir",
                str(root / "outbox"),
            )
            self.assertEqual(result.returncode, 2)
            payload = json.loads(result.stdout)
            self.assertTrue(payload["blocked"])
            self.assertTrue(Path(payload["approval_request"]).exists())
            self.assertTrue(Path(payload["feishu_outbox"]).exists())

    def test_approval_decision_cli_writes_decision(self):
        import tempfile

        with tempfile.TemporaryDirectory() as tmp:
            root = Path(tmp)
            request_result = self.run_cli(
                "approval-request",
                "--item",
                "Apply OpenClaw Skill proposal proposal-123",
                "--reason",
                "OpenClaw Skill apply changes agent behavior.",
                "--recommendation",
                "Approve after safety scan.",
                "--risk",
                "high",
                "--impact",
                "One agent receives the Skill.",
                "--rollback",
                "Remove the Skill and rerun checks.",
                "--action-type",
                "skill_apply",
                "--requested-command",
                "openclaw",
                "--requested-command",
                "skills",
                "--requested-command",
                "workshop",
                "--requested-command",
                "apply",
                "--requested-command",
                "proposal-123",
                "--output-dir",
                str(root / "approvals"),
                "--no-feishu-outbox",
            )
            request_path = json.loads(request_result.stdout)["approval_request"]
            decision_result = self.run_cli(
                "approval-decision",
                "--request",
                request_path,
                "--decision",
                "approve",
                "--decided-by",
                "Leon",
                "--output-dir",
                str(root / "approvals"),
            )
            self.assertEqual(decision_result.returncode, 0, decision_result.stderr)
            payload = json.loads(decision_result.stdout)
            self.assertTrue(Path(payload["approval_decision"]).exists())

    def test_feishu_decision_ingest_cli_writes_decision(self):
        import tempfile

        with tempfile.TemporaryDirectory() as tmp:
            root = Path(tmp)
            request_result = self.run_cli(
                "approval-request",
                "--item",
                "Send Feishu summary",
                "--reason",
                "External channel send requires approval.",
                "--recommendation",
                "Approve after preview.",
                "--risk",
                "high",
                "--impact",
                "Message reaches the project channel.",
                "--rollback",
                "Post correction and disable sender.",
                "--output-dir",
                str(root / "approvals"),
                "--no-feishu-outbox",
            )
            request_path = Path(json.loads(request_result.stdout)["approval_request"])
            request = json.loads(request_path.read_text(encoding="utf-8"))
            payload_path = root / "feishu-decision.json"
            payload_path.write_text(
                json.dumps(
                    {
                        "source": "feishu",
                        "request_id": request["request_id"],
                        "decision": "approve",
                        "operator": "Leon",
                        "notes": "preview checked",
                    }
                ),
                encoding="utf-8",
            )
            decision_result = self.run_cli(
                "feishu-decision-ingest",
                "--request",
                str(request_path),
                "--payload",
                str(payload_path),
                "--output-dir",
                str(root / "approvals"),
            )
            self.assertEqual(decision_result.returncode, 0, decision_result.stderr)
            payload = json.loads(decision_result.stdout)
            self.assertEqual(payload["decision_source"], "feishu")
            self.assertTrue(Path(payload["approval_decision"]).exists())

    def test_feishu_decision_ingest_cli_accepts_signed_callback_payload(self):
        import hashlib
        import tempfile

        with tempfile.TemporaryDirectory() as tmp:
            root = Path(tmp)
            request_result = self.run_cli(
                "approval-request",
                "--item",
                "Send Feishu summary",
                "--reason",
                "External channel send requires approval.",
                "--recommendation",
                "Approve after preview.",
                "--risk",
                "high",
                "--impact",
                "Message reaches the project channel.",
                "--rollback",
                "Post correction and disable sender.",
                "--output-dir",
                str(root / "approvals"),
                "--no-feishu-outbox",
            )
            request_path = Path(json.loads(request_result.stdout)["approval_request"])
            request = json.loads(request_path.read_text(encoding="utf-8"))
            payload_path = root / "feishu-callback.json"
            payload_path.write_text(
                json.dumps(
                    {
                        "schema": "2.0",
                        "header": {"event_type": "card.action.trigger"},
                        "event": {
                            "operator": {"operator_id": {"open_id": "ou_operator"}},
                            "action": {"value": {"request_id": request["request_id"], "decision": "approve", "notes": "preview checked"}},
                            "context": {"open_message_id": "om_card"},
                        },
                    },
                    separators=(",", ":"),
                ),
                encoding="utf-8",
            )
            body = payload_path.read_bytes()
            timestamp = "1700000000"
            nonce = "nonce-1"
            key = "unit-test-key"
            signature = hashlib.sha256(f"{timestamp}{nonce}{key}".encode("utf-8") + body).hexdigest()
            headers_path = root / "headers.json"
            headers_path.write_text(
                json.dumps(
                    {
                        "X-Lark-Request-Timestamp": timestamp,
                        "X-Lark-Request-Nonce": nonce,
                        "X-Lark-Signature": signature,
                    }
                ),
                encoding="utf-8",
            )
            decision_result = self.run_cli(
                "feishu-decision-ingest",
                "--request",
                str(request_path),
                "--payload",
                str(payload_path),
                "--headers-json",
                str(headers_path),
                "--require-signature",
                "--encrypt-key",
                key,
                "--output-dir",
                str(root / "approvals"),
                "--write",
                "--audit-dir",
                str(root / "audit"),
            )
            self.assertEqual(decision_result.returncode, 0, decision_result.stderr)
            payload = json.loads(decision_result.stdout)
            self.assertTrue(payload["signature_verified"])
            self.assertTrue(Path(payload["approval_decision"]).exists())
            self.assertTrue(Path(payload["audit_path"]).exists())

    def test_final_result_cli_rejects_unproven_pass(self):
        result = self.run_cli(
            "final-result",
            "--task-id",
            "TASK-1",
            "--status",
            "PASS",
            "--summary",
            "Task done.",
            "--qa-result",
            "QA says done.",
            "--evidence",
            "manual note only",
        )
        self.assertEqual(result.returncode, 2)
        payload = json.loads(result.stdout)
        self.assertFalse(payload["valid"])
        self.assertIn("PASS requires compile_passed=true", "\n".join(payload["errors"]))

    def test_final_result_cli_writes_rework_result(self):
        import tempfile

        with tempfile.TemporaryDirectory() as tmp:
            result = self.run_cli(
                "final-result",
                "--task-id",
                "TASK-1",
                "--status",
                "REWORK",
                "--summary",
                "Task needs another repair pass.",
                "--qa-result",
                "QA failed.",
                "--blocker",
                "Compile failed.",
                "--output-dir",
                tmp,
            )
            self.assertEqual(result.returncode, 0, result.stderr)
            payload = json.loads(result.stdout)
            self.assertTrue(Path(payload["final_task_result"]).exists())

    def test_readiness_audit_cli_outputs_status(self):
        result = self.run_cli("readiness-audit")
        self.assertIn(result.returncode, {0, 2})
        payload = json.loads(result.stdout)
        self.assertIn("overall_status", payload)
        self.assertIn("items", payload)

    def test_compliance_audit_cli_outputs_requirement_status(self):
        result = self.run_cli("compliance-audit")
        self.assertIn(result.returncode, {0, 2})
        payload = json.loads(result.stdout)
        self.assertIn("overall_status", payload)
        self.assertIn("items", payload)
        self.assertIn("github_repository_link", {item["requirement_id"] for item in payload["items"]})

    def test_goal_completion_audit_cli_reports_not_complete(self):
        import tempfile

        with tempfile.TemporaryDirectory() as tmp:
            root = Path(tmp)
            bundle_result = self.run_cli(
                "risk-plan",
                "--requirement-id",
                "all",
                "--write",
                "--write-approval-request",
                "--write-bundle-manifest",
                "--output-dir",
                str(root / "audit"),
                "--approval-dir",
                str(root / "approvals"),
                "--outbox-dir",
                str(root / "outbox"),
            )
            self.assertEqual(bundle_result.returncode, 0, bundle_result.stderr)
            manifest_path = json.loads(bundle_result.stdout)["approval_bundle_manifest"]
            result = self.run_cli(
                "goal-completion-audit",
                "--manifest",
                manifest_path,
                "--decision-dir",
                str(root / "approvals"),
                "--write",
                "--output-dir",
                str(root / "audit"),
            )
            self.assertEqual(result.returncode, 2)
            payload = json.loads(result.stdout)
            audit = payload["goal_completion_audit"]
            self.assertFalse(audit["complete"])
            self.assertEqual(audit["overall_status"], "NOT_COMPLETE")
            self.assertTrue(Path(payload["goal_completion_audit_file"]).exists())

    def test_v2_unblock_check_cli_writes_summary(self):
        import tempfile

        with tempfile.TemporaryDirectory() as tmp:
            root = Path(tmp)
            result = self.run_cli(
                "v2-unblock-check",
                "--bridge-root",
                str(root / "bridge"),
                "--write",
                "--write-report",
                "--output-dir",
                str(root / "audit"),
            )
            self.assertEqual(result.returncode, 2)
            payload = json.loads(result.stdout)
            self.assertIn("external_blocker_count", payload)
            self.assertIn("readiness", payload)
            self.assertIn("remediation", payload)
            self.assertIn("OPENCLAW_EXTERNAL_BLOCKER_RUNBOOK.md", payload["remediation"]["runbook"])
            self.assertIn("retry-external-blockers.ps1", payload["remediation"]["approved_retry_command"])
            self.assertTrue(Path(payload["audit_path"]).exists())
            self.assertTrue(Path(payload["report_path"]).exists())

    def test_risk_plan_cli_outputs_plan(self):
        result = self.run_cli("risk-plan", "--requirement-id", "openai_real_call")
        self.assertEqual(result.returncode, 0, result.stderr)
        payload = json.loads(result.stdout)
        self.assertEqual(payload["plans"][0]["requirement_id"], "openai_real_call")

    def test_risk_plan_cli_can_write_approval_request(self):
        import tempfile

        with tempfile.TemporaryDirectory() as tmp:
            root = Path(tmp)
            result = self.run_cli(
                "risk-plan",
                "--requirement-id",
                "openai_real_call",
                "--write-approval-request",
                "--approval-dir",
                str(root / "approvals"),
                "--outbox-dir",
                str(root / "outbox"),
            )
            self.assertEqual(result.returncode, 0, result.stderr)
            payload = json.loads(result.stdout)
            self.assertTrue(Path(payload["approval_requests"][0]["approval_request"]).exists())
            self.assertTrue(Path(payload["approval_requests"][0]["feishu_outbox"]).exists())

    def test_risk_plan_all_writes_unique_approval_outbox_files(self):
        import tempfile

        with tempfile.TemporaryDirectory() as tmp:
            root = Path(tmp)
            result = self.run_cli(
                "risk-plan",
                "--requirement-id",
                "all",
                "--write-approval-request",
                "--approval-dir",
                str(root / "approvals"),
                "--outbox-dir",
                str(root / "outbox"),
            )
            self.assertEqual(result.returncode, 0, result.stderr)
            payload = json.loads(result.stdout)
            outbox_paths = [item["feishu_outbox"] for item in payload["approval_requests"]]
            self.assertEqual(len(outbox_paths), 7)
            self.assertEqual(len(set(outbox_paths)), 7)
            self.assertTrue(all(Path(path).exists() for path in outbox_paths))

    def test_risk_plan_all_can_write_bundle_manifest(self):
        import tempfile

        with tempfile.TemporaryDirectory() as tmp:
            root = Path(tmp)
            result = self.run_cli(
                "risk-plan",
                "--requirement-id",
                "all",
                "--write",
                "--write-approval-request",
                "--write-bundle-manifest",
                "--task-id",
                "V2-APPROVAL-BUNDLE",
                "--output-dir",
                str(root / "audit"),
                "--approval-dir",
                str(root / "approvals"),
                "--outbox-dir",
                str(root / "outbox"),
            )
            self.assertEqual(result.returncode, 0, result.stderr)
            payload = json.loads(result.stdout)
            manifest_path = Path(payload["approval_bundle_manifest"])
            self.assertTrue(manifest_path.exists())
            manifest = json.loads(manifest_path.read_text(encoding="utf-8"))
            self.assertEqual(manifest["item_count"], 7)
            self.assertTrue(manifest["execution_safety"]["local_files_only"])
            self.assertFalse(manifest["execution_safety"]["external_actions_executed"])

    def test_risk_plan_bundle_manifest_requires_files(self):
        result = self.run_cli("risk-plan", "--requirement-id", "all", "--write-bundle-manifest")
        self.assertEqual(result.returncode, 2)
        payload = json.loads(result.stdout)
        self.assertFalse(payload["valid"])
        self.assertIn("--write-bundle-manifest requires --write and --write-approval-request", payload["errors"])

    def test_approval_bundle_status_cli_reports_pending_decision(self):
        import tempfile

        with tempfile.TemporaryDirectory() as tmp:
            root = Path(tmp)
            bundle_result = self.run_cli(
                "risk-plan",
                "--requirement-id",
                "all",
                "--write",
                "--write-approval-request",
                "--write-bundle-manifest",
                "--output-dir",
                str(root / "audit"),
                "--approval-dir",
                str(root / "approvals"),
                "--outbox-dir",
                str(root / "outbox"),
            )
            self.assertEqual(bundle_result.returncode, 0, bundle_result.stderr)
            manifest_path = json.loads(bundle_result.stdout)["approval_bundle_manifest"]
            status_result = self.run_cli(
                "approval-bundle-status",
                "--manifest",
                manifest_path,
                "--decision-dir",
                str(root / "approvals"),
                "--write",
                "--write-report",
                "--operator",
                "Leon",
                "--output-dir",
                str(root / "audit"),
            )
            self.assertEqual(status_result.returncode, 0, status_result.stderr)
            payload = json.loads(status_result.stdout)
            status = payload["approval_bundle_status"]
            self.assertEqual(status["overall_status"], "PENDING_DECISION")
            self.assertEqual(status["status_counts"]["WAITING_FOR_DECISION"], 7)
            self.assertTrue(Path(payload["approval_bundle_status_file"]).exists())
            report_path = Path(payload["approval_bundle_report"])
            self.assertTrue(report_path.exists())
            report_text = report_path.read_text(encoding="utf-8")
            self.assertIn("approval-bundle-decision", report_text)
            self.assertIn("--decision approve --decided-by \"Leon\"", report_text)

    def test_approval_bundle_decision_cli_records_decision_by_requirement_id(self):
        import tempfile

        with tempfile.TemporaryDirectory() as tmp:
            root = Path(tmp)
            bundle_result = self.run_cli(
                "risk-plan",
                "--requirement-id",
                "all",
                "--write",
                "--write-approval-request",
                "--write-bundle-manifest",
                "--output-dir",
                str(root / "audit"),
                "--approval-dir",
                str(root / "approvals"),
                "--outbox-dir",
                str(root / "outbox"),
            )
            self.assertEqual(bundle_result.returncode, 0, bundle_result.stderr)
            manifest_path = json.loads(bundle_result.stdout)["approval_bundle_manifest"]
            decision_result = self.run_cli(
                "approval-bundle-decision",
                "--manifest",
                manifest_path,
                "--requirement-id",
                "openai_real_call",
                "--decision",
                "approve",
                "--decided-by",
                "Leon",
                "--output-dir",
                str(root / "approvals"),
            )
            self.assertEqual(decision_result.returncode, 0, decision_result.stderr)
            decision_payload = json.loads(decision_result.stdout)
            self.assertTrue(Path(decision_payload["approval_decision"]).exists())
            self.assertFalse(decision_payload["executed"])

            status_result = self.run_cli(
                "approval-bundle-status",
                "--manifest",
                manifest_path,
                "--decision-dir",
                str(root / "approvals"),
            )
            self.assertEqual(status_result.returncode, 0, status_result.stderr)
            status = json.loads(status_result.stdout)["approval_bundle_status"]
            item_statuses = {item["requirement_id"]: item["status"] for item in status["items"]}
            self.assertEqual(item_statuses["openai_real_call"], "APPROVED_BLOCKED_PLACEHOLDER")

    def test_approval_bundle_scope_out_cli_records_scope_out(self):
        import tempfile

        with tempfile.TemporaryDirectory() as tmp:
            root = Path(tmp)
            bundle_result = self.run_cli(
                "risk-plan",
                "--requirement-id",
                "all",
                "--write",
                "--write-approval-request",
                "--write-bundle-manifest",
                "--output-dir",
                str(root / "audit"),
                "--approval-dir",
                str(root / "approvals"),
                "--outbox-dir",
                str(root / "outbox"),
            )
            self.assertEqual(bundle_result.returncode, 0, bundle_result.stderr)
            manifest_path = json.loads(bundle_result.stdout)["approval_bundle_manifest"]
            scope_result = self.run_cli(
                "approval-bundle-scope-out",
                "--manifest",
                manifest_path,
                "--requirement-id",
                "openai_real_call",
                "--scoped-out-by",
                "Leon",
                "--reason",
                "Not part of this phase.",
                "--output-dir",
                str(root / "approvals"),
            )
            self.assertEqual(scope_result.returncode, 0, scope_result.stderr)
            scope_payload = json.loads(scope_result.stdout)
            self.assertTrue(Path(scope_payload["scope_out"]).exists())
            self.assertFalse(scope_payload["external_actions_executed"])

            status_result = self.run_cli(
                "approval-bundle-status",
                "--manifest",
                manifest_path,
                "--decision-dir",
                str(root / "approvals"),
                "--scope-dir",
                str(root / "approvals"),
            )
            self.assertEqual(status_result.returncode, 0, status_result.stderr)
            status = json.loads(status_result.stdout)["approval_bundle_status"]
            item_statuses = {item["requirement_id"]: item["status"] for item in status["items"]}
            self.assertEqual(item_statuses["openai_real_call"], "SCOPED_OUT")

    def test_approval_bundle_scope_out_all_requires_confirmation(self):
        result = self.run_cli(
            "approval-bundle-scope-out-all",
            "--manifest",
            "missing.json",
            "--scoped-out-by",
            "Leon",
            "--reason",
            "Not part of this phase.",
        )
        self.assertEqual(result.returncode, 2)
        payload = json.loads(result.stdout)
        self.assertIn("--confirm-all is required", payload["errors"][0])

    def test_approval_bundle_scope_out_all_can_complete_temporary_bundle(self):
        import tempfile

        with tempfile.TemporaryDirectory() as tmp:
            root = Path(tmp)
            bundle_result = self.run_cli(
                "risk-plan",
                "--requirement-id",
                "all",
                "--write",
                "--write-approval-request",
                "--write-bundle-manifest",
                "--output-dir",
                str(root / "audit"),
                "--approval-dir",
                str(root / "approvals"),
                "--outbox-dir",
                str(root / "outbox"),
            )
            self.assertEqual(bundle_result.returncode, 0, bundle_result.stderr)
            manifest_path = json.loads(bundle_result.stdout)["approval_bundle_manifest"]
            scope_result = self.run_cli(
                "approval-bundle-scope-out-all",
                "--manifest",
                manifest_path,
                "--scoped-out-by",
                "Leon",
                "--reason",
                "Not part of this phase.",
                "--confirm-all",
                "--output-dir",
                str(root / "approvals"),
            )
            self.assertEqual(scope_result.returncode, 0, scope_result.stderr)
            scope_payload = json.loads(scope_result.stdout)
            self.assertEqual(scope_payload["scope_out_count"], 7)
            self.assertFalse(scope_payload["external_actions_executed"])

            status_result = self.run_cli(
                "approval-bundle-status",
                "--manifest",
                manifest_path,
                "--decision-dir",
                str(root / "approvals"),
                "--scope-dir",
                str(root / "approvals"),
            )
            self.assertEqual(status_result.returncode, 0, status_result.stderr)
            status = json.loads(status_result.stdout)["approval_bundle_status"]
            self.assertEqual(status["overall_status"], "COMPLETE")
            self.assertEqual(status["status_counts"]["SCOPED_OUT"], 7)

    def test_risk_execute_cli_previews_plan(self):
        import tempfile

        with tempfile.TemporaryDirectory() as tmp:
            root = Path(tmp)
            plan_result = self.run_cli(
                "risk-plan",
                "--requirement-id",
                "openai_real_call",
                "--write",
                "--output-dir",
                str(root),
            )
            plan_path = json.loads(plan_result.stdout)["risk_plan_files"][0]
            execute_result = self.run_cli("risk-execute", "--plan", plan_path)
            self.assertEqual(execute_result.returncode, 0, execute_result.stderr)
            payload = json.loads(execute_result.stdout)
            self.assertEqual(payload["execution"]["mode"], "preview")
            self.assertFalse(payload["execution"]["can_execute"])

    def test_feishu_send_cli_previews_without_sending(self):
        result = self.run_cli("feishu-send", "--target", "chat-id", "--message", "hello")
        self.assertEqual(result.returncode, 0, result.stderr)
        payload = json.loads(result.stdout)
        self.assertEqual(payload["mode"], "preview")
        self.assertFalse(payload["executed"])
        self.assertIn("--dry-run", payload["command"])

    def test_feishu_send_cli_can_write_audit_preview(self):
        import tempfile

        with tempfile.TemporaryDirectory() as tmp:
            result = self.run_cli("feishu-send", "--target", "chat-id", "--message", "hello", "--write", "--output-dir", tmp)
            self.assertEqual(result.returncode, 0, result.stderr)
            payload = json.loads(result.stdout)
            self.assertTrue(Path(payload["audit_path"]).exists())

    def test_openai_intervention_cli_previews_without_calling(self):
        import tempfile

        with tempfile.TemporaryDirectory() as tmp:
            package = Path(tmp) / "incident.json"
            package.write_text(json.dumps({"task_id": "TASK-1", "error": "compile failed"}), encoding="utf-8")
            result = self.run_cli("openai-intervention", "--package", str(package), "--model", "test-model")
            self.assertEqual(result.returncode, 0, result.stderr)
            payload = json.loads(result.stdout)
            self.assertEqual(payload["mode"], "preview")
            self.assertFalse(payload["executed"])
            self.assertIn("request_preview", payload)

    def test_openai_intervention_cli_can_write_audit_preview(self):
        import tempfile

        with tempfile.TemporaryDirectory() as tmp:
            package = Path(tmp) / "incident.json"
            out = Path(tmp) / "audit"
            package.write_text(json.dumps({"task_id": "TASK-1", "error": "compile failed"}), encoding="utf-8")
            result = self.run_cli("openai-intervention", "--package", str(package), "--model", "test-model", "--write", "--output-dir", str(out))
            self.assertEqual(result.returncode, 0, result.stderr)
            payload = json.loads(result.stdout)
            self.assertTrue(Path(payload["audit_path"]).exists())

    def test_openai_intervention_cli_accepts_utf8_bom_json(self):
        import tempfile

        with tempfile.TemporaryDirectory() as tmp:
            package = Path(tmp) / "incident.json"
            package.write_text("\ufeff" + json.dumps({"task_id": "TASK-1", "error": "compile failed"}), encoding="utf-8")
            result = self.run_cli("openai-intervention", "--package", str(package), "--model", "test-model")
            self.assertEqual(result.returncode, 0, result.stderr)
            payload = json.loads(result.stdout)
            self.assertEqual(payload["mode"], "preview")

    def test_security_snapshot_cli_outputs_results(self):
        result = self.run_cli("security-snapshot", "--timeout-seconds", "20")
        self.assertEqual(result.returncode, 0, result.stderr)
        payload = json.loads(result.stdout)
        self.assertTrue(payload["read_only"])
        self.assertIn("secrets_audit", payload["results"])


if __name__ == "__main__":
    unittest.main()
