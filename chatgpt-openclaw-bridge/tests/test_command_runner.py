import unittest

from src.command_runner import resolve_command


class CommandRunnerTests(unittest.TestCase):
    def test_non_openclaw_command_is_unchanged(self):
        self.assertEqual(resolve_command(["git", "status"]), ["git", "status"])

    def test_openclaw_command_is_resolved_or_left_with_args(self):
        resolved = resolve_command(["openclaw", "--version"])
        self.assertIn("--version", resolved)
        self.assertTrue(resolved[0].lower().endswith(("powershell", "powershell.exe", "openclaw", "openclaw.cmd")))


if __name__ == "__main__":
    unittest.main()

