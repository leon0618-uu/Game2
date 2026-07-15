# OpenClaw / Codex Collaboration System

> Source: `ChatGPT × OpenClaw 游戏开发协作系统.docx`, V2.0, 2026-07-14.
> Scope: 《星渊誓约》本地 AI 开发团队协作、阻塞介入和 Skill 自进化流程。

## 1. Roles

### ChatGPT / Codex decision layer

- Turn user goals into technical development directives.
- Review architecture, logs, code, tests, and build evidence.
- Diagnose blocked tasks and choose continue, repair, rework, replan, stop, or user approval.
- Produce patches or file-level instructions when needed.
- Extract reusable successful workflows into Skill candidates.

### OpenClaw execution layer

- Route tasks through `xingyuan-lead`.
- Delegate work to `xingyuan-architect`, `xingyuan-gameplay`, `xingyuan-ui-tools`, and `xingyuan-qa`.
- Modify local project files only within approved task scope.
- Run commands, compilation, tests, builds, and evidence collection.
- Install approved Skills through proposal, review, test, and grey release.
- Return real execution evidence. Natural-language confidence is not completion evidence.

### Feishu display layer

- Show task progress, blocked status, ChatGPT intervention summaries, Skill lifecycle updates, and user approval requests.
- Do not expose full chain-of-thought, repeated low-level logs, secrets, or large stack dumps.

### User

The user stays responsible for product direction, major gameplay/rule changes, remote Git operations, releases, paid services, account/security decisions, and high-risk installs.

## 2. Runtime Flow

Normal task flow:

```text
User game request
  -> ChatGPT/Codex technical directive
  -> xingyuan-lead task split
  -> specialist agents execute
  -> xingyuan-qa verifies
  -> Feishu summary
```

Blocked task flow:

```text
OpenClaw task blocks
  -> Task Supervisor collects evidence
  -> ChatGPT/Codex intervention
  -> OpenClaw applies repair or replan
  -> QA verifies
  -> reusable success becomes Skill candidate when appropriate
  -> Skill proposal, safety review, grey install, validation
  -> Feishu final summary
```

## 3. Task Supervisor Rules

The supervisor must prefer real OpenClaw task state over agent prose. Immediate escalation is required when any of these happen:

- Task state becomes `failed`, `timed_out`, or `lost`.
- Agent returns `CAPABILITY_GAP`.
- Required tool, environment, or permission is unavailable.
- Compilation, test, or build has a blocking error.
- Two agents provide mutually exclusive core technical conclusions.
- Operation may damage project files, data, or Git history.
- The fix would require changing confirmed core gameplay rules.

No-progress detection runs every 5 minutes by default. A task is `STALLED` after 3 consecutive checks with no substantive change in:

- Git diff.
- Changed file count.
- Logs.
- Test count or test result.
- Tool calls.
- Diagnostic conclusions.
- Subtask state.

Longer thresholds are allowed:

- Unity first import: 30 minutes.
- Full Player Build: 30 minutes.
- Large asset import: 45 minutes.
- Large automated test run: task-specific timeout.

Repeated failure escalation is required when:

- Same error appears 2 consecutive times.
- Same repair plan fails 2 times.
- Agent cycles among 3 plans.
- Automatic rework reaches 3 rounds.
- Multiple agents repeatedly edit the same file without removing the problem.
- Test result oscillates between pass and fail.

## 4. BlockedTaskPackage

Before escalation, OpenClaw must generate a redacted package:

```json
{
  "task_id": "",
  "project": "Xingyuan Covenant",
  "branch": "",
  "source_agent": "",
  "task_status": "",
  "goal": "",
  "acceptance_criteria": [],
  "current_problem": "",
  "first_error_time": "",
  "last_progress_time": "",
  "attempt_count": 0,
  "attempted_solutions": [],
  "commands_executed": [],
  "error_logs": [],
  "stack_trace": [],
  "git_diff_summary": "",
  "changed_files": [],
  "environment": {
    "os": "",
    "unity_version": "",
    "dotnet_version": "",
    "openclaw_version": ""
  },
  "agent_conclusion": "",
  "agent_confidence": 0.0,
  "requested_help": "",
  "screenshots": [],
  "sensitive_data_removed": true
}
```

Rules:

