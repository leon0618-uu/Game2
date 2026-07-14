# Starfall.Core.Map.Cover

> doc2 MAP-06 §4.2 + §4.5 掩体等级 + 方向 + 查询服务。

本目录属于 `Starfall.Core` 程序集，**纯 C# 实现**，不引用 `UnityEngine` /
`UnityEditor`（AGENTS.md §10.1）。所有判定都是纯函数，无副作用。

---

## 类型一览

| 类型 | 角色 | 关键约束 |
|------|------|----------|
| `CoverLevel` | enum byte（None=0, Half=1, Full=2） | doc2 §4.2；数值顺序 = 强度顺序 |
| `CoverDirection` | enum byte（North=0, East=1, South=2, West=3, All=4） | doc2 §4.2；前 4 值与 `GridDirection` 同 byte |
| `CoverQueryService` | static class（attacker→direction + query） | 接受 `ICoverLookup` 解耦；`QueryCover` + `QueryCoverDiagonal` |

---

## 攻击方向规则

`CoverQueryService.ComputeAttackDirection(attacker, defender)` 返回 defender
**看** attacker 所在方向（即掩体应放在 defender 哪一侧）：

| 几何关系 | 返回 |
|---|---|
| 同 tile | `All` |
| 共 X（Y 不同） | `North` 或 `South`（看 dy 符号） |
| 共 Y（X 不同） | `East` 或 `West`（看 dx 符号） |
| 对角线 | 主轴按 \|Δ\| 较大者；相等时 X 优先（确定 tie-break） |

---

## 数据层解耦

`CoverQueryService` 接受 `Starfall.Core.Map.LineOfSight.ICoverLookup` 接口，
不直接读 `MapState.Tiles` / `MapDefinition`。这是为了：
1. 单元测试可独立构造 lookup；
2. 后续 MAP-04 引入 `TileDef.Cover` 时由 Data 层构造适配器；
3. 不破 ADR-0003（`MapState` 字段表不变）。

当前没有 `TileDef` 的情况下，调用方传 `null` 或简单字典适配器即可。

---

## 引用

- `Docs/02_Technical_Development_Manual.md` §10.5（掩体数据契约）
- `Docs/MAP_SYSTEM_AUDIT.md` §6.1 Row 68
- `AGENTS.md` §11（4 邻居顺序、CoverDirection 与 GridDirection 对齐）
- `AGENTS.md` §10.1（Core 无 UnityEngine）
