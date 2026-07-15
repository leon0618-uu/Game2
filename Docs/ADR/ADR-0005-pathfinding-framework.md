# ADR-0005: 寻路 / 通行性 / 移动范围 / 移动配置框架（A* + Passability + Range + Profile）

- **状态**：**Accepted**（2026-07-15，MAP-05 gameplay commit `47f2e76`）
- **日期**：2026-07-15
- **作者**：xingyuan-gameplay（与 xingyuan-architect 联审）
- **关联任务包**：MAP-05（Task 21-F）
- **关联文档**：
  - [ADR-0001](./ADR-0001-core-data-model-and-hash.md) — Core FNV-1a 64 位哈希基础
  - [ADR-0003](./ADR-0003-map-state-hash.md) — MapState 哈希与字段字节序（MapStateHasher）
  - [ADR-0004](./ADR-0004-map-command-framework.md) — MapCommand 框架（16 个 commands）
  - [MAP_SYSTEM_FORWARD_PLAN](../../Docs/MAP_SYSTEM_FORWARD_PLAN.md) §3.6 route A 严格 scope

## Context

MAP-04 已经接入 `TileDefinition` + `TileDefinitionRegistry` + `TileOccupancyService`，
MAP-06 接入了 `HeightLevel` + `HeightTraversalService`，
MAP-07 接入了 `PhasePairTileId` + 双层 `MapTileState`。
但仍缺少**统一的移动决策服务**：

1. **不可寻路**：指挥单位时无法知道 A → B 的最短路径；
2. **不可通行判定**：无法判定某 tile 是否允许某单位进入（Footprint / 高度 / 占用 / 阻挡 / 跨层 / 区域）；
3. **不可移动范围**：HUD 无法高亮 "本回合 AP 用尽能到达的 tile"；
4. **移动配置散乱**：MAP-06 已定义 `Height.MovementProfile`，未来 5 个服务互不相通。

现有 MVP 路径：

- `Assets/Starfall/Core/Pathfinding/BFSPathfinder.cs`：仅 4 邻居 BFS，使用旧 `BoardState` + 旧 `TileState`，
  仅判定 `TileState.Blocked`，**不接** `TileDefinition` / `TileOccupancyService` / 高度 / 跨层。
- 总计 179 个 baseline EditMode 测试引用 `BFSPathfinder`（<c>CommandAndPathfinderTests.cs</c>）。

## Decision

### 1. 服务边界

新 5 个公共 API（全部在 `Assets/Starfall/Core/Map/Pathfinding/`，命名空间 `Starfall.Core.Map.Pathfinding`）：

| 类 / 类型 | 角色 | 关键 API |
| --- | --- | --- |
| `MapMovementProfile`（readonly struct） | 寻路用移动配置 | `Standard` / `Flyer` / `Heavy` |
| `MapPath` | 寻路结果（成功 / 失败） | `From` / `Null` + `PathFailure` 常量 + `RiskTags` |
| `PathfindingService`（static class） | A* 寻路 | `MapPath FindPath(MapState, GridCoord start, GridCoord goal, MapMovementProfile)` |
| `MapPassabilityService`（static class） | 单 tile 通行性 | `PassabilityResult CanEnter(MapState, from, to, profile, footprint)` |
| `MovementRangeService`（static class） | AP 内可达 tile | `IReadOnlyList<GridCoord> GetReachableTiles(MapState, origin, profile)` |
| `PassabilityResult`（readonly struct + `RejectionCode` enum） | 通行性结果 | `IsPassable` + `Reason` |

**保留**：`Assets/Starfall/Core/Pathfinding/BFSPathfinder.cs`
（向后兼容 179 个 baseline 测试；新代码统一使用 `PathfindingService`）。

### 2. A* 与 BFS 的边界

`PathfindingService`（A*）和 `BFSPathfinder`（BFS）的明确分工：

- `PathfindingService`：**doc2 标准寻路**（MAP-05 §9.4）。
  支持：tile 阻挡 / 占用 / 高度差 / 跨层 / 危险地形 / 单位移动成本；
  输出 `MapPath` 含 `PathFailure` 失败原因 + `RiskTags` 风险标签。
- `BFSPathfinder`：**MVP 兼容**，仅判定 `TileState.Blocked`；
  保留以满足既有 `CommandAndPathfinderTests`（旧）测试，
  等待后续 MAP-14 重构时统一迁移至 `PathfindingService`。

