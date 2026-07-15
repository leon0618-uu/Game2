# ChatGPT OpenClaw Bridge

Minimal local bridge scaffold for the Xingyuan Covenant OpenClaw / Codex collaboration flow.

This implementation starts with the V2.0 blocking-intervention core:

- redact sensitive data before logs, packages, or Feishu summaries;
- detect immediate escalation, stalled tasks, and repeated failures;
- validate `BlockedTaskPackage` payloads before escalation;
- classify Skill candidates and install risk;
- create structured user approval requests for high-risk actions;
- validate final task results against the V2.0 completion standard;
- keep user approval gates explicit for high-risk actions.

The bridge intentionally does not modify OpenClaw gateway config, install plugins, expose ports, push Git branches, or send Feishu messages by default.

## Validation

Run the included standard-library test suite:

```powershell
& "C:\Users\Leon\AppData\Roaming\uv\python\cpython-3.11.15-windows-x86_64-none\python.exe" `
  -m unittest discover -s chatgpt-openclaw-bridge\tests -t chatgpt-openclaw-bridge
```

Use `py -0p` to rediscover installed Python runtimes if this path changes.

## Commands

Recommended root-level entrypoint:

```powershell
.\chatgpt-openclaw-bridge\scripts\bridge.ps1 <command> [args]
```

The wrapper sets the repository root and runs `python -m src.main` from `chatgpt-openclaw-bridge`, so the Python module path is stable. Direct `python -m src.main ...` commands must be run from `chatgpt-openclaw-bridge` or with that directory on `PYTHONPATH`.

Read OpenClaw gateway health and print a redacted summary:

```powershell
& "C:\Users\Leon\AppData\Roaming\uv\python\cpython-3.11.15-windows-x86_64-none\python.exe" `
  -m src.main health
```

Read a redacted summary of OpenClaw tracked tasks through `openclaw tasks list --json`:

```powershell
& "C:\Users\Leon\AppData\Roaming\uv\python\cpython-3.11.15-windows-x86_64-none\python.exe" `
  -m src.main tasks
```

Use `--full` only for local debugging; it still applies the bridge redaction filter before printing.

Evaluate one task snapshot against V2.0 escalation rules:

```powershell
& "C:\Users\Leon\AppData\Roaming\uv\python\cpython-3.11.15-windows-x86_64-none\python.exe" `
  -m src.main evaluate --task-id TASK-00 --state failed --source-agent xingyuan-qa
```

Generate a redacted `BlockedTaskPackage` audit record:

```powershell
& "C:\Users\Leon\AppData\Roaming\uv\python\cpython-3.11.15-windows-x86_64-none\python.exe" `
  -m src.main blocked-package `
  --task-id TASK-00 `
  --source-agent xingyuan-qa `
  --task-status failed `
  --goal "Validate blocked package generation" `
  --current-problem "Synthetic validation failure" `
  --commands-executed "openclaw tasks list --json" `
  --error-logs "Synthetic error"
```

Collect real failed OpenClaw tasks, generate local incident files, and create Feishu dry-run messages without sending them:

```powershell
& "C:\Users\Leon\AppData\Roaming\uv\python\cpython-3.11.15-windows-x86_64-none\python.exe" `
  -m src.main collect-blocked --status failed --limit 2
```

Create a local Skill Proposal directory without applying or installing it:

```powershell
& "C:\Users\Leon\AppData\Roaming\uv\python\cpython-3.11.15-windows-x86_64-none\python.exe" `
  -m src.main propose-skill `
  --name blocked-task-escalation `
  --description "Collect evidence and escalate blocked tasks to ChatGPT." `
  --install-agent xingyuan-lead `
  --project-hard-constraint `
  --could-damage-project `
  --validated
```

Scan a local Skill Proposal before any workshop/apply step:

