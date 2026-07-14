# MAP SYSTEM FORWARD PLAN · MVP "断裂点三号" M5+

> Lead：`xingyuan-lead`（2026-07-14 09:32 GMT+8 起生效）
> 输入：[`.incoming/doc1-core-systems.txt`](../.incoming/doc1-core-systems.txt) + [`.incoming/doc2-map-dev-plan.txt`](../.incoming/doc2-map-dev-plan.txt)（已由 xingyuan-architect 吸收为 [Docs/MAP_SYSTEM_AUDIT.md](MAP_SYSTEM_AUDIT.md)）
> 路线：**Route A 增量升级**（保留 4 程序集 + `GridPos` / `BoardState` 命名 + `Assets/Starfall/Core/Map/` 新增子目录）
> 状态（2026-07-14 11:48 GMT+8）：**MAP-01 + MAP-02 已上线 main HEAD `25e035b`**；ADR-0003 Status:**Accepted**；qa Gate PASS；本计划文档已与该状态同步。

## 1. 已批准决策

| # | 决策项 | 选择 | 落地时间 | 提交 |
|---|---|---|---|---|
| 1 | 路线（route A / B / C） | **A 增量升级** | 2026-07-14 | 本文档 |
| 2 | Unity 版本 | 6000.5.3f1（已锁） | 用户 2026-07-14 09:15 批准 | — |
| 3 | `GridPos` 保留 / 重命名 | **保留** | 路线 A | — |
| 4 | `MapState` 独立 / 嵌入 | **嵌入 `BattleState` + 适配器** | route A | — |
| 5 | BFSPathfinder 邻居顺序修复 | **已批准** | 2026-07-13 完成 | `5cc4644` |
| 6 | Undo RestoreState 修复 | **已批准** | 2026-07-13 完成 | `617e332` |

## 2. P0 任务包完成情况

| 包 | 内容 | 状态 | HEAD |
|---|---|---|---|
| map-00-bugfix-bfs-neighbor-order | BFSPathfinder N→E→S→W | ✅ | `5cc4644` |
| map-00-unblock-undo-restore-state | BattleRunner.RestoreState + Undo 集成测试 | ✅ | `617e332` |
| map-01-grid-foundation | GridCoord / DimensionLayer / GridMap<T> / GridDirection / MapSize（61 测试） | ✅ | `1738269` |
| **map-02-map-state** | MapDefinition + MapState + MapStateCloner + MapStateHasher + MapRegion/MapObjectInstance POCOs + BattleState/Cloner 集成 + ADR-0003 + 3 套 45 测试 | ✅ | `25e035b` |
| map-06-line-of-sight | LineOfSightService / CoverQueryService / HeightTraversalService | ⏳ P0 候补 | — |
| map-08-phase-flip | FlipTilePhaseCommand / FallResolutionService / PhaseCompressionResolutionService | ⏳ P0 候补（核心玩法） | — |

## 3. 下一轮派活范围（待用户回执后立刻 spawn）

### 3.1 任务包 ID

`agent/map-02-map-state`

### 3.2 范围（严格 route A）

**新增文件**（`Assets/Starfall/Core/Map/State/`）：

| 文件 | 内容 |
|---|---|
| `MapDefinition.cs` | `readonly struct MapDefinition`：不可变配置（`MapId` / `Size` / `InitialActiveLayer` / `InitialGlobalCollapseValue` / `TilesetId` / `EnvironmentScheduleId`），不持可变集合。 |
| `MapState.cs` | `class MapState`：运行时唯一真相源（`MapDefinition Definition` / `int Version` / `ulong PostStateHash` / `IReadOnlyList<GridCoord> Tiles` / `IReadOnlyList<AnchorZone> Anchors` / `IReadOnlyList<MapRegion> Regions` / `IReadOnlyList<MapObjectInstance> Objects` / `int GlobalCollapseValue`）；本步只建容器，不接 Rule。|
| `MapStateCloner.cs` | `static class MapStateCloner`：纯函数 `DeepClone(MapState)`；所有集合彻底独立；记录修改痕迹。 |
| `MapStateHasher.cs` | `static class MapStateHasher`：`ulong CalculateDeterministicHash(MapState)`；FNV-1a 64 位 + 类型标记 + 长度前缀 + 集合先排序再写入；逐字段类型：MapId / Size / Version / ActiveLayer / GlobalCollapseValue / Tiles（按 `GridCoord.CompareTo` 排序）/ Anchors（按规范化顶点序列）/ Regions（按 RegionId）/ Objects（按 ObjectId）。 |

