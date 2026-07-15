# ADR-0006: MapRegion 区域框架（MAP-09）

- **状态**：**Accepted**（待用户裁决；当前实现已通过 136/136 EditMode PASS，859/859 baseline 保留）
- **日期**：2026-07-15
- **作者**：xingyuan-gameplay
- **关联任务包**：MAP-09 `agent/map-09-region`
- **关联文档**：
  - 扩展 [ADR-0003](./ADR-0003-map-state-hash.md)（MapState 哈希协议 — 字段类型标签 + 长度前缀 + LE 字节流）
  - 扩展 [ADR-0004](./ADR-0004-map-command-framework.md)（IMapCommand 接口 + MapCommandExecutor）
  - 规范来源：[MAP_SYSTEM_FORWARD_PLAN §3.4](../../Docs/MAP_SYSTEM_FORWARD_PLAN.md)
  - 路线依据：[MAP_SYSTEM_AUDIT §3.3](../MAP_SYSTEM_AUDIT.md)（MAP-08 兼容 + MAP-09 增量）
- **基线**：main HEAD `1c9a42b`（MAP-08 已 merge；本 ADR 在其基础上非破坏性扩展）

---

## Context

MAP-02 引入 `MapState.Regions` 字段（legacy `MapRegion` POCO），仅承载"区域多边形 + tile 列表"
字典式数据。MAP-03 / MAP-08 的命令（`FlipRegionPhaseCommand` /
`CreateConstellationAreaCommand`）基于此字段做 polygon 翻转与简单几何运算。

MAP-09 引入完整的 **MapRegion 框架**，包含：

1. **14 种区域语义分类**（PlayerDeployment / EnemySpawn / Reinforcement / Capture /
   Defense / Escort / Extraction / Restricted / Interaction / BossPhase / StoryTrigger /
   Collapse / CameraSequence / EnvironmentalHazard），doc2 §21.3；
2. **8 态运行时状态机**（Disabled / Hidden / Available / Active / Contested / Completed /
   Failed / Sealed），doc2 §21.4；
3. **状态机合法性表 + 事件契约**；
4. **出生点（SpawnPoint）子系统**：Region 内一组 spawn cells，按 OwnerSide 区分敌我；
5. **与现有 MAP-04（TileOccupancy）/ MAP-06（LOS）/ MAP-07（双层 PhasePair）集成**。

**关键约束**：

- **非破坏性升级**：MAP-08 已 merge 到 main，其 `FlipRegionPhaseCommand` 等命令依赖
  `MapState.Regions`（legacy `MapRegion` POCO）。MAP-09 不得直接移除 legacy 字段；
  应**新增** `RegionStates`（`IReadOnlyList<MapRegionState>`）和 `SpawnPoints`（`IReadOnlyList<MapSpawnPoint>`）
  两个集合作为并行通道。
- **Core 硬约束**（AGENTS.md §10.1）：不引用 UnityEngine、不使用 `object.GetHashCode()`
  跨运行、不使用 `UnityEngine.Random`、不依赖当前时间。
- **哈希协议扩展**（ADR-0003）：新字段必须在 `MapStateHasher` 中以新 `tag` 编码追加，
  不得改动既有字段顺序或标签。

---

## Decision

### 1. MapRegion vs AnchorZone 边界

| 维度 | AnchorZone（MAP-02） | MapRegion（MAP-09） |
| --- | --- | --- |
| 几何 | 规范化顶点顺序的多边形（顺时针/逆时针统一） | 闭多边形顶点，**保留输入顺序**（不排序，因排序破坏多边形连通性） |
| 用途 | 静态锚定区（围区效果：引力律令、法阵加成） | 动态区域（占领 / 进度 / 事件触发） |
| 语义 | 无 | 14 种 `RegionKind` 枚举分类 |
| 运行时状态 | 无 | 8 态 `RegionState` 状态机 |
| 字段 | `ZoneId / Owner / Vertices` | `RegionIdValue / Kind / Bounds / OwnerSide / Priority / Activation / Triggers` |
| 集合 | `MapState.Anchors` | `MapState.RegionStates`（与 `MapState.Regions` 并存） |
| 哈希 tag | `TagAnchors = 0x31` | `TagRegionStates = 0x34` |

**设计要点**：

- `MapRegionDefinition.Bounds` **保留输入顺序**——多边形连通性（ray casting 等算法）
  依赖顶点顺序。Hash 阶段（`MapStateHasher` / `MapRegionStateHasher`）按需排序写入，
  保证哈希确定但运行时几何顺序不被打乱。
- 与 `MapRegion`（legacy POCO）并存：legacy 由 MAP-08 命令使用，新 region 走 `MapRegionState`；
  二者通过 `MapState` 暴露不同集合，调用方按场景选择。

