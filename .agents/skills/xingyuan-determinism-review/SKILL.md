---
name: xingyuan-determinism-review
description: Audit battle code for stable ordering, hashing, pathfinding, events, replay, undo, and nondeterministic inputs.
---

# Xingyuan determinism review

Search for unordered collection iteration affecting results, random values, current time, GUID generation, unstable hashes, missing tie-breaks, floating-point geometry, and replay dependence on Unity objects.

Canonical rules:

- Grid: y then x.
- Pathfinder: down, left, right, up.
- Units: UnitId.
- Statuses: StatusId, remaining turns, instance ID.
- Decrees: instance ID.
- Commands/events: sequence number.
- Polygon ties: canonical vertex sequence.

Report exact evidence, reproducible input, expected stable order, minimal correction, and required regression tests.
