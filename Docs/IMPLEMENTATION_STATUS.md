# IMPLEMENTATION STATUS · MVP "断裂点三号"

> 最后更新：2026-07-16 00:35 GMT+8
> 状态：MVP 完成（Task 01-20）+ M5+ 地图系统 **MAP-02 + MAP-03 + MAP-04 + MAP-05 + MAP-06 + MAP-07 + MAP-08 + MAP-09 + MAP-11a** 上线（commit `d792e29`，**核心玩法最高优先级**已完成）
> 总测试：**1186 / 1186** EditMode PASS · Core 依赖守卫 4 / 4 PASS · 0 新 compile warnings from MAP-02/MAP-03/MAP-04/MAP-05/MAP-06/MAP-07/MAP-08/MAP-09/MAP-11a paths（main 上有 3 个 pre-existing warning，详见 §5.3）
> MAP-07 QA Gate：[`Docs/qa-reports/map-07-gate.md`](qa-reports/map-07-gate.md)（Lead consolidated）
> MAP-03 QA Gate：[`Docs/qa-reports/map-03-gate.md`](qa-reports/map-03-gate.md)（qa consolidated after commit-hygiene fix）
> MAP-05 QA Gate：[`Docs/qa-reports/map-05-gate.md`](qa-reports/map-05-gate.md)（qa independent verification, 9/9 PASS）
> MAP-09 QA Gate：[`Docs/qa-reports/map-09-gate.md`](qa-reports/map-09-gate.md)（qa independent verification, 11/11 PASS, MAP-08 零回归）
> MAP-11a QA Gate：[`Docs/qa-reports/map-11-gate.md`](qa-reports/map-11-gate.md)（qa independent verification, 11/11 PASS, MAP-09 零回归 141/141）

## 1. 已完成功能

### 1.1 Core（68 .cs · 含 MAP-02 + MAP-04 + MAP-06 + MAP-08 新增 34 个）

