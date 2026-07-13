# Starfall.Core.Map.Coordinates

> doc2 MAP-01 基础：三维逻辑网格坐标 + 方向枚举 + 地图尺寸 + 双层网格容器。

本目录属于 `Starfall.Core` 程序集，**纯 C# 实现**，不引用 `UnityEngine` /
`UnityEditor`（AGENTS.md §10.1）。所有战斗状态变化都通过 Command 表达，
本目录只提供无副作用的纯逻辑类型。

---

## 子目录说明

`Map/Coordinates/` 负责**地图的几何与容器语义**：

- 不实现 IMapCommand / MapState / MapDefinition（属于 MAP-02 范围）；
- 不解析 JSON / ScriptableObject（属于 `Starfall.Data`）；
- 不负责表现层（属于 `Starfall.Unity`）。

---

## 五个类型一览

| 类型 | 角色 | 关键约束 |
|------|------|----------|
| `DimensionLayer` | 维度层枚举（Reality=0, Astral=1） | doc2 §4.3；同 (X,Y) 不同 Layer 视为不同地块 |
| `GridDirection` | 4 邻居方向枚举（North=0, East=1, South=2, West=3） | AGENTS.md §11；**禁止重排** |
| `GridCoord` | 三维逻辑坐标 (X, Y, Layer) | doc2 §4.1；`readonly struct`，实现 `IEquatable` / `IComparable` |
| `MapSize` | 地图尺寸 (Width, Height)，Width ∈ [1,48]，Height ∈ [1,64] | doc2 §4.2；双层 TileCount = W × H × 2 |
| `GridMap<T>` | 双层网格容器（内部 `Dictionary<GridCoord, T>`） | doc2 §4.4；遍历按 `GridCoord.CompareTo` 排序 |

---

## 使用示例

```csharp
using Starfall.Core.Map.Coordinates;

// 1. 创建坐标（显式 Layer）。
var c = new GridCoord(5, 7, DimensionLayer.Reality);

// 2. 创建坐标（默认 Layer = Reality）。
var origin = new GridCoord(0, 0);

// 3. 双层网格容器 + 基本操作。
var map = new GridMap<int>(new MapSize(8, 10));
map.Set(c, 42);
int v = map[c];                       // 42
bool has = map.Contains(c);           // true

// 4. 4 邻居（顺序固定 North → East → South → West）。
foreach (var n in c.Neighbours())
    Console.WriteLine(n);

// 5. 曼哈顿距离（Layer 不参与距离）。
int d = c.ManhattanDistance(new GridCoord(8, 10, DimensionLayer.Astral));

// 6. 确定性遍历：所有已设置的坐标按 Y → X → Layer 排序。
foreach (var kv in map.AllEntries())
    Console.WriteLine($"{kv.Key} = {kv.Value}");

// 7. DeepClone：容器独立，原 map 改动不影响克隆。
var clone = map.DeepClone();
```

---

## 与现有 `GridPos` 的兼容性

doc2 地图系统引入 `GridCoord`（三维）作为**新地图相关代码**的标准坐标，
但**不替换** MVP 的 `GridPos`（仅 X/Y），原因：

1. `GridPos` 已经深度嵌入现有战斗 / Replay / Undo 系统（`BattleState`、
   `UnitState`、`Command`、`ReplayCodec` 等），替换会破坏 179+ 既有测试；
2. MVP 与 doc2 共存于同一仓库的过渡期，新代码用 `GridCoord`，旧代码保留 `GridPos`；
3. `GridCoord.CompareTo` 与 `GridPos.CompareTo` 排序键一致（Y → X），
   跨类型场景下序列化仍可比较。

未来在 MAP-03 之后逐步迁移 `BattleState` 时，`GridPos` 将被完全替换，
`GridCoord` 作为唯一坐标类型存在。本目录**不**提供 `GridPos → GridCoord`
隐式转换，避免隐式跨层操作带来的语义模糊。

---

## 引用

- `Docs/02_Technical_Development_Manual.md` §4.1–§4.5（坐标 / 尺寸 / 维度 / 容器 / 邻居）
- `Docs/03_Data_and_Content_Spec.md`（JSON 字段映射）
- `AGENTS.md` §11 确定性规则（4 邻居顺序、网格排序键）
- `AGENTS.md` §10.1 Core 硬约束（不引用 UnityEngine）

---

## 依赖

- `Starfall.Core`（无外部依赖）
- 测试：`Assets/Starfall/Tests/EditMode/Map/Coordinates/`（NUnit + EditMode）