未来 MAP-13 `MapPathRepository`、MAP-14 寻路可视化、MAP-15 重押规则应**全部基于**
`PathfindingService.FindPath`，不再扩展 `BFSPathfinder`。

### 3. 邻居顺序契约

四个边界共享的邻居顺序：**N → E → S → W**（上、右、下、左）。

| 位置 | 来源 |
| --- | --- |
| `GridCoord.Neighbours()` | `Assets/Starfall/Core/Map/Coordinates/GridCoord.cs:73` |
| `PathfindingService.Neighbours` | 同上（手动复制到局部数组） |
| `BFSPathfinder.Neighbors` | 同上 |
| `LineOfSightService` 等 | LOS 邻居顺序由 `GridDirection` 枚举决定（同样按 NESW） |

任何修改邻居顺序都会**破坏 766 baseline 测试的确定性**；
bug 修复记录于 git commit `5cc4644`（MAP-01 阶段）。

### 4. A* Tie-break 规则

openSet 在 F 值相等时按 (H, Y, X, Layer) 升序排序：

1. 启发式值 H（升）—— 倾向接近 goal 的节点；
2. Y（升）—— North 优先（与邻居顺序 N→E→S→W 一致）；
3. X（升）—— East 优先；
4. Layer byte（升）—— `Reality = 1 → Astral = 2`。

```csharp
// 关键比较函数（PathfindingService.OpenEntryComparer）
int c = x.F.CompareTo(y.F);
if (c != 0) return c;
c = x.H.CompareTo(y.H);
if (c != 0) return c;
c = x.Coord.Y.CompareTo(y.Coord.Y);  // Y ascending
if (c != 0) return c;
c = x.Coord.X.CompareTo(x.Coord.X);  // X ascending
if (c != 0) return c;
return ((byte)x.Coord.Layer).CompareTo((byte)y.Coord.Layer);
```

回溯路径时**强制回溯起点**，保证 `path[0] == start` + `path[path.Count - 1] == goal`，
顺序与 `GridCoord.CompareTo` 一致。

### 5. `MapMovementProfile` 三种预设语义

| 预设 | CanFly | MaxAscend / MaxDescend | CanCrossDimension | MaxMovementPoints | 典型用途 |
| --- | --- | --- | --- | --- | --- |
| `Standard` | false | 1 / 2 | false | 6 | 玩家步兵（默认） |
| `Flyer` | true | 0 / 0（由 CanFly 短路） | true | 6 | 飞行敌人 / 飞行单位 |
| `Heavy` | false | 0 / 1 | false | 4 | 重装 / 机甲方舟（不能爬梯） |

`MaxMovementPoints` **不参与 A***——A* 总返回全局最短路径；
AP 限制是 `MovementRangeService.GetReachableTiles` 的职责。
二者解耦避免"AP 不够 → A* 改路径"导致 Replay 不一致。

### 6. Passability 拒绝原因优先级与报告格式

校验链（首失败即终止，AGENTS.md §11 强制确定性）：

1. `TileDefinition.BlocksMovement`（MAP-04）→ `BlockedByTile`
2. 占用 / 越界 / 坍塌（`TileOccupancyService.IsCellPassable`）→ `BlockedByUnit` / `BlockedByTile`
3. 高度差（`HeightTraversalService.CanTraverse`）→ `BlockedByHeightDelta`
4. 跨层（`from.Layer != to.Layer` 且 `!profile.CanCrossDimension`）→ `BlockedByPhase`
5. 区域（MAP-09 占位）→ `BlockedByRegion`
6. 移动成本 > AP（占位）→ `InsufficientMovement`

报告格式：`PassabilityResult` readonly struct：

```csharp
public readonly struct PassabilityResult {
    public readonly RejectionCode Reason;     // 0..6
    public readonly GridCoord     FailedCoord;
    public readonly int           OccupantId;
    public readonly HeightLevel   FromHeight, ToHeight;
    public readonly DimensionLayer FromLayer,  ToLayer;
    public bool IsPassable => Reason == RejectionCode.Pass;
}
```

这允许 Presenter 直接高亮 `FailedCoord` 并展示拒绝原因，无需再比对多个 bool 值。

### 7. `MapPath` 失败码 → 业务映射

