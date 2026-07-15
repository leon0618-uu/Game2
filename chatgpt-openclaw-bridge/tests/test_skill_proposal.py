import tempfile
import unittest
from pathlib import Path

from src.models import SkillCandidateInput
from src.skill_proposal import write_skill_proposal


class SkillProposalTests(unittest.TestCase):
    def test_write_skill_proposal_creates_expected_files(self):
        with tempfile.TemporaryDirectory() as tmp:
            path = write_skill_proposal(
                SkillCandidateInput(name="agent-handoff", successful_workflow_count=2, validated=True),
                Path(tmp),
                "Standardize agent handoff.",
                "xingyuan-lead",
            )
            self.assertTrue((path / "PROPOSAL.md").exists())
            self.assertTrue((path / "references").is_dir())
            self.assertIn("Decision: `CREATE`", (path / "PROPOSAL.md").read_text(encoding="utf-8"))


if __name__ == "__main__":
    unittest.main()