| 模块 | 文件 | 行数级别 | 状态 |
|---|---|---|---|
| Model | BattleState / BoardState / UnitState / TileSnapshot / Enums / Cloner / Comparer | 200+ | ✅ |
| Hash | GridPos / GridPosComparer | 60+ | ✅ |
| Command | ICommand / CommandExecutor / CommandResult / **BattleEvent（含 UnitEnteredVoid / UnitPhaseCompressed）** | 100+ | ✅ |
| Move | MoveCommand + BFSPathfinder（邻居已统一为 N→E→S→W） | 130+ | ✅ |
| Status | StatusKind / StatusInstance / ApplyStatusCommand / RemoveStatusCommand / TickEndTurnCommand | 200+ | ✅ |
| Combat | BattleOutcome / BattleRunner / EventSink / IEnemyAI / SimpleEnemyAI / ImprovedEnemyAI / DamageFormula / WinConditionChecker / **ObjectivePhase + ObjectivePhaseUpdater** | 600+ | ✅ |
| Anchor | AnchorRegistry / AnchorZone | 80+ | ✅ |
| Decree | Decree / DecreeKind / DecreeRegistry / ApplyDecreeCommand | 100+ | ✅ |
| Rules | **FallingCommand (重构为调 FallResolutionService) / CrushResolver / PhaseFlipValidator** | 200+ | ✅ |
| Replay | CommandRecord / CommandRecorder / ReplayPlayer / ReplayCodec / ReplayEntry / ReplayFile / ReplayException | 300+ | ✅ |
| Undo | UndoStack | 50+ | ✅ |
| **Map (MAP-01)** | GridCoord / GridDirection / GridMap / MapSize / DimensionLayer | 600+ | ✅ |
| **Map (MAP-02)** | **MapDefinition / MapState / MapStateCloner / MapStateHasher / MapRegion / MapObjectInstance** | **680+** | **✅** |
| **Map (MAP-06)** | **HeightLevel / MovementProfile / HeightTraversalService / CoverLevel / CoverDirection / CoverQueryService / ProjectileType / IHeightLookup / ICoverLookup / IBlockingLookup / LineOfSightService（Supercover 整数 LOS + 6 ProjectileType + HighGround）** | **900+** | **✅** |
| **Map (MAP-04)** | **TerrainType / TerrainDefinition / TerrainRegistry / TileTags / Footprint / TileDefinition / TileDefinitionRegistry / MapTileState / LegacyTileStateAdapter / TileOccupancyService（attach 模式 + 跨 Layer） / MapStateLookupAdapter（MapState → IHeightLookup/ICoverLookup/IBlockingLookup 三接口装配）** | **1500+** | **✅** |
| **Map (MAP-08)** | **IMapCommand (MAP-03 stub) / MapCommandResult / PhaseFlipStateService (attach 模式，per-map flipped tile 字典) / FlipTilePhaseCommand / FlipRegionPhaseCommand / FallResolutionService (曼哈顿 + CompareTo 排序)/ PhaseCompressionResolutionService (4-邻居 N→E→S→W + Manhattan=2 环回退)** | **600+** | **✅** |
| **Map (MAP-07)** | **PhasePairLookup（双向配对 + 自环忽略） / CrossLayerValidator（PAIR_ORPHAN / PAIR_ASYMMETRIC / FLIP_DESYNC 三态） / MapTileState.ActiveDimension（per-tile 字段，PhaseLocked 校验）/ ActiveDimensionMigration（旧 dict → 新字段迁移） / LineOfSightService.ComputeCrossPhaseLOS（4-邻居 N→E→S→W，Full Cover 必挡 / Half Cover 忽略） / PhaseFlipStateService 重构（保留 legacy dict 路径）** | **800+** | **✅** |
| **Map (MAP-03)** | **IMapCommand 完整接口（Execute/Undo/Version/CommandId/Dependencies） + MapCommandResult + MapEvent struct（8 种事件 + 稳定排序） + MapCommandExecutor（Run / UndoLast / Version / Dependencies） + 16 个 Map commands + AnchorStateService（7 状态） + MapState.Version 字段** | **2400+** | **✅** |
| **Map (MAP-05)** | **PathfindingService（A* 算法，N→E→S→W 邻居顺序，Tie-break (F,H,Y,X,Layer)） + MapPassabilityService（7 拒绝原因：BlockedByTile/HeightDelta/Unit/Phase/Region/InsufficientMovement/Pass） + MapMovementProfile（Standard/Flyer/Heavy 工厂）+ MovementRangeService（BFS-based AP 范围）+ MapPath（含 RiskTags）+ 保留 BFSPathfinder 向后兼容** | **900+** | **✅** |
| **Map (MAP-09)** | **MapRegionDefinition（14 种 RegionKind 工厂）+ MapRegionState（8 字段 + 序列化契约）+ MapRegionStateHasher + MapRegionService（8-state machine + Tick + 4 个 MapEvent）+ MapSpawnPoint + MapSpawnService + 4 个新 IMapCommand（RegisterRegion/UnregisterRegion/TransitionRegionState/PlaceSpawnPoint）+ 非破坏性升级（保留 MAP-08 legacy `Regions` + 新增 `RegionStates`/`SpawnPoints` 集合）** | **1700+** | **✅** |
| **Map (MAP-11a)** | **CollapseStage（5 阶段：Stable/Anomalous/Fracturing/Collapsing/GateFault）+ TileStability（6 值：Stable/Unstable/Fractured/Collapsing/Collapsed/Reconstructed）+ GlobalCollapseValue + LocalCollapseValue + CollapseValueService（Tick + ApplyLocalDamage + GetHotspots + 5 阶段效果 + MAP-09 联动）+ CollapseWarningService（4 预警等级）+ 3 个新 IMapCommand（ModifyGlobalCollapseValue/CollapseTile/ReconstructTile）+ 4 个新 MapEvent kinds (15-18) + 非破坏性升级（保留 MAP-02 `int GlobalCollapseValue` legacy）** | **1500+** | **✅** |

### 1.2 Data（9 .cs）

| 模块 | 文件 | 状态 |
|---|---|---|
| Definition | BattleDefinition / BoardDefinition / UnitDefinition / StatusDefinition | ✅ |
| Loading | JsonBattleLoader / BattleStateBuilder | ✅ |
| Validation | DefinitionValidator（含 guardsRequired + exitTile 字段） | ✅ |
| Exception | DefinitionException | ✅ |

