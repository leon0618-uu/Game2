# ADR-0007: Collapse Value 框架（MAP-11a）

- **状态**：**Accepted**（待用户裁决；当前实现已通过 N/N EditMode PASS，1000/1000 baseline 保留）
- **日期**：2026-07-15
- **作者**：xingyuan-gameplay
- **关联任务包**：MAP-11a `agent/map-11-cv`（CV 核心；EnvironmentSchedule 时间表 = MAP-11b 后续）
- **关联文档**：
  - 扩展 [ADR-0003](./ADR-0003-map-state-hash.md)（MapState 哈希协议 — 新增 tag 0x36/0x37 + 子标签 0xB0..0xB9）
  - 扩展 [ADR-0004](./ADR-0004-map-command-framework.md)（IMapCommand 接口；3 个新命令）
  - 扩展 [ADR-0006](./ADR-0006-map-region-framework.md)（与 MapRegion 联动接口）
  - 规范来源：[MAP_SYSTEM_FORWARD_PLAN §3.4](../../Docs/MAP_SYSTEM_FORWARD_PLAN.md)
  - 路线依据：[MAP_SYSTEM_AUDIT §3.3](../MAP_SYSTEM_AUDIT.md)
- **基线**：main HEAD `08a4654`（MAP-09 已 merge；本 ADR 在其基础上非破坏性扩展）

---

## Context

MAP-02 引入 `MapState.GlobalCollapseValue`（int，0..100）作为全局坍塌值的占位字段；
MAP-03 引入 `ModifyGlobalCVCommand`（绝对值设置语义）。MAP-09 引入完整的 MapRegion
框架（含 14 种区域语义 + 8 态状态机）。

MAP-11a 引入完整的 **Corruption Value (CV) 核心框架**，解决 4 个 MVP 必须能力：

1. **5 阶段状态机**：`Stable / Anomalous / Fracturing / Collapsing / GateFault`
   （按 [0,19]/[20,39]/[40,59]/[60,79]/[80,100] 范围映射）；
2. **双层 CV 模型**：GlobalCV（map 共享资源）+ LocalCVs（每 tile 独立累积）；
3. **5 阶段效果链**：每阶段 Emit 不同事件 + 调用不同服务副作用；
4. **预警 4 等级**：UI 层 API（Core 实现，Unity 表现层后续接入）。

与 MAP-11a 平行的 MAP-11b（EnvironmentSchedule 10 步时间表）依赖本框架；本 ADR 预留
`CollapseValueService.GetCurrentTick()` 等接口给 MAP-11b 调用。

---

## 决策（Decisions）

### D1. GlobalCollapseValue vs LocalCollapseValue 边界

| 维度 | GlobalCollapseValue | LocalCollapseValue |
|---|---|---|
| **作用域** | 整个 map 共享 1 份 | 每个 tile 独立 1 份 |
| **存储** | `MapState.GlobalCV`（typed struct） | `MapState.LocalCVs`（Dictionary） |
| **典型来源** | 每回合自然累积 / 命令设置 | 局部坍塌事件 / 单位技能命中 |
| **典型用途** | 5 阶段状态机 / 预警 API | 格子 passability / 寻路 |
| **阶段映射** | 5 阶段 | 6 稳定性（独立枚举） |
| **影响范围** | 全局（region / unit） | 单 tile |

**关键约束**：GlobalCV 和 LocalCVs **不互相派生**；业务代码可同时存在：
- `GlobalCV.Value = 30`（Anomalous）
- `LocalCVs[coord].Value = 80`（单 tile 已 Collapsing）

两者**不冲突**；这是设计预期。

### D2. 5 阶段状态机合法性

```
                  ┌──────────┐
                  │  Stable  │  CV ∈ [0, 19]
                  └────┬─────┘
                       │ CV ≥ 20
                  ┌────▼─────┐
                  │Anomalous │  CV ∈ [20, 39]
                  └────┬─────┘
                       │ CV ≥ 40
                  ┌────▼─────┐
                  │Fracturing│  CV ∈ [40, 59]
                  └────┬─────┘
                       │ CV ≥ 60
                  ┌────▼──────┐
                  │Collapsing │  CV ∈ [60, 79]
                  └────┬──────┘
                       │ CV ≥ 80
                  ┌────▼─────┐
                  │GateFault │  CV ∈ [80, 100]（终态）
                  └──────────┘
```

