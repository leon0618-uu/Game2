---
name: xingyuan-architecture-review
description: Review module boundaries, assembly dependencies, command/event design, state ownership, and ADR needs.
---

# Xingyuan architecture review

Review against `AGENTS.md` and `Docs/02_Technical_Development_Manual.md`.

Check:

- `Starfall.Core` has no UnityEngine or UnityEditor dependency.
- Data depends on Core, not scenes or presenters.
- Unity calls Core; Core never calls Unity.
- All battle state changes enter through Command execution.
- Presenters do not own a second battle truth.
- Replay history contains reconstructable command data.
- Events contain IDs and values, not GameObjects.
- New abstractions are required by the current task, not speculative.

Output findings by severity, evidence with paths and symbols, boundary violations, risks, minimal fixes, and ADR proposals.