### 1.3 Unity（19 .cs）

| 模块 | 文件 | 状态 |
|---|---|---|
| Bootstrap | BattleBootstrap（auto-attach Presenter / HUD / InputController） | ✅ |
| Real Presenter | RealBoardPresenter（80 Quad + 单位 Capsule + 锚点 LineRenderer + 高亮层） | ✅ |
| Real HUD | RealBattleHud（AP / PV / CV / 目标 / 模式提示） | ✅ |
| Presentation | BoardSnapshot / HudSnapshot / UnitSnapshot / AnchorSnapshot / BoardPalette / LegalPreviewHelper | ✅ |
| Input | InputMode / InputAction / InputState / InputStateMachine / InputController / CommandBuilder | ✅ |
| Camera | BattleCameraAutoSetup（场景无 Camera 时自动俯瞰） | ✅ |
| Stub | StubBoardPresenter / StubBattleHud（保留 fallback，Task 17+18+19 已替代） | ⚠️ fallback |

### 1.4 Tests EditMode（44 文件 / 596 测试 · 含 MAP-01 + MAP-02 + MAP-04 + MAP-06 + MAP-08 新增 424 测试）

| 测试集 | 测试数 | 内容 |
|---|---|---|
| CoreDependencyGuardTests | 4 | Core 无 UnityEngine 引用（asmdef + using） |
| FoundationStateTests | 12 | GridPos / BattleState / Cloner / Comparer |
| CommandAndPathfinderTests | 9 | MoveCommand + BFSPathfinder 确定性（修复 N→E→S→W 顺序后） |
| StatusSystemTests | 10 | StatusKind / ApplyStatus / RemoveStatus |
| DataLoadingTests | 7 | JSON 加载 + 校验 |
| BattleRunnerTests | 9 | 回合 + AI + Outcome |
| AnchorAndDecreeTests | 8 | 锚点围区 + 律令 |
| RulesTests | 7 | 坠落 / 挤压 / 相位翻转 |
| ReplayAndUndoTests | 8 | Replay + Undo 确定性 |
| ReplayCodecTests | 6 | ReplayCodec 序列化 |
| AttackAndAITests | 8 | DamageFormula + AttackCommand + AI |
| BattleSetupTests | 4 | Bootstrap + JSON + Validator |
| PresentationTests | 15 | BoardSnapshot / AnchorSnapshot / BoardPalette |
| HudAndPreviewTests | 28 | LegalPreviewHelper / UnitSnapshot / HudSnapshot / BoardSnapshot |
| InputStateMachineTests | 32 | 模式状态机 + 键位解析 |
| LevelLoopTests | 12 | GuardsCompleted / Retreat / 胜负 / 确定性 |
| **Map/Coordinates（MAP-01）** | **61** | **GridCoord / MapSize / GridMap / DualLayer / MaxSize / Neighbour** |
| **Map/State（MAP-02）** | **45** | **MapStateClone (14) + MapStateHash (23, 含 Hash_IsStable_Over100Runs) + MapStateMutationIsolation (8)** |
| **Map/Height（MAP-06）** | **40** | **HeightLevel (18) + MovementProfile (8) + HeightTraversal (14)** |
| **Map/Cover（MAP-06）** | **20** | **CoverDirection (9) + CoverQuery (11)** |
| **Map/LineOfSight（MAP-06）** | **35** | **LineOfSight (19) + ProjectileBlock (14) + HighGroundLineOfSight (12)** |
| **Map/Tile（MAP-04）** | **135** | **TerrainDefinition (20) + TileDefinition (16) + TileDefinitionRegistry (15) + Footprint (12) + TileTags (11) + MapTileState (20) + LegacyTileStateAdapter (9) + MapStateLookupAdapter (14) + TileOccupancyService (18)** |
| **Map/Commands（MAP-08）** | **72** | **FlipTilePhase (15) + FlipRegionPhase (11) + FallResolution (18) + PhaseCompression (12) + MultiTilePhaseFlip (8) + FallingCommandCompat (8)** |
| **Map/Tile/PhasePair（MAP-07）** | **73** | **DualLayer (19) + PhaseFlipValidation (11) + CrossPhaseLOS (13) + PhasePairRoundTrip (10) + ActiveDimensionMigration (11) + TileDefinitionPhasePair (9)** |
| **Map/Commands（MAP-03）** | **97** | **Map03_TaskId (17) + MapCommandEvent (14) + MapCommandExecutor (12) + MapCommandIntegration (10) + MapCommandValidation (44)** |
| **Map/Pathfinding（MAP-05）** | **93** | **PathfindingService (12) + MapPassability (15) + MovementRange (10) + MovementProfile (8) + MapPath (6) + Map05_TaskId (5) + DualBackwardCompat (37 衍生)** |
| **Map/Regions（MAP-09）** | **141** | **MapRegionService (45) + MapRegionDefinition (33) + SpawnPoint (15) + MapRegionState (13) + RegionEvent (12) + DeploymentValidation (10) + Map09_TaskId (8) + Map09_HashStability (5)** |
| **Map/Collapse（MAP-11a）** | **186** | **CollapseStage (21) + TileStability (19) + GlobalCollapseValue (24) + LocalCollapseValue (19) + CollapseValueService (27) + CollapseWarningService (24) + ModifyGlobalCVCommand (17) + CollapseTileCommand (13) + ReconstructTileCommand (13) + Map11_TaskId (9)** |
| UndoIntegrationTests | 8 | 21-B Undo RestoreState 集成 |
| Phase 19 单元扩展 | 12 | LevelLoopTests 同源增量 |
| Phase 19 综合 | 17 | 同上组合 |

