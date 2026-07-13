# MAP SYSTEM ARCHITECTURE AUDIT

> main HEAD: 60f47ab (`test(playmode): M-35 demo - simulate M/A/F/D/Z/Space + capture hash/log`)
> Branch: `agent/map-architecture-audit`
> Worktree: `D:\AI-Worktrees\Xingyuan\architect`
> Auditor: xingyuan-architect
> Date: 2026-07-14
> Scope: existing MVP (post-Task 19 / M-35) vs doc2《Starfall Covenant Map Development Plan v1.0》18 个 MAP 任务
> Inputs: `D:\UntiyProject\XingyuanCovenant\.incoming\doc1.txt`, `doc2.txt`

---

## 0. TL;DR

| 项 | 数值 |
|---|---|
| doc2 MAP 任务总数 | 18 |
| 已实现（覆盖 ≥ 80%） | 3（MAP-01 基础、M3 锚点围区、M-Data 数据层基础） |
| 部分实现（30-79%） | 8（MAP-02/MAP-03/MAP-04/MAP-05/MAP-07/MAP-09/MAP-13/MAP-14） |
| 完全未实现（< 30%） | 7（MAP-06 视线/MAP-08 翻转/MAP-10 对象状态机/MAP-11 CV/MAP-12 多边形/MAP-15 交互预览/MAP-16 编辑器） |
| EditMode 测试总数 | 179（M-35 后 PASS 0/0/0） |
| 既有程序集数 | 4（Core / Data / Unity / Tests×2） |
| doc2 建议程序集数 | 6（Map.Core / Map.Runtime / Map.Editor / Tests×2 + Core） |

**推荐路线：路线 A（增量升级）** — 保留 4 程序集，在 `Assets/Starfall/Core/` 下新增 `Map/` 命名空间子目录；逐 MAP 升级；不重命名 `GridPos` / `BattleState.Board`。详见 §5。

---

## 1. 摘要

### 1.1 当前 MVP 真实定位

- MVP"断裂点三号"是 **8×10 单层棋盘**、10 单位、Light/Dark 二相位、5 状态 Tile（Normal/Blocked/Hazard/Objective）、Anchor 多边形围区（凸多边形）+ Decree 律令 + 关卡闭环（Guard → Retreat）的 **战斗纵切版本**。
- 地图系统被简化为 `BoardState`（8×10 单层）+ `TileState` 枚举（5 状态）+ `GridPos`（X/Y 二维）+ `BFSPathfinder`（4 邻居，单成本）。
- 没有 `DimensionLayer` 枚举、没有 `MapState` 顶层容器、没有 `TileDefinition/TileState` 二分、没有 CV、没有 `MapObject`、`MapRegion`、`MapPassabilityService`、`LineOfSightService`、`ConstellationPolygonService` 等 doc2 必备服务。

### 1.2 doc2 任务与 MVP 功能映射

| 类别 | 数量 | 含义 |
|---|---|---|
| ✅ 已实现 | 3 | MAP-01（MVP 用 `GridPos`，覆盖 doc2 GridCoord 80%；缺 Layer 字段）/ M3 锚点围区（`AnchorZone` + `AnchorRegistry`） / Data 加载（M-Data，含 validator） |
| 🟡 部分实现 | 8 | MAP-02（`BattleState` 嵌 `Board` + `PostStateHash`，但无 `MapState` 顶层）、MAP-03（`ICommand` 框架存在，但无 `IMapCommand` 分离）、MAP-04（`TileState` 枚举 5 值 + 占用检测，但无 `TileDefinition/TileState`）、MAP-05（BFS 寻路确定，但非 A\*）、MAP-07（单层 `BoardState` 而非双层）、MAP-09（无 `MapRegion`，但 `BattleState.ExitTile` 类似撤离）、MAP-13（M-Data JSON 数据 + 基础 validator）、MAP-14（`RealBoardPresenter` + HUD，但无 `TileView` 解耦类） |
| ❌ 未实现 | 7 | MAP-06（高度/掩体/视线，无 `LineOfSightService`）/ MAP-08（无 `FlipTilePhaseCommand` + `FallResolutionService` + `PhaseCompressionResolutionService`）/ MAP-10（无 `MapObject` + 状态机）/ MAP-11（无 CV）/ MAP-12（`AnchorZone.Contains` 凸多边形假设 + 浮点除法，不满足 doc2 整数定点 + 自相交排除）/ MAP-15（无 `MapInputController` 与路径预览）/ MAP-16（无 `Starfall Map Editor`）/ MAP-17（`MAP_DEV_PHASE_TEST_001` 不存在）/ MAP-18（性能基线未跑） |

注：MAP-03 在 doc2 中特指"地图 Command 与事件框架"——MVP 的 `ICommand` 框架是**通用战斗 Command**（MoveCommand / AttackCommand / FallingCommand），不针对**地图修改 Command**（FlipTilePhaseCommand / TransformTileCommand / ModifyGlobalCVCommand 等 16 个 doc2 §21.1 列出的命令）。

### 1.3 建议路线（详见 §5）

| 路线 | 简述 | 风险 | 推荐度 |
|---|---|---|---|
| **A 增量升级** | 保留 4 程序集，在 `Assets/Starfall/Core/Map/` 新增子目录；保留 `GridPos`/`BoardState` 名；逐步补 MAP-01~18 | 低 | ⭐⭐⭐ |
| B 完全重做 | 新建 `Starfall.Map.Core`/`Map.Runtime`/`Map.Editor`，废弃 `GridPos`/`BoardState` | 高（重写 1-2 周 + 重写 179 测试） | ⭐ |
| C 混合 | 在 `Starfall.Core/Map/` 子目录共存（与 Combat 共存于 Core 内） | 中（Core 长期增重，但程序集稳定） | ⭐⭐ |

---

## 2. 详细对照表（doc2 18 任务 vs MVP 现状）

> 标记：✅ 已实现 ≥ 80%；🟡 部分实现 30-79%；❌ 未实现 < 30% 或纯缺口。

