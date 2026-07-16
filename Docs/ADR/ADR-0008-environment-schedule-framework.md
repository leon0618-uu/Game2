# ADR-0008: EnvironmentSchedule 框架（MAP-11b）

- **状态**：**Accepted**（待用户裁决；当前实现已通过 1186/1186 EditMode PASS 保留）
- **日期**：2026-07-16
- **作者**：xingyuan-gameplay
- **关联任务包**：MAP-11b `agent/map-11b-env-schedule`
- **关联文档**：
  - 扩展 [ADR-0003](./ADR-0003-map-state-hash.md)（MapState 哈希协议 — 新增 tag 0x38 / 0x39 / 0x3A + 子标签 0xC0..0xCB）
  - 扩展 [ADR-0004](./ADR-0004-map-command-framework.md)（IMapCommand 接口；4 个新命令）
  - 扩展 [ADR-0007](./ADR-0007-collapse-value-framework.md)（与 MAP-11a CV / LocalCV / GlobalCV / TileStability 联动接口）
  - 扩展 [ADR-0006](./ADR-0006-map-region-framework.md)（与 MAP-09 Region 状态机联动接口）
  - 规范来源：[MAP_SYSTEM_FORWARD_PLAN §3.5](../../Docs/MAP_SYSTEM_FORWARD_PLAN.md)
  - 路线依据：[MAP_SYSTEM_AUDIT §3.3](../MAP_SYSTEM_AUDIT.md)
- **基线**：main HEAD `d792e29`（MAP-11a 已 merge；本 ADR 在其基础上非破坏性扩展）

---

## Context

MAP-11a 引入完整的 Corruption Value（CV）核心框架（5 阶段 + 双层 CV + 4 等级预警 + 3 内部 command）；
MAP-09 引入完整的 MapRegion 框架（14 种 region + 8 态状态机 + 单步 TransitionRegionStateCommand）；
MAP-03/04 引入 `IMapCommand` + `MapCommandExecutor` 统一执行栈。

MAP-11b 引入 **EnvironmentSchedule** —— 一个 10 步固定顺序的时间表框架，把上述
3 套系统的副作用按"时间表"维度编排：

1. **10 步固定顺序**：把"延迟机关 → 持续效果 → 局部 CV → 全局 CV → 地块状态 → 坠落 →
   区域激活 → 增援点 → 地图事件 → 预警"作为 1 个固定时间表，由 `EnvironmentPhaseResolver`
   按 phase 0..9 严格执行；
2. **每阶段 1 语义**：每个 phase 只处理 1 类环境副作用（局部 CV / 全局 CV / TileStability /
   Region 状态转移等），不允许跨 phase 顺序重叠；
3. **可注入 + 可撤销**：通过 4 个新 IMapCommand（ScheduleEnvironmentCommand /
   TickEnvironmentCommand / InjectEnvironmentEventCommand / ClearEnvironmentScheduleCommand）
   把 schedule 与 ActiveMap 状态同步；
4. **Reactive 预警**：phase 9 调 `CollapseWarningService` 评估当前 GlobalCV，
   Emit `OnAnomalyDetected` 事件并由未来 MAP-14 接入 HUD。

本 ADR 预留 Schedule JSON 序列化接口（MAP-13 后续）、`MAP_DEV_PHASE_TEST_001`
单测增强入口（MAP-17 后续）。

---

## 决策（Decisions）

### D1. 10 步固定顺序（不可换序）

```
phase 0  DeferredTriggers  (延迟机关)        — 发射到期延迟事件
phase 1  ContinuousEffects (持续效果)        — 每回合 tick 推进 GlobalCV
phase 2  LocalCollapseValue (局部 CV)        — LocalDamage 累积到指定 tile
phase 3  GlobalCollapseValue (全局 CV)       — GlobalCVDelta 累积偏移
phase 4  TileStability (地块状态)            — TileStabilityChange / TileReconstruct
phase 5  Falling (坠落)                       — FallTrigger（= Tile 强制 Collapsing）
phase 6  RegionActivation (区域激活)         — RegionActivation（= RegionState.Active）
phase 7  ReinforcementSpawn (增援点)         — PlaceMapObjectCommand stub
phase 8  MapEvent (地图事件)                  — MapEventRecord（任意描述性事件）
phase 9  WarningEmitted (预警)               — CollapseWarningService + OnAnomalyDetected
```

