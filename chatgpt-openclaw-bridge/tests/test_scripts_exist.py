import unittest
import json
import subprocess
from pathlib import Path


class ScriptsExistTests(unittest.TestCase):
    def test_windows_scripts_exist(self):
        root = Path(__file__).resolve().parents[1] / "scripts"
        for name in [
            "bridge.ps1",
            "start-bridge.ps1",
            "stop-bridge.ps1",
            "health-check.ps1",
            "install-service.ps1",
            "retry-external-blockers.ps1",
            "export-external-blocker-handoff.ps1",
        ]:
            self.assertTrue((root / name).exists(), name)

    def test_install_service_preview_is_non_destructive(self):
        script = Path(__file__).resolve().parents[1] / "scripts" / "install-service.ps1"
        result = subprocess.run(
            ["powershell", "-NoProfile", "-ExecutionPolicy", "Bypass", "-File", str(script)],
            text=True,
            encoding="utf-8",
            capture_output=True,
            check=False,
        )
        self.assertEqual(result.returncode, 0, result.stderr)
        payload = json.loads(result.stdout)
        self.assertEqual(payload["status"], "preview")
        self.assertFalse(payload["execute"])

    def test_install_service_execute_requires_approval(self):
        script = Path(__file__).resolve().parents[1] / "scripts" / "install-service.ps1"
        result = subprocess.run(
            ["powershell", "-NoProfile", "-ExecutionPolicy", "Bypass", "-File", str(script), "-Action", "Install", "-Execute"],
            text=True,
            encoding="utf-8",
            capture_output=True,
            check=False,
        )
        self.assertEqual(result.returncode, 2)
        payload = json.loads(result.stdout)
        self.assertEqual(payload["status"], "blocked")

    def test_retry_external_blockers_preview_is_non_destructive(self):
        script = Path(__file__).resolve().parents[1] / "scripts" / "retry-external-blockers.ps1"
        result = subprocess.run(
            ["powershell", "-NoProfile", "-ExecutionPolicy", "Bypass", "-File", str(script)],
            text=True,
            encoding="utf-8",
            capture_output=True,
            check=False,
        )
        self.assertEqual(result.returncode, 0, result.stderr)
        payload = json.loads(result.stdout)
        self.assertEqual(payload["status"], "preview")
        self.assertFalse(payload["execute"])
        self.assertIn("approval_bundle", payload["required_files"])
        self.assertTrue(any("goal-completion-audit" in command for command in payload["commands"]))

    def test_retry_external_blockers_execute_requires_approval(self):
        script = Path(__file__).resolve().parents[1] / "scripts" / "retry-external-blockers.ps1"
        result = subprocess.run(
            ["powershell", "-NoProfile", "-ExecutionPolicy", "Bypass", "-File", str(script), "-Execute"],
            text=True,
            encoding="utf-8",
            capture_output=True,
            check=False,
        )
        self.assertEqual(result.returncode, 2)
        payload = json.loads(result.stdout)
        self.assertEqual(payload["status"], "blocked")

    def test_retry_external_blockers_approved_read_only_path_reports_integer_exit_codes(self):
        script = Path(__file__).resolve().parents[1] / "scripts" / "retry-external-blockers.ps1"
        result = subprocess.run(
            [
                "powershell",
                "-NoProfile",
                "-ExecutionPolicy",
                "Bypass",
                "-File",
                str(script),
                "-Execute",
                "-Approved",
                "-RetryFeishu",
                "0",
                "-RetryOpenAI",
                "0",
            ],
            text=True,
            encoding="utf-8",
            capture_output=True,
            check=False,
        )
        self.assertNotEqual(result.returncode, 0)
        payload = json.loads(result.stdout)
        self.assertIsInstance(payload["unblock_check_exit_code"], int)
        self.assertIsInstance(payload["goal_completion_exit_code"], int)
        self.assertEqual(payload["unblock_check_exit_code"], 2)
        self.assertEqual(payload["goal_completion_exit_code"], 2)

    def test_export_external_blocker_handoff_writes_local_files(self):
        import tempfile

        script = Path(__file__).resolve().parents[1] / "scripts" / "export-external-blocker-handoff.ps1"
        with tempfile.TemporaryDirectory() as tmp:
            result = subprocess.run(
                ["powershell", "-NoProfile", "-ExecutionPolicy", "Bypass", "-File", str(script), "-OutputDir", tmp],
                text=True,
                encoding="utf-8",
                capture_output=True,
                check=False,
            )
            self.assertEqual(result.returncode, 0, result.stderr)
            payload = json.loads(result.stdout)
            self.assertTrue(Path(payload["handoff_markdown"]).exists())
            handoff_json = Path(payload["handoff_json"])
            self.assertTrue(handoff_json.exists())
            handoff = json.loads(handoff_json.read_text(encoding="utf-8-sig"))
            self.assertIn("external_blockers", handoff)
            self.assertIn("remediation", handoff)
            self.assertIn("retry-external-blockers.ps1", handoff["remediation"]["approved_retry_command"])


if __name__ == "__main__":
    unittest.main()
