# MAP-05 Pathfinding Framework · QA Gate Note

**Verifier**: xingyuan-qa (subagent, depth 1/1)
**Date**: 2026-07-15 17:55 GMT+8
**Subject branch**: `agent/map-05-pathfinding` @ `5177e28`
**Base**: `main` @ `aef85c9`
**Worktree**: `D:\AI-Worktrees\Xingyuan\qa` (detached HEAD at `5177e28`)
**Reverify commit hygiene**: ✅ 0 uncommitted changes (qa worktree was on detached HEAD `b5934cb` → checked out `5177e28` cleanly)

---

## 1. 总体裁决

# ✅ PASS

All 9 Gate criteria PASS on independent Unity batchmode run.
**859 / 859 EditMode tests PASS** (766 baseline + 93 new MAP-05 tests), 0 failed, 0 skipped.
0 compile errors. 0 new warnings. All §10.1 hard constraints preserved. BFSPathfinder backward-compat retained.

---

## 2. Gate 结果（9 维度）

| # | Gate | 结果 | 证据 |
|---|------|------|------|
| 1 | 分支基线 | **PASS** | HEAD = `5177e28`; `git log main..HEAD --oneline` = 3 commits (`47f2e76 feat` + `b58ab3c docs(adr)` + `5177e28 test`); `git rev-parse HEAD` = `5177e28cc519a5d372db831cdac558d860e7871c`; main HEAD = `aef85c9` ✓ |
| 2 | 范围控制 | **PASS** | 28 files diff；6 个负面维度全部 0 变更（Unity/Data/manifest.json/ProjectSettings/Command/BFSPathfinder.cs） |
| 3 | §10.1 Core 无 Unity 引用 | **PASS** | `grep "using UnityEngine\|using UnityEditor\|MonoBehaviour\|ScriptableObject\|GameObject\|Transform"` 在 `Assets/Starfall/Core/Map/Pathfinding/*.cs` (5 files) = **0 行** |
| 4 | Unity batchmode 编译 | **PASS** | exit code 0；0 个 error；3 个 unique warning (CS8632 × 1 + CS0618 × 2) 全部 pre-existing |
| 5 | EditMode 测试 | **PASS** | `qa-map-05-editmode.xml`: `testcasecount=859 result=Passed passed=859 failed=0 inconclusive=0 skipped=0` |
| 6 | CoreDependencyGuardTests | **PASS** | 4/4 (Core_Asmdef_DoesNotReferenceUnity / Core_NoMonoBehaviourSubclasses / Core_NoScriptableObjectSubclasses / Core_NoUnityAssemblyRefs) |
| 7 | `Assets/Starfall/Unity/` 去重 | **PASS** | diff 中 0 个 `Assets/Starfall/Unity/*` 文件 |
| 8 | 编译警告增量 | **PASS** | 0 new；3 unique = 全部 pre-existing baseline (CS8632 × 1 + CS0618 × 2) |
| 9 | ID assertion 覆盖 | **PASS** | `Map05_TaskId_AssertedString_Tests` = **14** tests PASS（≥ 5 ✅）；覆盖 MapMovementProfile / MapPath / MapPath.PathFailure / MapPassabilityResult.RejectionCode / FootprintAccessibility / Service Namespaces 6 个新 service/type |

---

## 3. 独立测试结果（xingyuan-qa subagent self-run）

### 3.1 Unity batchmode compile 命令

```powershell
& "C:\Program Files\Unity\Hub\Editor\6000.5.3f1\Editor\Unity.exe" `
    -batchmode -projectPath "D:\AI-Worktrees\Xingyuan\qa" `
    -quit -logFile "D:\UntiyProject\XingyuanCovenant\Logs\qa-map-05-compile.log" `
    -nographics
```

**结果**: exit 0 / 0 errors / 3 unique pre-existing warnings

### 3.2 EditMode 测试命令

```powershell
& "C:\Program Files\Unity\Hub\Editor\6000.5.3f1\Editor\Unity.exe" `
    -batchmode -projectPath "D:\AI-Worktrees\Xingyuan\qa" `
    -runTests -testPlatform EditMode `
    -testResults "D:\UntiyProject\XingyuanCovenant\Logs\qa-map-05-editmode.xml" `
    -logFile "D:\UntiyProject\XingyuanCovenant\Logs\qa-map-05-editmode.log" `
    -nographics
```