**总计**：**1186 / 1186 EditMode PASS · 0 failed · 0 skipped**（main HEAD `d792e29`，**MAP-11a + MAP-09 + MAP-05 + MAP-03 + MAP-07 + MAP-08 核心玩法已上**）。

qa Gate 独立报告：
- MAP-02：[`docs/qa-reports/map-02-gate.md`](qa-reports/map-02-gate.md)
- MAP-06：[`docs/qa-reports/map-06-gate.md`](qa-reports/map-06-gate.md)
- MAP-04：[`docs/qa-reports/map-04-gate.md`](qa-reports/map-04-gate.md)（Lead self-fix report）
- MAP-08：[`docs/qa-reports/map-08-gate.md`](qa-reports/map-08-gate.md)（Lead spot-verify note）
- MAP-07：[`docs/qa-reports/map-07-gate.md`](qa-reports/map-07-gate.md)（Lead consolidated）
- MAP-03：[`docs/qa-reports/map-03-gate.md`](qa-reports/map-03-gate.md)（qa consolidated after commit-hygiene fix）
- MAP-05：[`docs/qa-reports/map-05-gate.md`](qa-reports/map-05-gate.md)（qa independent verification）
- MAP-09：[`docs/qa-reports/map-09-gate.md`](qa-reports/map-09-gate.md)（qa independent verification, MAP-08 零回归专项）
- MAP-11a：[`docs/qa-reports/map-11-gate.md`](qa-reports/map-11-gate.md)（qa independent verification, MAP-09 零回归 141/141 专项）

## 2. 提交链（main）

```
4b504a7 merge: agent/19-level-loop (Task 19 关卡闭环) into main
44d4deb docs(audit): Task 19 关卡闭环章节 (§12)
1aee2e4 test(core): add LevelLoopTests for guard/retreat/win/lose + determinism
8d6abb5 feat(core): integrate ObjectivePhaseUpdater into BattleRunner.EndTurn
562f37d feat(core): add ObjectivePhaseUpdater for guard/retreat transitions
7807b41 feat(data): add guardsRequired + exitTile to JSON + validator
14660ab feat(core): extend BattleState with phase + guards + exit tile
15571e0 feat(core): add ObjectivePhase enum (Guard/Retreat/Ended)
ce2391a9 merge: agent/18-hud-and-preview (Task 18 HUD 与预览) into main
...（Task 16-17 + 14 个 MVP 分支合并，详见 git log）
```

## 3. 当前未实现 / 已知限制