**新增测试**（`Assets/Starfall/Tests/EditMode/Map/State/`）：

| 文件 | 数量 |
|---|---|
| `MapStateCloneTests.cs` | ≥ 10（克隆前后相等 / 引用不同 / 修改克隆不修改原 / 集合彻底独立 / 多层切片 / 空 MapState / 包含锚点的 MapState） |
| `MapStateHashTests.cs` | ≥ 10（空 MapState 哈希稳定 / 字段差异 / 集合不同插入顺序同 hash / 同字段多字段差异 / 跨运行一致 / 修改任何字段哈希变化） |
| `MapStateMutationIsolationTests.cs` | ≥ 5（修改 Tile 集合 / Anchor 集合 / Region 集合 / Object 集合 / 嵌套结构） |
| **合计** | **≥ 25 新测试** |

**编辑现有**：

- `Assets/Starfall/Core/Model/BattleState.cs`：新增 `MapState MapState { get; }` 属性 + 构造期初始化（不破坏现有 179 测试，向后兼容）。
- `Assets/Starfall/Core/Model/Cloner.cs`：`BattleStateCloner.Clone` 增加 `MapState.DeepClone()` 调用并验证 `MapState` 字段被复制（详见 audit §6.5 #3）。

**新增文档**：

- `docs/ADR/ADR-0003-map-state-hash.md`：明确 hash 字段集合 + 稳定排序规范 + 与现有 `BattleState.PostStateHash` 的字段划分。

### 3.3 不在范围（防止提前实现）

- 不实现 `IMapCommand` / `MapCommandExecutor`（MAP-03）
- 不实现地形 / 占用 / `TileDefinition` / 22 标签（MAP-04）
- 不实现 A\* 寻路 / `MovementRangeService`（MAP-05）
- 不实现视线 / 掩体 / 高度（MAP-06）
- 不接 `TileState.PhasePairTileId` 双层配对（MAP-07）
- 不接 `FlipTilePhaseCommand` / 坠落 / 挤压（MAP-08）
- 不接 Unity 表现层（`MapView` / `TileView` / `MapHighlightView` 等，MAP-14）
- 不接 JSON `MapDefinitionRepository` / `MapValidationService`（MAP-13）
- 不接 `MAP_DEV_PHASE_TEST_001`（MAP-17）
- 不接 `Starfall.Editor` 新程序集（MAP-16）
- 不写性能基准（MAP-18）

### 3.4 验收标准（**MAP-02 已达成：actual vs target**）