| MAP 任务 | 现状（文件 / 行 / 行为） | 覆盖度 | 缺口 | 建议（最小升级） |
|---|---|---|---|---|
| **MAP-01 棋盘坐标与基础网格** | `Core/Model/GridPos.cs` L1-50（X/Y + CompareTo + Neighbour 无，但 `Pathfinding` 内有 4 邻居表）；`Core/Model/Enums.cs` 无 `DimensionLayer`；无独立 `MapSize` / `GridMap<T>` / `GridDirection` 容器；`Core/Model/GridPosComparer.cs` L1-15 | 🟡 ~70% | 1) `GridPos` 缺 `Layer` 字段（doc2 要求 `GridCoord` 三维 `X/Y/Layer`）；2) 无 `GridMap<T>` 通用容器（当前用 `Dictionary<GridPos, TileState>`）；3) 无 `DimensionLayer.Reality/Astral` 枚举（当前 `Phase.Light/Dark` 是单位相位而非维度）；4) 无 4 邻居查询方法（散落在 `BFSPathfinder`）；5) 邻居顺序 ≠ doc2 `North→East→South→West`（`BFSPathfinder.Neighbors = {(0,1)下, (-1,0)左, (1,0)右, (0,-1)上}`，违反 doc2 §4.5 + MAP-05 §9.4 固定平局规则） | **保留 `GridPos` 名字**（向后兼容 179 测试），加 `Layer` 字段（默认 `Reality`）+ `GridPos3D` 包装类或 `Layer` 在新坐标；新增 `Assets/Starfall/Core/Map/GridMap.cs`；新增 `Assets/Starfall/Core/Map/DimensionLayer.cs`；**修复邻居顺序为 `North→East→South→West`**（这是硬性正确性 bug） |
| **MAP-02 MapState、深拷贝与状态哈希** | `Core/Model/BattleState.cs` L1-230（嵌入 `Board` + `Units` + `Anchors` + `Decrees` + `StatusInstances` + `PostStateHash` FNV-1a）；`Core/Model/Cloner.cs`（`BattleStateCloner.Clone` 深拷贝）；`Core/Model/Comparer.cs`（基于 Hash + 字段对比） | 🟡 ~60% | 1) 没有独立 `MapState`（doc2 要求与 `MapDefinition` 分离）；MVP 把"地图事实"混入 `BattleState`，并且 `BattleState` 还包括 `Units` / `Statuses` / `Decrees` 等非地图状态；2) `BattleStateCloner.Clone` **不复制** `Statuses` / `Anchors` / `Decrees`（Cloner L25-41 仅复制 Board + Units，Task 19 阶段回调注释 `_units 已在构造函数中填好`）；3) `PostStateHash` 含 TurnNumber / ActivePlayer / Unit HP 等**战斗字段**，与 doc2 "MapState 哈希应只含地图字段"冲突；4) `MapDefinition`（immutable）不存在 | **拆分**：`MapDefinition` (immutable, 含 map_id / size / initial_global_cv / tileset_id / environment_schedule_id 等 doc1 §19.1 字段) + `MapState` (运行时)；`BattleState` 持有 `MapState` 引用（不再嵌入 `Board`）；`BattleStateCloner` 升级为深拷贝 `MapState`（含 tiles / anchors / regions / map_objects） |
| **MAP-03 地图 Command 与事件框架** | `Core/Command/ICommand.cs` L1-15（`Execute(state, out events)`）；`Core/Command/CommandExecutor.cs` L1-15（直接 Run）；`Core/Command/BattleEvent.cs` L1-40（事件枚举 12 种）；`Core/Command/CommandRecord.cs`（含 seq + cmd + events） | 🟡 ~50% | 1) `ICommand` 是**通用战斗**接口（`Execute(BattleState, out BattleEvent)`），不是 doc2 `IMapCommand`（`Execute(MapState)` 返回 `MapCommandResult`）；2) 没有 doc2 §21.1 列出的 16 个地图命令（`FlipTilePhaseCommand` / `TransformTileCommand` / `SetTileStabilityCommand` / `ModifyGlobalCVCommand` / `CreateAnchorLinkCommand` / `CreateConstellationAreaCommand` 等）；3) 没有 `MapCommandExecutor`（验证 + 版本号 + 事件收集）；4) 没有 `SetMapDebugValueCommand` 测试命令；5) `CommandExecutor.Run` 不增加"地图版本"（doc2 要求 `MapState.Version`）；6) 事件名不一致（doc2 要求 `OnTileChanged` / `OnRegionChanged` / `OnPathGraphInvalidated` / `OnLineOfSightInvalidated` 等） | **双轨并行**：保留现有 `ICommand`（战斗），新增 `Assets/Starfall/Core/Map/IMapCommand.cs` + `MapCommandExecutor` + 16 个 `*Command` 类；`MapState` 内部维护 `int Version` 字段；事件用 `MapEvent` 结构体（与 `BattleEvent` 区分） |
| **MAP-04 地块、地形与占用系统** | `Core/Model/Enums.cs` L1-15（`TileState` 5 值：Normal/Blocked/Hazard/Objective）；`Core/Model/BoardState.cs`（`Dictionary<GridPos, TileState>`）；`Core/Model/TileSnapshot.cs`（Presenter 用）；`Core/Rules/CrushResolver.cs`（同格多单位检测，但**不是**地块占用） | 🟡 ~35% | 1) 没有 `TileDefinition`（immutable，含 TileId/Coord/TerrainTypeId/HeightLevel/BaseMoveCost/BlocksMovement/BlocksVision/BlocksProjectile/CoverLevel/CoverDirections/PhasePairTileId/Tags）；2) 没有 `TileState`（runtime，含 Stability/IsPassable/IsVisible/IsRevealed/OccupyingUnitId/OccupyingObjectId/LocalCollapseValue/TemporaryMoveCostModifier/ActiveMapEffects）；3) 没有 11 个基础地形（Plain/Rough/Ruins/Wall/BrokenBridge/LightBridge/Void/ShallowAstralTide/DeepAstralTide/GateTile/AnchorTile）——`TileState` 是**纯运行时状态枚举**，没有"地形定义"概念；4) 没有地块标签系统（22 种标签 `Walkable/Impassable/PhaseFlippable/PhaseLocked/Destructible/Collapsible/Hazardous/Bridge/Void/Wall/AnchorNode/Spawnable/Deployable/Interactable/Extraction` 等）；5) 没有 `TileOccupancyService`；6) 没有 `Footprint`（多格占位 1×1/2×2/3×3） | **新增**：`Assets/Starfall/Core/Map/TileDefinition.cs` + `TileState.cs`（注意与 `Starfall.Core.Model.TileState` 枚举同名，加命名空间隔离）+ `TerrainDefinition.cs` + `TileOccupancyService.cs` + `Footprint.cs`；保留旧 `Model.TileState` 枚举作为 legacy 适配层 |
| **MAP-05 通行图、寻路与移动范围** | `Core/Pathfinding/IPathfinder.cs`（`FindPath(BoardState, GridPos, GridPos)`）；`Core/Pathfinding/BFSPathfinder.cs`（4 邻居 BFS） | 🟡 ~40% | 1) 算法是 **BFS**，doc2 要求**确定性 A\***；2) 没有 `MapPassabilityService`（含拒绝原因、风险标签）；3) 没有 `MovementProfile`（最大高度差/飞行/穿越浅星潮/跨维）；4) 没有 `MovementRangeService`（移动范围 = 可达地块 + 到达成本 + 推荐路径）；5) 没有 `MapPath` 数据结构（含风险标签、是否跨维、是否经过强制停止地块）；6) **邻居顺序错误**：`BFSPathfinder.Neighbors = {(0,1), (-1,0), (1,0), (0,-1)}` = 下、左、右、上，**违反** doc2 §4.5 / MAP-05 §9.4 `North→East→South→West`（上、右、下、左）；7) 阻塞判定只看 `TileState.Blocked`，没有 `HeightLevel` / `Footprint` / 跨维规则 | **新增**：`Assets/Starfall/Core/Map/Pathfinding/PathfindingService.cs`（A\*，4 邻居 + 同 doc2 平局规则）+ `MapPassabilityService.cs` + `MovementRangeService.cs` + `MovementProfile.cs` + `MapPath.cs`；**修复邻居顺序 bug**（影响所有 `InputStateMachineTests`/`CommandAndPathfinderTests` 的预期路径） |
| **MAP-06 高度、掩体与视线系统** | 无 | ❌ 0% | 1) 没有 `HeightTraversalService`；2) 没有 `CoverLevel` / `CoverDirection` / `CoverQueryService`；3) **没有 `LineOfSightService`**（这是 doc2 §7.3 + §10.5 强制要求的"战斗视线不得依赖 `Physics.Raycast`"）；4) 没有 6 种弹道分类（Direct/Arc/Beam/Chain/GroundPropagation/CrossPhase）；5) 没有高地优势标志 | **全新子目录**：`Assets/Starfall/Core/Map/LineOfSight/` + `Cover/` + `Height/`；新增 `HeightTraversalService.cs` + `CoverLevel.cs`（`None/Half/Full`）+ `CoverDirection.cs`（`North/East/South/West/All`）+ `CoverQueryService.cs` + `LineOfSightService.cs`（含 `Bresenham`/`Supercover` 整数算法）+ `ProjectileType.cs`；预算 2-3 天 |
| **MAP-07 实相层与星相层** | `Core/Model/Enums.cs` 有 `Phase.Light/Dark`，但是**单位相位**而非**地图维度** | 🟡 ~30% | 1) 没有 `DimensionLayer.Reality/Astral` 枚举；2) `BoardState` 是**单层**字典（`Dictionary<GridPos, TileState>`），没有"两层独立 tile"的容器；3) 没有 `PhasePairService`；4) 没有 `PhaseTransitionLink`；5) `UnitState.Phase`（Light/Dark）是单位属性，不等于单位"所在维度"——doc2 §11.1 要求 `MapState` 区分 `Unit所在维度 / 当前激活维度 / 地图当前主显示维度` | **新增**：`Assets/Starfall/Core/Map/DimensionLayer.cs`（枚举 Reality/Astral）+ 改造 `GridPos` 加 `Layer` 字段 + `GridMap<T>` 双层容器 + `PhasePairService.cs` + `PhaseTransitionLink.cs`；`UnitState` 加 `Layer` 字段；预算 1-2 天 |
| **MAP-08 相位翻转、坠落与实体挤压** | `Core/Rules/PhaseFlipValidator.cs`（仅检查"单位本回合是否已 PhaseInvert 状态"——是**单位状态限制**而非**地块翻转规则**）；`Core/Rules/FallingCommand.cs`（扣 HP + 发事件，但**没**有查找合法落点）；`Core/Rules/CrushResolver.cs`（同格多单位扣 HP）；`Core/Status/StatusKind.cs` 含 `PhaseInvert` | 🟡 ~25% | 1) **没有 `FlipTilePhaseCommand` / `FlipRegionPhaseCommand`**（doc2 §21.1 列出，但 MVP 完全没有）；2) 没有"双层地块有效性切换"概念——`TileState` 只有 5 值，没有 `ActiveDimension`；3) 没有 `FallResolutionService`（"查找最近合法落点 + 排序：曼哈顿距离→Y→X→Layer"）；4) 没有 `PhaseCompressionResolutionService`（实体挤压弹回）；5) `FallingCommand` 当前是"扣 HP + 标记位 + 单位仍在棋盘"（MVP 简化），doc2 要求"移动占用状态"；6) 没有 `OnUnitEnteredVoid` / `OnUnitPhaseCompressed` 事件 | **新增**：`Assets/Starfall/Core/Map/Commands/FlipTilePhaseCommand.cs` + `FlipRegionPhaseCommand.cs` + `FallResolutionService.cs` + `PhaseCompressionResolutionService.cs`；`FallingCommand` 重构为调用 `FallResolutionService`；预算 2-3 天（**核心玩法，最高优先级**） |
| **MAP-09 地图区域、部署区与出生点** | `Core/Model/BattleState.cs` L80-90 有 `ExitTile`（`GridPos?`，撤离格）+ `CurrentPhase`（Guard/Retreat/Ended）+ `GuardsCompleted/Required`（关卡闭环） | 🟡 ~30% | 1) 没有 `MapRegionDefinition` / `MapRegionState` / `MapRegionService`；2) 没有 14 个区域类型（PlayerDeployment/EnemySpawn/Reinforcement/Capture/Defense/Escort/Extraction/Restricted/Interaction/BossPhase/StoryTrigger/Collapse/CameraSequence/EnvironmentalHazard）——MVP 的 `ExitTile` 是 `GridPos?`，仅支持"撤离"，且通过 `ObjectivePhaseUpdater` 推 `RetreatComplete` 事件；3) 没有 `MapSpawnPoint`；4) 没有 `OnRegionEntered/Exited/Occupied/Vacated/StateChanged` 事件；5) 没有 `RegionState`（8 值：Disabled/Hidden/Available/Active/Contested/Completed/Failed/Sealed） | **新增**：`Assets/Starfall/Core/Map/Regions/MapRegionDefinition.cs` + `MapRegionState.cs` + `MapRegionService.cs` + `MapSpawnPoint.cs`；保留 `BattleState.ExitTile` 作为 legacy；预算 2-3 天 |
| **MAP-10 地图对象与交互状态机** | `Core/Anchor/AnchorRegistry.cs` + `AnchorZone.cs`（已是 MVP 特色） | 🟡 ~25% | 1) MVP 只有 `AnchorZone`（多边形顶点列表 + `Contains`），**没有** doc2 `MapObjectDefinition`（含 ObjectType/Footprint/是否阻挡移动/视线/可破坏/状态机ID/事件列表）；2) 没有 `MapObjectService`；3) 没有 12 个对象类型（PhaseAnchor/ControlTerminal/Gate/Elevator/BreakableWall/MovableBlock/PressurePlate/ExtractionPortal/EnvironmentalGenerator 等）——MVP 的 `AnchorZone` 只覆盖 PhaseAnchor 一个；4) 没有 `MapObjectStateMachine`（数据驱动状态机）；5) 没有闸门（Closed/Opening/Open/Closing/Locked/Destroyed/PhaseLocked）；6) 没有升降平台状态；7) 没有可破坏物耐久值接口 | **新增**：`Assets/Starfall/Core/Map/Objects/MapObjectDefinition.cs` + `MapObjectState.cs` + `MapObjectService.cs` + `MapObjectStateMachine.cs`；预算 3-4 天（12 个对象类型 + 状态机） |
| **MAP-11 崩塌值与动态地图** | 无 | ❌ 0% | 1) **没有 `GlobalCollapseValue`**（doc1 §13.1 范围 0-100，5 阶段 Stable/Anomalous/Fracturing/Collapsing/GateFault）；2) 没有 `LocalCollapseValue`；3) 没有 `TileStability` 枚举（Stable/Unstable/Fractured/Collapsing/Collapsed/Reconstructed）；4) 没有 `CollapseValueService` / `ModifyGlobalCollapseValueCommand`；5) 没有 `MapEnvironmentSchedule` / `MapEnvironmentEvent` / `EnvironmentPhaseResolver`（doc1 §15.1 固定 10 步顺序：延迟机关→持续效果→局部CV→全局CV→地块状态→坠落→区域激活→增援点→地图事件→预警）；6) 没有坍塌预警 API | **新增**：`Assets/Starfall/Core/Map/Collapse/CollapseValueService.cs` + `TileStability.cs` + `ModifyGlobalCollapseValueCommand.cs` + `CollapseTileCommand.cs` + `ReconstructTileCommand.cs` + `MapEnvironmentSchedule.cs` + `EnvironmentPhaseResolver.cs`；预算 2 天（CV 数字状态机） + 2-3 天（环境时间表） |
| **MAP-12 相位锚点与星宿连线** | `Core/Anchor/AnchorRegistry.cs` + `AnchorZone.cs`（凸多边形 + `Contains`） | 🟡 ~40% | 1) MVP `AnchorZone.Contains` 用**经典浮点射线法**（L25-37），doc2 §14.4 强制"必须使用整数或定点数，禁止浮点"；2) `AnchorZone` 顶点只按 (Y,X) 升序排序（`AnchorZone.cs` L20），**没有固定顶点排序**（doc2 §12 要求"闭合路径 + 排除自相交 + 固定顶点排序"）；3) MVP 假设**凸多边形**（`AnchorZone.Contains` 注释），doc2 §12.3 要求"支持凹四边形"和"自相交拒绝"；4) 没有 `AnchorLinkService`（连线判定）；5) 没有 `ConstellationPolygonService`；6) MVP `AnchorRegistry` 没有"按 Owner 控制"概念（仅 Owner 字段 + 注册），doc2 §11.1 要求 7 个状态（Inactive/PlayerControlled/EnemyControlled/Overloaded/Damaged/Destroyed/Locked）；7) 没有 `OnAnchorLinkCreated` / `OnConstellationPolygonCreated` 事件；8) 没有"格子归属"（`AnchorZone.Contains` 是点归属，不是格子中心点归属，doc1 §14.4 要求） | **新增**：`Assets/Starfall/Core/Map/Constellation/`；`AnchorZone` 重构：改用整数射线法 + 固定顶点排序 + 自相交检测 + 格子归属（计算多边形中心点）；新增 `AnchorLinkService.cs` + `ConstellationPolygonService.cs` + `AnchorState.cs`；预算 2-3 天（**核心玩法，最高优先级**） |
| **MAP-13 地图 JSON 数据层与验证器** | `Data/Definition/BattleDefinition.cs`（顶层）+ `BoardDefinition.cs`（含 Width/Height/Tiles[]）+ `UnitDefinition.cs` + `StatusDefinition.cs`；`Data/Loading/JsonBattleLoader.cs`；`Data/Validation/DefinitionValidator.cs`（含 TurnNumber/Board Tiles/Units + Task 19 `GuardsRequired`/`ExitTile`） | 🟡 ~50% | 1) **缺 5 个 doc2 §19 必备表**：`map_definitions`（含 `map_id` / `width` / `height` / `supported_layers` / `initial_active_layer` / `initial_global_cv` / `tileset_id` / `environment_schedule_id` / `map_event_graph_id`）、`map_regions`、`map_objects`、`map_environment_schedules`、`map_event_definitions`——MVP 当前只有 `BattleDefinition` 单文件，相当于 doc2 §19.2 简化版；2) **没有 `schema_version` / `content_version`**（doc2 §13.3 要求）；3) `DefinitionValidator` **没有 doc2 §13.4 列出的 18 项检查**（仅检查 TurnNumber / Board 尺寸 / Tile 越界 / Unit 越界 / Unit 重复 / `GuardsRequired` / `ExitTile`），缺：双层尺寸一致 / `TileId` 重复 / `ObjectId` 重复 / `RegionId` 重复 / 部署区是否存在 + 容量 / 出生点合法 / 多格对象越界 / 区域引用 / 环境事件引用 / 锚点引用 / 初始占用冲突 / 撤离区可达 / 坍塌后主路径 / 非法状态机；4) 没有 `MapValidationService`；5) 没有 `MapRuntimeFactory`；6) 没有 `MapDefinitionRepository` | **新增**：`Assets/Starfall/Data/Map/` + `Assets/Starfall/Core/Map/Validation/MapValidationService.cs`；`MapDefinition` 顶层 DTO（5 个嵌套表）+ 升级 `DefinitionValidator` / 新建 `MapValidationService`；预算 2-3 天 |
| **MAP-14 Unity 地图表现与调试视图** | `Unity/RealBoardPresenter.cs`（80 Quad + Capsule 单位 + LineRenderer 锚点 + 高亮层）；`Unity/RealBattleHud.cs`（uGUI 9 行）；`Unity/Presentation/BoardSnapshot.cs` / `HudSnapshot.cs` / `UnitSnapshot.cs` / `AnchorSnapshot.cs` / `BoardPalette.cs` / `LegalPreviewHelper.cs` | 🟡 ~50% | 1) `RealBoardPresenter` 用 `Quad` GameObject（80 个）作为 tile，不是 doc2 `TileView`（推荐 `MonoBehaviour` 子类 + MaterialPropertyBlock + 索引化）；MVP 内嵌 `private class TileView : MonoBehaviour` 是**类内私有**而非 doc2 顶级类型；2) 没有 `MapView` / `MapObjectView` / `PhaseLayerView` / `MapHighlightView` 类型（MVP 直接用 `RealBoardPresenter` 一把抓）；3) 没有"双层显示"模式（Reality/Astral/Dual）——MVP 是单层；4) 没有"调试显示"开关（坐标 / TileId / 高度 / 移动成本 / 通行 / 占用 / 区域 / CV / 相位配对 / 视线阻挡 / 地块稳定状态）；5) `BoardSnapshot` 不含 CV / 区域 / 双层 tile 状态 | **新增**：`Assets/Starfall/Unity/Map/MapView.cs` + `TileView.cs`（提取为顶级类）+ `MapObjectView.cs` + `PhaseLayerView.cs` + `MapHighlightView.cs` + `MapDebugOverlay.cs`；`RealBoardPresenter` 重构为 `MapView` 子类；预算 3-4 天 |
| **MAP-15 地图交互、选择与预览** | `Unity/Input/InputController.cs` + `InputStateMachine.cs` + `CommandBuilder.cs`（已有 SelectUnit / MoveTarget / PhaseFlipTarget / AttackTarget / DecreeSelect 5 模式） | 🟡 ~50% | 1) 没有"路径预览"独立服务（`RealBoardPresenter.DrawHighlights` 只显示合法落点 + 攻击目标 + 坠落预览，但**不显示路径线**）；2) 没有 `TileSelectionController`；3) 没有"地块信息面板"独立组件（HUD 第 5-8 行只显示单位 / 模式 / 消息 / Outcome，**没有地块**）；4) 没有"高亮类型"枚举（Selectable/Reachable/Path/Target/Hazard/CollapseWarning/PhaseFlipPreview/InvalidRegion）——MVP 用 `BoardPalette.HighlightLegalMove/AttackTarget/FallRisk` 3 种颜色；5) 没有"相位翻转预览"独立流程（`PhaseFlipTarget` 模式只显示 `PhaseFlipPlan`，不预览翻转后地形 / 坠落方向 / 挤压落点）；6) **Undo 阻塞**：`InputController.DoUndo` 注释"需要 Core 暴露 `BattleRunner.RestoreState`"（L260-280），现版本 Undo 不真生效 | **新增**：`Assets/Starfall/Unity/Map/MapInputController.cs` + `TileSelectionController.cs` + `MapHighlightType.cs` + `PathPreviewView.cs` + `TileInfoPanel.cs` + `PhaseFlipPreview.cs`；**优先修复 Undo 阻塞**：Core 加 `BattleRunner.RestoreState(BattleState)`；预算 3-4 天 |
| **MAP-16 Unity 地图编辑器** | `Assets/Editor/MVPPlayModeHelper.cs`（仅是"Setup Battle Scene"菜单工具，**不是地图编辑器**） | ❌ 0% | 1) 没有 `Starfall Map Editor` 窗口；2) 没有"地块绘制 / 双层编辑 / 区域编辑 / 对象放置 / 事件配置"（doc1 §22）；3) 没有编辑器导出 JSON；4) 没有"调试预览"（寻路 / 移动范围 / 视线 / 掩体 / 相位翻转 / 坠落落点 / 多格占用 / 坍塌传播 / 星宿连线）；5) `MVPPlayModeHelper.cs` 在 `Assets/Editor/` 但**没有 asmdef**（MVP 全局只有 4 个程序集，没 Editor 程序集） | **新增**：`Assets/Starfall/Editor/MapEditor/` + `Starfall.Editor.asmdef`（独立程序集，Editor-only）；MVP `Assets/Editor/MVPPlayModeHelper.cs` 移到 `Starfall.Editor` 程序集；预算 **5-7 天**（独立子系统） |
| **MAP-17 地图验证关卡与系统集成** | `Assets/Scenes/MVP_Battle.unity`（8×10 单层）+ `Assets/StreamingAssets/data/battle_default.json`（8×10 教学关，10 单位） | ❌ 5% | 1) **没有 `MAP_DEV_PHASE_TEST_001`**（doc1 §28 强制要求 12×14 双层地图，含 6 玩家部署格 / 4 敌方出生格 / 1 实相断桥 / 1 星相光桥 / 2 可破坏墙 / 2 控制终端 / 3 相位锚点 / 1 撤离区 / 1 局部坍塌区 / 1 全局 CV 事件 / 1 2×2 首领 / 1 高台 / 1 升降平台）；2) 当前 `battle_default.json` 是**单层战斗关**（无 Layer / 无 Region / 无 MapObject / 无 EnvironmentSchedule），与 doc2 §13 验证关卡完全无关；3) **没有 `MapSystemIntegrationTests` / `MapCommandReplayTests` / `MapUndoIntegrationTests` / `MapDevLevelPlayModeTests`** | **新建** `Assets/StreamingAssets/data/map_dev_phase_test_001.json`（12×14 双层）+ `Assets/Scenes/MapDevTest.unity`；新增 4 个测试集；预算 1-2 天（数据） + 2-3 天（集成测试） |
| **MAP-18 性能、回放与最终验收** | 部分：MVP 的 `ReplayCodec` / `ReplayPlayer` / `CommandRecorder`（Command-based Replay）+ `LevelLoopTests`（11 测试）部分覆盖了"Replay + 确定性" | 🟡 ~30% | 1) 没有 `20×28 双层 MVP 压力地图` + `48×64 双层逻辑压力地图`（doc2 §18.1）；2) 没有性能基准测试（普通单目标寻路 ≤20ms / 普通移动范围 ≤50ms / 100格相位翻转 ≤100ms / 48×64 加载 / 100 次 Replay）；3) 没有"Replay 文件版本号 + magic"（`ReplayFile.cs` 只有 `FinalHash` + `InitialTurnNumber` + `InitialActivePlayer` + `Commands[]`，无 magic + schema_version）；4) 文档缺口：`docs/MAP_SYSTEM_ARCHITECTURE.md` / `docs/MAP_DATA_SCHEMA.md` / `docs/MAP_EDITOR_GUIDE.md` / `docs/MANUAL_ACCEPTANCE_CHECKLIST.md` 都不存在（MVP 有 `docs/MANUAL_ACCEPTANCE_CHECKLIST.md` 但只覆盖 MVP 任务，不覆盖 MAP-18）；5) 没有 48×64 双层逻辑压力地图 | **新增**：`Assets/StreamingAssets/data/perf_20x28_dual.json` + `perf_48x64_dual.json`（48×64 仅逻辑测试）；新增 `Assets/Starfall/Tests/EditMode/MapPerformanceTests.cs`；补 `ReplayFile.Magic` + `SchemaVersion`；新建 4 篇 doc；预算 3-4 天 |