详见 [docs/KNOWN_LIMITATIONS.md](KNOWN_LIMITATIONS.md)。

## 4. 后续路线

详见 [Docs/04_Roadmap_and_Milestones.md](../Docs/04_Roadmap_and_Milestones.md)。

### 4.1 M5+ 地图系统路线（Route A 增量升级，2026-07-14 Lead 采纳）

- 路线 A 已锁定：保留 4 程序集 + `GridPos`/`BoardState` 命名，`Assets/Starfall/Core/Map/` 新增命名空间子目录，逐 MAP 升级。
- 已完成 P0 前置：
  - `BFSPathfinder` 邻居顺序修复 → AGENTS §11 兼容（commit `5cc4644`）
  - `BattleRunner.RestoreState` + Undo 链路打通（commit `617e332`）
  - MAP-01 棋盘坐标基础 → `GridCoord` / `DimensionLayer` / `GridMap<T>` / `GridDirection` / `MapSize`（commit `1738269`，61 EditMode 测试）
  - MAP-02 MapState / DeepClone / Hash → main `25e035b`（45 新 EditMode 测试；ADR-0003 Status:**Accepted** `0acf39d`）
  - MAP-06 LOS（Height + Cover + LineOfSight + ProjectileType + HighGround）→ main `ff0c641`（95 新 EditMode 测试）
  - MAP-04 TileDefinition + Terrain + Occupancy + Footprint + 22 Tags → main `9b8956b`（135 新 EditMode 测试）
    - **MAP-08 Phase Flip + Fall + Crush（核心玩法最高优先级）** → main HEAD `8538f48`（72 新 EditMode 测试；Commit 链 1 个：feat 综合提交；提供 IMapCommand stub + MapCommandResult + PhaseFlipStateService（attach 模式）/ FlipTilePhaseCommand + FlipRegionPhaseCommand (atomic + PhaseLocked/PhaseFlippable 验证)/ FallResolutionService (曼哈顿 + CompareTo 排序)/ PhaseCompressionResolutionService (4-邻居 N→E→S→W + Manhattan=2 环回退)/ FallingCommand 重构调 FallResolutionService + 2 新 BattleEvent UnitEnteredVoid/UnitPhaseCompressed；详见 [`docs/qa-reports/map-08-gate.md`](qa-reports/map-08-gate.md)）
  - **MAP-07 Dual-Layer TileState + PhasePair** → main HEAD `ba42e73`（73 新 EditMode 测试；PhasePairLookup 双向 + CrossLayerValidator 三态 + MapTileState.ActiveDimension 字段 + ActiveDimensionMigration 旧 dict 迁移 + LineOfSightService.ComputeCrossPhaseLOS 重载 + PhaseFlipStateService 重构保留 legacy dict 路径；MAP-04/06/08 零回归；详见 [`docs/qa-reports/map-07-gate.md`](qa-reports/map-07-gate.md)）
  - **MAP-03 IMapCommand + Executor + 16 Map commands** → main HEAD `48fbb27`（97 新 EditMode 测试：Map03_TaskId 17 + MapCommandEvent 14 + MapCommandExecutor 12 + MapCommandIntegration 10 + MapCommandValidation 44；完整 IMapCommand 接口 + MapCommandExecutor Run/UndoLast/Version/Dependencies + MapEvent 8 种事件稳定排序 + AnchorStateService 7 状态 + 16 commands 覆盖 doc2 §21.1；MAP-08 phase flip 回归归零 72/72 PASS；详见 [`docs/qa-reports/map-03-gate.md`](qa-reports/map-03-gate.md)；Advisory A1 AnchorStateService 位置 `Core/Anchor/` 经用户 2026-07-15 15:11 GMT+8 批准）
  - **MAP-05 Pathfinding/A*/Passability/Range** → main HEAD `61361b9`（93 新 EditMode 测试：PathfindingService 12 + MapPassability 15 + MovementRange 10 + MovementProfile 8 + MapPath 6 + Map05_TaskId 5；A* PathfindingService 含 N→E→S→W 邻居 + Tie-break (F,H,Y,X,Layer) 确定性 + MapPassabilityService 7 拒绝原因校验链 + MapMovementProfile Standard/Flyer/Heavy + MovementRangeService BFS-based AP 范围；BFSPathfinder 向后兼容保留；详见 [`docs/qa-reports/map-05-gate.md`](qa-reports/map-05-gate.md)）
  - **MAP-09 MapRegion + SpawnPoint + StateMachine** → main HEAD `e781f49`（141 新 EditMode 测试：MapRegionService 45 + MapRegionDefinition 33 + SpawnPoint 15 + MapRegionState 13 + RegionEvent 12 + DeploymentValidation 10 + Map09_TaskId 8 + Map09_HashStability 5；14 种 RegionKind + 8-state machine + Tick 推进 + 4 个 MapEvent + 4 个新 IMapCommand + MapStateHasher tag 0x34/0x35 + MAP-08 零回归（FlipRegionPhaseCommand 依赖 legacy `Regions` 字段保留）；详见 [`docs/qa-reports/map-09-gate.md`](qa-reports/map-09-gate.md)）