**不可换序的理由**（AGENTS.md §11 + doc2 §15.1）：

- **Phase 0 在前**：延迟机关的发射必须在其它阶段前，否则下游 phase 看到的"已累积状态"会缺一段
  （如 phase 2 累积 LocalCV 时如果 phase 0 没把"上一次延迟发射"的 +N 量加进来，结果会失真）。
- **Phase 1/2 在前**：持续效果 + 局部 CV 是"基础量累积"，必须先完成增量计算；后续 phase（4/5/6）
  按"已累积到的 LCV / GlobalCV"作决策。
- **Phase 3 在前**：全局 CV 必须先到位，后续 phase 4（TileStability 决策）的"派生 stability"
  才能基于"最新 GlobalCV"决定是否进入 Anomalous / Fracturing 区段。
- **Phase 4 在 Phase 2/3 后**：tile stability 的变化需要"已知 LCV 是否 ≥ 50"、"已知 GlobalCV 是否 ≥ 40"
  才能 emit OnTileFractured 事件。如果 phase 4 提前到 phase 2 前，会 emit 不确定事件。
- **Phase 5 在 Phase 4 后**：坠落本质是"强制 tile 进入 Collapsing"。
  这种"强制赋值"必须在 tile stability 已稳定（phase 4 末尾）后才能执行，否则会与 phase 4 中
  用户/AI 主动设置的 stability 冲突。
- **Phase 6 在 Phase 5 后**：区域激活决策依赖"tile 是否被坍塌 / 是否在新位置"等状态信息；
  phase 5 完成了 tile 物理位置 / stability 变化后，phase 6 才能基于真实物理状态做 region 状态转移。
- **Phase 7 在 Phase 6 后**：增援点意味着"在 region 激活后才有 spawn"。
  如果 phase 7 提前，spawn 的"目标 region"可能还未注册到 MapState 上 → PlaceMapObjectCommand 失败。
- **Phase 8 在中间**：MapEventRecord 是纯描述性事件，作为"日志带"插在 Phase 7/9 之间，
  既不影响核心状态机，又能在预警前提供"上下文描述"。
- **Phase 9 在末**：预警 = "基于上述所有 phase 的累积结果 + GlobalCV 阈值"评估；
  必须在所有副作用都执行完才能 emit，避免"提前预警"漏算下游增量。

**顺序固化**：`EnvironmentPhaseIndex` 字节位序严格 `0..9`；禁止重排或跳号（AGENTS.md §11）。
`EnvironmentPhaseResolver.ExecuteAll(mapState, schedule)` 强制按 0..9 顺序循环。

**同 phase 内顺序**：由 schedule.Events 在构造时（`MapEnvironmentSchedule.FromEvents`）的
"按 phase 排序"决定；用户输入顺序作为 tie-break（同 phase 内按 List.Sort 稳定性保留）。
**不允许**用户在 schedule 内显式指定"phase 7 在 phase 4 之前" ——
`ValidateSchedule` 返回非 0 时拒绝执行。

### D2. EnvironmentEventKind 10 种（与 phase 1:1 映射）