| 项 | Target | Actual (PASS) | 证据 |
|---|---|---|---|
| EditMode 测试总数 | ≥ 272 | **294** | `Logs/qa-editmode-results.xml` + `Logs/editmode-map-02-results.xml` |
| Failed | 0 | **0** | 同上 |
| `CoreDependencyGuardTests` | 4/4 PASS | **4/4 PASS** | qa Gate 独立核对 |
| `BattleStateCloner`-using tests 继续 PASS | 全部 | **3 个在 `FoundationStateTests` + 1 个在 `LevelLoopTests` PASS** | 注意：qa Gate advisory #4 |
| `MapStateHasher.CalculateDeterministicHash ×100` 一致 | PASS | **PASS**（`Hash_IsStable_Over100Runs`，0.000385 s） | qa Gate |
| `Assets/Starfall/Unity/*` 修改 | 0 | **0** | qa Gate `git diff main..HEAD --stat` 检查 |
| `Packages/manifest.json` / `ProjectSettings/*` 修改 | 0 | **0** | qa Gate |
| 分支基于最新 main（HEAD ≥ `1738269`） | YES | **YES**（gameplay 创 branch HEAD `1738269`，qa 合后 base same） | git log |
| 编译 warnings | 0 new | **0 new**（main pre-existing 3 个：CS8632 × 1, CS0618 × 2） | qa Gate `Logs/qa-compile.log` |
| Build log + test results xml 路径 | 提供 | game：`Logs/compile-map-02.log` + `Logs/editmode-map-02-results.xml`；qa：`Logs/qa-compile.log` + `Logs/qa-editmode-results.xml` | 均 on `agent/qa-map-02-gate @ 5365adf` |
| `using UnityEngine` 在 `Assets/Starfall/Core/Map/State/` | 0 | **0** | qa Gate grep |
| `BattleState.PostStateHash` 公共签名 | 不变 | **不变** | gameplay + qa 都 grep |
| `BattleStateCloner.Clone` 调 `MapStateCloner.DeepClone` | YES | **YES**（`Assets/Starfall/Core/Model/Cloner.cs` ~L32） | qa Gate |
| `MapStateHasher` 字段编码 | type-tag + length-prefix | **YES** + UTF-8 + FNV-1a 64 + stable sort | [ADR-0003](../ADR/ADR-0003-map-state-hash.md) |

#### 已知遗留（qa advisory）

- **#4 "14 BattleStateClonerTests" in §3.4** — 本文档最初将"§3.4 'all existing 14 BattleStateCloner-related tests'" 误写为 14，**实际为：**
  - **`main` 上的 `BattleStateCloner.Clone`-using 测试**：3 个 in `FoundationStateTests`（`Cloner_DeepCopy_IndependentOfSource`、`Cloner_DoesNotShareUnitReferences`、`Comparer_Equals_TrueForClones`）+ 1 个 in `LevelLoopTests` → 共 **4 个，全部 PASS**
  - **未合到 main 的额外 14 个 `BattleStateClonerTests`**：位于 unmerged 分支 `agent/map-00-fix-battle-state-cloner`（不在 MAP-02 范围；用户 2026-07-14 12:38 GMT+8 明确不立单任务）
  - 修正：§3.4 上文写"旧 14 个 `BattleStateClonerTests` 继续 PASS"为误导。实际仅上表 4 个 main-on-tests 通过。如果用户后续决定合并 `agent/map-00-fix-battle-state-cloner`，那 14 个会补充回归覆盖。

### 3.5 委派矩阵

| Agent | 角色 | 工作区 | 输出 |
|---|---|---|---|
| `xingyuan-gameplay` | **主实现** | `D:\AI-Worktrees\Xingyuan\gameplay`（已存在） | 上面所有 .cs + .cs.meta + 测试文件 + build/test log；分支 `agent/map-02-map-state` |
| `xingyuan-architect` | **ADR 作者** | `D:\AI-Worktrees\Xingyuan\architect`（需自建 worktree） | `docs/ADR/ADR-0003-map-state-hash.md`，分支 `agent/adr-0003-map-state-hash` |
| `xingyuan-qa` | **Gate 验证** | `D:\AI-Worktrees\Xingyuan\qa`（需自建 worktree） | `Logs/map-02-qa-report.md` + 独立运行 EditMode 的截图/日志 |

### 3.6 时间盒与提交策略

