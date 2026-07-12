---
name: xingyuan-test-gate
description: Verify task acceptance using actual Unity/test evidence, determinism checks, logs, and manual acceptance records.
---

# Xingyuan test gate

Record agent, workspace, branch, commit, Unity version, exact command, suite/filter, totals, passed, failed, skipped, duration, result file, log paths, and failure summaries.

Rules:

- Do not accept a developer claim without evidence.
- Distinguish run-and-pass, run-and-fail, static-only, and not-run.
- Confirm tests match the current task.
- Confirm no unrelated files changed.
- Confirm documentation and implementation agree.
- Confirm determinism/replay tests for stateful rules.
- Block progression on compile errors, required test failures, missing evidence, or critical architecture violations.

Return PASS, CONDITIONAL PASS, or FAIL with reasons.
