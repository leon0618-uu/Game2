from __future__ import annotations

from .models import SkillRisk


def requires_user_approval(action: str, risk: SkillRisk | str) -> bool:
    normalized_risk = risk.value if isinstance(risk, SkillRisk) else str(risk).lower()
    lowered = action.lower()
    high_risk_action = any(
        term in lowered
        for term in [
            "push",
            "pull request",
            "merge",
            "release",
            "admin",
            "global dependency",
            "plugin install",
            "open port",
            "system config",
            "secret",
            "production",
        ]
    )
    return normalized_risk == SkillRisk.HIGH.value or high_risk_action