---

## 3. 程序集结构对照

### 3.1 既有程序集（4 个 + 1 个无 asmdef 的 Editor 目录）

| 程序集 | 路径 | 现状引用 | `noEngineReferences` | 备注 |
|---|---|---|---|---|
| Starfall.Core | `Assets/Starfall/Core/Starfall.Core.asmdef` | `[]`（空） | **true** ✓ | 完全无 UnityEngine 引用（AGENTS.md §10.1 硬约束，`CoreDependencyGuardTests` 4 项自动验证） |
| Starfall.Data | `Assets/Starfall/Data/Starfall.Data.asmdef` | `[Starfall.Core]` | false | 依赖 Core；`BattleStateBuilder.Build` / `JsonBattleLoader` 等 |
| Starfall.Unity | `Assets/Starfall/Unity/Starfall.Unity.asmdef` | `[Starfall.Core, Starfall.Data, Unity.InputSystem, UnityEngine.UI]` | false | 表现层 + 输入 |
| Starfall.Tests.EditMode | `Assets/Starfall/Tests/EditMode/Starfall.Tests.EditMode.asmdef` | `[Starfall.Core, Starfall.Data, Starfall.Unity, UnityEngine.TestRunner, UnityEditor.TestRunner]`；`includePlatforms: Editor`；`overrideReferences: true` + `nunit.framework.dll`；`defineConstraints: UNITY_INCLUDE_TESTS` | false | 13 个测试文件 / 179 测试 |
| Starfall.Tests.PlayMode | `Assets/Starfall/Tests/PlayMode/Starfall.Tests.PlayMode.asmdef` | `[Starfall.Core, Starfall.Data, Starfall.Unity, UnityEngine.TestRunner]`；`overrideReferences: true` + `nunit.framework.dll`；`defineConstraints: UNITY_INCLUDE_TESTS` | false | 1 个 M35DemoScript.cs |
| (无 asmdef) | `Assets/Editor/MVPPlayModeHelper.cs` | n/a | n/a | **缺口**：MVP 没有独立 `Starfall.Editor` 程序集，`MVPPlayModeHelper.cs` 借全局 Editor 隐式编译 |