```powershell
& "C:\Users\Leon\AppData\Roaming\uv\python\cpython-3.11.15-windows-x86_64-none\python.exe" `
  -m src.main skill-scan `
  --proposal-dir chatgpt-openclaw-bridge\data\skills\blocked-task-escalation
```

Prepare OpenClaw Skill Workshop commands without executing them:

```powershell
& "C:\Users\Leon\AppData\Roaming\uv\python\cpython-3.11.15-windows-x86_64-none\python.exe" `
  -m src.main workshop-plan `
  --action propose-create `
  --agent xingyuan-lead `
  --name blocked-task-escalation `
  --description "Collect evidence and escalate blocked tasks to ChatGPT." `
  --proposal-dir chatgpt-openclaw-bridge\data\skills\blocked-task-escalation
```

`workshop-plan --action apply --execute` is blocked unless `--approved` or an approved `--approval-decision` file is provided. Add `--write-approval-request` to write a local approval request and Feishu dry-run message instead of executing:

```powershell
& "C:\Users\Leon\AppData\Roaming\uv\python\cpython-3.11.15-windows-x86_64-none\python.exe" `
  -m src.main workshop-plan `
  --action apply `
  --proposal-id <proposal-id> `
  --execute `
  --write-approval-request
```

When a local approval decision exists, pass it to the execution gate:

```powershell
& "C:\Users\Leon\AppData\Roaming\uv\python\cpython-3.11.15-windows-x86_64-none\python.exe" `
  -m src.main workshop-plan `
  --action apply `
  --proposal-id <proposal-id> `
  --execute `
  --approval-decision chatgpt-openclaw-bridge\data\approvals\<decision>.json
```

Create a local intervention directive from a blocked-task incident. This uses a local fallback by default and does not call OpenAI:

```powershell
& "C:\Users\Leon\AppData\Roaming\uv\python\cpython-3.11.15-windows-x86_64-none\python.exe" `
  -m src.main intervention `
  --package chatgpt-openclaw-bridge\data\incidents\<incident>.json `
  --reason FAILED
```

To call OpenAI, set `OPENAI_API_KEY` in the shell and pass `--call-openai`. The key must never be written to files, YAML, Git, audit logs, or Feishu.

The default ChatGPT/OpenAI intervention model is `gpt-5.5`. Use `--model <model>` to override it for a specific intervention; future bridge runs may proactively choose a different model when the task requires a different capability, cost, or latency profile.

Preview an approval-gated OpenAI intervention call without calling the API:

```powershell
& "C:\Users\Leon\AppData\Roaming\uv\python\cpython-3.11.15-windows-x86_64-none\python.exe" `
  -m src.main openai-intervention `
  --package chatgpt-openclaw-bridge\data\incidents\<incident>.json `
  --model test-model
```

Actual OpenAI calls require `--execute`, `OPENAI_API_KEY`, and a matching approved decision for the exact command.

Write a Feishu-formatted dry-run message to local outbox without sending:

```powershell
& "C:\Users\Leon\AppData\Roaming\uv\python\cpython-3.11.15-windows-x86_64-none\python.exe" `
  -m src.main feishu-dry-run `
  --type blocked `
  --task TASK-DRYRUN `
  --text "Synthetic blocked task" `
  --attempts "1 recorded attempt"
```

Preview an approval-gated real Feishu send command without sending:

```powershell
& "C:\Users\Leon\AppData\Roaming\uv\python\cpython-3.11.15-windows-x86_64-none\python.exe" `
  -m src.main feishu-send `
  --target <chat-or-user-id> `
  --message "Bridge approval test"
```

Actual send requires `--execute` and a matching approved decision for the exact OpenClaw send command.

Create a structured user approval request and a Feishu dry-run approval message:

```powershell
& "C:\Users\Leon\AppData\Roaming\uv\python\cpython-3.11.15-windows-x86_64-none\python.exe" `
  -m src.main approval-request `
  --item "Apply Skill proposal" `
  --reason "OpenClaw Skill apply changes agent behavior." `
  --recommendation "Approve only after safety scan passes." `
  --risk high `
  --impact "One agent receives a new Skill." `
  --rollback "Remove the Skill and rerun checks." `
  --action-type skill_apply
```