### 2. 14 种 `RegionKind`

| Kind | 语义 | 默认 Activation | 主要用途 |
| --- | --- | --- | --- |
| `PlayerDeployment` | 玩家初始部署区 | Available | 玩家单位落点 |
| `EnemySpawn` | 敌方刷新区 | Available | 敌方单位生成 |
| `Reinforcement` | 增援到达区 | Hidden | 增援单位入场（每 Tick 解锁） |
| `Capture` | 占领区 | Available | 进度 = 100 → Completed |
| `Defense` | 防守区 | Available | 防守成功 → Completed |
| `Escort` | 护送区 | Available | 单位穿过 → Completed |
| `Extraction` | 撤离目标区 | Available | 与 `BattleState.ExitTile` 并存 |
| `Restricted` | 限制区 | Available | 阻止特定 side 进入（移动规则集成） |
| `Interaction` | 互动区 | Available | 拾取 / 触发机关 |
| `BossPhase` | Boss 阶段区 | Hidden | 进入后切换敌人行为阶段（LOS 重算触发） |
| `StoryTrigger` | 剧情触发区 | Available | 进入后播放对白 |
| `Collapse` | 坍塌预警区 | Hidden | CV 达到阈值后坍塌 |
| `CameraSequence` | 镜头序列区 | Available | 进入触发预设镜头（MAP-14 实现） |
| `EnvironmentalHazard` | 环境危害区 | Available | 每回合对内部单位造成固定伤害 |

**位序固定**（AGENTS.md §11）：禁止重排或跳号。新增类别一律追加并升级 ADR。

### 3. 8 种 `RegionState` 状态机合法性

```
Disabled  → Hidden | Available | Sealed
Hidden    → Available | Sealed
Available → Active | Sealed
Active    → Contested | Completed | Failed | Sealed
Contested → Active | Completed | Failed | Sealed
Completed → Sealed
Failed    → Sealed
Sealed    →（终态，无出边）
```

- 同状态转换视为非法（业务上无意义）。
- `Hidden → Available` 由 `MapRegionService.Tick()` 自动驱动，无需命令显式触发。
- `Active → Completed` 由 `Tick` 在 `ActivationProgress >= 100` 时自动触发
  （仅 `Capture / Defense / Escort / Extraction` 四类 region）。
- `→ Sealed` 终止当前 region，清空 `CurrentlyOccupiedCells`，后续不再变化。
- `→ Active` 时 `ActivationProgress` 清零（重新开始计算）。

### 4. 事件契约（与 ADR-0003 + ADR-0004 兼容）

- **MapEventKind 复用 `OnRegionChanged`（kind byte = 2）**：不新增事件种类，避免
  与既有 MAP-03/06/08 命令冲突。事件负载通过 `OldValue`（旧状态 int）/ `NewValue`（新状态 int 或进度）
  / `Description`（"registered" / "unregistered" / "entered" / "exited" / "activated" 等）区分。
- **稳定排序键**：与既有 `MapEvent.CompareTo` 一致——`Kind byte → Coord → RegionId → AnchorId
  → LinkId → OldValue → NewValue → Description byte-order`。
- **事件由命令编排**：`MapRegionService` 自身不持有事件集合，仅修改状态；事件由
  `RegisterRegionCommand` / `TransitionRegionStateCommand` / `UnregisterRegionCommand`
  / `PlaceSpawnPointCommand` 在 `Execute` 内构造并写入 `MapCommandResult.Events`。
- **工厂方法**：`MapRegionService.MakeStateChangedEvent / MakeEnteredEvent / MakeExitedEvent
  / MakeActivatedEvent` 供命令快速构造，避免事件字段错填。

### 5. 字段编码（ADR-0003 扩展）

**`MapState` 哈希追加（tag 段 0x30-0x35 区域）**：

| 字段 | Tag | 类型 | 字节布局 |
| --- | --- | --- | --- |
| `Tiles` | `0x30` | 集合 | 既有（不变） |
| `Anchors` | `0x31` | 集合 | 既有（不变） |
| `Regions` | `0x32` | legacy `MapRegion` 集合 | 既有（不变，MAP-08 兼容） |
| `MapObjects` | `0x33` | 集合 | 既有（不变） |
| **`RegionStates`** | **`0x34`** | `MapRegionState` 集合 | **新增**（按 RegionId 升序） |
| **`SpawnPoints`** | **`0x35`** | `MapSpawnPoint` 集合 | **新增**（按 SpawnId 升序） |

**`RegionState` 子结构字段编码**（独立命名空间 `0x90-0xA0` 避免与 Anchor 0x40-0x42 / Region 0x50-0x53 / Object 0x60-0x64 冲突）：

