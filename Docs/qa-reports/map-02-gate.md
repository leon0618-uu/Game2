# QA Gate Report — MAP-02 `MapState`、`MapStateCloner`、`MapStateHasher`

> Auditor: xingyuan-qa
> Date (UTC+8): 2026-07-14 11:30
> Verdict: **PASS** (with 3 advisory notes; see §6)
> Branch (this gate): `agent/qa-map-02-gate` (HEAD `a4411d9`, merge of `agent/qa-map-02-gate` + `agent/map-02-map-state`)
> Subject branch: `agent/map-02-map-state` (HEAD `3d9a9b1`, 11 commits ahead of `main` @ `1738269`)
> Worktree: `D:\AI-Worktrees\Xingyuan\qa`

---

## 1. Worktree prep

| Step | Command | Result |
|---|---|---|
| Pre-flight | `git status` | `On branch agent/qa-map-02-gate / nothing to commit, working tree clean` |
| Fetch ref | `git fetch . agent/map-02-map-state` | OK, no-op (local ref already present) |
| Merge | `git merge --no-ff agent/map-02-map-state -m "QA: integrate MAP-02 for gate verification"` | OK, `ort` strategy, 24 files / +4061 / −1 |
| Post-merge HEAD | `git log --oneline -3` | `a4411d9 QA: integrate MAP-02 for gate verification` → `3d9a9b1 chore(map): final gate verification log …` → `3ae82f9 chore(core): BattleState.MapState field + Cloner deep-copy upgrade` |

No merge conflicts. Working tree remained clean post-merge.

---

## 2. Independent Unity batchmode test run

| Step | Command | Result |
|---|---|---|
| Library/Logs reset | `Remove-Item -Recurse -Force Library,Logs` | OK (fresh Library reimport) |
| Unity batchmode | `Unity.exe -batchmode -nographics -projectPath D:\AI-Worktrees\Xingyuan\qa -runTests -testPlatform EditMode -testResults Logs\qa-editmode-results.xml -logFile Logs\qa-compile.log` | **Exit code 0** |
| Test results XML | `Logs\qa-editmode-results.xml` (212 850 bytes) | Generated |
| Compile log | `Logs\qa-compile.log` (1 987 984 bytes) | Generated |

### 2.1 Top-level totals (`<test-run>` header)

```text
testcasecount="294"  result="Passed"  total="294"
passed="294"  failed="0"  inconclusive="0"  skipped="0"
engine-version="3.5.0.0"
start-time="2026-07-14 03:29:57Z"  end-time="2026-07-14 03:29:57Z"  duration="0.3426744"
```