### 3.2 doc2 建议程序集（§六）

| 程序集 | 依赖 | 平台 |
|---|---|---|
| Starfall.Map.Core | → Starfall.Core | 不引用 UnityEngine |
| Starfall.Map.Runtime | → Starfall.Map.Core | 允许 UnityEngine |
| Starfall.Map.Editor | → Starfall.Map.Core, Starfall.Map.Runtime | 仅 Editor |
| Starfall.Map.EditModeTests | → Starfall.Map.Core | EditMode |
| Starfall.Map.PlayModeTests | → Starfall.Map.Core, Starfall.Map.Runtime | PlayMode |

**禁止**：`Core → Runtime`、`Core → Editor`、`Core → UnityEngine`、`Runtime → Editor`。

### 3.3 冲突与兼容性

| 项 | doc2 要求 | MVP 现状 | 冲突？ | 建议 |
|---|---|---|---|---|
| Core 不引用 UnityEngine | ✓ | ✓（`noEngineReferences: true`） | 0 | 保留 |
| Core / Runtime / Editor 三层分离 | ✓ | ✗（MVP 只有 Core / Unity 两层，Editor 隐式） | 中 | 路线 A 不要求拆三层；路线 B 要求拆 |
| EditMode / PlayMode 测试程序集 | ✓ | ✓ | 0 | 保留 |
| 命名空间 `Starfall.Map.*` | ✓ | ✗（MVP 用 `Starfall.Core.*` / `Starfall.Data.*` / `Starfall.Unity.*`） | 中 | 路线 A 在 `Starfall.Core/Map/` 子目录 + 命名空间 `Starfall.Core.Map.*`；路线 B 改 `Starfall.Map.Core.*` |
| 现有 `GridPos` vs doc2 `GridCoord` | doc2 | `GridPos`（X/Y） | **重大命名分歧** | **保留 `GridPos`**，新代码用 `GridCoord` 作别名（`public struct GridCoord : IEquatable<GridCoord> { public GridPos Pos; public DimensionLayer Layer; }`），向后兼容 179 测试 |
| 现有 `BoardState`（嵌入 `BattleState`） vs doc2 `MapState`（独立） | doc2 | `BoardState` | **重大结构分歧** | **保留嵌入**作为 MVP 简化层；doc2 的 `MapState` 通过 `MapDefinition` + `MapRuntimeFactory` 适配器提供；`BattleState.MapState` 引用（future field） |
| `BattleState.PostStateHash` 含战斗字段 vs doc2 MapState 哈希只含地图 | doc2 | 含 | 中 | 拆分：新增 `MapState.PostStateHash`（仅地图字段）；`BattleState.PostStateHash` 内部调用 `MapState.PostStateHash` 后再混入战斗字段（向后兼容） |
| 现有 `BFSPathfinder`（4 邻居下、左、右、上） vs doc2 顺序（上、右、下、左） | doc2 | ✗ | **正确性 bug** | **必须修复**——所有 `InputStateMachineTests` / `CommandAndPathfinderTests` 的"绕路测试"预期会变；要审 `MoveCommandTests` 8 项 |