Record a local approval decision for that request:

```powershell
& "C:\Users\Leon\AppData\Roaming\uv\python\cpython-3.11.15-windows-x86_64-none\python.exe" `
  -m src.main approval-decision `
  --request chatgpt-openclaw-bridge\data\approvals\<request>.json `
  --decision approve `
  --decided-by "<operator>"
```

Convert a Feishu approval callback payload into the same approval-decision format:

```powershell
& "C:\Users\Leon\AppData\Roaming\uv\python\cpython-3.11.15-windows-x86_64-none\python.exe" `
  -m src.main feishu-decision-ingest `
  --request chatgpt-openclaw-bridge\data\approvals\<request>.json `
  --payload chatgpt-openclaw-bridge\data\approvals\<feishu-payload>.json `
  --headers-json chatgpt-openclaw-bridge\data\approvals\<feishu-headers>.json `
  --require-signature `
  --write
```

The payload may be a saved Feishu-style object or a Feishu 2.0 card action callback with `event.action.value`. It must identify Feishu, include the matching `request_id`, contain one allowed decision, and name the operator. `--require-signature` verifies `X-Lark-Signature` with the callback encrypt key from `FEISHU_XINGYUAN_ENCRYPT_KEY` by default. URL verification challenge payloads return the challenge response without writing a decision.

Write and validate a final task result. `PASS` requires compile, tests, regression, diff review, evidence archive, Skill review, and Feishu summary evidence:

```powershell
& "C:\Users\Leon\AppData\Roaming\uv\python\cpython-3.11.15-windows-x86_64-none\python.exe" `
  -m src.main final-result `
  --task-id TASK-00 `
  --status REWORK `
  --summary "Task needs another repair pass." `
  --qa-result "QA failed." `
  --blocker "Compile failed."
```

Inspect current V2.0 bridge readiness without changing OpenClaw, Feishu, OpenAI, Git, or system services:

```powershell
& "C:\Users\Leon\AppData\Roaming\uv\python\cpython-3.11.15-windows-x86_64-none\python.exe" `
  -m src.main readiness-audit
```

Use `--write` to archive the readiness audit JSON under `chatgpt-openclaw-bridge\data\audit\`.

Inspect V2.0 document compliance requirement by requirement:

```powershell
& "C:\Users\Leon\AppData\Roaming\uv\python\cpython-3.11.15-windows-x86_64-none\python.exe" `
  -m src.main compliance-audit
```

Use `--write` to archive the compliance audit JSON. This audit verifies the GitHub remote, project AI-team rules, local bridge capabilities, tests, and approval-pending items without sending Feishu messages, calling OpenAI, applying Skills, installing services, pushing Git changes, or modifying OpenClaw configuration.

Run the conservative V2.0 completion gate. This command is the final local check before claiming the goal is complete:

```powershell
& "C:\Users\Leon\AppData\Roaming\uv\python\cpython-3.11.15-windows-x86_64-none\python.exe" `
  -m src.main goal-completion-audit `
  --manifest chatgpt-openclaw-bridge\data\audit\<approval-bundle>.json `
  --write
```

It returns non-zero while readiness/compliance are not `PASS`, while approval-bundle decisions are pending, or while approved high-risk work still lacks concrete execution and verification evidence.

Summarize the remaining V2.0 external blockers without sending Feishu messages or calling OpenAI:

```powershell
& "C:\Users\Leon\AppData\Roaming\uv\python\cpython-3.11.15-windows-x86_64-none\python.exe" `
  -m src.main v2-unblock-check `
  --write `
  --write-report
```

Use this after granting Feishu scopes or resolving OpenAI quota to confirm whether the blocker evidence has changed before rerunning the real send/intervention commands. `--write-report` also writes a Markdown summary suitable for handoff.