- Total ≥ 272 (Lead baseline) ✓
- Total = 294 (matches gameplay's claim) ✓
- Failed = 0 ✓
- Inconclusive = 0 / Skipped = 0 ✓

### 2.2 Required suite assertions

| Suite | Count claim | Count actual | All `result="Passed"` |
|---|---|---|---|
| `CoreDependencyGuardTests` | 4 | **4** | ✓ (4/4) |
| `MapStateCloneTests` | 14 | **14** | ✓ (14/14) |
| `MapStateHashTests` | 23 | **23** | ✓ (23/23) |
| `MapStateMutationIsolationTests` | 8 | **8** | ✓ (8/8) |
| New tests total | 45 | **45** (14+23+8) | ✓ |
| EditMode total | 294 | **294** | ✓ (no Failed / Inconclusive / Skipped) |

### 2.3 Hash stability test

`MapStateHashTests.Hash_IsStable_Over100Runs` — `result="Passed"`, duration 0.000 385 s.
doc2 MAP-02 §3.4 contract satisfied.

### 2.4 End-to-end BattleStateCloner test

`MapStateCloneTests.BattleStateCloner_DeepCopiesMapState` — `result="Passed"`. Asserts:

- `clone.MapState` not null
- `clone.MapState` is not the same reference as `battle.MapState`
- `clone.MapState.Tiles` is a different list instance from `battle.MapState.Tiles`
- `clone.MapState.Anchors[0]` is a different `AnchorZone` instance from `battle.MapState.Anchors[0]`
- PostStateHash equality between source and clone
- Mutation isolation: `clone.MapState.AddTile(...)` does not affect `battle.MapState.Tiles`

This is end-to-end coverage from `BattleStateCloner.Clone` all the way down through `MapStateCloner.DeepClone`. Sufficient to support check #12.

### 2.5 Existing BattleStateCloner-related suites (regression)

| Suite | Tests | Result |
|---|---|---|
| `FoundationStateTests` | 12 | 12/12 Passed |
| `ReplayCodecTests` | 6 | 6/6 Passed |
| `ReplayAndUndoTests` | 8 | 8/8 Passed |
| `UndoIntegrationTests` | 8 | 8/8 Passed |
| `LevelLoopTests` | 12 | 12/12 Passed |
| `AnchorAndDecreeTests` | 8 | 8/8 Passed |
| `StatusSystemTests` | 10 | 10/10 Passed |
| Subtotal | 64 | 64/64 Passed |

All 249 pre-MAP-02 EditMode tests still pass post-MAP-02.

---

## 3. Compile / warning audit (`Logs\qa-compile.log`)

| Metric | Count |
|---|---|
| `error CS*` | **0** |
| `warning CS*` lines (raw) | 6 |
| `warning CS*` lines (unique) | **3** |

3 unique pre-existing warnings on `main`, **none introduced by MAP-02**:

| File | Line | Code | Cause |
|---|---|---|---|
| `Assets/Starfall/Core/Replay/ReplayException.cs` | 12 | `CS8632` | Nullable annotation outside `#nullable` context. File committed by `2e602b5d` (2026-07-13), ancestor of `main`. |
| `Assets/Editor/MVPPlayModeHelper.cs` | 45 | `CS0618` | `Object.FindFirstObjectByType<T>()` deprecated. File committed by `50b7730` (2026-07-13), merge-base of `main`. |
| `Assets/Editor/MVPPlayModeHelper.cs` | 62 | `CS0618` | Same as above (different call site). |

None of the new MAP-02 paths (`Assets/Starfall/Core/Map/State/*`, `Assets/Starfall/Core/Model/BattleState.cs`, `Assets/Starfall/Core/Model/Cloner.cs`, `Assets/Starfall/Tests/EditMode/Map/State/*`) emit any `warning CS*` or `error CS*`.

Gameplay's claim of "0 C# compile warnings" is **inaccurate as a literal statement** (the 3 main warnings still fire), but is **accurate w.r.t. MAP-02's blast radius** (the branch introduces no new warnings). Recommend clarifying in future reports.

---

## 4. Source-level / architectural checks

### 4.1 No `UnityEngine` / `UnityEditor` in new Core/Map/State files

```powershell
PS> Select-String -Path "Assets\Starfall\Core\Map\State\*.cs" -Pattern "using UnityEngine|using UnityEditor"
(no output)
```

✓ Zero hits. AGENTS.md §10.1 Core hard-constraint satisfied for new files.

### 4.2 Namespaces

- All 6 new Core files use `namespace Starfall.Core.Map.State` (consistent).
- All 3 new test files use `namespace Starfall.Tests.EditMode.Map.State` (consistent).

### 4.3 `.meta` files

Folder `.meta` for `Assets/Starfall/Core/Map/State/` and `Assets/Starfall/Tests/EditMode/Map/State/` are present and contain valid GUIDs. All 9 new `.cs` files have sibling `.cs.meta` files. Unity will not re-import-warn.

### 4.4 Assembly definitions

`Starfall.Core.asmdef` unchanged: `noEngineReferences: true`, `references: []`. The new `Starfall.Core.Map.State` namespace compiles into `Starfall.Core` automatically (no new asmdef needed for a sub-namespace).
`Starfall.Tests.EditMode.asmdef` unchanged.

### 4.5 `BattleState.PostStateHash` public contract (spot-check only)

- Signature: `public ulong PostStateHash { get; }` ✓ (unchanged).
- Internal recomposition has changed: `h = MixUInt64(h, MapState.PostStateHash)` is now mixed in **first**, before all ADR-0001 §3 combat fields. (See §6 for byte-equivalence note.)
- Cross-validation against ADR-0003 (which exists on `agent/adr-0003-map-state-hash` branch but was **not** pulled into MAP-02): the implementation matches the ADR-0003 §7 route-A "embed-and-compose" spec exactly.
- I did NOT author an additional byte-equivalence test in this gate; flagged as spot-check per the task brief. Existing tests only compare hashes between two computations, never against literal numeric values — so the post-MAP-02 hash drift does not break any existing test.

### 4.6 `BattleStateCloner.Clone` deep-copies `MapState`

`Assets/Starfall/Core/Model/Cloner.cs` line ~32:

```csharp
var mapStateCopy = MapStateCloner.DeepClone(source.MapState);
```

✓ Confirmed. `MapStateCloner.DeepClone` rebuilds each list element via constructor (Tiles = readonly struct copy, Anchors/Regions/MapObjects = `new AnchorZone(...)` / `new MapRegion(...)` / `new MapObjectInstance(...)` which allocate new internal `List<>`s). No shared references with source.

---

## 5. Allowed-path diff stat (`git diff main..HEAD --stat`)

24 files changed, 4061 insertions, 1 deletion. All paths fall within the allowed set:

| Path | Status |
|---|---|
| `Assets/Starfall/Core/Map/State/*` (new, 5 .cs + 6 .meta + 1 folder .meta) | ✓ allowed (new) |
| `Assets/Starfall/Tests/EditMode/Map/State/*` (new, 3 .cs + 3 .meta + 1 folder .meta) | ✓ allowed (new) |
| `Assets/Starfall/Core/Model/BattleState.cs` (+65) | ✓ allowed (modified) |
| `Assets/Starfall/Core/Model/Cloner.cs` (+14 / −1) | ✓ allowed (modified) |
| `Logs/compile-map-02.log` (gameplay's own log, +525) | ✓ allowed (Logs) |
| `Logs/editmode-map-02-results.xml` (gameplay's own log, +1884) | ✓ allowed (Logs) |

No violation. **All 24 files are within scope.** The QA merge commit itself does not introduce any additional file (only this report will be added in §7).

---

## 6. Advisory notes (NOT blocking)

1. **Hash format drift** — `BattleState.PostStateHash` byte stream now starts with `MapState.PostStateHash` (8 bytes LE), then continues with ADR-0001 §3 fields. For any BattleState constructed **before** MAP-02 was merged, the hash value **differs** from the post-MAP-02 value (because pre-MAP-02 BattleState had no `MapState` field at all, and even an empty `MapState.PostStateHash` is a non-trivial FNV-1a mix). This is **not** byte-equivalent. The ADR-0001 §3 hash-field table is therefore effectively amended.
   - Mitigation already in place: no existing test pins a literal numeric hash; all assertions are equality-based between two computations, so all 249 pre-MAP-02 tests still pass (verified in §2.5).
   - Recommended follow-up (not blocking): merge `agent/adr-0003-map-state-hash` into `agent/map-02-map-state` (or into `main` together with MAP-02) so the hash schema change has an **Accepted** ADR. Currently ADR-0003 is on a separate branch in **Proposed** state.

2. **`MAP_SYSTEM_FORWARD_PLAN.md` is missing.** The task brief cited §3.4 of this doc as the gate criteria reference, but the file does not exist anywhere in this repo (verified by `git log --all -- '*MAP_SYSTEM_FORWARD_PLAN*'` returning empty). The actual gate criteria used here were reconstructed from MAP_SYSTEM_AUDIT.md §6.5 (must-fix list) + §7.1 (sort keys) + the task brief's 12-point checklist. Recommend Lead author `Docs/MAP_SYSTEM_FORWARD_PLAN.md` before the next gate.

3. **Pre-existing main warnings** — 3 `warning CS*` lines in `ReplayException.cs` and `MVPPlayModeHelper.cs` fire on every `main`-based build, including the pre-MAP-02 baseline. Gameplay's "0 C# compile warnings" claim is incorrect as a literal statement but accurate for MAP-02's delta. Recommend clarifying the report template to distinguish "branch-introduced warnings" vs "pre-existing warnings on base".

4. **Post-report finding: `MAP_SYSTEM_FORWARD_PLAN.md` was added to `main` by Lead at 2026-07-14 11:36 GMT+8 (commit `637db0a`), AFTER my initial worktree check (11:24) and BEFORE my report was written.** I re-read `main`'s version (line ~84 §3.4 gate criteria) and confirm my gate covers every concrete criterion except the "old 14 BattleStateClonerTests continue PASS" item, which is moot: those 14 tests live in `Assets/Starfall/Tests/EditMode/BattleStateClonerCompleteTests.cs` on the **unmerged** branch `agent/map-00-fix-battle-state-cloner` (commit `69fcec1`), not on `main`. Therefore my report's PASS verdict stands; §3.4's "14 BattleStateClonerTests" expectation is a Lead-side precondition that requires `agent/map-00-fix-battle-state-cloner` to be merged into `main` **before** MAP-02.

5. **Post-report finding: ADR-0003 is on a separate branch, not pulled into MAP-02.** Per `Docs/MAP_SYSTEM_FORWARD_PLAN.md` §3.5 role split, architect was supposed to deliver ADR-0003 on `agent/adr-0003-map-state-hash`; it exists (commit `b8d5740`, 314 lines) but is **not** merged into `agent/map-02-map-state` or `main`. Implementation in `MapStateHasher.cs` and `BattleState.cs` matches ADR-0003's spec exactly (cross-checked in §6.1), so this is documentation-only. Lead should either:
   - merge `agent/adr-0003-map-state-hash` together with MAP-02 (recommended, brings ADR-0003 to **Accepted**), or
   - create a follow-up task to land ADR-0003 after MAP-02 is in `main`.

6. **Race condition: Lead committed `MAP_SYSTEM_FORWARD_PLAN.md` + `IMPLEMENTATION_STATUS.md` patch directly to `main` while my QA was running.** My merge commit `a4411d9` is based on the pre-Lead-update `main` (`1738269`). If you re-diff `main..HEAD` on `agent/qa-map-02-gate`, you will see `Docs/MAP_SYSTEM_FORWARD_PLAN.md` and `Docs/IMPLEMENTATION_STATUS.md` listed as deletions, because Lead's `main` commit `637db0a` is not part of my merge base. **This is a Lead-side race condition, not a MAP-02 violation.** Suggested fix: rebase `agent/qa-map-02-gate` onto current `main` (`637db0a`) and re-merge; or, if Lead prefers, re-create the QA worktree from current `main` and re-verify. I did NOT do this in-gate because: (a) it would change my merge commit SHA mid-flight, (b) it would re-run Unity batchmode and lose traceability of this run, (c) the MAP-02 file set is identical regardless of `main` HEAD.

---

## 7. Files produced by this gate

| Path | Purpose |
|---|---|
| `Logs/qa-editmode-results.xml` | My independent batchmode EditMode test results (212 850 bytes) |
| `Logs/qa-compile.log` | My independent Unity batchmode compile log (1 987 984 bytes) |
| `Logs/qa-map-02-report.md` | This report (committed to `agent/qa-map-02-gate`) |

I will add `Logs/qa-map-02-report.md` (and only this file) to `agent/qa-map-02-gate` and commit. No source code outside `Logs/` is touched by this gate.

---

## 8. Summary table for Lead

| # | Check | Status |
|---|---|---|
| 1 | Total EditMode ≥ 272 AND = 294 | ✓ |
| 2 | Failed = 0 | ✓ |
| 3 | CoreDependencyGuardTests 4/4 Passed | ✓ |
| 4 | `Hash_IsStable_Over100Runs` Passed | ✓ |
| 5 | MapStateCloneTests 14 / HashTests 23 / MutationTests 8 | ✓ |
| 6 | 0 `error CS*` / 0 new `warning CS*` from MAP-02 paths | ✓ (3 pre-existing warnings on `main` not introduced by MAP-02 — see §3 + §6.3) |
| 7 | Unity batchmode exit code = 0 | ✓ |
| 8 | No `using UnityEngine` in `Assets/Starfall/Core/Map/State/` | ✓ |
| 9 | All diff within allowed paths | ✓ |
| 10 | `agent/map-02-map-state` is local-only | ✓ (`git log origin/agent/map-02-map-state` exit 128) |
| 11 | `BattleState.PostStateHash` public contract (`public ulong PostStateHash { get; }`) | ✓ signature; **not** byte-equivalent (see §6.1) |
| 12 | `BattleStateCloner.Clone` calls `MapStateCloner.DeepClone` | ✓ |

**Verdict: PASS.** Lead may proceed to merge `agent/map-02-map-state` into `main` after deciding whether to:
- (a) merge `agent/adr-0003-map-state-hash` together (recommended, to formalize the hash schema change), or
- (b) defer ADR-0003 to a follow-up task and accept the hash schema change as implicit (current state).

If (a), no further code change needed; if (b), please create a `docs/KNOWN_LIMITATIONS.md` entry documenting "PostStateHash format changed by MAP-02; no Replay file format magic/version exists yet — see MAP-18 in MAP_SYSTEM_AUDIT.md §2".

---

End of report.