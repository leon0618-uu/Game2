from __future__ import annotations

from pathlib import Path
from typing import Any

from .audit_logger import utc_timestamp
from .secret_filter import redact_text


def _quote(value: str) -> str:
    return '"' + value.replace('"', '\\"') + '"'


def _decision_command(manifest_path: str, requirement_id: str, decision: str, operator: str) -> str:
    manifest_part = f" --manifest {_quote(manifest_path)}" if manifest_path else " --manifest <approval-bundle.json>"
    return f"python -m src.main approval-bundle-decision{manifest_part} --requirement-id {requirement_id} --decision {decision} --decided-by {_quote(operator)}"


def _scope_out_command(manifest_path: str, requirement_id: str, operator: str) -> str:
    manifest_part = f" --manifest {_quote(manifest_path)}" if manifest_path else " --manifest <approval-bundle.json>"
    return f"python -m src.main approval-bundle-scope-out{manifest_part} --requirement-id {requirement_id} --scoped-out-by {_quote(operator)} --reason \"<scope-out reason>\""


def _scope_out_all_command(manifest_path: str, operator: str) -> str:
    manifest_part = f" --manifest {_quote(manifest_path)}" if manifest_path else " --manifest <approval-bundle.json>"
    return f"python -m src.main approval-bundle-scope-out-all{manifest_part} --scoped-out-by {_quote(operator)} --reason \"<scope-out reason>\" --confirm-all"


def build_approval_bundle_report(status: dict[str, Any], operator: str = "<operator>", manifest_path: str = "") -> str:
    lines: list[str] = [
        f"# Approval Bundle Report: {status.get('bundle_id', '')}",
        "",
        f"- Status: `{status.get('overall_status', '')}`",
        f"- Created: `{status.get('created_at', '')}`",
        f"- Sensitive data removed: `{str(status.get('sensitive_data_removed') is True).lower()}`",
        "",
        "## Status Counts",
        "",
    ]
    for key, value in sorted((status.get("status_counts") or {}).items()):
        lines.append(f"- `{key}`: {value}")

    lines.extend(
        [
            "",
            "## Items",
            "",
        ]
    )

    for item in status.get("items", []):
        request_path = str(item.get("approval_request", ""))
        requirement_id = str(item.get("requirement_id", "unknown"))
        lines.extend(
            [
                f"### {requirement_id}",
                "",
                f"- Status: `{item.get('status', '')}`",
                f"- Risk: `{item.get('risk', '')}`",
                f"- Action type: `{item.get('action_type', '')}`",
                f"- Can execute now: `{str(item.get('can_execute') is True).lower()}`",
                f"- Approval request: `{request_path}`",
                f"- Feishu dry-run outbox: `{item.get('feishu_outbox', '')}`",
                f"- Risk plan: `{item.get('risk_plan_file', '')}`",
                f"- Approval decision: `{item.get('approval_decision') or 'none'}`",
                f"- Approval reason: `{item.get('approval_reason', '')}`",
                "",
                "Decision commands:",
                "",
                "```powershell",
                _decision_command(manifest_path, requirement_id, "approve", operator),
                _decision_command(manifest_path, requirement_id, "reject", operator),
                _decision_command(manifest_path, requirement_id, "pause_and_inspect", operator),
                "```",
                "",
                "Scope-out command:",
                "",
                "```powershell",
                _scope_out_command(manifest_path, requirement_id, operator),
                "```",
                "",
            ]
        )
        placeholders = item.get("placeholder_commands") or []
        if placeholders:
            lines.extend(["Placeholder commands blocking execution:", ""])
            for command in placeholders:
                lines.append(f"- `{command}`")
            lines.append("")
        missing_files = item.get("missing_files") or []
        if missing_files:
            lines.extend(["Missing files:", ""])
            for missing in missing_files:
                lines.append(f"- `{missing}`")
            lines.append("")

    lines.extend(
        [
            "## Bulk Scope-Out",
            "",
            "Use this only when every high-risk item is explicitly out of scope for the current V2.0 completion claim.",
            "",
            "```powershell",
            _scope_out_all_command(manifest_path, operator),
            "```",
            "",
            "## Safety",
            "",
            "- This report is read-only.",
            "- It does not send Feishu messages, call OpenAI, apply Skills, install services, push Git changes, or modify OpenClaw configuration.",
            "- Executing any high-risk action still requires a matching approval decision and concrete commands without placeholders.",
            "- To scope out every item, use `approval-bundle-scope-out-all` with `--confirm-all`; this still only writes local scope-out records.",
            "",
        ]
    )
    return redact_text("\n".join(lines))


def write_approval_bundle_report(output_dir: Path, report: str, bundle_id: str) -> Path:
    output_dir.mkdir(parents=True, exist_ok=True)
    safe_id = "".join(ch if ch.isalnum() or ch in ("-", "_") else "-" for ch in bundle_id or "approval-bundle")
    path = output_dir / f"{utc_timestamp()}-approval-bundle-report-{safe_id}.md"
    path.write_text(report, encoding="utf-8")
    return path
