# Starfall.Core.Map.Height

> doc2 MAP-06 §4.1 + §4.4 高度遍历与移动配置。

本目录属于 `Starfall.Core` 程序集，**纯 C# 实现**，不引用 `UnityEngine` /
`UnityEditor`（AGENTS.md §10.1）。所有判定都是纯函数，无副作用。

---

## 类型一览

| 类型 | 角色 | 关键约束 |
|------|------|----------|
| `HeightLevel` | readonly struct（0..4） | doc2 §4.1；越界静默 clamp；`IEquatable` + `IComparable` |
| `MovementProfile` | readonly struct（CanFly / Ascend / Descend / CrossDim） | doc2 §9.4；`Standard` + `Flyer` 内置 |
| `HeightTraversalService` | static class（3 个判定方法 + 1 个排序工具） | 纯函数；飞行短路；`Y → X` 稳定排序 |

---

## 与现有 `Core/Model/UnitState.MovementType` 的关系

MVP 的 `UnitState` 已经有 `MovementType` 枚举（Walk/Fly），但**字段语义太粗**：
- `MovementType.Fly` → 无视地形，但 doc2 §9.4 还要求"可下降 / 不可下降"；
- `MovementType.Walk` → 单值，无法表达重装 vs 步兵的差异。

`MovementProfile` 是 `MovementType` 的**细粒度升级**，并存于本目录：
- MVP 代码继续用 `MovementType`（向后兼容）；
- 新 doc2 代码（MAP-06 视线 / MAP-08 翻转 / 后续）统一用 `MovementProfile`。

未来 MAP-03 后 `UnitState.MovementType` 将被替换为 `UnitState.MovementProfile`
并写入序列化；本目录**不**直接替换。

---

## 使用示例

```csharp
using Starfall.Core.Map.Height;

// 1. 标准步兵：上 1 下 2
var std = MovementProfile.Standard;
bool canClimb2 = HeightTraversalService.CanTraverse(
    new HeightLevel(1), new HeightLevel(3), std);  // false（+2 超过 MaxAscend=1）

// 2. 飞行单位：无视所有高度差
bool canFly = HeightTraversalService.CanTraverse(
    new HeightLevel(0), new HeightLevel(4), MovementProfile.Flyer);  // true

// 3. 同 height → 恒 true
bool same = HeightTraversalService.CanTraverse(
    new HeightLevel(2), new HeightLevel(2), std);  // true

// 4. 排序（Y → X，height 优先）
var sorted = HeightTraversalService.SortByHeightAscending(new[] {
    new KeyValuePair<GridCoord, HeightLevel>(new GridCoord(3, 1), new HeightLevel(2)),
    new KeyValuePair<GridCoord, HeightLevel>(new GridCoord(1, 0), new HeightLevel(0)),
    new KeyValuePair<GridCoord, HeightLevel>(new GridCoord(2, 0), new HeightLevel(1)),
});
// sorted = [{ (1,0)=0 }, { (2,0)=1 }, { (3,1)=2 }]
```

---

## 引用

- `Docs/02_Technical_Development_Manual.md` §9.4（移动配置）
- `Docs/MAP_SYSTEM_AUDIT.md` §6.1 Row 68（MAP-06 范围）
- `AGENTS.md` §11（稳定排序、确定性）
- `AGENTS.md` §10.1（Core 无 UnityEngine）
