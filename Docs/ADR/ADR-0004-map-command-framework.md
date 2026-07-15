# ADR-0004: MapCommand 框架（IMapCommand / MapCommandResult / MapEvent / 16 个 commands）

- **状态**：**Accepted**（2026-07-15，MAP-03 gameplay commit `cbd7b47`）
- **日期**：2026-07-15
- **作者**：xingyuan-gameplay（与 xingyuan-architect 联审）
- **关联任务包**：MAP-03（Task 21-D）
- **关联文档**：
  - [ADR-0001](./ADR-0001-core-data-model-and-hash.md) — Core FNV-1a 64 位哈希基础
  - [ADR-0003](./ADR-0003-map-state-hash.md) — MapState 哈希与字段字节序（MapStateHasher）
  - [MAP_SYSTEM_FORWARD_PLAN](../../Docs/MAP_SYSTEM_FORWARD_PLAN.md) §3.2 route A 严格 scope

## Context

MAP-02 已经接入 `MapState` 作为地图运行时唯一真相源；但缺乏 **修改入口** ——
任何对 `MapState` 集合 / Version / `GlobalCollapseValue` / 运行时常量（Phase flip / Anchor state /
MapTileState.Stability 等）的变化都需要一条**统一的命令通道**。

MAP-03 之前的状态（MAP-08 阶段）：
- `IMapCommand` 是 stub，仅 `Execute(MapState)` 一个方法；
- `MapCommandResult` 只带 `AffectedTiles`（`IReadOnlyList<GridCoord>`）+ `FailureReason`；
- 没有事件通道、没有版本自增、没有依赖声明、没有 `Undo` 入口。

问题：

1. **不可审计**：调用方无法知道某次命令在哪个版本施加了什么副作用；
2. **不可撤销**：MVP 必须支持"Replay + Undo"（doc2 §21.1），但 Flip 命令在 MAP-08 阶段没有 Undo 入口；
3. **不可组合**：复杂场景（"先 create anchor → modify state → invalidate LOS"）缺乏显式
   依赖声明；
4. **不可扩展**：未来 14 类命令（doc2 §21.1）没有共同的事件总线，下游订阅
   只能 hardcode 各个命令类。

## Decision

### 1. 接口边界（与 `ICommand` 平行）

```
Starfall.Core.Command.ICommand            Starfall.Core.Map.Commands.IMapCommand
        │                                          │
        │ Battle state (HP / 状态 / 回合)          │ Map state (tile / anchor / LOS / path / CV)
        │ BattleEvent (战斗事件流)                │ MapEvent    (地图事件流)
        ▼                                          ▼
CommandExecutor (run-only,                 MapCommandExecutor
                  单次 history)             (Run + UndoLast + Dependency check)
```

**关键约束**：
- 两个接口、两个 executor、两个事件 struct 名字相近但**互不引用**；
- `BattleRunner` 可同时持有一个 `BattleCommandExecutor` 和一个 `MapCommandExecutor`，
  按事件来源分别 dispatch；
- 任何命令实现必须**自包含**：依赖（attach 模式字典 / 运行时 state 字典）必须显式 attach
  by caller；不在命令内部隐式建立。

### 2. `IMapCommand` 完整契约

```csharp
public interface IMapCommand {
    MapCommandResult Execute(MapState mapState);
    void             Undo(MapState mapState);
    int              Version { get; }              // 命令自身契约版本（不在 MapState.Version 作用域）
    string           CommandId { get; }           // 稳定标识 {type}:{summary}
    IReadOnlyList<string> Dependencies { get; }   // 显式声明其他 CommandId 依赖（升序）
}
```

**失败语义硬约束**：
- `Execute` 内部失败 → 返回 `Fail(reason)`；**禁止抛异常**（保持与 MAP-08 stub 一致 + 简化 executor 逻辑）。
- `Execute` 必须在任何写操作**之前**完成所有校验，保证 Fail 路径下 `mapState` 完全不变。
- `Undo` 不支持时抛 `NotSupportedException`（executor 不 catch，业务可决定是否回退）。

