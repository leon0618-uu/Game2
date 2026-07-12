---
name: xingyuan-dev-workflow
description: Enforce the Xingyuan task-package, documentation, Git, approval, testing, and reporting workflow.
---

# Xingyuan development workflow

Use for every Xingyuan Covenant task.

## Preflight

1. Confirm agent ID, workspace, branch, and `git status`.
2. Read `AGENTS.md` and the relevant files under `Docs/`.
3. Resolve the request to one task package in `Docs/04_Roadmap_and_Milestones.md`.
4. State scope, out-of-scope work, dependencies, and acceptance criteria.
5. Inspect existing implementation before modifying files.

## Rules

- Complete only the approved task package.
- Keep the project compiling.
- Add or update tests with logic changes.
- Do not Push, create PRs, merge, publish, delete files, change Unity version, or install packages without approval.
- Do not invent core gameplay rules; report gaps or propose an ADR.
- Keep Core free of Unity dependencies.
- Preserve deterministic ordering and replay behavior.

## Completion report

Report task, agent, workspace, branch, changed files, completed work, incomplete work, commands run, actual test evidence, known issues, impact, next task, and user decisions needed.