---

## 4. 测试覆盖对照

| doc2 MAP 任务 | doc2 要求测试 | 现有测试覆盖 | 覆盖度 | 备注 |
|---|---|---|---|---|
| MAP-01 | GridCoordTests / MapSizeTests / GridMapTests / GridNeighbourTests | `FoundationStateTests.GridPos_*`（5 测试）+ `CoreDependencyGuardTests`（4 测试） | 🟡 50% | 缺 GridMap / DimensionLayer / 4 邻居方法 |
| MAP-02 | MapStateCloneTests / MapStateHashTests / MapStateMutationIsolationTests | `FoundationStateTests.BattleState_*`（5 测试）+ `ReplayCodecTests.Capture_ProducesCorrectHashAndEntry`（1 测试） | 🟡 60% | 现有 `BattleStateCloner.Clone` **不复制 Statuses/Anchors/Decrees**（Task 19 限制），克隆测试覆盖不全 |
| MAP-03 | MapCommandExecutorTests / MapCommandValidationTests / MapCommandEventTests | `CommandAndPathfinderTests`（9 测试，含 ICommand 通用验证） | 🟡 40% | 缺 doc2 IMapCommand 分离 + 16 个命令测试 |
| MAP-04 | TileDefinitionTests / TileStateTests / TerrainDefinitionTests / TileOccupancyTests / FootprintPlacementTests | `RulesTests.FallingCommand_*`（2 测试）+ `RulesTests.CrushResolver_*`（部分） | 🟡 30% | 缺 TileDefinition / TerrainDefinition / 多格占位 |
| MAP-05 | PathfindingTests / MovementRangeTests / DynamicObstaclePathTests / MultiTilePathfindingTests / PathTieBreakTests | `CommandAndPathfinderTests.BFSPathfinder_*`（5 测试） | 🟡 50% | BFS 而非 A\*；缺 MovementRange；**邻居顺序错** |
| MAP-06 | HeightTraversalTests / CoverDirectionTests / LineOfSightTests / ProjectileBlockTests / HighGroundLineOfSightTests | 0 测试 | ❌ 0% | 全缺口 |
| MAP-07 | DualLayerMapTests / PhasePairTests / PhaseTransitionLinkTests / LayerIsolationTests | 0 测试 | ❌ 0% | 全缺口 |
| MAP-08 | PhaseFlipTests / PhaseFlipValidationTests / FallResolutionTests / PhaseCompressionTests / MultiTilePhaseFlipTests | `RulesTests.PhaseFlipValidator_*`（1 测试）+ `FallingCommand` 2 测试 | 🟡 25% | 缺 FlipTilePhaseCommand / FallResolution / PhaseCompression |
| MAP-09 | MapRegionTests / DeploymentValidationTests / SpawnPointTests / RegionEventTests | `LevelLoopTests.RetreatComplete_*`（2 测试）+ `BattleSetupTests`（4 测试） | 🟡 30% | 缺 MapRegion / MapSpawnPoint |
| MAP-10 | MapObjectStateMachineTests / GateObjectTests / BreakableWallTests / ElevatorTests / MovableObjectTests / MapObjectCommandTests | `AnchorAndDecreeTests`（8 测试） | 🟡 30% | AnchorZone 是 doc2 PhaseAnchor 子集；缺 11 个其他对象 |
| MAP-11 | GlobalCollapseValueTests / LocalCollapseValueTests / TileStabilityTests / EnvironmentScheduleTests / CollapseWarningTests / TerrainTransformationTests | 0 测试 | ❌ 0% | 全缺口 |
| MAP-12 | AnchorLinkTests / TrianglePolygonTests / QuadrilateralPolygonTests / PolygonIntersectionTests / PolygonTileContainmentTests / ConstellationDeterminismTests | `AnchorAndDecreeTests`（8 测试，部分覆盖 `AnchorZone.Contains`） | 🟡 35% | AnchorZone 当前凸多边形 + 浮点除法，需重写 |
| MAP-13 | MapJsonLoadTests / MapSchemaVersionTests / MapValidationTests / InvalidReferenceTests / DuplicateIdTests / MapRuntimeFactoryTests | `DataLoadingTests`（7 测试）+ `BattleSetupTests`（4 测试） | 🟡 50% | 缺 doc2 18 项验证 / schema_version / MapValidationService |
| MAP-14 | MapViewCreationTests / TileViewSyncTests / LayerDisplayTests / MapObjectViewTests | `PresentationTests`（15 测试）+ `HudAndPreviewTests`（28 测试，含 BoardSnapshot / HudSnapshot） | 🟡 60% | `TileView` 当前是 `RealBoardPresenter` 私有内嵌类，非 doc2 顶级类型 |
| MAP-15 | TileSelectionTests / MapHighlightTests / PathPreviewTests / PhaseFlipPreviewTests / TileInfoPanelTests | `InputStateMachineTests`（32 测试）+ `HudAndPreviewTests`（部分） | 🟡 50% | 缺路径预览独立服务 + 地块信息面板 |
| MAP-16 | MapEditorSerializationTests / MapEditorExportTests / MapEditorValidationTests / MapEditorRoundTripTests | 0 测试 | ❌ 0% | 全缺口 |
| MAP-17 | MapSystemIntegrationTests / MapCommandReplayTests / MapUndoIntegrationTests / MapDevLevelPlayModeTests | `ReplayAndUndoTests`（8 测试，部分覆盖 Replay）+ `ReplayCodecTests`（6 测试） | 🟡 30% | 缺 MAP_DEV_PHASE_TEST_001 关卡；缺 Undo 集成（InputController 已阻塞） |
| MAP-18 | （性能基准 + 文档） | `LevelLoopTests`（12 测试，含确定性）+ `ReplayCodecTests`（Round-trip） | 🟡 25% | 缺性能基准 + 4 篇 MAP 文档 |

**汇总**：

- 现有 179 EditMode 测试覆盖 doc2 18 任务的 **8 个部分**（MAP-01/02/03/04/05/09/10/13/14/15/17/18 中部分），10 个任务几乎 0 测试覆盖。
- 4 个 doc2 必测但 MVP 完全未覆盖：**MAP-06 / MAP-07 / MAP-11 / MAP-16**。
- **Undo 实际不可用**：`InputController.DoUndo` 注释（Line 263-282）明示需要 Core 暴露 `BattleRunner.RestoreState`，但 Core 没有这个方法；`UndoStack` 只 push 不真生效。这是 MVP 已知技术债（详见 §5.5 风险）。

---

## 5. 推荐路线（3 选 1，给用户裁决）

### 5.1 路线 A：增量升级 ⭐⭐⭐（推荐）

**核心原则**：保留现有 4 程序集 + `GridPos` / `BattleState.Board` / 现有命名空间。**逐 MAP 升级**，不重命名、不破坏 179 测试。

**目录调整**（最小变动）：

```
Assets/Starfall/Core/
  ├─ Model/                    ← 现有（保留）
  │  ├─ GridPos.cs             ← 保留（向后兼容 179 测试）
  │  ├─ GridPosComparer.cs     ← 保留
  │  ├─ BoardState.cs          ← 保留为"旧 BoardState"（legacy alias）
  │  └─ BattleState.cs         ← 保留（嵌入 Board + 加 MapState 引用）
  └─ Map/                      ← 新增子目录
     ├─ Coordinates/           ← MAP-01
     │  ├─ GridCoord.cs        ← doc2 GridCoord（包含 GridPos + Layer）
     │  ├─ DimensionLayer.cs   ← 新增
     │  ├─ GridMap.cs          ← 新增
     │  └─ GridDirection.cs    ← 新增
     ├─ State/                 ← MAP-02
     │  ├─ MapDefinition.cs    ← 新增（immutable）
     │  ├─ MapState.cs         ← 新增（含 tiles / anchors / regions / map_objects / cv）
     │  ├─ MapStateCloner.cs   ← 新增（深拷贝）
     │  └─ MapStateHasher.cs   ← 新增（确定性哈希）
     ├─ Commands/              ← MAP-03 + 后续
     │  ├─ IMapCommand.cs
     │  ├─ MapCommandExecutor.cs
     │  ├─ FlipTilePhaseCommand.cs   ← MAP-08
     │  ├─ FlipRegionPhaseCommand.cs
     │  ├─ ModifyGlobalCVCommand.cs  ← MAP-11
     │  └─ ...
     ├─ Terrain/               ← MAP-04
     │  ├─ TileDefinition.cs
     │  ├─ TileState.cs        ← 注意：与 Model.TileState 同名，命名空间隔离
     │  ├─ TerrainDefinition.cs
     │  └─ TileTags.cs         ← 22 标签枚举
     ├─ Pathfinding/           ← MAP-05
     │  ├─ PathfindingService.cs  ← A*
     │  ├─ MovementProfile.cs
     │  ├─ MovementRangeService.cs
     │  └─ MapPath.cs
     ├─ LineOfSight/           ← MAP-06
     ├─ Phase/                 ← MAP-07
     │  ├─ PhasePairService.cs
     │  └─ PhaseTransitionLink.cs
     ├─ Regions/               ← MAP-09
     ├─ Objects/               ← MAP-10
     ├─ Collapse/              ← MAP-11
     ├─ Constellation/         ← MAP-12
     ├─ Validation/            ← MAP-13
     └─ Occupancy/             ← MAP-04 占用服务
```