| Kind | Phase | 工厂 | 用途 |
|---|---|---|---|
| `DeferredTrigger`     | 0 | `DeferredTrigger(coord, delayTicks, [triggerTick])` | Phase 0：延迟发射的延迟机关 |
| `LocalDamageAmount`   | 1/2 | `LocalDamage(coord, amount)` | Phase 1/2：调用 `CollapseValueService.ApplyLocalDamage` |
| `GlobalCVDelta`       | 3 | `GlobalCVShift(delta, [triggerTick], [tag])` | Phase 3：累积到 `MapState.GlobalCV` |
| `TileStabilityChange` | 4 | `TileStabilityChange(coord, newStabilityByte)` | Phase 4：调用 `CollapseTileCommand` 拆分逻辑 |
| `TileReconstruct`     | 4 | `TileReconstruct(coord)` | Phase 4：调用 `ReconstructTileCommand` 拆分逻辑 |
| `FallTrigger`         | 5 | `FallTrigger(coord)` | Phase 5：把 LCV 强制设为 80（Collapsing） |
| `RegionActivation`    | 6 | `RegionActivation(regionId, [triggerTick])` | Phase 6：调 `TransitionRegionStateCommand` |
| `ReinforcementSpawn`  | 7 | `ReinforcementSpawn(spawnId, coord)` | Phase 7：调 `PlaceMapObjectCommand` stub |
| `MapEventRecord`      | 8 | `MapEvent(description, [triggerTick])` | Phase 8：Emit 描述性 MapEvent |
| `WarningEmitted`      | 9 | `WarningEmitted(levelByte, coords)` | Phase 9：调 `CollapseWarningService` + Emit OnAnomalyDetected |

**位序固定**（AGENTS.md §11）：禁止重排或跳号；任何序列化 / 哈希 / 网络协议都依赖此位序。
总计 11 个枚举值（None = 0 + 10 种 Kind）。

**与 MAP-11a 的事件映射**（ADR-0007 ↔ ADR-0008 边界）：

| Phase | Emit | 备注 |
|---|---|---|
| Phase 1 | `OnGlobalCVChanged`（GlobalCV tick 后） | 由 `CollapseValueService.Tick` 隐式 |
| Phase 2 | `OnTileFractured`（LCV stability 越过临界） | 由 `ApplyLocalDamageWithEvent` 检测 |
| Phase 3 | `OnGlobalCVChanged`（delta 累加后） | |
| Phase 4 | `OnTileStabilityChanged` + `OnTileFractured`（如进入 fragmented） | |
| Phase 5 | `OnTileFractured`（forced collapse） | |
| Phase 6 | `OnRegionChanged`（state machine transfer） | |
| Phase 7 | `OnMapObjectPlaced`（stub，MAP-10 后续） | |
| Phase 8 | 任意（description 路由） | |
| Phase 9 | `OnAnomalyDetected`（按 level / 阈值触发） | |

### D3. 与 MAP-11a CV 联动接口契约

**Phase 1 接口**：`CollapseValueService.Tick(mapState, regionService)` →
副作用 = `mapState.GlobalCV += DefaultTickDelta`（默认 +1）；返回本次事件类型列表。
该接口已存在（MAP-11a），本 ADR 不修改，仅定义"在 phase 1 内调"的契约。

**Phase 2 接口**：`CollapseValueService.ApplyLocalDamage(mapState, coord, amount)`
→ 副作用 = `LocalCVsInternal[coord] = lcv.WithDelta(amount)`。
本 ADR 不修改接口；定义"在 phase 2 内 N 次调用"的契约。

**Phase 3 接口**：直接修改 `mapState.GlobalCV = mapState.GlobalCV.WithValue(newValue)`，
clamp 到 `[0, 100]`；与 `ModifyGlobalCollapseValueCommand` 等价，但 ScheduleEnvironmentCommand
不通过 IMapCommand 嵌套（避免循环）—— ScheduleEnvironmentCommand 自身负责 mapState.Version 自增。

**Phase 9 接口**：`CollapseWarningService.ShouldWarn(mapState, threshold=CautionThreshold)`
→ 返回 bool；`true` 时 Emit `OnAnomalyDetected` per top hotspot coord。

### D4. 与 MAP-09 Region 联动接口契约

