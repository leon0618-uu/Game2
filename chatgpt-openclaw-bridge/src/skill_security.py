from __future__ import annotations

from dataclasses import dataclass, field
from pathlib import Path

from .secret_filter import SECRET_PATTERNS


DANGEROUS_PATTERNS = [
    "remove-item",
    "rm -rf",
    "rmdir /s",
    "del /s",
    "format ",
    "schtasks /create",
    "new-scheduledtask",
    "start-process powershell",
    "invoke-webrequest",
    "curl ",
    "wget ",
    "upload",
    "administrator",
    "runas",
    "set-executionpolicy",
    "git push",
    "git reset --hard",
]

NEGATED_SAFE_PHRASES = [
    "no administrator",
    "no network upload",
    "no destructive",
    "does not upload",
    "without upload",
]

SCRIPT_SUFFIXES = {".ps1", ".py", ".js", ".ts", ".sh", ".bat", ".cmd"}


@dataclass(frozen=True)
class SkillScanFinding:
    severity: str
    path: str
    message: str


@dataclass(frozen=True)
class SkillScanReport:
    proposal_dir: str
    status: str
    findings: list[SkillScanFinding] = field(default_factory=list)

    @property
    def passed(self) -> bool:
        return self.status == "PASS"


def scan_skill_proposal(proposal_dir: Path, workspace_root: Path | None = None) -> SkillScanReport:
    root = proposal_dir.resolve()
    findings: list[SkillScanFinding] = []
    if workspace_root is not None:
        workspace = workspace_root.resolve()
        try:
            root.relative_to(workspace)
        except ValueError:
            findings.append(SkillScanFinding("high", str(root), "proposal directory is outside workspace"))
    if not root.exists():
        return SkillScanReport(str(root), "BLOCKED", [SkillScanFinding("high", str(root), "proposal directory does not exist")])
    if not (root / "PROPOSAL.md").exists():
        findings.append(SkillScanFinding("medium", str(root), "missing PROPOSAL.md"))

    for path in root.rglob("*"):
        if not path.is_file():
            continue
        try:
            relative = str(path.relative_to(root))
        except ValueError:
            relative = str(path)
        if path.suffix.lower() in SCRIPT_SUFFIXES:
            findings.append(SkillScanFinding("medium", relative, f"script file requires review: {path.suffix}"))
        text = _read_text_best_effort(path)
        lowered = text.lower()
        for pattern in DANGEROUS_PATTERNS:
            if pattern in lowered:
                if _is_negated_safety_statement(lowered, pattern):
                    continue
                findings.append(SkillScanFinding("high", relative, f"dangerous pattern: {pattern}"))
        for secret_pattern in SECRET_PATTERNS:
            if secret_pattern.search(text):
                findings.append(SkillScanFinding("high", relative, "secret-like value detected"))

    status = "PASS"
    if any(f.severity == "high" for f in findings):
        status = "BLOCKED"
    elif any(f.severity == "medium" for f in findings):
        status = "REVIEW"
    return SkillScanReport(str(root), status, findings)


def _read_text_best_effort(path: Path) -> str:
    try:
        return path.read_text(encoding="utf-8")
    except UnicodeDecodeError:
        return path.read_text(encoding="utf-8", errors="replace")


def _is_negated_safety_statement(text: str, pattern: str) -> bool:
    if pattern.strip() == "upload":
        return any(phrase in text for phrase in NEGATED_SAFE_PHRASES)
    if pattern.strip() == "administrator":
        return any(phrase in text for phrase in NEGATED_SAFE_PHRASES)
    return False