**Unity 表现层调整**：

```
Assets/Starfall/Unity/
  ├─ Map/                     ← 新增子目录
  │  ├─ MapView.cs
  │  ├─ TileView.cs           ← 提取 RealBoardPresenter 私有类为顶级
  │  ├─ MapObjectView.cs
  │  ├─ PhaseLayerView.cs
  │  ├─ MapHighlightView.cs
  │  ├─ MapDebugOverlay.cs
  │  ├─ MapInputController.cs     ← 提取 InputController 地图部分
  │  ├─ TileSelectionController.cs
  │  ├─ PathPreviewView.cs
  │  ├─ TileInfoPanel.cs
  │  └─ PhaseFlipPreview.cs
  └─ RealBoardPresenter.cs    ← 保留，作为 MapView 子类（向后兼容）
```

**Editor 子目录**：

```
Assets/Starfall/Editor/                   ← 新建 Starfall.Editor.asmdef
  └─ MapEditor/
     ├─ StarfallMapEditorWindow.cs       ← MAP-16
     ├─ MapEditorSerialization.cs
     ├─ MapEditorExport.cs
     └─ MapEditorValidation.cs
```

**理由**：

1. **AGENTS.md §10.1 硬约束已通过**：`CoreDependencyGuardTests` 4 项自动验证 `noEngineReferences: true` + 无 `using UnityEngine`；route A 完全保留此约束。
2. **现有 179 测试零修改**：所有现有测试用 `Starfall.Core.Model.GridPos` / `BoardState` / `BattleState`，route A 不破坏这些引用。
3. **MVP 战斗闭环已稳定**：Task 19 关卡闭环（Guard → Retreat）已实现，路线 A 不重做这一层。
4. **doc2 的命名空间 `Starfall.Map.*` 通过命名空间子目录实现**：用 `Starfall.Core.Map.*`（路线 A）vs `Starfall.Map.Core.*`（路线 B），区别仅在命名空间深度，对 Unity 编译无影响。

**风险**：

- `Starfall.Core` 命名空间会增大（Combat / Anchor / Decree / Map 4 大子系统共存）；可通过目录层级 + `using Starfall.Core.Map.*;` 维持清晰度。
- "Map" 子目录的代码量最终可能超过现有 Combat 体积；未来若 Core > 5000 行，可考虑再拆 `Starfall.Map.Core`（route A → route B 渐进迁移）。

### 5.2 路线 B：完全重做（不推荐）

按 doc2 §五 + §六 完全新建 6 个程序集：

```
Assets/Starfall/
  ├─ Core/                  ← 现有 Core（保留为通用战斗 Core）
  ├─ Map/
  │  ├─ Core/               ← 新建 Starfall.Map.Core（GridCoord / MapState / IMapCommand）
  │  ├─ Runtime/            ← 新建 Starfall.Map.Runtime
  │  ├─ Editor/             ← 新建 Starfall.Map.Editor
  │  └─ Data/               ← 新建 Starfall.Map.Data
  └─ ...
```

**废弃**：`GridPos` → `GridCoord`，`BoardState` → `MapState`，`BattleState.Board` → `BattleState.MapState`。

**代价**：

- 1-2 周纯重写
- 现有 179 测试需全部修改（GridPos → GridCoord 等）
- 现有 `BattleStateCloner` / `PostStateHash` / `ReplayCodec` 全部需要重写
- `BattleState` 必须解耦 `Board`（`BattleState` 不能直接拥有 tile 字典）
- `MVPPlayModeHelper.cs` 在新程序集下要重新归位

**优势**：

- doc2 §六 推荐的程序集结构（Map.Core / Map.Runtime / Map.Editor 三层分离）
- 长期一致性好：未来添加 4 个其他子系统（角色 / 技能 / AI / 任务）时，各自的程序集命名也清晰

**不建议理由**：MVP 已通过 179 测试 + 视觉验收（M-35），重做 1-2 周纯写代码，**对当前玩家/用户零价值**。

### 5.3 路线 C：混合

保留 4 程序集；在 `Starfall.Core/Map/` 新建子目录（与 Combat 共存于 Core）。Unity 表现层仍放 `Starfall.Unity/Map/`。

vs 路线 A 的区别：路线 A 推荐把 `Map/...` 放在 `Starfall.Core/` 下，路线 C 把 `Map/...` 也放 `Starfall.Core/` 但所有 doc2 必备类型（`MapState` / `IMapCommand` 等）都用 doc2 命名（`GridCoord` 而非保留 `GridPos`）。

**不推荐理由**：与路线 A 几乎相同，但会破坏 179 测试中 `GridPos` 引用（如 `FoundationStateTests.GridPos_*`），性价比不如路线 A。

---

## 6. 实现缺口优先级

### 6.1 P0：核心玩法 + 战斗前置（建议第 1 批授权）

| 缺口 | doc2 任务 | 风险 | 估算 | 阻塞 |
|---|---|---|---|---|
| **修复 BFSPathfinder 邻居顺序 bug** | MAP-05 | 中（正确性） | 0.5 天 | 阻塞所有 MAP-05 测试 |
| **`Starfall.Core.Map.*` 目录 + 命名空间** | MAP-01 | 低 | 0.5 天 | 无 |
| **`DimensionLayer` + `GridMap<T>` + `GridCoord`** | MAP-01 / MAP-07 | 中（API 设计） | 1 天 | MAP-07 |
| **视线 / 掩体 / 高度** | MAP-06 | 中 | 2-3 天 | 战斗伤害前置 |
| **双层维度独立地形** | MAP-07 | 中 | 1-2 天 | MAP-08 |
| **相位翻转 Command + 坠落 + 实体挤压** | MAP-08 | 高（核心玩法） | 2-3 天 | MAP-12 |
| **撤销修复（Core 暴露 `BattleRunner.RestoreState`）** | (技术债) | 高（已阻塞 1 个里程碑） | 0.5-1 天 | MAP-15 |

### 6.2 P1：扩展 + 编辑器（建议第 2 批）

| 缺口 | doc2 任务 | 风险 | 估算 |
|---|---|---|---|
| 锚点连线多边形（整数定点 + 自相交 + 固定排序） | MAP-12 | 高（核心玩法） | 2-3 天 |
| 地图 JSON 数据层（5 表 + validator 18 项） | MAP-13 | 中 | 2-3 天 |
| 崩塌值 + 环境时间表 | MAP-11 | 中 | 2 天（CV）+ 2-3 天（schedule） |
| 地图区域 + 部署区 + 出生点 | MAP-09 | 中 | 2-3 天 |
| 地图对象 + 状态机（12 类型） | MAP-10 | 中 | 3-4 天 |
| 路径预览 + 地块信息面板 + 相位翻转预览 | MAP-15 | 中 | 3-4 天 |

### 6.3 P2：编辑器 + 验证关卡 + 性能（建议第 3 批）

| 缺口 | doc2 任务 | 风险 | 估算 |
|---|---|---|---|
| **Unity 地图编辑器** | MAP-16 | 高（独立子系统） | **5-7 天** |
| **`MAP_DEV_PHASE_TEST_001`（12×14 双层）+ 集成测试** | MAP-17 | 中 | 1-2 天（数据）+ 2-3 天（集成测试） |
| **性能基线 + 4 篇 MAP 文档 + 压力地图** | MAP-18 | 中 | 3-4 天 |

### 6.4 总估算（路线 A 增量升级）

- P0：~10-12 工作日
- P1：~15-18 工作日
- P2：~10-13 工作日
- **总计**：约 **35-45 工作日**（7-9 周，1 人）

### 6.5 关键修复（无论选哪条路线都必须做）

1. **BFSPathfinder 邻居顺序 bug**：`Neighbors = {(0,1), (-1,0), (1,0), (0,-1)}`（下、左、右、上）违反 doc2 §4.5 + MAP-05 §9.4 `North→East→South→West`。修复后所有 `InputStateMachineTests.ConfirmMove_*` / `CommandAndPathfinderTests.BFSPathfinder_*` 预期路径会变；需逐测试调整。
2. **Undo 阻塞**：`InputController.DoUndo` 等待 Core 暴露 `BattleRunner.RestoreState(BattleState)`。这是 MVP 已知技术债，建议优先修复。
3. **`BattleStateCloner.Clone` 不复制 Statuses / Anchors / Decrees**（Task 19 限制）：升级到 MapState 后必须修正，否则 Replay Round-trip + Undo Restore 会丢状态。