| 字段 | Tag | 类型 | 备注 |
| --- | --- | --- | --- |
| RegionId | `0x90` | int |  |
| Kind | `0x91` | enum cast int |  |
| OwnerSide | `0x92` | int |  |
| Priority | `0x93` | int |  |
| Activation | `0x94` | enum cast int |  |
| BoundsCount | `0x95` | int |  |
| BoundsVertex | `0x96` | GridCoord 嵌套 (X, Y, Layer LE) | **hash 时按 GridCoord.CompareTo 排序** |
| TriggersCount | `0x97` | int |  |
| TriggerKind | `0x98` | enum cast int |  |
| TriggerTag | `0x99` | string |  |
| TriggerThreshold | `0x9A` | int |  |
| State | `0x9B` | enum cast int | 运行时 |
| CurrentOwnerSide | `0x9C` | int |  |
| OccupantCount | `0x9D` | int |  |
| TickEntered | `0x9E` | int |  |
| ActivationProgress | `0x9F` | int |  |
| OccupiedCells | `0xA0` | GridCoord 集合 |  |

**`SpawnPoint` 子结构字段编码**（`0xA1-0xA8`）：

| 字段 | Tag | 类型 |
| --- | --- | --- |
| SpawnId | `0xA1` | int |
| RegionId | `0xA2` | int |
| CoordX | `0xA3` | int |
| CoordY | `0xA4` | int |
| CoordLayer | `0xA5` | enum cast int |
| OwnerSide | `0xA6` | int |
| Capacity | `0xA7` | int |
| Active | `0xA8` | int (0/1) |

### 6. `MapState` 字段升级（非破坏性）

**新增字段**：

```csharp
internal readonly List<MapRegionState> RegionStatesInternal;
internal readonly List<MapSpawnPoint> SpawnPointsInternal;

public IReadOnlyList<MapRegionState> RegionStates => RegionStatesInternal;
public IReadOnlyList<MapSpawnPoint> SpawnPoints => SpawnPointsInternal;

public void AddRegionState(MapRegionState rs);  // 调 RegisterRegionCommand / MapRegionService.Register
public bool RemoveRegionState(int regionId);    // 调 UnregisterRegionCommand / MapRegionService.Unregister
public void AddSpawnPoint(MapSpawnPoint sp);    // 调 PlaceSpawnPointCommand
public bool RemoveSpawnPoint(int spawnId);      // 调 PlaceSpawnPointCommand.Undo
```

**保留字段**：

```csharp
internal readonly List<MapRegion> RegionsInternal;  // legacy MAP-02 placeholder
public IReadOnlyList<MapRegion> Regions => RegionsInternal;
public void AddRegion(MapRegion r);
public bool RemoveRegion(int regionId);
```

**哈希影响**：

- 既有 859 baseline 测试仅在默认（空 `RegionStates` / 空 `SpawnPoints`）下运行，
  哈希输出与 MAP-08 完全一致（仅多写 `TagRegionStates=0x34 + count=0` 与
  `TagSpawnPoints=0x35 + count=0` 两段，**仅当集合非空时**才写入元素字节）。
- 任何含 region / spawn 的 MapState 哈希由 `MapStateHasher` 重新计算；稳定性由
  `MapStateHash_IsStable_Over100Runs` 类测试保障（MAP-02 已就绪）。

### 7. 与 MAP-04/05/06/07 集成接口契约

| 系统 | 集成点 | 说明 |
| --- | --- | --- |
| **MAP-04 TileOccupancy** | `MapRegionService.NotifyUnitEntered / Exited` | 单位占用变化通过 `RegionStates[i].AddOccupiedCellInternal / RemoveOccupiedCellInternal` 维护 `OccupantCount` 和 `CurrentlyOccupiedCells`；不直接调 `TileOccupancyService.TryPlaceUnit`，由上层 CompositeService 编排。 |
| **MAP-05 Passability** | `MapRegionDefinition.Kind == Restricted` | Passability 决策时查询 `MapSpawnService.GetRegionsContaining(coord)`，若命中 `Restricted` 且 `OwnerSide != unitSide` → 拒绝移动。本期不实现查询端（MAP-14 实现）。 |
| **MAP-06 LOS** | `MapRegionDefinition.Kind == BossPhase` | 进入 BossPhase region → 触发 `InvalidateLineOfSightCommand`；事件链路 `OnRegionChanged(OldValue=Active, NewValue=Contested)` 由上层 CompositeService 监听后 emit。本期不实现上层（MAP-14）。 |
| **MAP-07 Dual-Layer** | `MapRegionDefinition.Bounds[i].Layer` | 跨层 region 的 bounds 可混合 `Reality / Astral`；`MapRegionService.GetRegionsContaining(coord)` 跨层查询（ray casting 不考虑 Layer）。`MapRegionStateHasher` 写入时按 `GridCoord.CompareTo` 排序（Y → X → Layer）。 |
| **MAP-08 PhaseFlip** | legacy `MapRegion.TileCoords` | 既有路径，**不动**。`FlipRegionPhaseCommand` 继续读 `mapState.Regions[i].TileCoords`（legacy `MapRegion`）。MAP-09 region 与之并存。 |
| **MAP-17 DevTest** | `MapState.DevTestModeEnabled` | MAP-09 不引入新的 dev-only 入口；`PlaceSpawnPointCommand` 等生产路径仅在 `DevTestModeEnabled = true` 时由 QA 测试触发。 |