- Include actual commands, relevant Git diff, changed files, full stack trace, test/build results, and current agent judgment.
- For large logs, include the relevant error window plus log path.
- UI or scene issues require screenshots when feasible.
- Remove API keys, tokens, cookies, passwords, Feishu secrets, private account identifiers, and unrelated personal data.
- Never escalate with only "cannot solve, please help".

## 5. ChatGPT Intervention Levels

| Level | Use when | Output |
|---|---|---|
| L1 Direction | Agent can execute but lacks direction | root cause, wrong attempts to stop, correct steps, files, tests, rollback |
| L2 Diagnosis | Root cause unknown or cross-module | additional commands, minimal repro, package/asmdef checks, Unity batchmode, single failing tests, logs |
| L3 Repair | Fix is clear enough to direct | patch plan, file-level edits, config/test patches, validation commands |
| L4 Replan | Original task design is not executable | task split, ownership changes, sequencing, planned gap, return to architecture |
| L5 Capability | Stable process or tool knowledge is missing | Skill, Plugin, MCP, Hook, or helper-program path |

Structured intervention response:

```json
{
  "intervention_id": "INT-YYYYMMDD-001",
  "task_id": "",
  "root_cause": "",
  "confidence": 0.0,
  "intervention_level": "L1",
  "decision": "REPAIR",
  "stop_current_attempts": [],
  "instructions": [
    {
      "agent": "",
      "action": "",
      "files": [],
      "commands": []
    }
  ],
  "patches": [],
  "tests_required": [],
  "acceptance_criteria": [],
  "rollback": [],
  "skill_candidate": false,
  "user_approval_required": false
}
```

Allowed decisions:

```text
CONTINUE
REPAIR
REWORK
REPLAN
CREATE_SKILL
INSTALL_TOOL
USER_APPROVAL_REQUIRED
BLOCKED_EXTERNAL
STOP
PASS
CONDITIONAL_PASS
```

## 6. Skill Self-Evolution

Use Skill when the agent already has tools but needs repeatable workflow, constraints, review standards, or project knowledge.

Do not use Skill alone when the system lacks:

- A required tool.
- A required API.
- Runtime interception.
- Permission control.
- Gateway lifecycle changes.

Use Plugin, MCP server, Hook, local helper program, or third-party CLI for those cases.

Create a Skill candidate when:

- Same issue appears at least twice.
- Same successful workflow is used at least twice.
- A first issue is complex and clearly reusable.
- The knowledge is a project hard constraint.
- Mistakes could damage the project.
- Agents repeatedly miss the same validation step.
- Multiple agents need the same handoff format.

Do not create a Skill when:

- The operation is one-off.
- The fix is not validated.
- It only patches one specific file.
- It contains secrets or private data.
- It relies on unclear-source scripts.
- It grants overbroad permissions.
- No validation method exists.

## 7. Skill Proposal Workflow

1. Extract experience from the blocked task, failed attempts, successful fix, final patch, validation commands, scope, forbidden cases, and rollback.
2. Generate a Skill Workshop proposal. Do not directly overwrite production `SKILL.md`.
3. ChatGPT reviews reuse value, trigger breadth, overlap, dangerous commands, path safety, dependencies, privacy, tests, and rollback.
4. Run format, safety, prompt-injection, script, path, permission, normal case, error case, and rollback checks.
5. Grey install to one agent first.
6. Re-run the original blocked task and compare failure count, success, time, token usage, Skill triggering, and side effects.
7. Promote or roll back.

Preferred OpenClaw commands, adjusted to the local OpenClaw version:

```powershell
openclaw skills workshop propose-create --name "<skill-name>" --description "<description>" --proposal-dir "<proposal-dir>"
openclaw skills workshop inspect <proposal-id>
openclaw skills workshop apply <proposal-id>
openclaw skills check --agent <agent-id>
openclaw skills install .\path\to\skill --agent <agent-id> --as <skill-name>
```

If a command is unavailable in the installed OpenClaw version, record the exact error and inspect `openclaw skills workshop --help` or `openclaw skills --help` before changing the process.

## 8. Skill Install Risk

| Risk | Examples | Policy |
|---|---|---|
| Low | Markdown workflow, checklist, code review rule, handoff format | ChatGPT review, auto apply, one-agent grey release |
| Medium | PowerShell/Python/Node scripts, project file edits, build/test runners, trusted Git source | ChatGPT security review, sandbox test, designated-agent grey release, QA verification |
| High | Admin rights, system config, global dependency, secrets/accounts/production access, unknown downloads, delete/upload/remote execution, Plugin install, network port exposure | ChatGPT review, Feishu user approval, execute only after approval |

