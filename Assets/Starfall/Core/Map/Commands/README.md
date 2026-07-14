# Map/Commands/ — MAP-08 Phase Flip + Fall + Crush

doc2 §3.4 MAP-08 核心玩法命令集。本目录存放跨 MAP-03 Phase 1 地图命令、
MAP-08 Phase Flip / Fall / Crush 子系统共用的命令接口与子目录说明。

## 文件索引（本轮 MAP-08）

- `IMapCommand.cs` — Stub 接口（MAP-08 引入；完整实现待 MAP-03）。
- `MapCommandResult.cs` — 命令执行结果（成功 / 失败 + 影响 cells + 失败原因）。
- `PhaseFlipStateService.cs` — per-tile phase flip 副作用 attach 模式状态。
- `FlipTilePhaseCommand.cs` — 单 tile 相位翻转（含 PhaseLocked / PhaseFlippable 校验）。
- `FlipRegionPhaseCommand.cs` — 区域内所有 tile 同步翻转（atomic）。
- `Fall/FallResolutionService.cs` — 单位原坐标 invalid → 最近合法落点。
- `Compression/PhaseCompressionResolutionService.cs` — 同 (X,Y) 多 unit → 弹回一个。

## 设计约束（继承 AGENTS.md §10.1 + §11 + BOOTSTRAP §核心玩法）

1. **Core 纯净** — 不引用 UnityEngine / UnityEditor、不读 prefab / scene。
2. **确定性** — 所有循环按 Y → X → Layer 排序；不含随机；不读时间。
3. **不改 MAP-04 字段** — `Assets/Starfall/Core/Map/Tile/*` 自 `9b8956b` 起冻结。
4. **不改 MAP-06 字段** — `Assets/Starfall/Core/Map/{LineOfSight,Cover,Height}/*` 自 `ff0c641` 起冻结。
5. **不改 MapState 字段表** — `MapStateCloner` / `MapStateHasher` 自 ADR-0003 Accept 起冻结。
6. **PhaseFlipStateService 与 TileOccupancyService 一致** — attach 模式 +
   静态字段，测试 [SetUp] / [TearDown] 必须 Attach / Detach。

## 命令 → 事件流（MAP-08 不发业务事件）

本目录命令**不**发 BattleEvent。上层 BattleRunner 在成功执行 Flip/Fall/Compression 后
注入 `BattleEvent.UnitEnteredVoid` / `BattleEvent.UnitPhaseCompressed` 到 EventSink。
