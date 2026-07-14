# Starfall.Core.Map.Tile

> doc2 MAP-04 地块、地形、占用、占地、标签系统。

本目录属于 `Starfall.Core` 程序集，**纯 C# 实现**，不引用 `UnityEngine` /
`UnityEditor`（AGENTS.md §10.1）。所有战斗状态变化都通过 Command 表达，
本目录只提供无副作用的纯逻辑类型。

---

## 子目录说明

`Map/Tile/` 负责 **doc2 地图的地块 / 地形 / 占用语义**：

- 不实现 IMapCommand / MapCommandExecutor（属于 MAP-03 范围）；
- 不解析 JSON / ScriptableObject（属于 `Starfall.Data`）；
- 不负责表现层（属于 `Starfall.Unity`）；
- 不修改 `MapState.cs` 字段表（属于 ADR-0003 哈希冻结）；
- 不修改 MAP-06 三个接口（`IHeightLookup` / `ICoverLookup` / `IBlockingLookup`）。

---

## 类型一览

| 类型 | 角色 | 关键约束 |
|------|------|----------|
| `TerrainType` | enum byte（11 类地形） | doc2 §4.1；数值 0..10，禁止重排 |
| `TerrainDefinition` | readonly struct（地形不可变配置） | doc2 §4.1；BaseMoveCost ∈ [1, 5] |
| `TerrainRegistry` | static class（11 类标准值） | doc2 §3.4 验收矩阵；按 byte 升序 |
| `TileTags` | [Flags] enum int（22 个标签） | doc2 §4.2；bit 0..21，禁止重排 |
| `Footprint` | enum byte + 扩展方法 | doc2 §4.3；SingleCell/TwoByTwo/ThreeByThree |
| `TileDefinition` | readonly struct（地块不可变定义） | doc2 §4.4；TileId ≥ 1 |
| `TileDefinitionRegistry` | sealed class（登记表） | doc2 §4.5；按 GridCoord 升序遍历 |
| `MapTileState` | sealed class（地块运行时状态） | doc2 §4.6；Stability [0, 100] |
| `LegacyTileStateAdapter` | static class（旧 enum 桥） | doc2 §4.7；4 类旧 enum → 新 TileDef |
| `TileOccupancyService` | static class（占用服务） | doc2 §4.8；内部维护 unit/object 索引 |
| `MapStateLookupAdapter` | sealed class（MAP-06 适配） | doc2 §4.9；IHeight/ICover/IBlocking |

---

## 数据流

```
JSON 加载 (Data 层, MAP-13)
        ↓
TileDefinition[] → TileDefinitionRegistry.Register(def)
        ↓
   (可选) MapTileState per coord
        ↓
TileOccupancyService.AttachTileDefinitionRegistry(map, registry)
        ↓
   MapStateLookupAdapter(map, registry)
        ↓
   LineOfSightService.ComputeLineOfSight(map, from, to, adapter.GetHeight(), adapter.GetCover(), adapter.BlocksLineOfSight())
```

---

## 确定性

所有集合遍历按 `GridCoord.CompareTo` 升序（Y → X → Layer），符合 AGENTS.md §11。
`MapTileState.ActiveMapEffects` 保留插入顺序，序列化时由调用方按字典序归一化
（如果跨进程一致性需要）。

---

## 与 MVP 的兼容性

doc2 地图系统引入新的 `TileDefinition` / `MapTileState` 类型，但 `Core.Model.TileState`
（Normal/Blocked/Hazard/Objective 4 类旧 enum）继续保留，由 179+ 既有测试使用。

`LegacyTileStateAdapter` 把 4 类旧 enum 映射为新 `TileDefinition`，确保过渡期内的双向兼容。
后续在 MAP-13 Data 层加载 JSON 时，旧 enum 字段将被自动转换为新 `TileDefinition`，无需修改现有测试。

---

## 引用

- `Docs/02_Technical_Development_Manual.md` §4.1–§4.9（地形 / 标签 / 占地 / 定义 / 登记 / 状态 / 适配 / 占用 / 接口）
- `Docs/MAP_SYSTEM_AUDIT.md` §6.1 Row 66 (P0 MAP-04, 状态 35% → 100%)
- `Docs/MAP_SYSTEM_AUDIT.md` §4 测试矩阵 Row 131
- `AGENTS.md` §10.1 Core 硬约束（不引用 UnityEngine）
- `AGENTS.md` §11 确定性规则（集合遍历稳定排序）

---

## 依赖

- `Starfall.Core`（`Map/Coordinates` + `Map/Cover` + `Map/Height` + `Map/LineOfSight` + `Map/State`）
- 测试：`Assets/Starfall/Tests/EditMode/Map/Tile/`（NUnit + EditMode）
- 不依赖 `Starfall.Unity` / `Starfall.Data`