### 3. `MapCommandResult` 与事件

```csharp
public readonly struct MapCommandResult {
    public readonly bool Success;
    public readonly string FailureReason;
    public readonly IReadOnlyList<MapEvent> Events;   // 多事件 stable 排序
    public readonly int NewVersion;                    // success 时 = mapState.Version + 1
}
```

**兼容性策略**：保留 `AffectedTiles` 作为**派生视图**（过滤 `Events` 中
`OnTileChanged` 类型的 `Coord`）—— 这样 MAP-08 stub 已有测试无需修改即可继续 PASS。

### 4. `MapEvent` 稳定排序契约

| 字段顺序（写入） | 排序键 |
| --- | --- |
| Kind（byte） | 1 |
| Coord（Y, X, Layer，若有） | 2 |
| RegionId | 3 |
| AnchorId | 4 |
| LinkId | 5 |
| OldValue | 6 |
| NewValue | 7 |
| Description（StringComparer.Ordinal） | 8 |

**与 ADR-0003 hash 一致性**：事件本身**不直接进 MapState 哈希作用域**（不在 `MapStateHasher`
字节流中），但事件的 `Coord` / `RegionId` / `AnchorId` 通过 map state **集合层**反映
——`OnTileChanged` 对应 `TileDefinition` 在 Registry 中的字段变化（hash 见 `TagTiles`），
`OnRegionChanged`/`OnAnchorLinkCreated` 对应 `Anchors` / `Regions` 集合变化（hash 见
`TagAnchors` / `TagRegions`）。这保证了 Replay 后 "相同事件序列 → 相同 hash" 的根等式。

### 5. `MapCommandExecutor` 行为

- `Run(IMapCommand, MapState)`：
  1. 检查 `cmd.Dependencies` ⊆ `ExecutedCommandIds`；不满足则 `Fail("missing dependency: ...")`，
     **不调用 Execute**。
  2. 调 `Execute` → 失败时返回 `Fail(reason)`，mapState 不变。
  3. 成功：`mapState.Version = cmd.NewVersion`（命令实现负责计算正确 Version）；
     push `HistoryEntry{cmd, mapState.Version}`；add `cmd.CommandId` 到 `ExecutedCommandIds`。
  4. 维护 `MaxHistoryDepth`（默认 50）：超限时弹最旧条目 + 从 `ExecutedCommandIds` 移除其 CommandId。
- `UndoLast(MapState)`：
  - history 为空 → 返回 false。
  - 否则 pop → 调 `cmd.Undo(mapState)` → 把 `mapState.Version` 恢复到 `HistoryEntry.PreviousVersion` →
    从 `ExecutedCommandIds` 移除该 CommandId。

**线程模型**：非线程安全；`BattleRunner` 单线程顺序调用即可。

**版本号策略**：
- `MapState.Version` 是单调递增整数，每次成功执行后 `+1`。
- 命令实现自身的 `Version` 字段与"实现契约版本"绑定（如 `FlipTilePhaseCommand.Version = 2` 是
  MAP-03 完整化时升级一档的标记），**与** `MapState.Version` **不同**，不进入 map state 哈希作用域。

### 6. 16 个 Map commands 依赖图

> 单格 / 独立命令：dependencies = 空。
> 复合 / 顺序命令：dependencies = 一组 `CommandId` 升序列表。