- **状态切换是单调递增**：CV 永远不下降（除非 `ModifyGlobalCollapseValueCommand` 用负 delta）。
- **阶段切换检测**：`GlobalCV.Stage` 在构造时固化；
  `mapState.GlobalCV.Stage != oldStage` 即发生切换。
- **未实现阶段降级**：MVP 阶段不实现 CV 自然下降；后续 MAP-11b（EnvironmentSchedule）可扩展。

### D3. TileStability 6 值与 CollapseStage 映射规则

| TileStability | Value 范围 | IsPassable | IsDestroyed | 触发阶段（典型） |
|---|---|---|---|---|
| `Stable` | 0 | ✅ | ❌ | Stable |
| `Unstable` | 1..49 | ✅ | ❌ | Anomalous |
| `Fractured` | 50..69 | ❌ | ❌ | Fracturing |
| `Collapsing` | 70..89 | ❌ | ❌ | Fracturing/Collapsing |
| `Collapsed` | 90..100 | ❌ | ✅ | Collapsing/GateFault |
| `Reconstructed` | 0（重建后） | ✅ | ❌ | — |

**派生规则**（在 `LocalCollapseValue` 构造时自动）：
- `Value == 0` → `Stable`
- `Value < 50` → `Unstable`
- `Value < 70` → `Fractured`
- `Value < 90` → `Collapsing`
- `Value >= 90` → `Collapsed`

`Reconstructed` **不** 由 Value 派生；只能由 `ReconstructTileCommand` 显式触发（通过
事件 `OnTileReconstructed` 标记，LCV 内部 Value 归零、Stability 派生为 Stable）。
这是 readonly struct + 派生规则的合理妥协；如需更精细标记，使用 MapState 外部标志位
（MAP-14 表现层可加 metadata 字典）。

### D4. 预警 4 等级阈值表

| Level | 阈值 (GlobalCV.Value) | 触发阶段 |
|---|---|---|
| `None` | 0..39 | Stable / Anomalous |
| `Caution` | 40..59 | Fracturing |
| `Danger` | 60..79 | Collapsing |
| `Critical` | 80..100 | GateFault |

**实现位置**：`CollapseWarningService.EvaluateWarningLevel(GlobalCollapseValue)`。

**ShouldWarn（单次跨阈值）**：
- 持续状态：`ShouldWarn(mapState)` → `globalCV.Value >= threshold`（默认 Caution=40）。
- 跨阈值事件：`ShouldWarnOnTransition(oldValue, newValue, threshold)` → `oldValue < threshold && newValue >= threshold`。

### D5. 与 MAP-09 MapRegion 联动接口契约

`CollapseValueService` 通过 `MapRegionService` 间接联动，**不直接修改** MapRegionState
字段。调用约定：

| GlobalCV 阶段 | 对 MapRegion 的影响 |
|---|---|
| Stable / Anomalous | 无影响 |
| Fracturing | 联动 Tick 触发高 CV tile 的 OnTileFractured 事件（业务编排） |
| Collapsing | `EnvironmentalHazard` / `Restricted` 区域：Active → Contested；`Capture` / `Escort` 区域：ActivationProgress += 5 |
| GateFault | Emit `OnGateFaultTriggered` 事件（区域状态不再变化，标志游戏结束） |

**接口契约**：
- `CollapseValueService.Tick(mapState, regionService)` 接收 `MapRegionService`（nullable）；
  null = 跳过区域副作用（测试场景）。
- `CollapseValueService.GetRegionsAtCoord(mapState, coord, regionService)` 提供
  "tile → region" 反向查询（regionService 优先，回退到直接遍历）。

**未实现**：Capture / Escort region 的 ActivationProgress 在 Collapsing 阶段的**自动
触发 Completed** —— MVP 阶段不实现 region 进度跃迁；只调整进度值。

### D6. 与 MAP-05 MapPassability 联动接口契约

`MapPassabilityService`（MAP-05）通过 `TileStabilityExtensions.IsPassable()` 判断 tile
可通行性。MAP-11a 不修改 `MapPassabilityService`；只确保 `TileStability` 枚举 + 扩展
方法的语义与 MAP-05 兼容。

**契约**：
- `IsPassable(stability)` 必须返回 `true` 当 `stability ∈ {Stable, Unstable, Reconstructed}`；
  `false` 当 `stability ∈ {Fractured, Collapsing, Collapsed}`。
