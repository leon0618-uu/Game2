# Map/Commands/Compression/ — MAP-08 PhaseCompression 解析

> doc2 MAP-08 §6.1 — 同 (X, Y, Layer) 上多 unit → 弹回一个 unit 到最近空 cell。

## 与 CrushResolver 的关系

| 维度 | `CrushResolver`（既有）| `PhaseCompressionResolutionService`（MAP-08 新增） |
|---|---|---|
| 语义 | HP damage（扣血）| 弹回位移（移动到邻居 cell）|
| 触发 | 同 `GridPos` ≥2 unit | 同 `GridCoord`（含 Layer）≥2 unit |
| 输出 | `AffectedUnitIds`（全员扣血）| `(displacedUnitId, newCoord)` |

两者**并存不冲突**：squeeze（挤压）= 位移，crush = 损血。doc2 §6.1 明确这一点。

## 弹回规则

1. 取 `unitIdsAtCoord` 最后一个为 `displacedUnitId`。
2. 优先 4 邻居（N→E→S→W，曼哈顿距离 1）。
3. 退到曼哈顿距离 2 圈（按 CompareTo Y→X→Layer 排序）。
4. 全不可达 → 返回 `null`（调用方 fallback）。

## 不修改占用

本服务只计算 `(displacedUnitId, newCoord)`。实际 `TileOccupancyService.TryRemoveUnit` +
`TryPlaceUnit` 由 `FallingCommand` 重构版完成。