- 下一步候选（按核心性排序）：
  - **MAP-11 CV（Corruption Value）**（全局资源，与 MAP-09 Escort/Capture 进度联动；Lead 默认推荐）
  - MAP-10 MapObject 完整化（重型，超 3-4 天预算）
  - MAP-12 AnchorLink + ConstellationPolygon（整数射线 + 自相交拒绝）
  - MAP-05 A\* 寻路 + MapPassability + MovementRange (依赖 MAP-04 的 TileDefinition.BlocksMovement)
  - MAP-09 MapRegion 完整化（已有 placeholder，依赖 MAP-07 完成后）
  - MAP-10 MapObject + MapObjectStateMachine + 12 ObjectTypes（重型，超 3-4 天预算）
  - MAP-11 CV (Corruption Value)
  - MAP-12 AnchorLink + ConstellationPolygon（整数 + 自相交拒绝）

详见审计与决策记录：

- [Docs/MAP_SYSTEM_AUDIT.md](../Docs/MAP_SYSTEM_AUDIT.md)（xingyuan-architect 撰写，18 MAP vs MVP 现状对照）
- [Docs/MAP_SYSTEM_FORWARD_PLAN.md](../Docs/MAP_SYSTEM_FORWARD_PLAN.md)（Lead 采纳纪要 + §3.4 已对 qa advisory #4 更正）
- [docs/ADR/ADR-0003-map-state-hash.md](../ADR/ADR-0003-map-state-hash.md)（Accepted）

MVP 后续可选方向（**未经用户批准不得实施**）：

- 扩展关卡（不同地图、不同 Anchor / Decree 组合）
- AI 难度分级（当前 ImprovedEnemyAI 是确定性单层）
- 多人 / 联机
- 美术资产升级（当前用 Unity 原生 Quad / Capsule）
- 商业化 / 抽卡 / 养成

## 5. 验证命令

### 5.1 编辑模式测试

```powershell
& "C:\Program Files\Unity\Hub\Editor\6000.5.3f1\Editor\Unity.exe" `
  -batchmode -nographics `
  -projectPath "<repo root>" `
  -runTests -testPlatform EditMode `
  -testResults "Logs/editmode-results.xml" `
  -logFile -
```

预期结果：`result="Passed"`, `total="1186"`, `passed="1186"`, `failed="0"`, `errors="0"`, `warnings="0"`。

### 5.2 编译验证

```powershell
& "C:\Program Files\Unity\Hub\Editor\6000.5.3f1\Editor\Unity.exe" `
  -batchmode -nographics `
  -projectPath "<repo root>" `
  -quit `
  -logFile "Logs/compile.log"
```

预期结果：退出码 0，日志中无 `error CS` / `warning CS`。

### 5.3 Core 依赖守卫

`Assets/Starfall/Tests/EditMode/CoreDependencyGuardTests.cs` 包含 4 个测试：
- Core asmdef 无 UnityEngine.dll 引用
- Core asmdef 无 UnityEditor.dll 引用
- Core 源文件不包含 `using UnityEngine`
- Core 源文件不包含 `using UnityEditor`

这 4 个测试是 AGENTS.md §10.1 硬约束的自动验证。