External remediation steps are tracked in `Docs/OPENCLAW_EXTERNAL_BLOCKER_RUNBOOK.md`.

Export a local handoff package for the external blocker owner without executing external actions:

```powershell
.\chatgpt-openclaw-bridge\scripts\export-external-blocker-handoff.ps1
```

After both external blockers are fixed, rerun the approved real external checks and the unblock summary together:

```powershell
.\chatgpt-openclaw-bridge\scripts\retry-external-blockers.ps1 -Execute -Approved
```

Without `-Execute`, the script prints the exact files, environment checks, and commands it would use. With `-Execute` but without `-Approved`, it blocks before any external action. Approved execution retries Feishu/OpenAI, then writes both `v2-unblock-check` and `goal-completion-audit` evidence.

Capture read-only OpenClaw security evidence before approving SecretRefs or command-owner changes:

```powershell
& "C:\Users\Leon\AppData\Roaming\uv\python\cpython-3.11.15-windows-x86_64-none\python.exe" `
  -m src.main security-snapshot `
  --write
```

This runs only read-only OpenClaw commands and redacts output before writing audit JSON.

Generate approval-ready plans for high-risk pending items. This is read-only unless `--write` or `--write-approval-request` is supplied, and it never executes the planned command:

```powershell
& "C:\Users\Leon\AppData\Roaming\uv\python\cpython-3.11.15-windows-x86_64-none\python.exe" `
  -m src.main risk-plan `
  --requirement-id all `
  --write
```

To create local approval requests and Feishu dry-run messages for a plan:

```powershell
& "C:\Users\Leon\AppData\Roaming\uv\python\cpython-3.11.15-windows-x86_64-none\python.exe" `
  -m src.main risk-plan `
  --requirement-id openai_real_call `
  --write-approval-request
```

To create the full V2.0 approval packet for all seven pending high-risk items:

```powershell
& "C:\Users\Leon\AppData\Roaming\uv\python\cpython-3.11.15-windows-x86_64-none\python.exe" `
  -m src.main risk-plan `
  --requirement-id all `
  --write `
  --write-approval-request `
  --write-bundle-manifest `
  --task-id V2-APPROVAL-BUNDLE
```

This writes seven risk plans, seven approval requests, seven unique Feishu dry-run outbox JSON files, and one approval-bundle manifest under `chatgpt-openclaw-bridge\data\`. The manifest maps each pending item to its plan, request, outbox file, planned command, and required evidence. It still does not send Feishu messages, call OpenAI, apply Skills, install services, push Git changes, or modify OpenClaw configuration.

Inspect a generated approval bundle without executing any planned command:

```powershell
& "C:\Users\Leon\AppData\Roaming\uv\python\cpython-3.11.15-windows-x86_64-none\python.exe" `
  -m src.main approval-bundle-status `
  --manifest chatgpt-openclaw-bridge\data\audit\<approval-bundle>.json `
  --write `
  --write-report `
  --operator "<operator>"
```

The status JSON checks referenced files, approval decisions, placeholder commands, and execution readiness. It can report `WAITING_FOR_DECISION`, `APPROVED_BLOCKED_PLACEHOLDER`, `READY_TO_EXECUTE`, `DECISION_REJECTED`, `PAUSE_AND_INSPECT`, or `INVALID_OR_MISSING_EVIDENCE`. `--write-report` also writes a Markdown approval report with per-item decision commands.

Record one local decision from the bundle by requirement id. This writes an approval-decision JSON file only; it does not execute the risk plan:

```powershell
& "C:\Users\Leon\AppData\Roaming\uv\python\cpython-3.11.15-windows-x86_64-none\python.exe" `
  -m src.main approval-bundle-decision `
  --manifest chatgpt-openclaw-bridge\data\audit\<approval-bundle>.json `
  --requirement-id openai_real_call `
  --decision pause_and_inspect `
  --decided-by "<operator>"