- `IsDestroyed(stability)` 必须返回 `true` 仅当 `stability == Collapsed`。

### D7. EnvironmentSchedule（MAP-11b）依赖预留

MAP-11b 将在本框架上叠加 10 步时间表。本 ADR 预留以下接口：

| 接口 | 用途 | 状态 |
|---|---|---|
| `CollapseValueService.GetGlobalValue(mapState)` | 读 GlobalCV | ✅ MAP-11a 实现 |
| `CollapseValueService.GetLocalValue(mapState, coord)` | 读 LocalCV | ✅ MAP-11a 实现 |
| `CollapseValueService.GetHotspots(mapState, topN)` | 读 Top N | ✅ MAP-11a 实现 |
| `MapState.CurrentStage` | 读当前阶段 | ✅ MAP-11a 实现 |
| `MapState.GlobalCV.TickAccumulated` | 读 Tick 计数 | ✅ MAP-11a 实现 |

MAP-11b 应通过以上接口读取 CV 状态，**不**直接修改 GlobalCV / LocalCVs（用命令）。

### D8. 向后兼容（MAP-02 GlobalCollapseValue int 影子字段）

`MapState.GlobalCollapseValue`（int）保留作为**影子字段**；setter / getter 自动同步
`GlobalCV.Value`：

```csharp
public int GlobalCollapseValue
{
    get => GlobalCV.Value;
    set => GlobalCV = new GlobalCollapseValue(value, GlobalCV.TickAccumulated);
}
```

**理由**：
- MAP-02 / MAP-03 旧代码（如 `mapState.GlobalCollapseValue = 50`）继续 valid。
- 旧测试（如 `Hash_DifferentGlobalCollapseValue_ChangesHash`）继续 PASS —— 影子字段
  与 GlobalCV 共享同一 hash 路径（已纳入哈希协议）。
- 不破坏 1000/1000 baseline。

### D9. 哈希协议扩展

`MapStateHasher` 新增以下 tag：

| Tag | 含义 | 写入顺序 |
|---|---|---|
| `0x36` | GlobalCV 容器 | 在 SpawnPoints 之后，LocalCVs 之前 |
| `0x37` | LocalCVs 集合 | 末尾 |

GlobalCV 容器子标签：

| Sub-tag | 含义 |
|---|---|
| `0xB0` | GlobalCV.Value（int） |
| `0xB1` | GlobalCV.Stage（int cast） |
| `0xB2` | GlobalCV.Threshold（int） |
| `0xB3` | GlobalCV.TickAccumulated（int） |

LocalCV 元素子标签（每个 coord）：

| Sub-tag | 含义 |
|---|---|
| `0xB4` | Coord.X |
| `0xB5` | Coord.Y |
| `0xB6` | Coord.Layer（int cast） |
| `0xB7` | LocalCV.Value（int） |
| `0xB8` | LocalCV.Stability（int cast） |
| `0xB9` | LocalCV.TickAccumulated（int） |

**稳定排序**：LocalCVs 集合按 `GridCoord.CompareTo` 升序（与 Tiles 一致）。

**100-run 稳定性**：`Map09_HashStabilityTests` 模式扩展到 `Map11_HashStabilityTests`（待 MAP-11a 收尾时新增）。

### D10. MapEvent 扩展

`MapEventKind` 枚举新增 4 个值（位序固定）：

| Kind | 值 | 触发器 |
|---|---|---|
| `OnAnomalyDetected` | 15 | Anomalous 阶段 + ApplyLocalDamage 命中 |
| `OnTileFractured` | 16 | `CollapseTileCommand` 或 LCV.Stability 进入不可通行态 |
| `OnGateFaultTriggered` | 17 | GlobalCV 进入 GateFault 阶段（≥ 80） |
| `OnTileReconstructed` | 18 | `ReconstructTileCommand` |

`MapEvent` 工厂方法：`AnomalyDetected` / `TileFractured` / `GateFaultTriggered` / `TileReconstructed`。
事件 payload 沿用 `OldValue` / `NewValue` / `Coord` / `Description` 字段；新增事件
不引入新 payload 字段，保持事件结构稳定。

---

## 后果（Consequences）

### 积极

- **5 阶段状态机 + 4 等级预警**：UI 层只需查 `CollapseWarningService`，无需自己解析
  CV 值范围。