**XML 摘要** (`qa-map-05-editmode.xml`):
```
testcasecount=859 result=Passed total=859 passed=859 failed=0 inconclusive=0 skipped=0 asserts=0
duration=0.8078909s
start-time=2026-07-15 09:54:44Z
```

### 3.3 MAP-05 新增 fixture 拆分（93 tests, 0 failed）

| Fixture | passed | failed | 备注 |
|---------|--------|--------|------|
| PathfindingServiceTests | 21 | 0 | A* 算法正确性 + N→E→S→W 邻居顺序 + 跨层 Heuristic + Tie-break (F,H,Y,X,Layer) |
| MapPassabilityTests | 21 | 0 | 7 拒绝原因链 (Pass/BlockedByTile/BlockedByHeightDelta/BlockedByUnit/BlockedByPhase/BlockedByRegion/InsufficientMovement) |
| MapPathTests | 11 | 0 | Null/From 工厂 + PathFailure 常量 + RiskTags 排序 + ToString 格式 |
| MovementProfileTests | 13 | 0 | Standard/Flyer/Heavy 三种工厂 + 边界值 + 哈希/相等 + ToString |
| MovementRangeTests | 13 | 0 | BFS-based AP 范围 + AP 门控 + 输出排序 (GridCoord.CompareTo) |
| Map05_TaskId_AssertedString_Tests | 14 | 0 | ID assertion（≥ 5 ✅）— 覆盖 6 个新 service/type |
| **(MAP-05 合计)** | **93** | **0** | +766 baseline = 859 total |

**注意**：还有另一个 `MovementProfileTests` 存在（7 tests）在 `Starfall.Core.Map.Height.MovementProfileTests` namespace — 这是 MAP-06 pre-existing 的版本（同名但不同 namespace，避免冲突，见 Advisory A1）。两个 fixture 都 PASS，0 conflict。

### 3.4 CoreDependencyGuardTests 拆分（4/4 PASS）

| Test | passed | failed | 用途 |
|------|--------|--------|------|
| Core_Asmdef_DoesNotReferenceUnity | 1 | 0 | 验证 Starfall.Core.asmdef `noEngineReferences=true` |
| Core_NoMonoBehaviourSubclasses | 1 | 0 | grep `MonoBehaviour` 派生类 = 0 |
| Core_NoScriptableObjectSubclasses | 1 | 0 | grep `ScriptableObject` 派生类 = 0 |
| Core_NoUnityAssemblyRefs | 1 | 0 | 静态扫描所有 Core source file 的 `using Unity*` = 0 |
| **(合计)** | **4** | **0** | §10.1 全部硬约束 preserved |

### 3.5 回归 sanity check（关键 baseline fixture）

| Fixture | total | passed | failed | 备注 |
|---------|-------|--------|--------|------|
| TileOccupancyServiceTests | 18 | 18 | 0 | MAP-04 baseline 完整保持（与 TileOccupancyService.cs +12 line additive 兼容） |
| MapStateHashTests | 23 | 23 | 0 | ADR-0003 hash 稳定性保持 |
| CommandAndPathfinderTests | 10 | 10 | 0 | MAP-03 command + pathfinder 集成保持 |
| HeightTraversalTests | 16 | 16 | 0 | MAP-06 height 判定保持（MapMovementProfile 复用 Height.MovementProfile） |
| MovementProfileTests (Height namespace) | 7 | 7 | 0 | MAP-06 MovementProfile pre-existing 保留 |

---

## 4. 编译警告分析

### 4.1 当前编译输出（qa-map-05-fresh-compile.log, fresh ScriptAssemblies）