```

Explicitly scope one pending high-risk item out of the current V2.0 completion scope. This writes a scope-out JSON file only; it does not approve or execute the risk plan:

```powershell
& "C:\Users\Leon\AppData\Roaming\uv\python\cpython-3.11.15-windows-x86_64-none\python.exe" `
  -m src.main approval-bundle-scope-out `
  --manifest chatgpt-openclaw-bridge\data\audit\<approval-bundle>.json `
  --requirement-id openai_real_call `
  --scoped-out-by "<operator>" `
  --reason "Not part of this phase."
```

Scope out every pending item only with explicit confirmation:

```powershell
& "C:\Users\Leon\AppData\Roaming\uv\python\cpython-3.11.15-windows-x86_64-none\python.exe" `
  -m src.main approval-bundle-scope-out-all `
  --manifest chatgpt-openclaw-bridge\data\audit\<approval-bundle>.json `
  --scoped-out-by "<operator>" `
  --reason "Not part of this phase." `
  --confirm-all
```

Preview or execute an approved risk plan. The default is a dry-run preview. `--execute` requires a matching approved decision, and commands containing placeholders such as `<incident.json>` are blocked until made concrete:

```powershell
& "C:\Users\Leon\AppData\Roaming\uv\python\cpython-3.11.15-windows-x86_64-none\python.exe" `
  -m src.main risk-execute `
  --plan chatgpt-openclaw-bridge\data\audit\<risk-plan>.json
```

Risk execution records include `mode`, `blocked`, `can_execute`, `placeholder_commands`, and redacted command results when execution actually occurs.

Run one supervisor scan. This reads real OpenClaw failed tasks, writes local incidents, local intervention directives, and local outbox messages:

```powershell
& "C:\Users\Leon\AppData\Roaming\uv\python\cpython-3.11.15-windows-x86_64-none\python.exe" `
  -m src.main supervisor-run --once --limit 1
```

PowerShell helpers:

```powershell
.\chatgpt-openclaw-bridge\scripts\bridge.ps1 readiness-audit
.\chatgpt-openclaw-bridge\scripts\health-check.ps1
.\chatgpt-openclaw-bridge\scripts\start-bridge.ps1 -Once -Limit 1
.\chatgpt-openclaw-bridge\scripts\stop-bridge.ps1
.\chatgpt-openclaw-bridge\scripts\install-service.ps1
.\chatgpt-openclaw-bridge\scripts\export-external-blocker-handoff.ps1
```

`install-service.ps1` defaults to a non-destructive preview. Installing or uninstalling the Windows Scheduled Task requires `-Execute -Approved`:

```powershell
.\chatgpt-openclaw-bridge\scripts\install-service.ps1 -Action Install
.\chatgpt-openclaw-bridge\scripts\install-service.ps1 -Action Install -Execute -Approved
.\chatgpt-openclaw-bridge\scripts\install-service.ps1 -Action Uninstall -Execute -Approved
```

V2.0 requires a matching approval decision before changing startup behavior.

## Phase Boundary

Current phase: V2.0 local rules scaffold plus read-only OpenClaw health/task inspection, one-shot supervisor scans, local intervention fallback, Skill Proposal generation and apply evidence, structured approval requests and decisions, signed Feishu callback decision ingestion, final-result validation, readiness auditing, high-risk action planning, dynamic approval-bundle status, and Feishu dry-run outbox.

Still externally blocked or pending:

- real Feishu send: implemented and attempted, but the Feishu app currently lacks `contact:user.employee_id:readonly`.
- real OpenAI intervention call: implemented and attempted with the default `gpt-5.5`, but the API account currently returns `insufficient_quota`.
- Production Feishu callback delivery still requires Feishu app callback/event routing and the real callback encrypt key, but the local signed callback ingestion path is implemented and audited.
- Windows service installation through Scheduled Task was denied; the installer falls back to the current user's Startup folder.

Those require additional approvals where they affect secrets, system services, or external messaging.