**Phase 6 接口**：`MapRegionService.TryTransitionState(mapState, regionId, RegionState.Active)`
→ 若成功 Emit `OnRegionChanged` 事件。
本 ADR 不修改 `MapRegionService`；定义"在 phase 6 内按 event.Tags[0] 解析 regionId"的契约：

| `RegionActivation` event | 解析逻辑 |
|---|---|
| `Tags = { "1" }` | `int.TryParse("1")` → `regionId = 1` |
| 空 Tags | 跳过（无效 event） |
| 非数字 Tags | 跳过（无效 event） |

无 `MapRegionService` 注入时（`resolver.RegionService == null`），phase 6 完全 no-op；
测试用 `MapRegionService` 实例校验 8 态状态机转移合法性。

### D5. 4 个新 IMapCommand 的 Run / Undo 语义

| Command | Run 语义 | Undo 语义 |
|---|---|---|
| `ScheduleEnvironmentCommand(schedule)` | 1) 校验 schedule 内部顺序 2) 保存旧 schedule + tick 3) 写入新 schedule + tick+1 4) 调 `ExecuteAll` 5) 返回累计 events | 恢复旧 schedule + tick；不回滚 phase 内副作用（设计上） |
| `TickEnvironmentCommand(phaseIndex, tickDelta)` | 1) 校验 phase 0..9 2) 校验 tick 累积不溢出 3) 保存旧 tick 4) 新 tick = 旧 + delta 5) 调 `ExecutePhase(phaseIndex)` | 恢复 tick = 旧值；不回滚 phase 内副作用 |
| `InjectEnvironmentEventCommand(ev)` | 1) 校验 ev 非 null + phaseIndex 合法 2) 校验非重复（按 `Equals` 全等） 3) 构造新 schedule（含 ev）4) 写入 | 恢复旧 schedule |
| `ClearEnvironmentScheduleCommand()` | 1) 保存旧 schedule + pending events 2) 写入 Empty schedule + 清空 pending events | 恢复旧 schedule + pending events |

**共同约束**：

- 每个 command 都 `version += 1`（`MapCommandExecutor` 标准行为）；
- 每个 command 不依赖其他 command（`Dependencies` = `Array.Empty<string>()`）；
- `CommandId` 格式稳定（见 D6）；
- Undo 必须紧跟 Execute；连续 Undo 抛 `InvalidOperationException`（与既有 IMapCommand 接口契约一致）。

**phase 内副作用不回滚的设计选择**：

- 每个 phase 内的副作用（如 LCV 累积、GlobalCV 累加）通过 IMapCommand 自管理 Undo；
- `ScheduleEnvironmentCommand` 自身不"嵌套 record"phase 副作用历史；
- 调用方若需完整 Undo phase 副作用，应：
  1) 在 Run 前对受影响 tile / GlobalCV 做 snapshot；
  2) Undo ScheduleEnvironmentCommand + 手动回滚 snapshotted 副作用；
- 这是 ADR-0004 §D3 的延伸：单条命令负责"自管理的写入" + "自管理的回滚"，跨命令 Undo 由外部协调。

### D6. Schedule 序列化与 Hash 字段编码

**MapState Hash 协议扩展**（ADR-0003 §D 增量）：

| Tag | 字段 | 备注 |
|---|---|---|
| `0x38` | `ActiveSchedule` struct | 子标签 `0xC0..0xCB` |
| `0x39` | `EnvironmentTickAccumulator` int | |
| `0x3A` | `PendingEvents` collection | 每个 event 按插入顺序，与 schedule 内 events 共享子标签 |

**ActiveSchedule 字段编码**：

```
0x38
  ├── int32 ScheduleId                (tag=0xC0)
  ├── int32 CreatedTick               (tag=0xC1)
  └── int32 Count[N events]           (tag=0xC2 + repeated)
        ├── int32 Kind                (tag=0xC3)
        ├── int32 TriggerTick         (tag=0xC4)
        ├── int32 Magnitude           (tag=0xC5)
        ├── int32 CoordsCount[K]      (tag=0xC6 + K times)
        │     ├── int32 X             (tag=0xC7)
        │     ├── int32 Y             (tag=0xC8)
        │     └── int32 Layer         (tag=0xC9)
        └── int32 TagsCount[M]        (tag=0xCA + M times)
              └── string tag          (tag=0xCB)
```