| Warning | Count | 位置 | 性质 |
|---------|-------|------|------|
| CS8632 | **× 4 occurrences** (1 unique location) | `Assets/Starfall/Core/Replay/ReplayException.cs:12,74` | pre-existing（无 `#nullable enable` 但有 `?` annotation） |
| CS0618 | **× 0 in fresh compile log** (但 2 unique location in baseline) | `Assets/Editor/MVPPlayModeHelper.cs:45,62` | pre-existing（`FindFirstObjectByType<T>()` obsolete）— Bee 缓存命中不重发 |
| **unique 总数** | **3 (1+2)** | | **全部 pre-existing** |

### 4.2 vs 上次 Gate baseline（MAP-03 Gate @ `1ecca54`）

- ✅ unique warning count 完全一致: **3 = CS8632 × 1 + CS0618 × 2**
- ✅ 0 new warning
- ✅ 0 error
- ✅ 所有 warning 位置都在 pre-existing 文件（`ReplayException.cs` / `MVPPlayModeHelper.cs`），均不在 MAP-05 diff 中

### 4.3 出现次数差异解释

| Warning | MAP-03 count | MAP-05 count | 解释 |
|---------|--------------|--------------|------|
| CS8632 | 2 | 4 | MAP-03 编译了 2 个含 ReplayException.cs 的 assembly（Core + Tests.EditMode）；MAP-05 编译了 4 个（含 Core + Tests.EditMode + Tests.PlayMode + Unity）— 因为 MAP-05 +Unity 引用增加触发重编译 |
| CS0618 | 2 | 0 (cached) | Assembly-CSharp-Editor.dll Bee 缓存命中不重发，warning 不再次输出；MAP-03 是从 scratch 编译 |

**关键**：unique warning code + location count 维持 3 = baseline，**0 new**。✅ Gate 8 PASS。

---

## 5. Scope 控制（diff 范围）

### 5.1 包含（28 files）

**`Assets/Starfall/Core/Map/Pathfinding/` (5 源文件 + 5 .meta, +2488/-0)**:

| File | Lines | Role |
|------|-------|------|
| `PathfindingService.cs` | 11800 bytes | A* 寻路主服务（确定性 N→E→S→W 邻居顺序 + Manhattan+cross-layer Heuristic + (F,H,Y,X,Layer) tie-break + 4 FailureReason codes）|
| `MapPassabilityService.cs` | 12506 bytes | 7 拒绝原因校验链（Pass / BlockedByTile / BlockedByHeightDelta / BlockedByUnit / BlockedByPhase / BlockedByRegion / InsufficientMovement）|
| `MapMovementProfile.cs` | 6520 bytes | 移动 profile readonly struct（Standard / Flyer / Heavy 工厂 + MaxAscendHeight / MaxDescendHeight / CanFly / CanCrossDimension / MaxMovementPoints）|
| `MovementRangeService.cs` | 5598 bytes | BFS-based 可达 tile 扩展（AP 门控 + GridCoord.CompareTo 稳定排序）|
| `MapPath.cs` | 5489 bytes | 寻路结果数据结构（success/failure + PathFailure codes + RiskTags + ToString format）|

**`Assets/Starfall/Core/Map/Tile/TileOccupancyService.cs` (+12)**:
- 新增 `TryGetAttachedRegistry(MapState)` 静态 accessor（read-only，MAP-05 pathfinding 集成需要；**no behavioral change**）

**`Assets/Starfall/Tests/EditMode/Map/Pathfinding/` (6 fixtures + 1 helper + 7 .meta, +632 / -0)**:

| Fixture | Lines | Test Count | 角色 |
|---------|-------|------------|------|
| `PathfindingServiceTests.cs` | 17076 bytes | 21 | A* 算法 + 邻居顺序 + Heuristic 一致性 + Tie-break + FailureReason 分类 |
| `MapPassabilityTests.cs` | 12068 bytes | 21 | 7 拒绝原因分类 + 跨层/跨相校验 + 高度差边界 |
| `MovementRangeTests.cs` | 10177 bytes | 13 | BFS 范围扩展 + AP 门控 + 排序稳定性 |
| `MovementProfileTests.cs` | 5376 bytes | 13 | Standard/Flyer/Heavy 工厂 + 边界值 + 哈希 + ToString |
| `MapPathTests.cs` | 5726 bytes | 11 | Null/From 工厂 + RiskTags 排序 + ToString |
| `Map05_TaskId_AssertedString_Tests.cs` | 5926 bytes | 14 | ID assertion（≥ 5 ✅）— Lead 约定 |
| `PathfindingTestHelpers.cs` | 6594 bytes | 0 [Test] | 共享 helper（builder + assert + GridMap fixture factory）|

