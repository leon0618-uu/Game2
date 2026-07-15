from __future__ import annotations

from .models import SkillRisk


HIGH_RISK_TERMS = {
    "admin",
    "administrator",
    "global dependency",
    "open port",
    "plugin install",
    "production",
    "remote execution",
    "secret",
    "system config",
    "upload",
}

MEDIUM_RISK_TERMS = {
    ".ps1",
    ".py",
    ".js",
    "build",
    "modify project files",
    "run tests",
    "trusted git",
}


def classify_install_risk(description: str) -> SkillRisk:
    lowered = description.lower()
    if any(term in lowered for term in HIGH_RISK_TERMS):
        return SkillRisk.HIGH
    if any(term in lowered for term in MEDIUM_RISK_TERMS):
        return SkillRisk.MEDIUM
    return SkillRisk.LOW

