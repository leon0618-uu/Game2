from __future__ import annotations

import json
from pathlib import Path

from .audit_logger import utc_timestamp
from .secret_filter import redact_text


def progress_message(task: str, status: str, owner: str, stage: str, progress: str) -> str:
    return redact_text(
        "\n".join(
            [
                "【任务进度】",
                "",
                f"任务：{task}",
                f"状态：{status}",
                f"负责人：{owner}",
                f"当前阶段：{stage}",
                f"最新进展：{progress}",
                "是否需要用户操作：否",
            ]
        )
    )


def blocked_message(task: str, blocked_minutes: int, problem: str, attempts: str) -> str:
    return redact_text(
        "\n".join(
            [
                "【任务阻塞】",
                "",
                f"任务：{task}",
                f"阻塞时间：{blocked_minutes} 分钟",
                f"问题：{problem}",
                f"已尝试：{attempts}",
                "系统处理：已自动升级给 ChatGPT",
                "是否需要用户操作：否",
            ]
        )
    )


def intervention_message(root_cause: str, action: str, executor: str, verifier: str, confidence: int) -> str:
    return redact_text(
        "\n".join(
            [
                "【ChatGPT 已介入】",
                "",
                f"根因：{root_cause}",
                f"处理方式：{action}",
                f"执行 Agent：{executor}",
                f"验证 Agent：{verifier}",
                f"置信度：{confidence}%",
                "是否需要用户操作：否",
            ]
        )
    )


def skill_message(skill: str, source: str, install_agent: str, test_result: str, status: str) -> str:
    return redact_text(
        "\n".join(
            [
                "【能力增强】",
                "",
                f"新 Skill：{skill}",
                f"来源：{source}",
                f"安装对象：{install_agent}",
                f"测试结果：{test_result}",
                f"状态：{status}",
                "是否需要用户操作：否",
            ]
        )
    )


def approval_message(item: str, reason: str, recommendation: str, risk: str, impact: str, rollback: str) -> str:
    return redact_text(
        "\n".join(
            [
                "【需要用户审批】",
                "",
                f"事项：{item}",
                f"原因：{reason}",
                f"ChatGPT 建议：{recommendation}",
                f"风险等级：{risk}",
                f"影响：{impact}",
                f"回滚方式：{rollback}",
                "操作：[批准] [拒绝] [暂停并查看详情]",
            ]
        )
    )


def write_outbox_message(outbox_dir: Path, message_type: str, message: str) -> Path:
    outbox_dir.mkdir(parents=True, exist_ok=True)
    safe_type = "".join(ch if ch.isalnum() or ch in ("-", "_") else "-" for ch in message_type)
    created_at = utc_timestamp()
    path = outbox_dir / f"{created_at}-{safe_type}.json"
    suffix = 2
    while path.exists():
        path = outbox_dir / f"{created_at}-{safe_type}-{suffix}.json"
        suffix += 1
    path.write_text(
        json.dumps({"message_type": message_type, "created_at": created_at, "message": redact_text(message)}, ensure_ascii=False, indent=2),
        encoding="utf-8",
    )
    return path