**PendingEvents 集合**：插入顺序写入；不参与 phase 排序 ——
PendingEvents 由 MapState.AddPendingEvent 维护，是 `DeferredTrigger` 的输入队列；
本 ADR 不规范化 PendingEvents 的 phase 归属。

**EnvironmentTickAccumulator**：int32 LE；改变后 `PostStateHash` 即变化；与 `Version` 字段独立。

**Schedule JSON 序列化**（MAP-13 后续）：本 ADR 不定义 JSON 协议；预留
`MapEnvironmentEvent.Kind` / `Magnitude` / `TriggerTick` / `AffectedCoords` /
`Tags` 字段供 Data 层消费。

### D7. 与未来 MAP-17（`MAP_DEV_PHASE_TEST_001`）的集成接口

MAP-17 引入"逐 phase 单测增强"测试钩子（`EnvPhaseTestHarness`）；本 ADR 预留
1 个接口位：

```csharp
// 在 EnvironmentPhaseResolver 内预留（MAP-17 接入）
internal interface IEnvPhaseHook
{
    void BeforePhase(MapState mapState, int phaseIndex, MapEnvironmentSchedule schedule);
    void AfterPhase(MapState mapState, int phaseIndex, IReadOnlyList<MapEvent> output);
}
```

本轮不实现 `IEnvPhaseHook`；MAP-17 接入时改 `ExecutePhase` 内部允许 hook 注入即可，
不破坏现有 IMapCommand 接口（命令实现只读 `ExecutePhase` 返回的事件列表）。

### D8. 失败 / 拒绝语义

| Command | 失败条件 | 行为 |
|---|---|---|
| `ScheduleEnvironmentCommand` | `ValidateSchedule` 返回非 0 | 返 `Fail("schedule out of order (code=N)")`；mapState 不变 |
| `TickEnvironmentCommand` | phaseIndex ∉ [0, 9] / tick 溢出 | 返 `Fail("phase index out of range")` / `Fail("tick overflow")` |
| `InjectEnvironmentEventCommand` | ev==null / 重复 ev | 抛 `ArgumentNullException` / 返 `Fail("duplicate event")` |
| `ClearEnvironmentScheduleCommand` | （无失败条件） | 总成功 |

`MapEnvironmentSchedule.FromEvents` 校验每个 event phaseIndex ∈ [0, 9]；
违例抛 `ArgumentException`（构造时阻止）。

### D9. Core 依赖守卫扩展

`CoreDependencyGuardTests`（现有 4 项）保持不变；新增的 `Map/Environment/` 目录
不引用 UnityEngine / UnityEditor，故守卫仍然 4/4 PASS。

---

## 影响范围

### 修改文件

- `Assets/Starfall/Core/Map/State/MapState.cs`：新增 `ActiveSchedule` + `EnvironmentTickAccumulator` + `PendingEvents` 字段 + 访问入口；
- `Assets/Starfall/Core/Map/State/MapStateHasher.cs`：新增 tag 0x38 / 0x39 / 0x3A + 子标签 0xC0..0xCB；
- `Assets/Starfall/Core/Map/State/MapStateCloner.cs`：deep-clone `ActiveSchedule` + `PendingEvents`。

### 新增文件

