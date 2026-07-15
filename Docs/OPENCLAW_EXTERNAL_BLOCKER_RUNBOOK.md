# OpenClaw / Codex V2.0 External Blocker Runbook

This runbook covers the two remaining V2.0 blockers that cannot be resolved only by local code changes.

## Current Status

Latest local gate result:

```powershell
.\chatgpt-openclaw-bridge\scripts\bridge.ps1 v2-unblock-check --write
```

Expected blocked state before external remediation:

```text
readiness: PASS 33 / EXTERNAL_BLOCKED 2
compliance: PASS 22 / EXTERNAL_BLOCKED 2
approval bundle: COMPLETE 5 / EXTERNAL_BLOCKED 2
goal completion: NOT_COMPLETE
```

## Blocker 1: Feishu Real Send

Current failure:

```text
Feishu code 99991672
Missing scope: contact:user.employee_id:readonly
```

Required external action:

1. Open the Feishu developer console for the app used by the `xingyuan` Feishu account.
2. Add the required permission scope: `contact:user.employee_id:readonly`.
3. Publish or release the app permission change according to the Feishu app workflow.
4. Confirm the app credentials in the user environment are still valid:

```powershell
[Environment]::GetEnvironmentVariable("FEISHU_XINGYUAN_APP_SECRET", "User")
[Environment]::GetEnvironmentVariable("FEISHU_DEFAULT_APP_SECRET", "User")
[Environment]::GetEnvironmentVariable("OPENCLAW_GATEWAY_TOKEN", "User")
```

Do not print or paste the returned secret values into Git, chat, Feishu, or audit notes.

Reference:

- Feishu Open Platform: https://open.feishu.cn/document/faq/trouble-shooting/how-to-fix-the-99991672-error

## Blocker 2: OpenAI Real Intervention Call

Current failure:

```text
OpenAI HTTP 429
code: insufficient_quota
message: You exceeded your current quota, please check your plan and billing details.
```

Required external action:

1. Open the OpenAI Platform billing, usage, and limits pages for the organization/project that owns `OPENAI_API_KEY`.
2. Confirm the API key belongs to the intended project and organization.
3. Resolve the quota condition by adding credits, raising the relevant project or organization budget, or selecting a project/key with usable quota.
4. Confirm the key is still available in the user environment:

```powershell
[Environment]::GetEnvironmentVariable("OPENAI_API_KEY", "User")
```

Do not print or paste the returned key value into Git, chat, Feishu, or audit notes.

Reference:

- OpenAI API error codes: https://developers.openai.com/api/docs/guides/error-codes#api-errors

## Post-Remediation Verification

Preview the retry plan without executing external actions:

```powershell
.\chatgpt-openclaw-bridge\scripts\retry-external-blockers.ps1
```

Run the approved real external checks after both blockers are fixed:

```powershell
.\chatgpt-openclaw-bridge\scripts\retry-external-blockers.ps1 -Execute -Approved
```

The retry script writes both `v2-unblock-check` and `goal-completion-audit` evidence. To rerun the final gates manually:

```powershell
.\chatgpt-openclaw-bridge\scripts\bridge.ps1 v2-unblock-check --write
.\chatgpt-openclaw-bridge\scripts\bridge.ps1 goal-completion-audit --manifest chatgpt-openclaw-bridge\data\audit\20260714T163415Z-approval-bundle-APPROVALBUNDLE-20260714T163415Z.json --write
```

Completion is valid only when:

```text
readiness: PASS
compliance: PASS
approval bundle: COMPLETE
goal completion: COMPLETE
```

If either Feishu or OpenAI still reports `EXTERNAL_BLOCKED`, keep the goal blocked and use the newest audit JSON path from `v2-unblock-check` as the evidence record.
