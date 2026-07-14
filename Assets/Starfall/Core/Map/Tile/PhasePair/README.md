# MAP-07 · 双层配对（PhasePairLookup + CrossLayerValidator）

## 角色

doc2 MAP-07 双层地块模型的**配对存储 + 验证**子模块。本目录提供：

1. `PhasePairLookup` — attach-mode 静态服务：根据 `TileDefinition.PhasePairTileId`
   字段，索引双向配对关系（`tileA → tileB` 与 `tileB → tileA`）。
2. `CrossLayerValidator` — 静态校验：检查 (a) 配对双向一致、(b) `PhasePairTileId`
   指向有效 tile、(c) flip 后两个 tile 同步。
3. `ValidationResult` — 校验结果值类型。

## 不引用 UnityEngine

本子模块严格遵循 AGENTS.md §10.1：`Starfall.Core.noEngineReferences = true`。

## 纯 C# / 零随机 / 稳定排序

所有遍历按 `(tileId)` 升序；本服务不持有 `MapState` 任何字段引用
（map 仅作为 attach key，不持有 `MapState` 内 map）。所有 `IReadOnly*`
接口返回稳定快照，调用方多次访问结果一致。

## 与既有系统的关系

- **MAP-04**：`TileDefinition.PhasePairTileId`（`int?`）字段已经存在；
  本服务把该字段"wire"为可查询的配对关系。
- **MAP-08**：`PhaseFlipStateService` 改用 `MapTileState.ActiveDimension` 字段后，
  本服务提供 `TryGetPair` 给 `FlipTilePhaseCommand` / `FlipRegionPhaseCommand`
  实现 cascade flip。
- **MAP-06**：`CrossLayerValidator` 提供 flip 验证，与 LOS 服务无依赖。

## 不变量

1. **双向一致**：若 tileA 指向 tileB，则 tileB.PhasePairTileId 必须等于 tileA
   —— 否则 `CrossLayerValidator.Validate` 返回 `PAIR_ASYMMETRIC`。
2. **孤儿检测**：`PhasePairTileId` 指向不存在的 tileId → `PAIR_ORPHAN`。
3. **同步校验**：配对的两个 tile 已 flip 时 `ActiveDimension` 必须一致 →
   `FLIP_DESYNC`（仅在传入 runtimeStates 时校验）。

## 测试

本子目录下 tests 不在本目录下，统一放到
`Assets/Starfall/Tests/EditMode/Map/Tile/DualLayerTests.cs` +
`PhaseFlipValidationTests.cs` + `CrossPhaseLineOfSightTests.cs` +
`PhasePairRoundTripTests.cs` + `TileDefinitionPhasePairTest.cs`。

## Lead self-fix 参考

实施前已读 `D:\UntiyProject\XingyuanCovenant\memory\2026-07-15.md`
6 类常见 bug playbook；本目录实现已规避：

1. .cs.meta 用最简 `fileFormatVersion: 2 + guid:` 格式（无 inline instanceID）。
2. 拼写错：使用 `DimensionLayer.Reality` / `DimensionLayer.Astral` 正确
   拼写（不写 `ShalterAstralTide` 等错别字）。
3. 数组初始化：`Validate(map, registry, runtimeStates)` 参数分行处理，
   避免多元素内联 `new[] {...}` 解析失败。
4. Contains 扩展歧义：本服务**不调用** `.Contains()` 扩展方法（避免
   解析到 `MemoryExtensions.Contains`）；用 `Dictionary.TryGetValue` +
   `HashSet.Contains`（实例方法）替代。
5. cost boundary：BaseMoveCost 范围由 MAP-04 既有约束，不在本轮放宽。
6. 跨层测试缺注册：所有跨层测试 fixture 在 `[SetUp]` 注册 Reality +
   Astral 两层 tile（双层样本）。