---

## 7. 冲突与风险汇总

### 7.1 命名冲突

| 冲突 | doc2 命名 | MVP 命名 | 解决 |
|---|---|---|---|
| 坐标类型 | `GridCoord`（X/Y/Layer） | `GridPos`（X/Y） | **保留 `GridPos`**；新增 `GridCoord = GridPos + DimensionLayer`（包装类）；未来新代码用 `GridCoord`，legacy 仍兼容 |
| 地图状态 | `MapState`（独立） | `BattleState.Board`（嵌入） | **保留嵌入**；`MapState` 作为适配器提供（独立类，但由 `BattleState.MapState` getter 返回） |
| 地块状态 | `TileState`（runtime，含 Stability/CV 等） | `TileState`（枚举，5 值） | **保留枚举 `Starfall.Core.Model.TileState`**；新增 `Starfall.Core.Map.Terrain.TileState`（class）——命名空间隔离，零冲突 |
| 顶点集合 | `AnchorZone`（已与 doc2 同名） | `AnchorZone` | **完全兼容**；doc2 §12 的扩展（整数射线 + 自相交 + 固定排序）在原文件升级 |
| 状态机 | `MapObjectStateMachine` | 无 | 新增，不冲突 |

### 7.2 算法冲突

| 冲突 | doc2 要求 | MVP 实际 | 影响 |
|---|---|---|---|
| 寻路 | A\* + 整数成本 + 平局规则 | BFS + 单成本 + (Y,X) 平局 | 性能 + 平局正确性。doc2 §9.3 要求"支持多格、跨维、高度预留"——BFS 难扩展。**建议升级为 A\*** |
| 邻居顺序 | North → East → South → West | Down → Left → Right → Up（=下、左、右、上） | **正确性 bug**。修复涉及测试断言 |
| 多边形包含 | 整数定点射线法 | 浮点除法射线法 | 确定性保证失败（浮点除法跨平台不可重现）。**必须升级** |
| 多边形顶点排序 | 固定顶点排序（消除重复） | 仅 (Y,X) 升序 | doc2 §12 要求"闭合路径 + 排除自相交 + 固定顶点排序"——MVP 不满足 |
| 哈希 | 确定性，与 `Dictionary` 顺序无关 | FNV-1a + 字段排序，已正确 | 兼容（doc2 §2.4 同要求） |

### 7.3 性能与规模

| 项 | doc2 要求 | MVP 实际 | 差距 |
|---|---|---|---|
| 最大地图 | 48×64 双层（6144 格） | 8×10 单层（80 格） | 77× |
| A\* 寻路 ≤20ms / 移动范围 ≤50ms | 必达 | 未测（80 格太小） | 需建 20×28 + 48×64 压力地图 |
| 100 格相位翻转 ≤100ms | 必达 | 无（无翻转） | 需建压力地图 |
| 60 FPS 视觉 | 必达 | M-35 已通过（80 格） | 需重测 20×28 + 48×64 |

### 7.4 文档缺口

| 缺口 | doc2 §18.5 要求 | MVP 现状 |
|---|---|---|
| `docs/MAP_SYSTEM_ARCHITECTURE.md` | ✓ | ❌（**本文档 `MAP_SYSTEM_AUDIT.md` 是审计，不是架构文档**） |
| `docs/MAP_DATA_SCHEMA.md` | ✓ | ❌ |
| `docs/MAP_EDITOR_GUIDE.md` | ✓ | ❌ |
| `docs/MANUAL_ACCEPTANCE_CHECKLIST.md` | ✓ | ✓（已有 `docs/MANUAL_ACCEPTANCE_CHECKLIST.md`，但只覆盖 MVP 任务） |
| `docs/KNOWN_LIMITATIONS.md` | ✓ | ✓ |
| `docs/IMPLEMENTATION_STATUS.md` | ✓ | ✓ |

---

## 8. 下一步建议（提交给用户裁决）

### 8.1 路线决策（必填）

| 选项 | 含义 | 推荐 |
|---|---|---|
| **路线 A 增量升级** | 保留 4 程序集 + `GridPos`/`BoardState` 命名；新增 `Starfall.Core/Map/` 子目录；逐 MAP 升级 | ✅ **首选** |
| 路线 B 完全重做 | 新建 6 程序集；废弃 `GridPos`/`BoardState`；重写 179 测试 | 不推荐 |
| 路线 C 混合 | 路线 A + 重命名 `GridPos` → `GridCoord`（破坏现有测试） | 不推荐 |

### 8.2 P0 任务包（路线 A）

如果批准路线 A，建议**第一批**授权以下任务包：

1. **`agent/map-00-bugfix-bfs-neighbor-order`**（0.5 天）：修复 `BFSPathfinder` 邻居顺序为 `North→East→South→West` + 更新相关测试断言。**这是正确性 bug，必须前置**。
2. **`agent/map-00-unblock-undo-restore-state`**（0.5-1 天）：Core 暴露 `BattleRunner.RestoreState(BattleState)` + `InputController.DoUndo` 实际生效。修复 MVP 已知技术债。
3. **`agent/map-01-grid-foundation`**（1 天）：按 doc2 MAP-01 + MAP-07 创建 `Starfall.Core/Map/Coordinates/` 子目录：`GridCoord`（含 `Layer`）、`DimensionLayer`、`GridMap<T>`、`GridDirection`；保留 `GridPos` 作为 legacy。
4. **`agent/map-06-line-of-sight`**（2-3 天）：doc2 MAP-06 完整视线 / 掩体 / 高度；战斗前置。
5. **`agent/map-08-phase-flip`**（2-3 天）：doc2 MAP-08 相位翻转 + 坠落 + 实体挤压；**核心玩法**。

### 8.3 风险点（必读）

- **Unity 版本决策**：doc2 §一.1 明示"当前唯一硬阻塞：Unity版本决策 U-A / §6.1(b)——MVP 是 6000.5.3f1，doc2 目标 6.3 LTS"。在用户完成 A/B/C 决策前，MAP 任务不得正式开始。
- **Task 02 全局前置**：doc2 §一.2 列出 12 项 Task 02 强制项（MVP 已完成大部分但 doc2 要求"BuildPipeline.BuildPlayer 验证" + "Assembly Definition 依赖测试"——MVP `CoreDependencyGuardTests` 4 项已覆盖 asmdef 依赖，但 **BuildPlayer 验证尚未在 CI 中跑**）。
- **第一轮授权顺序**：doc2 §九 强制"第一轮地图任务只能授权 MAP-01"。建议第一批只授权 `agent/map-00-bugfix-bfs-neighbor-order` + `agent/map-00-unblock-undo-restore-state` + `agent/map-01-grid-foundation`，其余 P0 等 MAP-01 验收后逐任务授权。

### 8.4 需用户决策的项

| 决策项 | 选项 | 建议 |
|---|---|---|
| 路线 A/B/C | 见 §5 | A |
| 是否修复 BFSPathfinder 邻居顺序 bug | 是 / 否 | **是**（正确性） |
| 是否修复 Undo 阻塞 | 是 / 否（接受 MVP 已知技术债） | **是** |
| Unity 版本（6000.5.3f1 vs doc2 目标 6.3 LTS） | A/B/C（doc2 §一.1） | 由 Lead 单独 follow-up |
| `GridPos` 保留 / 重命名为 `GridCoord` | 保留 / 重命名 | **保留**（向后兼容） |
| `MapState` 独立 / 嵌入 `BattleState` | 独立（doc2）/ 嵌入（route A 兼容） | **嵌入 + 适配器**（route A） |
| `battle_default.json` 是否改为 12×14 双层 `MAP_DEV_PHASE_TEST_001` | 是 / 否（先增 12×14 测试地图） | **新增**（不动现有） |
| `Assets/Editor/MVPPlayModeHelper.cs` 是否归入新 `Starfall.Editor` 程序集 | 是 / 否 | **是**（与 MAP-16 共用程序集） |

---

## 9. 附录：MVP 已实现的地图相关功能（详细列表）

> 用于与 doc2 对照，避免重复实现。

### 9.1 Core（已实现）

