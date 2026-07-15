from __future__ import annotations

import json
import os
import urllib.error
import urllib.request
from dataclasses import dataclass
from typing import Any

from .secret_filter import redact


DEFAULT_MODEL = "gpt-5.5"
RESPONSES_URL = "https://api.openai.com/v1/responses"


@dataclass(frozen=True)
class OpenAIResult:
    ok: bool
    payload: dict[str, Any] | None = None
    error: str = ""


class OpenAIClient:
    def __init__(self, api_key_env: str = "OPENAI_API_KEY", model: str = DEFAULT_MODEL, timeout_seconds: int = 60) -> None:
        self.api_key_env = api_key_env
        self.model = model
        self.timeout_seconds = timeout_seconds

    def build_intervention_request(self, blocked_package: dict[str, Any]) -> dict[str, Any]:
        return {
            "model": self.model,
            "input": [
                {
                    "role": "system",
                    "content": [
                        {
                            "type": "input_text",
                            "text": "Return only a JSON intervention directive matching the provided schema. Do not include secrets.",
                        }
                    ],
                },
                {
                    "role": "user",
                    "content": [
                        {
                            "type": "input_text",
                            "text": json.dumps({"blocked_task": redact(blocked_package)}, ensure_ascii=False),
                        }
                    ],
                },
            ],
            "text": {
                "format": {
                    "type": "json_schema",
                    "name": "intervention_directive",
                    "strict": True,
                    "schema": {
                        "type": "object",
                        "additionalProperties": False,
                        "required": [
                            "intervention_id",
                            "task_id",
                            "root_cause",
                            "confidence",
                            "intervention_level",
                            "decision",
                            "stop_current_attempts",
                            "instructions",
                            "patches",
                            "tests_required",
                            "acceptance_criteria",
                            "rollback",
                            "skill_candidate",
                            "user_approval_required",
                        ],
                        "properties": {
                            "intervention_id": {"type": "string"},
                            "task_id": {"type": "string"},
                            "root_cause": {"type": "string"},
                            "confidence": {"type": "number"},
                            "intervention_level": {"enum": ["L1", "L2", "L3", "L4", "L5"]},
                            "decision": {
                                "enum": [
                                    "CONTINUE",
                                    "REPAIR",
                                    "REWORK",
                                    "REPLAN",
                                    "CREATE_SKILL",
                                    "INSTALL_TOOL",
                                    "USER_APPROVAL_REQUIRED",
                                    "BLOCKED_EXTERNAL",
                                    "STOP",
                                    "PASS",
                                    "CONDITIONAL_PASS",
                                ]
                            },
                            "stop_current_attempts": {"type": "array", "items": {"type": "string"}},
                            "instructions": {"type": "array", "items": {"type": "string"}},
                            "patches": {"type": "array", "items": {"type": "string"}},
                            "tests_required": {"type": "array", "items": {"type": "string"}},
                            "acceptance_criteria": {"type": "array", "items": {"type": "string"}},
                            "rollback": {"type": "array", "items": {"type": "string"}},
                            "skill_candidate": {"type": "boolean"},
                            "user_approval_required": {"type": "boolean"},
                        },
                    },
                }
            },
        }

    def request_intervention(self, blocked_package: dict[str, Any]) -> OpenAIResult:
        api_key = os.environ.get(self.api_key_env)
        if not api_key:
            return OpenAIResult(False, error=f"{self.api_key_env} is not set")
        body = json.dumps(self.build_intervention_request(blocked_package), ensure_ascii=False).encode("utf-8")
        request = urllib.request.Request(
            RESPONSES_URL,
            data=body,
            headers={
                "Authorization": f"Bearer {api_key}",
                "Content-Type": "application/json",
            },
            method="POST",
        )
        try:
            with urllib.request.urlopen(request, timeout=self.timeout_seconds) as response:
                payload = json.loads(response.read().decode("utf-8"))
        except urllib.error.HTTPError as exc:
            error_body = exc.read().decode("utf-8", errors="replace")[:2000]
            return OpenAIResult(False, error=f"OpenAI HTTP {exc.code}: {exc.reason}: {redact(error_body)}")
        except urllib.error.URLError as exc:
            return OpenAIResult(False, error=f"OpenAI URL error: {exc.reason}")
        except TimeoutError:
            return OpenAIResult(False, error="OpenAI request timed out")
        return OpenAIResult(True, payload=payload)
