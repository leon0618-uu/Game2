import json
import subprocess
import tempfile
import unittest
from pathlib import Path

from src.openclaw_security_snapshot import SECURITY_COMMANDS, build_openclaw_security_snapshot, write_openclaw_security_snapshot


class OpenClawSecuritySnapshotTests(unittest.TestCase):
    def test_commands_are_read_only(self):
        encoded = json.dumps(SECURITY_COMMANDS)
        self.assertNotIn("--fix", encoded)
        self.assertNotIn("--allow-exec", encoded)

    def test_snapshot_redacts_secret_like_output(self):
        fake_token = "abc12345" + "678901234567890"

        def runner(command, cwd, timeout):
            return subprocess.CompletedProcess(command, 1, stdout=json.dumps({"token": fake_token}), stderr="")

        snapshot = build_openclaw_security_snapshot(Path("."), runner=runner)
        encoded = json.dumps(snapshot)
        self.assertNotIn(fake_token, encoded)
        self.assertEqual(set(snapshot["results"]), set(SECURITY_COMMANDS))

    def test_write_snapshot(self):
        def runner(command, cwd, timeout):
            return subprocess.CompletedProcess(command, 0, stdout='{"ok": true}', stderr="")

        snapshot = build_openclaw_security_snapshot(Path("."), runner=runner)
        with tempfile.TemporaryDirectory() as tmp:
            path = write_openclaw_security_snapshot(Path(tmp), snapshot)
            self.assertTrue(path.exists())


if __name__ == "__main__":
    unittest.main()