- `Assets/Starfall/Core/Map/Environment/MapEnvironmentEvent.cs` （10 Kind + 11 工厂 + EnvironmentPhaseIndex）
- `Assets/Starfall/Core/Map/Environment/MapEnvironmentSchedule.cs`（readonly struct + 工厂 + FromEvents）
- `Assets/Starfall/Core/Map/Environment/EnvironmentPhaseResolver.cs`（10 步 + ValidateSchedule + ExecuteAll）
- `Assets/Starfall/Core/Map/Commands/ScheduleEnvironmentCommand.cs`
- `Assets/Starfall/Core/Map/Commands/TickEnvironmentCommand.cs`
- `Assets/Starfall/Core/Map/Commands/InjectEnvironmentEventCommand.cs`
- `Assets/Starfall/Core/Map/Commands/ClearEnvironmentScheduleCommand.cs`
- `Assets/Starfall/Tests/EditMode/Map/Environment/MapEnvironmentEventTests.cs` (11 tests)
- `Assets/Starfall/Tests/EditMode/Map/Environment/MapEnvironmentScheduleTests.cs` (7 tests)
- `Assets/Starfall/Tests/EditMode/Map/Environment/EnvironmentPhaseResolverTests.cs` (16 tests)
- `Assets/Starfall/Tests/EditMode/Map/Environment/EnvironmentIntegrationTests.cs` (13 tests)
- `Assets/Starfall/Tests/EditMode/Map/Environment/Map11b_TaskId_AssertedString_Tests.cs` (7 tests)
- `Assets/Starfall/Tests/EditMode/Map/Commands/ScheduleEnvironmentCommandTests.cs` (9 tests)
- `Assets/Starfall/Tests/EditMode/Map/Commands/TickEnvironmentCommandTests.cs` (8 tests)
- `Assets/Starfall/Tests/EditMode/Map/Commands/InjectEnvironmentEventCommandTests.cs` (8 tests)
- `Assets/Starfall/Tests/EditMode/Map/Commands/ClearEnvironmentScheduleCommandTests.cs` (5 tests)

### 未修改（边界确认）

- `Assets/Starfall/Unity/`（表现层；MAP-14 HUD 接入）；
- `Assets/Starfall/Data/`（JSON 加载；MAP-13 接入）；
- `Packages/manifest.json`、`ProjectSettings/*`、`Memory/`（[AGENTS.md §13 红线]）；
- `CoreDependencyGuardTests`（4 项检查保持 PASS）。

---

## 风险与验证

### 风险

- **R1（已缓解）**：`MapEnvironmentSchedule.FromEvents` 在构造时强制排序，可能改变用户输入顺序。
  **缓解**：使用 `List<T>.Sort` 的稳定排序（保留同 phase 内相对顺序），与既有 `MapRegionDefinition.Bounds` 排序约定一致。
- **R2（已缓解）**：phase 内副作用不回滚可能让"Undo + Replay"测试不直观。
  **缓解**：设计上明确"单命令自管理 Undo"；跨命令 Undo 由外部协调；测试覆盖 Undo 路径（≥ 4 个测试）。
- **R3（已缓解）**：10 步固定顺序硬编码可能限制未来扩展。
  **缓解**：留 `EnvironmentPhaseIndex` 枚举供追加（≥ 10），新增 phase 必须通过 ADR 升级；当前 10 个值不预留。

### 验证

- **基线**：1186/1186 EditMode PASS（MAP-02..MAP-11a 累计）；
- **新增**：~84 单元测试 + ~10 集成测试（待编辑后确认 actual count）；
- **CoreDependencyGuardTests**：4/4 PASS；
- **0 新 compile warning**（pre-existing CS8632 + CS8632 in `ReplayException.cs` 不计入 MAP-11b）；
- **跨运行 hash 稳定**（含 `ActiveSchedule` 字段后）；
- **文档**：ADR-0008 本文件 + MAP_SYSTEM_FORWARD_PLAN §3.5 引用 + IMPLEMENTATION_STATUS §1.1 新行。

### 待用户裁决

- 用户裁决：本 ADR 是否被接受？
- 用户裁决：未来 MAP-14（HUD 接入）/ MAP-13（JSON 序列化）/ MAP-17（Dev phase test harness）
  是否仍按上述接口契约执行？
