from __future__ import annotations

import json
from pathlib import Path
from typing import Any

from .audit_logger import utc_timestamp
from .secret_filter import redact


RISK_PLAN_TEMPLATES: dict[str, dict[str, Any]] = {
    "real_feishu_send": {
        "action_type": "other",
        "item": "Enable real Feishu message sending",
        "reason": "The bridge currently writes Feishu dry-run outbox files only.",
        "recommendation": "Approve only after choosing the target Feishu channel/account and confirming message templates.",
        "risk": "high",
        "impact": "Messages may be sent to real Feishu users or groups.",
        "rollback": "Disable real send mode, return to local outbox, and review sent message audit records.",
        "planned_commands": ["python -m src.main feishu-send --target <target> --message <message> --execute --approval-decision <decision.json>"],
        "required_evidence": ["Feishu target account/channel confirmed", "message templates reviewed", "redaction scan passed"],
    },
    "openai_real_call": {
        "action_type": "other",
        "item": "Enable real OpenAI intervention calls",
        "reason": "The bridge currently uses local fallback intervention unless --call-openai is explicitly passed.",
        "recommendation": "Approve only for a redacted blocked-task package and a shell-scoped OPENAI_API_KEY.",
        "risk": "high",
        "impact": "A redacted task package will be sent to the OpenAI API.",
        "rollback": "Unset OPENAI_API_KEY, remove --call-openai, and use local fallback directives.",
        "planned_commands": ["python -m src.main openai-intervention --package <incident.json> --execute --approval-decision <decision.json>"],
        "required_evidence": ["blocked package redaction verified", "OPENAI_API_KEY kept out of files", "model selected intentionally"],
    },
    "skill_apply_install": {
        "action_type": "skill_apply",
        "item": "Apply or install an OpenClaw Skill",
        "reason": "Skill apply/install can change agent behavior.",
        "recommendation": "Approve only after proposal inspection, safety scan, and one-agent grey rollout plan.",
        "risk": "high",
        "impact": "One or more agents may receive new behavior or workflow instructions.",
        "rollback": "Remove or roll back the Skill for the target agent, then run OpenClaw skills check.",
        "planned_commands": ["python -m src.main workshop-plan --action apply --proposal-id <id> --execute --approval-decision <decision.json>"],
        "required_evidence": ["skill-scan PASS", "workshop inspect reviewed", "target agent chosen", "rollback command identified"],
    },
    "persistent_service": {
        "action_type": "system_config",
        "item": "Install persistent bridge service or Scheduled Task",
        "reason": "Persistent startup behavior changes local system execution.",
        "recommendation": "Approve only after one-shot supervisor scans are stable and log retention is configured.",
        "risk": "high",
        "impact": "The bridge may run automatically in the background.",
        "rollback": "Disable or remove the Scheduled Task/service and run stop-bridge.ps1.",
        "planned_commands": ["powershell -File chatgpt-openclaw-bridge/scripts/install-service.ps1 -Action Install -Execute -Approved"],
        "required_evidence": ["health-check PASS", "start-bridge -Once PASS", "approval decision recorded"],
    },
    "openclaw_secretrefs": {
        "action_type": "secret_config",
        "item": "Migrate OpenClaw plaintext secrets to SecretRefs",
        "reason": "OpenClaw secrets audit reported plaintext secret-bearing fields.",
        "recommendation": "Approve only with a config backup and a verified OpenClaw secrets workflow.",
        "risk": "high",
        "impact": "Local OpenClaw configuration and credential resolution may change.",
        "rollback": "Restore the pre-migration OpenClaw config backup and rerun openclaw secrets audit --check.",
        "planned_commands": ["openclaw secrets audit --check", "openclaw secrets configure", "openclaw secrets apply"],
        "required_evidence": ["config backup path recorded", "current secrets audit captured", "gateway restart plan known"],
    },
    "command_owner": {
        "action_type": "system_config",
        "item": "Configure OpenClaw command owner",
        "reason": "OpenClaw security audit reported no command owner for owner-only commands.",
        "recommendation": "Approve only after confirming the trusted operator identity.",
        "risk": "high",
        "impact": "Owner-only command authorization will change.",
        "rollback": "Restore prior OpenClaw config and restart the gateway.",
        "planned_commands": ["openclaw config set commands.ownerAllowFrom <trusted-operator-id>", "openclaw gateway restart"],
        "required_evidence": ["trusted operator id confirmed", "config backup path recorded", "gateway restart window approved"],
    },
    "feishu_decision_ingest": {
        "action_type": "other",
        "item": "Enable Feishu approval decision ingestion",
        "reason": "Approval decisions should be ingestible from signed Feishu callback payloads.",
        "recommendation": "Approve only after defining accepted Feishu buttons/messages and replay protection.",
        "risk": "high",
        "impact": "Feishu responses may authorize high-risk local actions.",
        "rollback": "Disable Feishu callback ingestion and require local approval-decision JSON files again.",
        "planned_commands": ["python -m src.main feishu-decision-ingest --request <request.json> --payload <feishu-callback.json> --headers-json <feishu-headers.json> --require-signature --write"],
        "required_evidence": ["X-Lark-Signature verified", "request id matching enforced", "operator identity recorded", "audit retention enabled"],
    },
}