Recommended policy direction:

- Enable install policy before broad automation.
- Fail closed if the policy checker errors.
- Keep high-risk installs behind explicit user approval.

## 9. Initial Skill Allocation

### xingyuan-lead

- `task-decomposition`
- `agent-handoff`
- `blocked-task-escalation`
- `chatgpt-directive-parser`
- `feishu-development-report`
- `user-approval-gate`
- `skill-candidate-extractor`

### xingyuan-architect

- `unity-architecture-adr`
- `module-boundary-review`
- `dependency-risk-review`
- `save-data-versioning`
- `unity-package-selection`
- `technical-debt-register`

### xingyuan-gameplay

- `deterministic-gameplay-code`
- `tactical-combat-formulas`
- `state-machine-implementation`
- `grid-pathfinding-workflow`
- `gameplay-unit-test-generation`
- `combat-debugging`
- `compile-error-triage`

### xingyuan-ui-tools

- `unity-project-bootstrap`
- `unity-editor-tooling`
- `prefab-scene-safety`
- `ui-data-binding`
- `unity-serialization-triage`
- `build-player-pipeline`
- `unity-batchmode-validation`
- `compile-error-triage`

### xingyuan-qa

- `unity-batchmode-validation`
- `compile-error-triage`
- `determinism-replay-test`
- `save-load-compatibility-test`
- `regression-gate`
- `evidence-package-generation`

First local candidates to create or verify:

- `blocked-task-escalation`
- `agent-handoff`
- `unity-batchmode-validation`
- `compile-error-triage`
- `evidence-package-generation`
- `skill-candidate-extractor`

## 10. Feishu Message Templates

Normal progress:

```text
【任务进度】

任务：
状态：
负责人：
当前阶段：
最新进展：
是否需要用户操作：否
```

Blocked alert:

```text
【任务阻塞】

任务：
阻塞时间：
问题：
已尝试：
系统处理：已自动升级给 ChatGPT
是否需要用户操作：否
```

ChatGPT intervention:

```text
【ChatGPT 已介入】

根因：
处理方式：
执行 Agent：
验证 Agent：
置信度：
是否需要用户操作：否
```

Skill lifecycle:

```text
【能力增强】

新 Skill：
来源：
安装对象：
测试结果：
状态：
是否需要用户操作：否
```

User approval:

```text
【需要用户审批】

事项：
原因：
ChatGPT 建议：
风险等级：
影响：
回滚方式：
操作：[批准] [拒绝] [暂停并查看详情]
```

### Structured approval request

High-risk actions must also write a structured local request before Feishu display:

```json
{
  "request_id": "APPROVAL-YYYYMMDDTHHMMSSZ-item",
  "created_at": "YYYYMMDDTHHMMSSZ",
  "status": "pending",
  "action_type": "skill_apply",
  "task_id": "",
  "item": "",
  "reason": "",
  "recommendation": "",
  "risk": "high",
  "impact": "",
  "rollback": "",
  "command": [],
  "evidence": [],
  "requested_by": "codex",
  "requires_user_approval": true,
  "allowed_decisions": ["approve", "reject", "pause_and_inspect"],
  "sensitive_data_removed": true
}
```

Approval decisions must be recorded as separate evidence before any high-risk execution gate consumes them:

```json
{
  "decision_id": "DECISION-YYYYMMDDTHHMMSSZ-request",
  "request_id": "APPROVAL-YYYYMMDDTHHMMSSZ-item",
  "decided_at": "YYYYMMDDTHHMMSSZ",
  "decision": "approve",
  "decided_by": "",
  "notes": "",
  "request_action_type": "skill_apply",
  "request_item": "",
  "request_command": [],
  "sensitive_data_removed": true
}
```

Execution gates must fail closed when the decision is missing, rejected, paused, has the wrong action type, or references a different command.

## 11. Loop Limits

- One task: max 3 automatic rework rounds.
- ChatGPT intervention: max 3 consecutive calls without user review.
- Same patch: apply at most once.
- Same Skill: max 2 automatic revisions.
- Same error: after 2 repeats, do not reuse the same failed plan.
- One task: max 1 new Skill.
- Failed grey Skill: disable or roll back.
- No new evidence: do not call ChatGPT again.
- Reaching limits becomes `USER_APPROVAL_REQUIRED` or `BLOCKED_EXTERNAL`.