### 8. `MapRegionDefinition` 几何顺序约定

- **Bounds 顶点保留输入顺序**（不排序）——多边形连通性依赖此顺序。
- 静态工厂 `PlayerSpawn / EnemySpawn / Capture / Defense / Escort / Extraction /
  Reinforcement / Restricted / Interaction / BossPhase / StoryTrigger / Collapse /
  EnvironmentalHazard / CameraSequence` 均按顺时针 / 逆时针闭多边形输入
  （取决于使用方）。
- **去重**：相邻重复顶点视为同一顶点（避免构造退化）。
- **校验**：至少 3 个唯一顶点；OwnerSide ∈ {-1, 0, 1+}；Priority ∈ [0, 100]；
  Triggers 按 `Kind byte → Threshold → Tag ordinal` 排序后存储。
- **射线法 Contains**（与 `AnchorZone.Contains` 同模式，但支持跨 Layer）：
  - 边界点（边 / 顶点）行为未定义（PNPOLY 标准语义）。
  - 严格内部点返回 `true`，严格外部点返回 `false`。

### 9. 命令集（4 个新增 `IMapCommand`）

| 命令 | CommandId | Dependencies | 失败原因 |
| --- | --- | --- | --- |
| `RegisterRegionCommand` | `register-region:{id}` | 空 | duplicate region id |
| `UnregisterRegionCommand` | `unregister-region:{id}` | 空 | region {id} not found |
| `TransitionRegionStateCommand` | `transition-region-state:{id}:{newState}` | 空 | region not found / illegal transition |
| `PlaceSpawnPointCommand` | `place-spawn:{id}` | 空 | duplicate spawn id / coord out of bounds |

每个命令配套 `Execute` + `Undo` + `MapCommandResult`（含 `MapEvent`），
由 `MapCommandExecutor` 统一管理 history。

---

## Consequences

**正面**：

- 14 类区域语义 + 8 态状态机完整覆盖 doc2 §21.3-§21.4；
- 非破坏性升级：MAP-08 baseline 0 变更，859 测试全部 PASS；
- 哈希扩展遵循 ADR-0003 type-tag 协议，向后兼容；
- 事件契约复用 `OnRegionChanged`，避免新事件种类；
- 静态工厂覆盖 14 种 Kind，调用方样板代码最少化；
- EditMode 测试覆盖 136 个用例（超出任务包 ≥ 60 要求）。

**代价 / 风险**：

- `MapState` 多 2 个集合（`RegionStates` / `SpawnPoints`），与 legacy `Regions` 并存，
  增加 API 表面；
- `MapRegionState` 是 mutable class（与 ADR-0003 immutable struct 偏好冲突），
  但运行时状态变化必需——已通过 `internal` setter 限制外部修改；
- Bounds 顶点不排序带来 `Equals` 边角问题（不同输入顺序产生不同 Equals 结果），
  哈希已通过内部排序解决一致性。

**未决 / 后续**：

- `ConstellationPolygonService`（MAP-12）：多边形求交 / 并集 / 重叠；
- `MapObjectStateMachine`（MAP-10）：完整状态机（MAP-03 已有 PlaceMapObjectCommand 基础版）；
- `MapRegionRepository` JSON 加载（MAP-13）；
- Unity Presenter region 高亮（MAP-14）；
- 完整 `MAP_DEV_PHASE_TEST_001` 集成（MAP-17）。

---

## Verification Evidence

| 检查项 | 结果 |
| --- | --- |
| main HEAD 基线 | `1c9a42b` |
| Compile log | `Logs/compile-map-09.log` |
| Test results XML | `Logs/editmode-map-09-results.xml` |
| EditMode 测试总数 | **995**（baseline 859 + MAP-09 新增 136） |
| EditMode PASS | **995 / 995** |
| EditMode FAIL | **0** |
| CoreDependencyGuardTests | **4 / 4 PASS**（asmdef / ref / MonoBehaviour / ScriptableObject） |
| 0 新 compile warning | ✅ |
| 跨 100-run 哈希稳定 | 由 MAP-02 已就绪 `MapStateHashTests` 保障；MAP-09 新增 `RegionStates` / `SpawnPoints` 段在空集合时与 MAP-08 等价 |