| 功能 | 文件 | 行 | 状态 |
|---|---|---|---|
| `GridPos`（X/Y + CompareTo + Equals + GetHashCode） | `Core/Model/GridPos.cs` | 49 | ✅ |
| `GridPosComparer` | `Core/Model/GridPosComparer.cs` | 14 | ✅ |
| `BoardState`（Width/Height + `Dictionary<GridPos, TileState>`） | `Core/Model/BoardState.cs` | 39 | ✅（单层） |
| `BattleState`（嵌入 Board + Units + Anchors + Decrees + Statuses + 哈希） | `Core/Model/BattleState.cs` | 230 | ✅ |
| `UnitState`（UnitId/Pos/Hp/MaxHp/Phase/Owner） | `Core/Model/UnitState.cs` | 27 | ✅ |
| `TileState` 枚举（Normal/Blocked/Hazard/Objective） | `Core/Model/Enums.cs` | 16 | ✅（5 值） |
| `Phase` 枚举（Light/Dark，单位相位） | `Core/Model/Enums.cs` | 同上 | ✅（**注意：与 doc2 `DimensionLayer` 是不同概念**） |
| `Owner` 枚举（Player/Enemy） | `Core/Model/Enums.cs` | 同上 | ✅ |
| `TileSnapshot`（Pos + State，Presenter 用） | `Core/Model/TileSnapshot.cs` | 36 | ✅ |
| `BattleStateCloner.Clone`（深拷贝，**不含 Statuses/Anchors/Decrees**） | `Core/Model/Cloner.cs` | 41 | ⚠️ 已知限制 |
| `BattleStateComparer`（Hash + 字段对比） | `Core/Model/Comparer.cs` | 64 | ✅ |
| `BattleState.PostStateHash`（FNV-1a 64 位链式） | `Core/Model/BattleState.cs` L90-180 | 90+ | ✅ |
| `ICommand` / `CommandExecutor` | `Core/Command/` | 15+15 | ✅（通用战斗） |
| `MoveCommand` / `AttackCommand` / `ApplyStatusCommand` / `RemoveStatusCommand` / `EndTurnCommand` / `TickEndTurnCommand` | `Core/Command/` | 各 30-80 | ✅ |
| `BFSPathfinder`（4 邻居 + (Y,X) 平局） | `Core/Pathfinding/BFSPathfinder.cs` | 75 | ⚠️ **邻居顺序错** |
| `IPathfinder` | `Core/Pathfinding/IPathfinder.cs` | 14 | ✅ |
| `AnchorRegistry` + `AnchorZone`（凸多边形 + `Contains`） | `Core/Anchor/` | 25+50 | 🟡（凸多边形 + 浮点除法） |
| `DecreeRegistry` + `Decree` + `ApplyDecreeCommand` | `Core/Decree/` | 100+ | ✅ |
| `StatusKind` / `StatusInstance` / `StatusInstanceComparer` | `Core/Status/` | 100+ | ✅ |
| `FallingCommand`（扣 HP + 发事件） | `Core/Rules/FallingCommand.cs` | 50 | 🟡（无合法落点） |
| `CrushResolver`（同格多单位扣 HP） | `Core/Rules/CrushResolver.cs` | 50 | 🟡 |
| `PhaseFlipValidator`（"单位本回合已 PhaseInvert 状态"） | `Core/Rules/PhaseFlipValidator.cs` | 26 | 🟡（仅状态限制，非翻转规则） |
| `CommandRecord` / `CommandRecorder` / `ReplayCodec` / `ReplayPlayer` / `ReplayFile` / `ReplayEntry` / `ReplayException` | `Core/Replay/` | 300+ | ✅ |
| `UndoStack`（深拷贝 push + pop） | `Core/Undo/UndoStack.cs` | 50 | ⚠️（InputController 调用但未生效） |
| `BattleOutcome` / `BattleRunner` / `EventSink` / `IEnemyAI` / `SimpleEnemyAI` / `ImprovedEnemyAI` / `DamageFormula` / `WinConditionChecker` / `ObjectivePhase` / `ObjectivePhaseUpdater` | `Core/Combat/` | 600+ | ✅ |

### 9.2 Data（已实现）

| 功能 | 文件 | 状态 |
|---|---|---|
| `BattleDefinition` / `BoardDefinition` / `UnitDefinition` / `StatusDefinition` / `TileEntry` | `Data/Definition/` | ✅ |
| `JsonBattleLoader` | `Data/Loading/JsonBattleLoader.cs` | ✅ |
| `BattleStateBuilder.Build`（定义 → 状态） | `Data/Loading/BattleStateBuilder.cs` | ✅ |
| `DefinitionValidator`（TurnNumber / Board / Tiles / Units + Task 19 字段） | `Data/Validation/DefinitionValidator.cs` | 🟡（缺 doc2 13.4 18 项检查） |
| `DefinitionException` | `Data/DefinitionException.cs` | ✅ |

### 9.3 Unity（已实现）

| 功能 | 文件 | 状态 |
|---|---|---|
| `BattleBootstrap`（自动加载 + 挂 Presenter/HUD/InputController） | `Unity/BattleBootstrap.cs` | ✅ |
| `RealBoardPresenter`（80 Quad + Capsule 单位 + LineRenderer 锚点 + 高亮层 + 3D 伤害数字） | `Unity/RealBoardPresenter.cs` | 🟡（单层 + 无 `TileView` 解耦） |
| `RealBattleHud`（uGUI 9 行） | `Unity/RealBattleHud.cs` | ✅ |
| `BoardSnapshot` / `HudSnapshot` / `UnitSnapshot` / `AnchorSnapshot` / `BoardPalette` / `LegalPreviewHelper` | `Unity/Presentation/` | ✅ |
| `IBoardPresenter` / `IBattleHud` / `IUnitPresenterRegistry` / `PresentationEvent` | `Unity/Presentation/` | ✅ |
| `StubBoardPresenter` / `StubBattleHud` | `Unity/Stub*.cs` | ⚠️ 已知 fallback（保留） |
| `InputController`（键盘 + 鼠标 + InputSystem） | `Unity/Input/InputController.cs` | 🟡（Undo 阻塞） |
| `InputStateMachine`（5 模式） | `Unity/Input/InputStateMachine.cs` | ✅ |
| `InputMode` / `InputAction` / `InputState` / `CommandBuilder` | `Unity/Input/` | ✅ |
| `BattleCameraAutoSetup` | `Unity/BattleCameraAutoSetup.cs` | ✅ |

### 9.4 Editor

| 功能 | 文件 | 状态 |
|---|---|---|
| `MVPPlayModeHelper.SetupScene`（菜单 + 命令行入口） | `Assets/Editor/MVPPlayModeHelper.cs` | ✅（**注意：没有独立 `Starfall.Editor` asmdef**） |

### 9.5 Tests（已实现）

| 测试集 | 测试数 | 内容 |
|---|---|---|
| `FoundationStateTests` | 12 | GridPos / BattleState / Cloner / Comparer |
| `CommandAndPathfinderTests` | 9 | ICommand + BFSPathfinder 确定性 |
| `CoreDependencyGuardTests` | 4 | Core 无 UnityEngine 引用（asmdef + using） |
| `DataLoadingTests` | 7 | JSON 加载 + 校验 |
| `BattleRunnerTests` | 9 | 回合 + AI + Outcome |
| `AnchorAndDecreeTests` | 8 | 锚点围区 + 律令 |
| `RulesTests` | 7 | 坠落 / 挤压 / 相位翻转 |
| `ReplayAndUndoTests` | 8 | Replay + Undo 确定性 |
| `ReplayCodecTests` | 6 | ReplayCodec 序列化 |
| `AttackAndAITests` | 8 | DamageFormula + AttackCommand + AI |
| `BattleSetupTests` | 4 | Bootstrap + JSON + Validator |
| `PresentationTests` | 15 | BoardSnapshot / AnchorSnapshot / BoardPalette |
| `HudAndPreviewTests` | 28 | LegalPreviewHelper / UnitSnapshot / HudSnapshot / BoardSnapshot |
| `InputStateMachineTests` | 32 | 模式状态机 + 键位解析 |
| `LevelLoopTests` | 12 | GuardsCompleted / Retreat / 胜负 / 确定性 |
| `StatusSystemTests` | 10 | StatusKind / ApplyStatus / RemoveStatus |
| **总计** | **179** | **0 失败 / 0 错误 / 0 警告（M-35 后）** |

### 9.6 PlayMode

| 文件 | 内容 |
|---|---|
| `Assets/Starfall/Tests/PlayMode/M35DemoScript.cs` | M-35 demo 自动化（模拟 M/A/F/D/Z/Space 键盘 + 捕获 hash/log） |

### 9.7 文档（已实现）

| 文档 | 路径 | 状态 |
|---|---|---|
| `01_Project_Overview_and_GDD.md` | `Docs/` | ✅ |
| `02_Technical_Development_Manual.md` | `Docs/` | ✅ |
| `03_Data_and_Content_Spec.md` | `Docs/` | ✅ |
| `04_Roadmap_and_Milestones.md` | `Docs/` | ✅ |
| `05_Test_and_Acceptance.md` | `Docs/` | ✅ |
| `IMPLEMENTATION_STATUS.md` | `docs/` | ✅ |
| `KNOWN_LIMITATIONS.md` | `docs/` | ✅ |
| `MANUAL_ACCEPTANCE_CHECKLIST.md` | `docs/` | ✅ |
| `OPENCLAW_REPOSITORY_AUDIT.md` | `docs/`（~3700 行审计历史） | ✅ |
| `ADR-0001-core-data-model-and-hash.md` | `docs/ADR/` | ✅ |
| `ADR-0002-presenter-sync-contract.md` | `docs/ADR/` | ✅ |

---

## 10. 结论

**一句话总结**：MVP 已经在 4 程序集 + 179 测试 + 80 格单层棋盘上完成 **战斗纵切**；doc2 要求的 18 个 MAP 任务中，3 个基本覆盖、8 个部分覆盖、7 个完全未实现。**推荐路线 A 增量升级**，预算 35-45 工作日（7-9 周，1 人）。**关键修复前置**：`BFSPathfinder` 邻居顺序 + Undo 阻塞 + `BattleStateCloner` 缺复制。

**提交**：本文档 `docs/MAP_SYSTEM_AUDIT.md`（1 文件 / 0 代码改动）。

**给 Lead**：等用户裁决 §8 决策项后，按 §8.2 P0 任务包顺序逐任务授权。