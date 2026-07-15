import tempfile
import unittest
from pathlib import Path

from src.skill_security import scan_skill_proposal


class SkillSecurityTests(unittest.TestCase):
    def test_safe_proposal_passes(self):
        with tempfile.TemporaryDirectory() as tmp:
            root = Path(tmp)
            (root / "PROPOSAL.md").write_text("Safe checklist only.", encoding="utf-8")
            report = scan_skill_proposal(root, workspace_root=root)
            self.assertEqual(report.status, "PASS")

    def test_script_requires_review(self):
        with tempfile.TemporaryDirectory() as tmp:
            root = Path(tmp)
            (root / "PROPOSAL.md").write_text("Includes script.", encoding="utf-8")
            (root / "script.ps1").write_text("Write-Output ok", encoding="utf-8")
            report = scan_skill_proposal(root, workspace_root=root)
            self.assertEqual(report.status, "REVIEW")

    def test_dangerous_command_blocks(self):
        with tempfile.TemporaryDirectory() as tmp:
            root = Path(tmp)
            (root / "PROPOSAL.md").write_text("rm -rf .", encoding="utf-8")
            report = scan_skill_proposal(root, workspace_root=root)
            self.assertEqual(report.status, "BLOCKED")

    def test_negated_safety_phrases_do_not_block(self):
        with tempfile.TemporaryDirectory() as tmp:
            root = Path(tmp)
            (root / "PROPOSAL.md").write_text("- No administrator permissions.\n- No network upload.", encoding="utf-8")
            report = scan_skill_proposal(root, workspace_root=root)
            self.assertEqual(report.status, "PASS")


if __name__ == "__main__":
    unittest.main()
