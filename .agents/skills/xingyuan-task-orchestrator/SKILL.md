---
name: xingyuan-task-orchestrator
description: Help xingyuan-lead decompose one task package, delegate to named agents, review evidence, and report to the user.
---

# Xingyuan task orchestrator

Use only as `xingyuan-lead`.

1. Resolve the request to one task package.
2. Read project rules and acceptance criteria.
3. Select required agents.
4. Define non-overlapping child scopes.
5. Spawn each child with an explicit `agentId` and clear workspace, allowed files, prohibited actions, tests, and output format.
6. Wait for required results.
7. Verify claims against diffs, logs, and test evidence.
8. Ask QA to evaluate the gate when code changes.
9. Summarize in normal user-facing language.

Do not let multiple writers modify the same files concurrently. Do not claim completion from child text alone. Do not Push, create PRs, merge, publish, or change configuration without approval.
