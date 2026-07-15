from __future__ import annotations

from pathlib import Path

from .models import SkillCandidateInput
from .skill_factory import evaluate_skill_candidate


def write_skill_proposal(candidate: SkillCandidateInput, output_root: Path, description: str, install_agent: str) -> Path:
    result = evaluate_skill_candidate(candidate)
    proposal_dir = output_root / candidate.name
    proposal_dir.mkdir(parents=True, exist_ok=True)
    (proposal_dir / "references").mkdir(exist_ok=True)
    (proposal_dir / "examples").mkdir(exist_ok=True)
    (proposal_dir / "scripts").mkdir(exist_ok=True)
    (proposal_dir / "templates").mkdir(exist_ok=True)
    body = "\n".join(
        [
            f"# Skill Proposal: {candidate.name}",
            "",
            f"Description: {description}",
            f"Install agent: `{install_agent}`",
            f"Decision: `{result.decision.value}`",
            "",
            "## Reasons",
            "",
            *[f"- {reason}" for reason in result.reasons],
            "",
            "## Safety Checklist",
            "",
            "- No secrets or private data.",
            "- No administrator permissions.",
            "- No network upload.",
            "- No destructive file operations.",
            "- Requires ChatGPT review before apply.",
            "- Requires grey install to one agent before promotion.",
            "",
            "## Rollback",
            "",
            "Do not apply the proposal, or disable/remove the installed Skill if grey validation fails.",
            "",
        ]
    )
    (proposal_dir / "PROPOSAL.md").write_text(body, encoding="utf-8")
    return proposal_dir