def build_risk_plan(requirement_id: str) -> dict[str, Any]:
    if requirement_id not in RISK_PLAN_TEMPLATES:
        raise KeyError(f"Unknown risk requirement: {requirement_id}")
    template = RISK_PLAN_TEMPLATES[requirement_id]
    plan = {
        "plan_id": f"RISKPLAN-{utc_timestamp()}-{requirement_id}",
        "created_at": utc_timestamp(),
        "requirement_id": requirement_id,
        "status": "PENDING_USER_APPROVAL",
        **template,
        "sensitive_data_removed": True,
    }
    return redact(plan)


def build_all_risk_plans() -> list[dict[str, Any]]:
    return [build_risk_plan(requirement_id) for requirement_id in RISK_PLAN_TEMPLATES]


def build_approval_bundle_manifest(
    *,
    plans: list[dict[str, Any]],
    risk_plan_files: list[str],
    approval_requests: list[dict[str, str]],
    task_id: str,
    requested_by: str,
) -> dict[str, Any]:
    created_at = utc_timestamp()
    items: list[dict[str, Any]] = []
    for index, plan in enumerate(plans):
        approval = approval_requests[index] if index < len(approval_requests) else {}
        risk_plan_file = risk_plan_files[index] if index < len(risk_plan_files) else ""
        items.append(
            {
                "requirement_id": plan["requirement_id"],
                "status": "PENDING_USER_APPROVAL",
                "risk": plan["risk"],
                "action_type": plan["action_type"],
                "item": plan["item"],
                "planned_commands": list(plan["planned_commands"]),
                "required_evidence": list(plan["required_evidence"]),
                "risk_plan_file": risk_plan_file,
                "approval_request": approval.get("approval_request", ""),
                "feishu_outbox": approval.get("feishu_outbox", ""),
            }
        )
    manifest = {
        "bundle_id": f"APPROVALBUNDLE-{created_at}",
        "created_at": created_at,
        "task_id": task_id,
        "requested_by": requested_by,
        "status": "PENDING_USER_APPROVAL",
        "item_count": len(items),
        "items": items,
        "execution_safety": {
            "local_files_only": True,
            "external_actions_executed": False,
            "requires_matching_approval_decision": True,
            "placeholder_commands_block_execution": True,
        },
        "sensitive_data_removed": True,
    }
    return redact(manifest)


def validate_approval_bundle_manifest(manifest: dict[str, Any]) -> list[str]:
    errors: list[str] = []
    if not isinstance(manifest.get("bundle_id"), str) or not manifest["bundle_id"].strip():
        errors.append("bundle_id must be a non-empty string")
    if manifest.get("status") != "PENDING_USER_APPROVAL":
        errors.append("status must be PENDING_USER_APPROVAL")
    if manifest.get("sensitive_data_removed") is not True:
        errors.append("sensitive_data_removed must be true")
    safety = manifest.get("execution_safety")
    if not isinstance(safety, dict):
        errors.append("execution_safety must be an object")
    else:
        for field in ("local_files_only", "requires_matching_approval_decision", "placeholder_commands_block_execution"):
            if safety.get(field) is not True:
                errors.append(f"execution_safety.{field} must be true")
        if safety.get("external_actions_executed") is not False:
            errors.append("execution_safety.external_actions_executed must be false")
    items = manifest.get("items")
    if not isinstance(items, list):
        errors.append("items must be a list")
        return errors
    if manifest.get("item_count") != len(items):
        errors.append("item_count must match items length")
    seen_requirement_ids: set[str] = set()
    seen_paths: set[str] = set()
    for index, item in enumerate(items):
        if not isinstance(item, dict):
            errors.append(f"items[{index}] must be an object")
            continue
        requirement_id = item.get("requirement_id")
        if not isinstance(requirement_id, str) or not requirement_id:
            errors.append(f"items[{index}].requirement_id must be a non-empty string")
        elif requirement_id in seen_requirement_ids:
            errors.append(f"items[{index}].requirement_id is duplicated")
        else:
            seen_requirement_ids.add(requirement_id)
        for field in ("risk_plan_file", "approval_request", "feishu_outbox"):
            value = item.get(field)
            if not isinstance(value, str) or not value.strip():
                errors.append(f"items[{index}].{field} must be a non-empty string")
            elif value in seen_paths:
                errors.append(f"items[{index}].{field} path is duplicated")
            else:
                seen_paths.add(value)
        commands = item.get("planned_commands")
        if not isinstance(commands, list) or not all(isinstance(command, str) and command for command in commands):
            errors.append(f"items[{index}].planned_commands must be a non-empty list of strings")
    return errors


def write_approval_bundle_manifest(output_dir: Path, manifest: dict[str, Any]) -> Path:
    errors = validate_approval_bundle_manifest(manifest)
    if errors:
        raise ValueError("; ".join(errors))
    output_dir.mkdir(parents=True, exist_ok=True)
    safe_id = "".join(ch if ch.isalnum() or ch in ("-", "_") else "-" for ch in manifest["bundle_id"])
    path = output_dir / f"{utc_timestamp()}-approval-bundle-{safe_id}.json"
    path.write_text(json.dumps(redact(manifest), ensure_ascii=False, indent=2), encoding="utf-8")
    return path


def write_risk_plan(output_dir: Path, plan: dict[str, Any]) -> Path:
    output_dir.mkdir(parents=True, exist_ok=True)
    safe_id = "".join(ch if ch.isalnum() or ch in ("-", "_") else "-" for ch in plan["plan_id"])
    path = output_dir / f"{utc_timestamp()}-risk-plan-{safe_id}.json"
    path.write_text(json.dumps(redact(plan), ensure_ascii=False, indent=2), encoding="utf-8")
    return path
