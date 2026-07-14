# Starfall.Core.Map.LineOfSight

> doc2 MAP-06 §4.3 + §4.6 视线服务 + 弹道分类 + 高地优势。

本目录属于 `Starfall.Core` 程序集，**纯 C# 实现**，**严禁 `Physics.Raycast`**。
视线判定使用整数 Supercover（Bresenham 变体）枚举路径格子，与浮点 / 物理
引擎解耦，保证 Replay 确定性（AGENTS.md §10.1 + §11）。

---

## 类型一览

| 类型 | 角色 | 关键约束 |
|------|------|----------|
| `ProjectileType` | enum byte（Direct=0..CrossPhase=5） | doc2 §4.3；6 类；数值顺序固定 |
| `IHeightLookup` / `ICoverLookup` / `IBlockingLookup` | 3 个查询接口 | 数据层解耦；null = 视全部为 0/None/false |
| `LineOfSightService.Result` | readonly struct（HasLOS / HasHG / Penalty / Blockers） | 调用方消费 |
| `LineOfSightService` | static class（ComputeLOS + ComputeProjectileLOS + TraceSupercoverPath） | 纯函数；同 Layer 默认；CrossPhase 跨层 |

---

## 算法：Supercover

```text
x0, y0 = from.XY
x1, y1 = to.XY
dx = abs(x1 - x0)
dy = abs(y1 - y0)
sx = x0 < x1 ? +1 : -1
sy = y0 < y1 ? +1 : -1
err = dx - dy
loop:
  emit (x, y)
  if (x, y) == (x1, y1) break
  e2 = 2 * err
  if e2 > -dy: err -= dy; x += sx
  if e2 <  dx: err += dx; y += sy
```

**不变量**：
- 同 (x0, y0) → (x1, y1) 永远得到同一序列（无分支依赖输入顺序之外的因素）；
- 输出包含起点 + 终点；
- 整数算术，无浮点（跨平台一致）；
- 同起终点的 path 唯一（与 Bresenham 选择 X-first / Y-first 的惯例一致）。

---

## 高地优势（High Ground）

| 条件 | 结果 |
|---|---|
| 同 Layer + attacker.Height - defender.Height ≥ 1 | `HasHighGroundBonus = true` |
| 跨 Layer | `HasHighGroundBonus = false`（CrossPhase 弹道不算） |
| High Ground 下 Half Cover | 忽略（不计入 CoverPenalty） |
| High Ground 下 Full Cover | 仍给 CoverPenalty=2（由调用方决定是否忽略） |

---

## 6 种弹道规则

| 类型 | Full Cover | Half Cover | 跨 Layer |
|---|---|---|---|
| `Direct` | 阻挡 | penalty=1 | 默认阻挡 |
| `Arc` | 阻挡 | **忽略**（penalty=0） | 默认阻挡 |
| `Beam` | 同 Direct | penalty=1 | 默认阻挡 |
| `Chain` | 同 Direct | penalty=1 | 默认阻挡 |
| `GroundPropagation` | 阻挡（仅看 ground 层） | 仅看 ground 层 | 仅 ground |
| `CrossPhase` | 不挡 | penalty=1 | **穿透**（先 attacker.Layer 再 defender.Layer） |

> Beam / Chain 的"次目标选择"留上层（`MapCommandExecutor` 后续），
> LOS 服务只判定首目标可达性。

---

## 与 `MapState` 的集成

`ComputeLineOfSight` / `ComputeProjectileLOS` 接受 `MapState` 作为第一个参数，
但**只读 `MapDefinition.Size`**（用于越界检查），不读任何运行时字段。
本轮不修改 `MapState` 字段表（保持 MAP-02 已合并的纯数据容器 + ADR-0003
哈希契约）。

---

## 引用

- `Docs/02_Technical_Development_Manual.md` §10.5（视线数据契约）
- `Docs/MAP_SYSTEM_AUDIT.md` §6.1 Row 68
- `AGENTS.md` §11（Supercover 输出唯一性 + 排序）
- `AGENTS.md` §10.1（Core 无 UnityEngine + 不准 `Physics.Raycast`）