```text
  flip-tile-phase:{id}                  (独立)
  flip-region-phase:{anchor-id}         (独立)
  transform-tile:{id}                   (独立)
  set-tile-stability:{id}               (独立)
  modify-global-cv                      (独立)
  create-anchor-link:{zone-id}  ───┐
  create-constellation-area:{id}     │ （独立）
  modify-anchor-state:{zone-id}   ◀──┘  depends-on create-anchor-link:{zone-id}
  set-map-debug-value:{key}            (独立；要求 mapState.EnableDevTestMode())
  invalidate-path-graph                (独立；纯事件)
  invalidate-line-of-sight             (独立；纯事件)
  place-map-object:{object-id}         (独立)
  remove-map-object:{object-id}        (独立)
  move-unit-on-map:{unit-id}:{to}      (独立；并行 BattleRunner.MoveCommand)
  compress-phase:{coord}               (独立；包装 PhaseCompressionResolutionService)
  decompress-phase:{unit-id}:{to}      (独立；与 CompressPhaseCommand.Undo 区分)
```

后续可能的 chain（未在 MAP-03 实施）：
- `modify-anchor-state` + `invalidate-line-of-sight` 串行（anchor 状态变化触发 LOS 重算）；
- `transform-tile` + `invalidate-path-graph` 串行（地形变化触发路径图失效）；
- `compress-phase` + `invalidate-line-of-sight` 串行（挤压可能改变覆盖判定）。

### 7. 命令实现契约（MUST 列表）

每个 `IMapCommand` 实现必须：

1. **构造时严格校验输入**（如 `TileId < 1` 抛 `ArgumentException`），**不允许延迟到 Execute**。
2. **执行时先全部校验再写**——保证 Fail 路径完全不改 mapState。
3. **记录 undo 所需的最小状态**（如 prev layer / prev stability / prev occupancy）。
4. **Emit 事件按稳定顺序**（`Events.Sort()` on `MapEvent.CompareTo`）。
5. **`Undo` 严格反向 + 清零已执行标志**——避免二次重复 Execute 后 Undo 误回滚。
6. **`CommandId` 包含关键 scope**（如 `transform-tile:{tileId}`）以满足依赖判定。
7. **不引用 `UnityEngine` / `UnityEditor`**（AGENTS.md §10.1）。
8. **不读时间 / 线程 / 实例地址**（AGENTS.md §11 确定性）。

### 8. 服务挂载模式（attach-mode singleton）

为了避免修改 `MapState` 字段集，新增两个静态挂载服务（与
`PhaseFlipStateService` 同模式）：

- `Starfall.Core.Anchor.AnchorStateService`：持有 `Dictionary<MapState, Dictionary<int, AnchorZoneState>>`。
- `MapState.DebugValues`（字典）：由 `DevTestModeEnabled` 开关保护 ——
  仅 `SetMapDebugValueCommand` 在 `EnableDevTestMode()` 模式下可写。

每个 service 必须提供 `Attach(map) / Detach(map) / Clear()` 三件套，
以配合 NUnit `[SetUp]` / `[TearDown]` 隔离。

## Consequences

### 正面

- **Replay 校验可行**：16 命令 + 14 服务依赖 (`PhaseFlipStateService` +
  `AnchorStateService`) 都显式声明 → Replay 后状态可重放；
- **Undo 链可组合**：16 命令各自实现 `Undo` → executor 可一条条弹栈；
- **事件总线统一**：Presenter / Debug UI / Scenario manager 订阅 `MapEvent` 即可感知所有变化；
- **测试隔离干净**：attach-mode services + `MapState.EnableDevTestMode()` 让 test 代码不污染生产路径；
- **MAP-08 stub 兼容**：保留 `AffectedTiles` 派生视图 + `Ok(List<GridCoord>, int)` overload，
  不破坏 669 baseline EditMode 测试。

### 负面

- **命令实现必须自己持有 undo 状态**：每个 `IMapCommand` 类需要 `private bool _executed` +
  `_previousXxx` 字段，对 *immutable* 风格的 struct 类是个微小让步；
- **`mapState.Version` 元数据被命令实现约定为 `previous + 1`**：若命令实现错算版本号
  会让 executor 与命令实现间出现不一致；目前通过
  "executor 信任 `result.NewVersion`，只 fallback 到 `mapState.Version + 1`" 兜底；