## 12. Completion Standard

A blocked task may close only after:

- Root cause is identified.
- Fix is applied.
- Compilation passes.
- Relevant tests pass.
- Regression checks pass.
- No severe new issue exists.
- Git diff is reviewed.
- Evidence is archived.
- New Skill, if any, passed safety review.
- Feishu final summary is sent.

OpenClaw/Codex must record the final state as structured evidence:

```json
{
  "result_id": "FINAL-YYYYMMDDTHHMMSSZ-task",
  "created_at": "YYYYMMDDTHHMMSSZ",
  "task_id": "",
  "status": "PASS",
  "summary": "",
  "evidence": [],
  "qa_result": "",
  "compile_passed": true,
  "tests_passed": true,
  "regression_passed": true,
  "severe_new_issue": false,
  "git_diff_reviewed": true,
  "evidence_archived": true,
  "skill_review_passed": true,
  "feishu_summary_sent": true,
  "caveats": [],
  "blockers": [],
  "next_actions": [],
  "sensitive_data_removed": true
}
```

`PASS` must fail validation if any required evidence flag is absent. Use `CONDITIONAL_PASS`, `REWORK`, or `BLOCKED` when caveats, failed checks, blockers, or follow-up actions remain.

Not accepted as evidence:

- "Looks fine."
- "Code was generated."
- "Theoretically should pass."
- "Agent thinks it is complete."
- Compile-only validation when tests are required.
- Test-only validation when Player Build is required.
- Skill creation without real task validation.

## 13. Readiness Audit

Before claiming the V2.0 collaboration system is complete, run a readiness audit that separates:

- Implemented and locally verified requirements.
- Missing local files, worktrees, scripts, schemas, or checks.
- High-risk operations that are intentionally pending user approval.

The audit must stay read-only. It must not send Feishu messages, call OpenAI, apply Skills, push Git branches, install services, or modify OpenClaw configuration.

The current local bridge command is:

```powershell
python -m src.main readiness-audit
```

For requirement-by-requirement proof against this V2.0 workflow, run:

```powershell
python -m src.main compliance-audit
```

The compliance audit must verify the GitHub remote, project AI-team rules, OpenClaw inspection, Task Supervisor, BlockedTaskPackage, intervention gates, Feishu layer, Skill workflow, approval gates, risk plans, completion standard, loop limits, redaction, worktrees, service gate, and readiness audit. It must still separate real external/high-risk operations as `PENDING_APPROVAL`.

Before claiming this V2.0 objective is complete, run the conservative completion gate:

```powershell
python -m src.main goal-completion-audit --manifest <approval-bundle.json> --write
```

The completion gate may return `COMPLETE` only when readiness is `PASS`, compliance is `PASS`, and high-risk approval-bundle work is resolved with user decisions, concrete execution evidence, verification, or explicit scope-out. `CONDITIONAL_PASS`, pending decisions, placeholder commands, rejected decisions, missing evidence, or unverified external actions must keep the goal `NOT_COMPLETE`.

External blockers that require Feishu app permission or OpenAI billing/quota changes are handled by `Docs/OPENCLAW_EXTERNAL_BLOCKER_RUNBOOK.md`; after remediation, rerun `chatgpt-openclaw-bridge\scripts\retry-external-blockers.ps1 -Execute -Approved` and then the completion gate.

The goal may be considered complete only when missing items are resolved and pending approval items are either approved and verified or explicitly scoped out by the user.

## 14. High-Risk Action Plans

Each readiness item with `PENDING_APPROVAL` must have a local risk plan before it can enter execution. The plan must include:

- Requirement id.
- Action type.
- Reason for approval.
- ChatGPT recommendation.
- Risk level.
- Impact.
- Rollback.
- Planned commands.
- Required evidence.

Risk plans are approval material, not execution. They may create local approval requests and Feishu dry-run messages, but must not send Feishu messages, call OpenAI, apply Skills, install services, push Git changes, or modify OpenClaw configuration by themselves.

Real Feishu sending must go through the sender gate:

```powershell
python -m src.main feishu-send --target <chat-or-user-id> --message "<message>"
```

Default mode is preview. Real sending requires `--execute` and an approved decision that matches the exact OpenClaw message command.