- Lead spawn 后：`session_yield` 等完成事件（避免轮询）。
- 期望时序：3–5 小时（worktree 自建 + ADR 1h + 实现 2h + 测试 1h + 合并 0.5h）。
- 提交粒度：
  1. `chore(map-02): create Map/State/ directory + .meta`
  2. `feat(map): MapDefinition readonly struct`
  3. `feat(map): MapState runtime container`
  4. `feat(map): MapStateCloner deep copy`
  5. `feat(map): MapStateHasher deterministic FNV-1a`
  6. `test(map): MapStateCloneTests 10`
  7. `test(map): MapStateHashTests 10`
  8. `test(map): MapStateMutationIsolationTests 5`
  9. `chore(map): attach Core guard tests to new files`
  10. `merge: agent/map-02-map-state into main`（必须由 Lead 在 QA Gate 通过 + 用户批准后执行）

### 3.7 禁止事项

- 不写 Unity 表现（`Unity/` 目录本轮零修改）
- 不写超出 route A 范围的全局重构（如把 `BattleState` 全部重构为 `MapState` 嵌入模型）
- 不动 `Packages/manifest.json` / `ProjectSettings/*` / `Memory/`
- 不 Push、不发 PR、不合并 main — Lead 等 QA 通过 + 用户批准再合并
- 不接任何 doc2 §12 阶段的 16 个地图 Command（MAP-03 是下一轮）

## 4. 待用户裁决的事项

> MAP-02 已完成（main HEAD `25e035b`），下面 Q1-Q5 等用户裁决后再启动 MAP-06 / MAP-08 下一轮。

| # | 决策 | Lead 默认假设 | 备注 |
|---|---|---|---|
| Q1 | 是否在 MAP-02 完成后立即启动 **MAP-06 LOS**（LineOfSightService + 掩体 + 高度，~2-3 天） | 否，每次一个任务包；MAP-02 完成后再问 | 与路线 A 一致 |
| Q2 | `MAP_DEV_PHASE_TEST_001`（12×14 双层）何时启动 | P2（route A 路线），等 MAP-17 阶段 | — |
| Q3 | `agent/map-00-fix-battle-state-cloner`（14 BattleStateClonerTests）是否立单任务 | **用户 2026-07-14 12:38 GMT+8 明确不立**；保留为 unmerged 分支 | qa advisory #4 描述 |
| Q4 | `MAP-08 相位翻转 + 坠落 + 实体挤压`（核心玩法，最高优先级）何时启动 | 待 Q1-Q3 决议后由 Lead 提议 | audit §6.1 P0 |
| Q5 | 推送到 origin/main 是否分批（先 MAP-02 部分、再下个 MAP 部分） | 一次性 16 commit push（含历史 unmerged 部分） | AGENTS §9 push 需批准 |
| Q3 | `battle_default.json` 是否同时新增 12×14 双层测试地图 | 否，先加不破坏现有 8×10 | — |
| Q4 | `MVPPlayModeHelper.cs` 何时归入新 `Starfall.Editor` 程序集 | 与 MAP-16（路线编辑器）一起 | — |
| Q5 | ADR-0003 由 architect 还是 gameplay 写 | architect（spec/标准文档） | — |

## 5. 文档交叉引用

- 完整审计：[Docs/MAP_SYSTEM_AUDIT.md](MAP_SYSTEM_AUDIT.md)（xingyuan-architect 2026-07-14 01:26）
- 原始输入：[.incoming/doc1-core-systems.txt](../.incoming/doc1-core-systems.txt) + [.incoming/doc2-map-dev-plan.txt](../.incoming/doc2-map-dev-plan.txt)
- 当前实现状态：[Docs/IMPLEMENTATION_STATUS.md](IMPLEMENTATION_STATUS.md) §4.1 已同步
- MVP 路线图：[Docs/04_Roadmap_and_Milestones.md](../Docs/04_Roadmap_and_Milestones.md)
- 已知限制：[Docs/KNOWN_LIMITATIONS.md](KNOWN_LIMITATIONS.md)
- 总开发规则：[AGENTS.md](../AGENTS.md)

---

**C 完成**：`.incoming/` 两份 doc 已被 architect 吸收并落地为 `MAP_SYSTEM_AUDIT.md`；本计划文档作为 Lead 的采纳纪要；接着请批准 §3 派活。
