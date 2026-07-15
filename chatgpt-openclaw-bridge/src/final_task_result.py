from __future__ import annotations

import json
from pathlib import Path
from typing import Any

from .audit_logger import utc_timestamp
from .secret_filter import redact


FINAL_STATUSES = {"PASS", "CONDITIONAL_PASS", "REWORK", "BLOCKED"}


def build_final_task_result(
    *,
    task_id: str,
    status: str,
    summary: str,
    evidence: list[str] | None = None,
    qa_result: str = "",
    compile_passed: bool = False,
    tests_passed: bool = False,
    regression_passed: bool = False,
    severe_new_issue: bool = False,
    git_diff_reviewed: bool = False,
    evidence_archived: bool = False,
    skill_review_passed: bool = True,
    feishu_summary_sent: bool = False,
    caveats: list[str] | None = None,
    blockers: list[str] | None = None,
    next_actions: list[str] | None = None,
) -> dict[str, Any]:
    result = {
        "result_id": f"FINAL-{utc_timestamp()}-{task_id}",
        "created_at": utc_timestamp(),
        "task_id": task_id,
        "status": status,
        "summary": summary,
        "evidence": evidence or [],
        "qa_result": qa_result,
        "compile_passed": compile_passed,
        "tests_passed": tests_passed,
        "regression_passed": regression_passed,
        "severe_new_issue": severe_new_issue,
        "git_diff_reviewed": git_diff_reviewed,
        "evidence_archived": evidence_archived,
        "skill_review_passed": skill_review_passed,
        "feishu_summary_sent": feishu_summary_sent,
        "caveats": caveats or [],
        "blockers": blockers or [],
        "next_actions": next_actions or [],
        "sensitive_data_removed": True,
    }
    return redact(result)


def validate_final_task_result(result: dict[str, Any]) -> list[str]:
    errors: list[str] = []
    for field in ("result_id", "created_at", "task_id", "status", "summary", "qa_result"):
        value = result.get(field)
        if not isinstance(value, str) or not value.strip():
            errors.append(f"{field} must be a non-empty string")
    if result.get("status") not in FINAL_STATUSES:
        errors.append(f"status must be one of {sorted(FINAL_STATUSES)}")
    for field in ("evidence", "caveats", "blockers", "next_actions"):
        value = result.get(field)
        if not isinstance(value, list) or not all(isinstance(item, str) and item.strip() for item in value):
            errors.append(f"{field} must be a list of non-empty strings")
    for field in (
        "compile_passed",
        "tests_passed",
        "regression_passed",
        "severe_new_issue",
        "git_diff_reviewed",
        "evidence_archived",
        "skill_review_passed",
        "feishu_summary_sent",
        "sensitive_data_removed",
    ):
        if not isinstance(result.get(field), bool):
            errors.append(f"{field} must be a boolean")
    status = result.get("status")
    if status == "PASS":
        required_true = [
            "compile_passed",
            "tests_passed",
            "regression_passed",
            "git_diff_reviewed",
            "evidence_archived",
            "skill_review_passed",
            "feishu_summary_sent",
        ]
        for field in required_true:
            if result.get(field) is not True:
                errors.append(f"PASS requires {field}=true")
        if result.get("severe_new_issue") is not False:
            errors.append("PASS requires severe_new_issue=false")
        if not result.get("evidence"):
            errors.append("PASS requires at least one evidence item")
        if result.get("caveats"):
            errors.append("PASS must not include caveats; use CONDITIONAL_PASS instead")
        if result.get("blockers"):
            errors.append("PASS must not include blockers")
    elif status == "CONDITIONAL_PASS":
        if not result.get("caveats"):
            errors.append("CONDITIONAL_PASS requires at least one caveat")
        if not result.get("evidence"):
            errors.append("CONDITIONAL_PASS requires evidence")
    elif status in {"REWORK", "BLOCKED"}:
        if not result.get("blockers") and not result.get("next_actions"):
            errors.append(f"{status} requires blockers or next_actions")
    return errors


def write_final_task_result(output_dir: Path, result: dict[str, Any]) -> Path:
    errors = validate_final_task_result(result)
    if errors:
        raise ValueError("; ".join(errors))
    output_dir.mkdir(parents=True, exist_ok=True)
    safe_id = "".join(ch if ch.isalnum() or ch in ("-", "_") else "-" for ch in result["result_id"])
    path = output_dir / f"{utc_timestamp()}-final-task-result-{safe_id}.json"
    path.write_text(json.dumps(redact(result), ensure_ascii=False, indent=2), encoding="utf-8")
    return path
