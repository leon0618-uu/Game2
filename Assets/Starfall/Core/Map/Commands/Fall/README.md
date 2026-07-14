# Map/Commands/Fall/ — MAP-08 Fall 解析

> doc2 MAP-08 §6.1 — 单位从 invalid cell → 最近合法落点。

## 文件

- `FallResolutionService.cs` — 静态服务；`FindNearestLegalLanding(map, coord, unitId)` → `GridCoord?`。

## 搜索排序

1. 曼哈顿距离 `|x - ox| + |y - oy|` 升序（Layer 不参与距离）。
2. `GridCoord.CompareTo()`（Y → X → Layer）升序。
3. 同坐标跨 Layer：原 layer 优先（因为 Manhattan=0 时 CompareTo 固定）。

## 合法落点定义

- `coord.IsInBounds(map.Definition.Size)`
- `!def.BlocksMovement && !def.PhaseLocked`（阻挡相位锁的 tile 直接拒绝）
- `!TileOccupancyService.IsOccupied(map, coord)`（除非占者就是 unitId 自身）
- tile 的 ActiveDimension 等于 coordinate.Layer（未 flip → map.ActiveLayer 默认）

## 无解语义

返回 `null` → 上层 FallingCommand fallback 扣 HP + 发 `BattleEvent.UnitEnteredVoid`。