- **`AnchorZone` 状态走 side-channel**（`AnchorStateService` 字典）：与 `PhaseFlipState` 早期
  `dict` 模式相同，未来若引入"PerZone 字段"需要回炉修改 `AnchorZone`。

### 后续关注

- [MAP-09] ConstellationPolygonService —— 让 `CreateConstellationAreaCommand` 升级为完整算法；
- [MAP-10] MapObjectStateMachine —— 让 `PlaceMapObjectCommand` 支持 12 类状态机；
- [MAP-13] JSON `MapCommandRepository` —— 反序列化 + 应用于 mapState；
- [MAP-14] Unity Presenter 绑定 —— `BattleRunner` 集成 `MapCommandExecutor`；
- [MAP-15] Replay 一致性 —— Replay 命令 + 校验 `PostStateHash` 一致；
- [MAP-XX] 复合 execute 链（业务场景下多个命令的原子提交）：是否在 `MapCommandExecutor`
  引入 `RunBatch(...)` API 待评估。

## 测试矩阵

| 命令 | Happy | Failure 1 | Failure 2 | 事件 |
| --- | --- | --- | --- | --- |
| `FlipTilePhase`  (MAP-08 stub) | ✓ | ✓ not flippable | ✓ phase locked | ✓ 1+1 cascade |
| `FlipRegionPhase` (MAP-08 stub) | ✓ | ✓ already target | ✓ phase locked in region | ✓ N events sorted |
| `TransformTile` | ✓ | ✓ pair self | ✓ pair not found | ✓ OnTileChanged |
| `SetTileStability` | ✓ | ✓ out of range | ✓ occupied + 0 | ✓ OnTileStabilityChanged old/new |
| `ModifyGlobalCV` | ✓ | ✓ out of range | ✓ no-op same value | ✓ OnGlobalCVChanged old/new |
| `CreateAnchorLink` | ✓ | ✓ dup zone id | ✓ bad owner | ✓ OnAnchorLinkCreated + OnRegionChanged |
| `CreateConstellationArea` | ✓ | ✓ dup region id | ✓ OOB tile | ✓ OnConstellationPolygonCreated |
| `ModifyAnchorState` | ✓ | ✓ anchor not found | ✓ no-op same state | ✓ OnRegionChanged |
| `SetMapDebugValue` | ✓ | ✓ dev test off | ✓ duplicate key OK | ✓ OnMapDebugValueChanged |
| `InvalidatePathGraph` | ✓ | n/a | n/a | ✓ OnPathGraphInvalidated |
| `InvalidateLineOfSight` | ✓ | n/a | n/a | ✓ OnLineOfSightInvalidated |
| `PlaceMapObject` | ✓ | ✓ dup object id | ✓ OOB anchor | ✓ OnMapObjectPlaced |
| `RemoveMapObject` | ✓ | ✓ object not found | (n/a) | ✓ OnMapObjectRemoved |
| `MoveUnitOnMap` | ✓ | ✓ not on from | ✓ target occupied | ✓ 2 OnTileChanged + 1 OnUnitMovedOnMap |
| `CompressPhase` | ✓ | ✓ < 2 units | ✓ no free neighbor | ✓ 2 OnTileChanged + 1 OnPhaseCompressed |
| `DecompressPhase` | ✓ | ✓ not on from | ✓ target occupied | ✓ 2 OnTileChanged + 1 OnPhaseDecompressed |

`MapCommandExecutor` 测试（10+）：
Run 成功 / Run 失败 / UndoLast 成功 / UndoLast 空历史 / Version 自增 / Dependencies 通过 / Dependencies
拒绝 / 多命令顺序 / 历史深度限制 / CommandId 重新加入 Dependency 失败。

`MapCommandIntegration` 测试（8+）：
链式 Run → hash 一致 → 再 Run → undo 链 → reset。

总计 ≥ 43 个新测试 + 16 个 `Map03_TaskId_AssertedString` 验收 ID 测试。