Local Feishu decision collection must go through the ingestion parser:

```powershell
python -m src.main feishu-decision-ingest --request <approval-request.json> --payload <feishu-callback.json> --headers-json <feishu-headers.json> --require-signature --write
```

The parser accepts saved Feishu-style JSON payloads and Feishu 2.0 card action callback payloads. It must reject missing or non-Feishu sources, mismatched `request_id`, unsupported decisions, missing operator identity, and invalid `X-Lark-Signature` when signature verification is required. URL verification challenge payloads return the Feishu challenge response without writing an approval decision. Production use still requires Feishu app callback/event routing and the real callback encrypt key.

OpenAI intervention calls must go through the OpenAI gate:

```powershell
python -m src.main openai-intervention --package <incident.json> --model <model>
```

Default mode is preview and must only show the redacted request body. Real API calls require `--execute`, a shell-scoped `OPENAI_API_KEY`, and an approved decision that matches the exact command.

The default ChatGPT/OpenAI intervention model is `gpt-5.5`. Codex/OpenClaw may proactively override it with `--model <model>` for a specific task when the task benefits from a different capability, cost, latency, or reliability profile. The chosen model must appear in the approval request, execution preview, and audit evidence.

The current local bridge command is:

```powershell
python -m src.main risk-plan --requirement-id all --write
```

To generate the full approval packet for every pending high-risk item:

```powershell
python -m src.main risk-plan --requirement-id all --write --write-approval-request --write-bundle-manifest --task-id V2-APPROVAL-BUNDLE
```

This must produce seven risk plans, seven structured approval requests, seven unique Feishu dry-run outbox files, and one approval-bundle manifest. The manifest is the approval index: each pending item must map to its risk plan, approval request, Feishu outbox, planned command, and required evidence. It remains a local evidence-generation step only; it must not execute the planned commands.

Approval bundle status must be checked before any execution attempt:

```powershell
python -m src.main approval-bundle-status --manifest <approval-bundle.json> --write --write-report --operator <operator>
```

This status check must stay read-only. It verifies referenced files, matching approval decisions, placeholder commands, and execution readiness for each pending item. The optional Markdown report must include per-item approve, reject, and pause-and-inspect decision commands for user review.

User decisions may be recorded by bundle requirement id:

```powershell
python -m src.main approval-bundle-decision --manifest <approval-bundle.json> --requirement-id <pending-item-id> --decision approve --decided-by <operator>
```

This command writes only a local approval-decision JSON file. It must not execute the associated risk plan.

If the user explicitly excludes a pending high-risk item from the current V2.0 completion scope, record that separately:

```powershell
python -m src.main approval-bundle-scope-out --manifest <approval-bundle.json> --requirement-id <pending-item-id> --scoped-out-by <operator> --reason "<reason>"
```

Scope-out is not approval. It must only mean the item is intentionally out of scope for the current completion claim. It writes a local scope-out JSON file and must not execute the associated risk plan.

Bulk scope-out requires explicit confirmation:

```powershell
python -m src.main approval-bundle-scope-out-all --manifest <approval-bundle.json> --scoped-out-by <operator> --reason "<reason>" --confirm-all
```

Without `--confirm-all`, bulk scope-out must fail closed.

Approved plans may be passed to an execution gate:

```powershell
python -m src.main risk-execute --plan <risk-plan.json> --approval-decision <decision.json>
```

The execution gate must remain fail-closed: no decision means preview only, rejected or mismatched decisions block, and placeholder commands block even when an approval exists.

## 15. OpenClaw Security Evidence

Before approving SecretRefs migration or command owner changes, capture read-only OpenClaw security evidence:

```powershell
python -m src.main security-snapshot --write
```

The snapshot must use read-only commands only:

```text
openclaw secrets audit --check --json
openclaw security audit --json
openclaw config validate --json
```

It must not use `--fix`, `--allow-exec`, restart the gateway, edit config, or print secret values.

## 16. Persistent Bridge Service

Persistent background execution must be installed only through an approval-gated script:

```powershell
.\chatgpt-openclaw-bridge\scripts\install-service.ps1 -Action Install -Execute -Approved
```

Default mode must stay non-destructive preview. Without `-Approved`, install or uninstall attempts must block and record the reason. The script may use Windows Scheduled Tasks after approval, but must provide an uninstall path and must not run silently in the background without the user approving startup behavior.