| `PathFailure` 常量 | 触发条件 | 业务语义 |
| --- | --- | --- |
| `NoPath` | A* 探索完所有可达邻居仍未命中 goal | 地图不可达，UI 红框 |
| `GoalBlocked` | goal 越界 / 阻挡 / 占用 / 坍塌 | 不能站在那；重新选点 |
| `StartOccupied` | start 越界 / `BlocksMovement = true` | 起点非法；可能是 bug |
| `Unreachable` | start == goal 但 tile 不可落座 / 跨层错配 | 单位已在目标，但是"假落座" |

### 8. 不在范围（明确拒绝实现）

- **不**修改 `Assets/Starfall/Unity/`、`Assets/Starfall/Data/`（架构硬约束，AGENTS.md §10.1）
- **不**修改 `Packages/manifest.json`、`ProjectSettings/*`、`memory/`
- **不**删除 `BFSPathfinder`（向后兼容 179 baseline 测试）
- **不**实现完整 `ConstellationPolygonService`（MAP-12 后续）
- **不**接 Unity Presenter 寻路可视化（MAP-14 后续）
- **不**接 JSON `MapPathRepository`（MAP-13 后续）

### 9. 与既有 `Height.MovementProfile` 的共处

MAP-06 已定义 `Starfall.Core.Map.Height.MovementProfile`
（字段：`CanFly` / `MaxAscend` / `MaxDescend` / `CanCrossDimension`）。
MAP-05 新定义 `Starfall.Core.Map.Pathfinding.MapMovementProfile`
（字段：`MaxAscendHeight` / `MaxDescendHeight` / `CanFly` /
`CanCrossDimension` / `MaxMovementPoints`）。

**两者并存不冲突**（不同命名空间 + 不同字段语义）：
- `Height.MovementProfile` 面向高度差判定；
- `MapMovementProfile` 面向寻路可达范围。

`MapPassabilityService` 内部把 `MapMovementProfile` 的字段映射到既有
`Height.MovementProfile` 后再调用 `HeightTraversalService.CanTraverse`，
保留 MAP-06 已通过的所有测试。

## Consequences

### 优点

1. **可寻路**：UI / AI 可调用 `PathfindingService.FindPath`；
2. **可单步判定**：`MapPassabilityService.CanEnter` 支持 footprint 跨格校验；
3. **可移动范围**：`MovementRangeService.GetReachableTiles` 给出 BFS 可达集合；
4. **可读失败**：`PassabilityResult` + `MapPath.PathFailure` 业务含义明确。

### 代价

1. 新增 1 个 readonly struct (`MapMovementProfile`) 与现有
   `Height.MovementProfile` 字段语义不同——命名差异是**有意为之**，
   避免静默替换既有测试 fixture；
2. A* 在无 registry 状态下使用默认 cost = 1 兜底；测试需显式
   `AttachTileDefinitionRegistry` 才能覆盖 cost 路径。

### 验证

- 766 baseline EditMode 测试不破；
- 新增 ≥ 56 测试覆盖（A* / Passability / Range / Profile / MapPath / TaskId）；
- BFSPathfinder 在 4 邻居简单无障碍场景下与 A* 输出一致（spot-check 5 个）；
- `MapMovementProfile.Heavy` 不允许 `MaxAscendHeight = 0 + MaxDescendHeight = 1` 等边界由
  参数校验抛 `ArgumentOutOfRangeException` 强制。

## Alternatives Considered

1. **删除 BFSPathfinder 改成 A* 单一实现** ——
   拒绝：会破坏 179 个 baseline 测试；按 AGENTS.md §7 "每轮只允许一个任务包" 应保留。
2. **用 `Starfall.Core.Model.GridPos` 作为 A* 输入** ——
   拒绝：doc2 标准是 `GridCoord`；`GridPos` 仅用于 MVP Replay / Undo，
   二者共存由 ADR-0001 + MAP-01 §4.1 已说明。
3. **把 `MapMovementProfile` 重命名为 `MovementProfile` 并替换既有 `Height.MovementProfile`** ——
   拒绝：会触发 MAP-06 (`HeightTraversalTests.cs`) 全面修改；超出 MAP-05 scope。
4. **直接用 LINQ 实现 MovementRange BFS** ——
   拒绝：LINQ 顺序非确定；AGENTS.md §11 + ADR-0001 要求确定性。