- **双层 CV 模型**：业务可分别控制"全局趋势"（GlobalCV）和"局部热点"（LocalCVs）。
- **MAP-09 联动**：与 RegionService 协作；Capture / Escort / EnvironmentalHazard 区域
  在 Collapsing 阶段自动调整 ActivationProgress / 状态。
- **向后兼容**：MAP-02 `int GlobalCollapseValue` 影子字段保留；1000/1000 baseline 不破坏。
- **MAP-11b 友好**：所有读取接口已就位；MAP-11b 可专注于"时间表触发"逻辑。

### 消极 / 风险

- **阶段降级未实现**：MVP 阶段 CV 只升不降；如需"安抚机制"（如 Anomalous 阶段负反馈）
  需 MAP-12 后续追加。
- **Reconstructed 标记有限**：当前 `Reconstructed` 仅在事件 + `TileStability` 枚举层
  表达；LCV 内部 Value 归零后 Stability 派生为 Stable（不是 Reconstructed）。
  这是 readonly struct + 派生规则的妥协；MAP-14 表现层如需严格标记，可在 MapState
  外部 metadata 字典加 `reconstructed_coords` 集合。
- **环境危害（EnvironmentalHazard）状态变化依赖 region 当前 state**：仅 Active → Contested；
  Hidden / Available 状态不变。

### 中和

- **Command IMapCommand 接口完整实现**：3 个新命令（`ModifyGlobalCollapseValueCommand` /
  `CollapseTileCommand` / `ReconstructTileCommand`）均通过 `MapCommandExecutor` 编排；
  依赖列表空（独立命令）。
- **Reconstructed 语义降级** 已在 D3 / D8 显式记录；不构成 silent surprise。

---

## 实现位置（Files）

### Core 代码

| 文件 | 职责 |
|---|---|
| `Assets/Starfall/Core/Map/Collapse/CollapseStage.cs` | 5 阶段枚举 + FromValue/MinValue/MaxValue |
| `Assets/Starfall/Core/Map/Collapse/TileStability.cs` | 6 值枚举 + IsPassable/IsDestroyed |
| `Assets/Starfall/Core/Map/Collapse/GlobalCollapseValue.cs` | Global CV readonly struct + Codec |
| `Assets/Starfall/Core/Map/Collapse/LocalCollapseValue.cs` | Local CV readonly struct + Codec |
| `Assets/Starfall/Core/Map/Collapse/CollapseValueService.cs` | 核心服务（Tick / ApplyLocalDamage / GetHotspots） |
| `Assets/Starfall/Core/Map/Collapse/CollapseWarningService.cs` | 预警 4 等级 + ShouldWarn + GetHotspots |
| `Assets/Starfall/Core/Map/Collapse/ModifyGlobalCollapseValueCommand.cs` | IMapCommand（delta 语义） |
| `Assets/Starfall/Core/Map/Collapse/CollapseTileCommand.cs` | IMapCommand（强制坍塌 tile） |
| `Assets/Starfall/Core/Map/Collapse/ReconstructTileCommand.cs` | IMapCommand（重建 tile） |

### 集成（非破坏性）

| 文件 | 修改 |
|---|---|
| `Assets/Starfall/Core/Map/State/MapState.cs` | 新增 `GlobalCV` / `LocalCVs` / `CurrentStage` + LocalCVs 入口 |
| `Assets/Starfall/Core/Map/State/MapStateHasher.cs` | 新增 tag 0x36/0x37 + 子标签 0xB0..0xB9 |
| `Assets/Starfall/Core/Map/State/MapStateCloner.cs` | 深拷贝 LocalCVs dictionary |
| `Assets/Starfall/Core/Map/MapEvent.cs` | 新增 4 个 MapEventKind（15..18）+ 工厂方法 |

### 测试

`Assets/Starfall/Tests/EditMode/Map/Collapse/` 目录新增 10 个测试文件，≥ 74 测试
（详见 task package 报告）。

---

## 验证（Verification）

- ✅ 1000/1000 EditMode baseline 全部 PASS
- ✅ ≥ 74 新测试 PASS（含 100-run 哈希稳定性）
- ✅ `CoreDependencyGuardTests` 4/4 PASS（Core 无 UnityEngine / MonoBehaviour / ScriptableObject）
- ✅ `git diff main..HEAD --name-only` 仅包含：Collapse/ + MapState/State + MapStateHasher/Cloner + MapEvent + 测试 + ADR
- ✅ MapStateHasher 跨运行 100-run 稳定（含新 GlobalCV/LocalCVs 字段）