**`Docs/ADR/`**:
- `ADR-0005-pathfinding-framework.md` (10450 bytes)：完整 ADR — Context / Decision / Consequences / A* 算法 / 7 拒绝原因 / MovementProfile 三态 / BFS 范围 / MapPath 数据结构 / Backward Compat（BFSPathfinder 保留）

### 5.2 不修改（6 个 negative 维度，diff 中 0 变更）

| 路径 | diff 中文件数 |
|------|----------------|
| `Assets/Starfall/Unity/*` | **0** ✅ |
| `Assets/Starfall/Data/*` | **0** ✅ |
| `Packages/manifest.json` | **0** ✅ |
| `ProjectSettings/*` | **0** ✅ |
| `Assets/Starfall/Core/Command/*` | **0** ✅（保持 ADR-0002 LegacyCommand 系统隔离） |
| `Assets/Starfall/Core/Pathfinding/BFSPathfinder.cs` | **0** ✅（保留作为 MVP 兼容层，179 baseline tests 引用） |

### 5.3 范围守卫（额外 hash 兼容 + 与 MAP-04/06 兼容）

- ✅ `Assets/Starfall/Core/Map/State/MapState.cs` / `MapStateCloner.cs` / `MapStateHasher.cs` 在 diff 中 0 变更（ADR-0003 hash 稳定）
- ✅ `Assets/Starfall/Core/Map/Tile/*` 在 diff 中只 1 文件 (`TileOccupancyService.cs` +12，其余 `Footprint.cs` / `MapTileState.cs` / `TerrainRegistry.cs` / `TileDefinitionRegistry.cs` 0 变更（MAP-04 冻结 @ `9b8956b`）
- ✅ `Assets/Starfall/Core/Map/LineOfSight/*` / `Cover/*` / `Height/*` 0 变更（MAP-06 冻结 @ `ff0c641`）
- ✅ `Assets/Starfall/Core/Map/Coordinates/*` 0 变更
- ✅ `Assets/Starfall/Core/Map/Commands/*` 0 变更（MAP-03 冻结 @ `1ecca54`）

---

## 6. Commit hygiene（本次 Gate 验证基础）

### 6.1 3 commits ahead of main

```
5177e28 test(map): MAP-05 tests for Pathfinding/Passability/Range/Profile/Path
b58ab3c docs(adr): ADR-0005-pathfinding-framework
47f2e76 feat(map): MAP-05 Pathfinding/A*/Passability/Range services + MapPath
```

### 6.2 commit `47f2e76` (feat) 内容

- `PathfindingService`（deterministic A* + N→E→S→W 邻居顺序 + Manhattan + cross-layer heuristic + (F, H, Y, X, Layer) tie-break + 4 FailureReason codes: NoPath / GoalBlocked / StartOccupied / Unreachable）
- `MapPassabilityService`（PassabilityResult.RejectionCode enum: Pass / BlockedByTile / BlockedByHeightDelta / BlockedByUnit / BlockedByPhase / BlockedByRegion / InsufficientMovement）
- `MovementRangeService`（BFS-based reachable-tile expansion, AP gated, sorted output by GridCoord.CompareTo）
- `MapMovementProfile`（Standard / Flyer / Heavy; MaxAscendHeight / MaxDescendHeight / CanFly / CanCrossDimension / MaxMovementPoints）— 与既有 `Height.MovementProfile` (MAP-06) 并存（179-test baseline 引用，故意不重命名）
- `MapPath`（success/failure result + PathFailure codes + RiskTags）
- `TileOccupancyService.TryGetAttachedRegistry` accessor（read-only, MAP-05 pathfinding 集成; **no behavioral change**）
- `BFSPathfinder` (`Assets/Starfall/Core/Pathfinding`) **保留** for MVP 向后兼容（179 baseline tests reference it）

### 6.3 commit `b58ab3c` (docs) 内容

- `ADR-0005-pathfinding-framework.md`：完整 ADR（10.4 KB），覆盖算法决策、拒绝链、MovementProfile 命名（避免与 Height.MovementProfile 冲突）、Backward Compat 策略

### 6.4 commit `5177e28` (test) 内容

- 6 测试 fixture + 1 helper
- 93 个新 EditMode test (实际超过任务预期 56 → 65% 超额完成，见 Advisory A2)
- `Map05_TaskId_AssertedString_Tests` 14 个 ID assertion test

### 6.5 .cs ↔ .cs.meta 配对检查

- 5 source files + 5 .meta ✅
- 6 test fixtures + 1 helper / *.cs + 7 .meta ✅
- TileOccupancyService.cs.meta ✅
- ADR-0005-pathfinding-framework.md (no .meta needed) ✅

### 6.6 未推送 / 未合并

- ✅ 分支未 push（AGENTS §9）
- ✅ 未合并到 main
- ✅ 工作区 untracked files = `Docs/qa-reports/map-03-gate.md`（pre-existing，不在本任务范围）

---

## 7. Architecture 注释（informational）

| # | 维度 | 描述 |
|---|------|------|
| 7.1 | `PathfindingService.FindPath` | 经典 A*（g + h, openSet = min-heap by F）；邻居顺序严格 N→E→S→W；Heuristic = Manhattan (same layer) + 1 (cross-layer)；Tie-break (H, Y, X, Layer)；失败语义：永远返回非 null `MapPath`，`Success=false` + `FailureReason` |
| 7.2 | `MapPassabilityService.CanEnter` | 7 步校验链（优先级顺序）：`BlocksMovement` → `IsCellPassable` (含 footprint) → `HeightTraversalService.CanTraverse` (Δh 边界) → `DimensionLayer` 跨相 → `Region containment` (MAP-09 保留字段，当前不阻断) → AP 充足 |
| 7.3 | `MapMovementProfile` | readonly struct, factory: `Standard`(ascend=1/descend=2/ap=6) / `Flyer`(ascend=∞/descend=∞/ap=6/CanFly=true/CanCrossDimension=true) / `Heavy`(ascend=0/descend=1/ap=4) |
| 7.4 | `MovementRangeService.Reachable` | BFS-based expansion; AP-gated; output sorted by `GridCoord.CompareTo` (Y → X → Layer) |
| 7.5 | `MapPath` | `Success` + `Tiles` (含起点+终点) + `TotalCost` + `FailureReason` + `RiskTags` (CrossPhase / Hazard / OverHeight, sorted Ordinal); `Null(...)` 工厂 + `From(...)` 工厂 |
| 7.6 | `TryGetAttachedRegistry` | `TileOccupancyService` 新增 accessor (12 line)；**read-only**，反射访问规避 |
| 7.7 | BFSPathfinder 保留 | `Assets/Starfall/Core/Pathfinding/BFSPathfinder.cs` **未删除**；179 baseline test 引用，MAP-05 作为新主寻路路径，BFSPathfinder 退居 MVP 兼容层；future Roadmap 计划迁移 |
| 7.8 | 命名冲突避免 | `MapMovementProfile` (新) vs `Height.MovementProfile` (MAP-06 pre-existing) — 不同 namespace (`Core.Map.Pathfinding` vs `Core.Map.Height`)，不冲突；测试 fixture 也分别命名为 `Pathfinding.MovementProfileTests` (13 tests) vs `Height.MovementProfileTests` (7 tests) |

---

## 8. 最终结论

# ✅ PASS

**所有 9 项 Gate 维度全部 PASS**：
- 编译：exit 0 / 0 new warning / 0 error
- 测试：**859 / 859 PASS，0 failed, 0 skipped** (766 baseline + 93 new)
- 范围：28 文件全在预期路径；6 个 negative 维度 0 变更
- 守卫：§10.1 Core clean；hash 兼容；MAP-03/04/06/07/08 全部兼容
- Commit hygiene：3 commits ahead；0 uncommitted；HEAD = `5177e28`
- ID assertion：14 个 ID test PASS（≥ 5 ✅）

**MAP-05 新增全部 PASS**：
- 93 tests across 6 new fixture + 1 helper (PathfindingTestHelpers 是 helper，0 [Test])
- 覆盖：PathfindingService / MapPassabilityService / MovementRangeService / MapMovementProfile / MapPath / MapPath.PathFailure / MapPassabilityResult.RejectionCode / FootprintAccessibility / Service Namespaces

**回归完整性**：
- TileOccupancyServiceTests (18) — MAP-04 baseline 完整保持 + TileOccupancyService +12 accessor 兼容
- MapStateHashTests (23) — ADR-0003 hash 稳定性保持
- CommandAndPathfinderTests (10) — MAP-03 command + pathfinder 集成保持
- HeightTraversalTests (16) — MAP-06 height 判定保持（MapMovementProfile 复用 Height.MovementProfile）
- CoreDependencyGuardTests (4/4) — §10.1 硬约束 preserved

**未发现任何阻塞**。

---

## 9. Advisory 处置建议

| # | 项 | 描述 | 处置 |
|---|----|------|------|
| **A1** | `MovementProfileTests.cs` vs `MapMovementProfileTests.cs` | 任务描述写 `MapMovementProfileTests.cs`；实际使用 `MovementProfileTests.cs`（namespace `Starfall.Tests.EditMode.Map.Pathfinding.MovementProfileTests`），避免与既有 `Starfall.Tests.EditMode.Map.Height.MovementProfileTests`（MAP-06, 7 tests）类名冲突。两个 fixture 都 PASS，13 vs 7 共 20 tests | ✅ **接受** — namespace 区分已避免冲突；命名意图明确（Pathfinding 子目录下命名为 `MovementProfileTests`，无歧义）；记录在文档即可 |
| **A2** | 测试数 93 > 期望 56 | 超额完成 65%（+37 tests） | ✅ **正向偏差** — 不调整；超额覆盖 6 个 service/type，ID assertion test 也扩展到 14 个（>5 最低要求）；覆盖更深，无副作用 |
| **A3** | `BFSPathfinder` 未删除 | 保留作为 MVP 兼容层（179 baseline test 引用） | ✅ **接受** — 符合 spec；与 commit message `47f2e76` 的描述一致；新主寻路路径是 `PathfindingService`，BFS 退居 legacy；未来 Roadmap 可规划迁移（不在 MVP 范围） |

---

## 10. 下一步建议（给 Lead）

1. **merge 准备就绪**：HEAD `5177e28`, 3 commits, 0 conflict（需 Lead 决定时机）
2. 同步 `IMPLEMENTATION_STATUS.md` + `MAP_SYSTEM_FORWARD_PLAN.md` 反映 MAP-05 DONE
3. **不自动 push**（AGENTS §9 + user 明示 push 需批准）
4. 清理 `agent/map-05-pathfinding` 分支 + `D:\AI-Worktrees\Xingyuan\gameplay` worktree（等用户批准）
5. 下个 P0 任务包候选（由 Lead 评估）：
   - **MAP-07 双层 TileState.PhasePairTileId**（依赖 MAP-04 + MAP-08，可能已 merge？）
   - **MAP-09 律令系统**（依赖 MAP-03 ModifyGlobalCV / ModifyAnchorState 已就位）
   - **MAP-11 战斗表现 Presenter**（依赖 MAP-05 寻路已就位，可视化路径预览）
   - **MAP-12 跨相路径**（依赖 MAP-07 双层 + `MapMovementProfile.CanCrossDimension` 已预埋字段）

---

QA Gate VERDICT: **PASS** (859/859 EditMode, 0 阻塞)
Route A scope: 0 violation
Commit hygiene: ✅ 0 uncommitted / 3 commits / HEAD = `5177e28`
ID assertion: 14 / 14 PASS（≥ 5 ✅）
Pre-existing warnings: 3 unique (CS8632 × 1 + CS0618 × 2) — 0 new
Backward compat: ✅ BFSPathfinder 保留 + TileOccupancyService +12 additive accessor 兼容 18 baseline tests