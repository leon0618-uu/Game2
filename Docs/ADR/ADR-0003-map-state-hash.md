# ADR-0003: MapState 哈希契约（深拷贝 + 确定性 FNV-1a）

- **状态**：Proposed（将在 `agent/map-02-map-state` 实现通过 [MAP_SYSTEM_FORWARD_PLAN §3.4](../../Docs/MAP_SYSTEM_FORWARD_PLAN.md#34-验收标准) Gate 后升为 Accepted）
- **日期**：2026-07-14
- **作者**：xingyuan-architect
- **关联任务包**：MAP-02 `agent/map-02-map-state`（Route A 增量升级）
- **关联文档**：
  - 扩展 [ADR-0001](./ADR-0001-core-data-model-and-hash.md)（Core 数据模型与 FNV-1a 64 哈希基础）
  - 被 [ADR-0002](./ADR-0002-presenter-sync-contract.md) 隐含引用（`BattleState` 快照驱动 Render 的输入）
  - 规范来源：[MAP_SYSTEM_FORWARD_PLAN §3.2](../../Docs/MAP_SYSTEM_FORWARD_PLAN.md#32-范围严格-route-a)
  - 路线依据：[MAP_SYSTEM_AUDIT §3.3](../MAP_SYSTEM_AUDIT.md#33-冲突与兼容性)（`MapState` 嵌入 `BattleState` 的 route A 决策）
  - 已知修复清单：[MAP_SYSTEM_AUDIT §6.5](../MAP_SYSTEM_AUDIT.md#65-关键修复无论选哪条路线都必须做)

---

## Context

MAP-02 (`agent/map-02-map-state`) 引入 `MapState` 作为地图侧运行时唯一真相源后，立即面临两个互锁问题：

1. **可克隆性**：Reentrant 场景下 `MapState` 必须能深拷贝，且克隆后修改不得影响原状态；当前 `BattleStateCloner.Clone` 在 [MAP_SYSTEM_AUDIT §6.5](../MAP_SYSTEM_AUDIT.md#65-关键修复无论选哪条路线都必须做) #3 已知限制下**不复制 `Statuses / Anchors / Decrees`，嵌入的 `MapState` 也未被复制**。MAP-02 必须同时解决嵌入层复制问题。
2. **可哈希性**：Replay 验证（Task 12 / MAP-18）要求「相同初始状态 + 相同 Command 序列 = 相同 `PostStateHash`」。如果 `MapState` 沿用 [ADR-0001](./ADR-0001-core-data-model-and-hash.md) §3 把地图字段塞进 `BattleState.PostStateHash` 的字段表（位置 5-8），则任何地图字段的插入 / 删除都会破坏现有 179 EditMode 测试的预期哈希——这是 [MAP_SYSTEM_AUDIT §3.3](../MAP_SYSTEM_AUDIT.md#33-冲突与兼容性) 明确指出的「中度冲突」。

route A 选择「**嵌入 + 适配器**」：`MapState` 是独立 `class`，由 `BattleState.MapState` 属性持有引用；`MapState` 自身拥有 `PostStateHash`（仅含地图字段），`BattleState.PostStateHash` 内部调用 `MapState.PostStateHash` 后再混入战斗字段（与 [ADR-0001](./ADR-0001-core-data-model-and-hash.md) §3 的字段顺序向后兼容）。

这一选择把哈希责任下沉到 `MapState` 自己的 `MapStateHasher`，让后续 MAP-07（双层维度）、MAP-11（CV / Tile 稳定性）扩展时只修改 `MapStateHasher` 而不动 `BattleState` 的字节流布局——只要 §7「`BattleState` 边界」维持。

---

## Decision

### 1. 哈希算法

| 项 | 值 |
| --- | --- |
| 算法 | **FNV-1a 64 位**（与 [ADR-0001](./ADR-0001-core-data-model-and-hash.md) §2 一致，零依赖跨平台） |
| Offset basis | `0xCBF29CE484222325`（十进制 `14695981039346656037`） |
| Prime | `0x100000001B3`（十进制 `1099511628211`） |
| 字节序 | 小端序（little-endian） |
| 中间状态 | `ulong` 无符号溢出回卷（与 .NET 默认一致） |
| 输出 | `ulong PostStateHash`，范围 `[0, 2^64)` |

伪代码：

```text
hash = 0xCBF29CE484222325
for each byte in byte_stream:
    hash = (hash XOR byte) * 0x100000001B3
return hash
```

### 2. 编码格式（按字段顺序链式写入）

每个字段按本 ADR §4 表顺序进入字节流。**类型标记 + 长度前缀**保证未来字段插入不会破坏历史哈希：

- **类型标记**：`uint8` 1 字节，标识字段类型（见 §3 枚举）。
- **定长字段**：类型标记后接定长字节块（int32 = 4 字节 LE、enum-as-int = 4 字节 LE 等）。
- **变长字段**（字符串、集合、嵌套结构）：类型标记后接 `uint32` 长度前缀（LE，4 字节），再接 N 个内容字节。
- **空集合**：长度前缀 = `0x00000000`（4 字节 LE 全零），**不**写任何内容字节。
- **字节序**：所有多字节整数 LE；字符串 UTF-8 无 BOM。

### 3. 类型编码表

| 字段类型 | 类型标记 (`uint8`) | 内容字节 |
| --- | --- | --- |
| `string`（UTF-8） | `0x01` | `uint32` 长度（字节数，LE） + N 个 UTF-8 字节 |
| `int32` | `0x02` | 4 字节 LE |
| `int`（enums cast 为 int） | `0x03` | 4 字节 LE |
| `GridCoord`（包装 `GridPos` + `Layer`） | `0x10` | 嵌套记录（见 §4 Tile 行） |
| 集合起始标记（`Array` / `List`） | `0x20` | `uint32` 元素数（LE）+ 逐元素字节 |
| 嵌套结构 `MapState` 字段（`Definition`） | `0x30` | 内联展开（见 §6），不再嵌套 `MapState.PostStateHash`——避免双重哈希 |
| 保留 | `0x00` / `0x80-0xFF` | n/a（未来扩展） |

> 浮点（`float` / `double`）**不在 MapState 哈希范围**（见 §5）；如未来必须引入，按 `0x04` / `0x05` IEEE-754 LE + 显式 NaN 规范化（IEEE 754-2008 `canonicalNaN`）。

### 4. 字段编码表（顺序固定）

> 任何字段顺序变更必须新建 ADR（与 [ADR-0001](./ADR-0001-core-data-model-and-hash.md) §Consequences 同样的不可变原则）。空集合 = `0x00000000` 长度。

| # | 字段 | 类型 | 类型标记 | 字节布局 |
| --- | --- | --- | --- | --- |
| 1 | `MapId` | `string` | `0x01` | UTF-8 字节 + `uint32` 长度前缀 |
| 2 | `Size.Width` | `int32` | `0x02` | 4 字节 LE |
| 3 | `Size.Height` | `int32` | `0x02` | 4 字节 LE |
| 4 | `Version` | `int32` | `0x02` | 4 字节 LE |
| 5 | `ActiveLayer` | `int`（`DimensionLayer` 枚举 cast） | `0x03` | 4 字节 LE |
| 6 | `GlobalCollapseValue` | `int32` | `0x02` | 4 字节 LE（合法范围 0-100，doc1 §13.1） |
| 7 | `Tiles` | `IReadOnlyList<GridCoord>` | `0x20` | `uint32` count + 按 §5 排序的 `GridCoord` 链 |
| 8 | `Anchors` | `IReadOnlyList<AnchorZone>` | `0x20` | `uint32` count + 按 §5 排序的 `AnchorZone` 链 |
| 9 | `Regions` | `IReadOnlyList<MapRegion>` | `0x20` | `uint32` count + 按 §5 排序的 `MapRegion` 链 |
| 10 | `Objects` | `IReadOnlyList<MapObjectInstance>` | `0x20` | `uint32` count + 按 §5 排序的 `MapObjectInstance` 链 |
| 11 | `Definition` 字段组 | 嵌套（内联） | `0x30` | 见 §6（**扁平展开，不嵌套 `MapState.PostStateHash`**） |

子结构 `GridCoord`（类型标记 `0x10`）写入顺序：

```text
type_tag 0x10
Layer    int32 LE  (DimensionLayer cast)
X        int32 LE
Y        int32 LE
// 写入顺序与 CompareTo 比较顺序（Y → X → Layer）**不同**；
// 写入顺序按 Layer → X → Y 是为字节流「前向兼容」——历史 Replay 在 Layer 字段插入时仍可继续读取
```

子结构 `AnchorZone`（类型标记 `0x10`）写入顺序：

```text
type_tag 0x10
ZoneId   int32 LE
Owner    string  (UTF-8 + uint32 长度)
Vertices 集合标记 0x20 + uint32 count + 按 §5 排序的 GridPos 链
```

子结构 `MapRegion`（类型标记 `0x10`）写入顺序：

```text
type_tag 0x10
RegionId   string (UTF-8 + uint32 长度)
State      int  (RegionState 枚举 cast, 4 字节 LE)
其余字段顺序在本 ADR 升 Accepted 时由 map-02 实现钉死
```

子结构 `MapObjectInstance`（类型标记 `0x10`）写入顺序：

```text
type_tag 0x10
ObjectId  string (UTF-8 + uint32 长度)
State     int  (MapObjectState 枚举 cast, 4 字节 LE)
其余字段顺序在本 ADR 升 Accepted 时由 map-02 实现钉死
```

### 5. 排序键与确定性规则

**强制先排序后写入**（与 [AGENTS.md §11](../AGENTS.md)「影响结果的集合遍历必须稳定排序」一致）：

| 集合 | 排序键 | 备注 |
| --- | --- | --- |
| `Tiles` | `GridCoord.CompareTo`（**Y → X → Layer**） | 与 [ADR-0001](./ADR-0001-core-data-model-and-hash.md) `GridPos.CompareTo` 一致（Y 优先）；扩展为含 `Layer` 三维比较；写入顺序 `Layer → X → Y` 仅用于字节流（见 §4） |
| `Anchors` | `ZoneId` 升序 | `AnchorZone` 内部顶点已在构造期按 `GridPos.CompareTo` 排序（见 [Assets/Starfall/Core/Anchor/AnchorZone.cs](../../Assets/Starfall/Core/Anchor/AnchorZone.cs) L20），故顶点的稳定写入由 `AnchorZone` 自身保证 |
| `Regions` | `RegionId` 字符串序数（ordinal） | 当前 `RegionId` 为 `string`；后续若改为 `RegionId : IComparable<RegionId>` 需新建 ADR |
| `Objects` | `ObjectId` 字符串序数（ordinal） | 同 `RegionId` 约束 |
| `Anchor.Vertices` | `GridPos.CompareTo`（Y → X） | 由 `AnchorZone` 构造器保证（[Assets/Starfall/Core/Anchor/AnchorZone.cs](../../Assets/Starfall/Core/Anchor/AnchorZone.cs) L19 `list.Sort()`） |

**已知跟踪项（不阻塞 MAP-02）**：

- `AnchorZone` 当前排序只到 `(Y, X)`，**未做** doc2 §12 要求的「闭合路径 + 排除自相交 + 固定顶点排序（旋转 / 镜像不变）」。这是 MAP-12 锚点连线阶段的工作，详见 [MAP_SYSTEM_AUDIT §3.3](../MAP_SYSTEM_AUDIT.md#33-冲突与兼容性)「多边形顶点排序」行；本 ADR 范围**仅依赖现有 `GridPos.CompareTo` 排序**，不修改 `AnchorZone`。
- 邻居顺序修复已在 `5cc4644` 完成（`BFSPathfinder N→E→S→W`，[MAP_SYSTEM_AUDIT §6.5](../MAP_SYSTEM_AUDIT.md#65-关键修复无论选哪条路线都必须做) #1）。本 ADR 不涉及寻路。

### 6. `MapDefinition` 签名字段组（`0x30` 内联展开）

`MapDefinition`（`readonly struct`，[MAP_SYSTEM_FORWARD_PLAN §3.2](../../Docs/MAP_SYSTEM_FORWARD_PLAN.md#32-范围严格-route-a)）字段：`MapId` / `Size` / `InitialActiveLayer` / `InitialGlobalCollapseValue` / `TilesetId` / `EnvironmentScheduleId`。

**决策**：**字段组内联展开**，不嵌套调用 `MapDefinition.GetHash()`。理由：

1. 字节流保持扁平，避免 `hash = (hash XOR inner_hash)` 二次 FNV-1a 调用引入的额外碰撞维度。
2. `MapStateHasher` 是单一职责类（一个状态 → 一个字节流 → 一个哈希），不依赖 `MapDefinition` 实现 `IHashable`。
3. 历史 Replay 在新增 `MapDefinition` 字段时只需在 §4 字段表追加——`MapState.PostStateHash` 的字节布局是线性追加而非嵌套结构。

`MapDefinition` 字段组写入顺序（`0x30` 后）：

```text
0x30 (字段组标记)
MapId                          string (0x01)
Size.Width                     int32 (0x02)
Size.Height                    int32 (0x02)
InitialActiveLayer             int enum-cast (0x03)
InitialGlobalCollapseValue     int32 (0x02)
TilesetId                      string (0x01)
EnvironmentScheduleId          string (0x01)
```

注意：`MapId` 在 §4 字段表位置 1 也出现一次（`MapState.MapId` 字符串字段）。这是有意的——`MapState` 与 `MapDefinition` 是两个独立对象，hash 必须对两者的 `MapId` 一致性敏感（未来若 `Definition.MapId` 被 patch，hash 必须变）。如果未来发现重复输入导致冗余，可在 `ADR-0004-hash-schema-v2` 中删除其一。

### 7. 与 `BattleState.PostStateHash` 的边界（route A embed-and-compose）

`BattleState.PostStateHash`（[ADR-0001](./ADR-0001-core-data-model-and-hash.md) §3）字节流 = `MapState` 字段 + 战斗字段，按以下顺序：

```text
1. MapState.PostStateHash        ← 本 ADR §4 字段表（11 个字段，含 §6 内联展开）
2. TurnNumber                    ← ADR-0001 §3 位置 1
3. ActivePlayer                  ← ADR-0001 §3 位置 2
4. GridWidth                     ← ADR-0001 §3 位置 3
5. GridHeight                    ← ADR-0001 §3 位置 4
6. Units                         ← ADR-0001 §3 位置 5
7. TileStates                    ← ADR-0001 §3 位置 6
8. Statuses                      ← ADR-0001 §3 位置 7
9. PendingDecrees                ← ADR-0001 §3 位置 8
```

**关键不变量**：

- `BattleState.PostStateHash` 字节流第 1 段是 `MapState.PostStateHash` 的完整 8 字节 ulong（先按 §4 算完 ulong，再以 LE 8 字节嵌入 `BattleState` 字节流），**不是**把 §4 字段直接平铺到 `BattleState` 字节流。这避免了两个层级共用同一字节流布局带来的 future field 冲突。
- 后续如果 `MapState` 字段表扩充（例如 MAP-07 加 `Layer` 双层维度字段），`MapState.PostStateHash` 改变，但 `BattleState.PostStateHash` 字节流第 1 段仍然以「先算 ulong 再嵌入」的方式吸收——下游 Replay 只在 `MapState` 字段变更时哈希变化，战斗字段不变时不动。
- 旧 179 测试中已有的 `BattleState.PostStateHash` 期望值**保持不变**——因为 `BattleState` 嵌入的是 `MapState` 的旧字段集合对应的 ulong（如果旧测试用的 `BattleState` 没有 `MapState` 字段，则回退为 0——见 §8 #4）。

### 8. `MapStateCloner` 契约（与 hasher 配套）

> 哈希相同 ⇏ 浅拷贝隔离。MAP-02 同时落地 cloner 与 hasher（[MAP_SYSTEM_FORWARD_PLAN §3.2](../../Docs/MAP_SYSTEM_FORWARD_PLAN.md#32-范围严格-route-a)）。

```csharp
namespace Starfall.Core.Map.State
{
    public static class MapStateCloner
    {
        /// <summary>深拷贝。集合彻底独立（修改克隆不修改原状态）。Definition 是 readonly struct 直接复制。</summary>
        public static MapState DeepClone(MapState source);

        /// <summary>BattleStateCloner 调用入口。</summary>
        public static MapState DeepCloneForBattleState(MapState source);
    }

    public static class MapStateHasher
    {
        /// <summary>按本 ADR §1-§4 计算确定性哈希。</summary>
        public static ulong CalculateDeterministicHash(MapState state);
    }
}
```

强制规则：

1. **集合彻底独立**：`Tiles` / `Anchors` / `Regions` / `Objects` 必须新建 `List<T>`，不得共享 `IReadOnlyList<T>` 内部数组引用。
2. **嵌套元素深拷贝**：`AnchorZone` / `MapRegion` / `MapObjectInstance` 各自实现 `DeepClone()`，返回新实例。`GridCoord` 是 `readonly record struct` 隐式深拷贝。
3. **`Definition` 是 `readonly struct`**：按值复制，零分配。
4. **向后兼容 [MAP_SYSTEM_AUDIT §6.5](../MAP_SYSTEM_AUDIT.md#65-关键修复无论选哪条路线都必须做) #3**：升级后的 `BattleStateCloner.Clone` 必须显式调用 `MapStateCloner.DeepCloneForBattleState`，并验证克隆前后 `BattleState.MapState` 是不同引用、但 `BattleState.MapState.PostStateHash` 相等。
5. **零 UnityEngine 依赖**：`MapStateCloner` / `MapStateHasher` 不得 `using UnityEngine`，与 [AGENTS.md §10.1](../AGENTS.md) 一致；`CoreDependencyGuardTests` 自动验证。

---

## Test invariants（gate from [MAP_SYSTEM_FORWARD_PLAN §3.4](../../Docs/MAP_SYSTEM_FORWARD_PLAN.md#34-验收标准)）

实现必须通过以下不变式测试（与 [MAP_SYSTEM_FORWARD_PLAN §3.4](../../Docs/MAP_SYSTEM_FORWARD_PLAN.md#34-验收标准) 8 项 gate 对齐）：

1. **空 `MapState` 哈希稳定**：`CalculateDeterministicHash(empty)` 在 100 次连续调用中返回相同 ulong。
2. **跨运行一致**：同一 `MapState` 在不同进程（不同 .NET runtime、CLR seed）下哈希相同。
3. **修改任何字段哈希变化**：单字段 mutation（`MapId` / `Size.W` / `Size.H` / `Version` / `ActiveLayer` / `GlobalCollapseValue` / 任一 `Tile` / 任一 `Anchor` / 任一 `Region` / 任一 `Object` / 任一 `Definition` 字段）必须产生不同哈希。
4. **集合插入顺序无关**：向 `Tiles` / `Anchors` / `Regions` / `Objects` 以不同顺序插入相同元素集合，哈希相同。
5. **集合深拷贝**：`DeepClone(s)` 修改克隆的 `Tiles[0]` 不影响原 `s.Tiles[0]`；修改克隆的 `AnchorZone.Owner` 不影响原状态。
6. **`MapState` 引用独立**：`BattleStateCloner.Clone(b).MapState` 与 `b.MapState` 是不同对象引用但 `PostStateHash` 相等。
7. **战斗字段隔离**：未触战斗字段（仅修改 `MapState` 内部）时，`BattleState.PostStateHash` 仅第 1 段（`MapState.PostStateHash`）变化，战斗字段段不变。
8. **FNV-1a 已知向量**：实现时附带以下已知向量测试，防止 FNV-1a 实现错误：
   - `CalculateDeterministicHash(empty) = 0xCBF29CE484222325`（无任何字段 → 字节流为空 → FNV 初始值 = offset basis）
   - 单 `int32` 字段 `Version = 1`：`0xCBF29CE484222325 ^ 0x02 → * prime → ^ 0x01 0x00 0x00 0x00 → * prime` 链式 6 字节

> **测试规模最低线**（[MAP_SYSTEM_FORWARD_PLAN §3.2](../../Docs/MAP_SYSTEM_FORWARD_PLAN.md#32-范围严格-route-a)）：`MapStateCloneTests ≥ 10` + `MapStateHashTests ≥ 10` + `MapStateMutationIsolationTests ≥ 5` = **≥ 25 新 EditMode 测试**。EditMode `total ≥ 272`（247 baseline + 25）。

---

## Consequences

### 正面

- **Task 12 Replay 可独立验证地图层**：`MapState.PostStateHash` 是单一字段级哈希，可在不计算 `BattleState` 哈希的情况下比对两个 `MapState`（如 Replay 浏览器中按地图状态查找存档）。
- **未来 MAP-07 / MAP-11 字段扩展只影响 `MapState.PostStateHash`**：双层维度、CV / Tile 稳定性等字段直接加入 §4 字段表即可，`BattleState.PostStateHash` 第 1 段吸收变化；下游战斗字段段零修改。
- **`MapStateCloner` 修复 [MAP_SYSTEM_AUDIT §6.5](../MAP_SYSTEM_AUDIT.md#65-关键修复无论选哪条路线都必须做) #3**：升级后的 `BattleStateCloner.Clone` 把 `MapState` 一并复制，旧 14 个 `BattleStateClonerTests` 保持 PASS。
- **零新依赖**：FNV-1a 64 与 [ADR-0001](./ADR-0001-core-data-model-and-hash.md) 同实现，无第三方 dll，不改 `Packages/manifest.json`。
- **AGENTS.md §10.1 / §11 一致**：Core 无 UnityEngine 引用、集合稳定排序、FNV-1a 字节序明确。

### 负面 / 约束

- **`AnchorZone` 排序未达 doc2 §12 理想**：当前 `(Y, X)` 排序对 MAP-02 哈希足够，但不是旋转 / 镜像不变的规范化。MAP-12 阶段须新建 ADR（如 `ADR-000X-anchor-canonical-form`）重新定义顶点排序键。**本 ADR 显式记录此限制**——见 §5 已知跟踪项。
- **`MapId` 在 §4 与 §6 各写一次**：理论上引入轻微碰撞维度。当前决策是「保守写法」（两个字段都独立哈希）。如发现冗余，在 `ADR-0004` 删除 §6 中的 `MapId`。
- **浮点禁止**：`MapState` 哈希范围内不得出现 `float` / `double`。这意味着 `AnchorZone.Contains`（用浮点除法，见 [MAP_SYSTEM_AUDIT §3.3](../MAP_SYSTEM_AUDIT.md#33-冲突与兼容性)）的浮点计算**不进入哈希**——`AnchorZone` 只哈希顶点 `GridPos` 整数对，不哈希几何包含结果。MAP-12 阶段会引入整数定点 `Contains`，届时可考虑加入 §4。
- **`BattleState.PostStateHash` 旧期望值兼容**：179 测试中已有的 `BattleState.PostStateHash` 期望值基于「无 `MapState` 字段」计算。新实现必须让这些测试继续 PASS——见 §7 #4（无 `MapState` 时回退为 0）+ [MAP_SYSTEM_FORWARD_PLAN §3.4](../../Docs/MAP_SYSTEM_FORWARD_PLAN.md#34-验收标准)「旧 14 个 `BattleStateClonerTests` 继续 PASS」。

---

## Alternatives considered

### MapState 独立 top-level 哈希（route B 思路）

- **优点**：`MapState` 与 `BattleState` 完全解耦；未来若有 `MapState`-only 工具（如离线地图编辑器），直接复用 `MapStateHasher`。
- **否决理由**：route A 明确「嵌入 + 适配器」（[MAP_SYSTEM_AUDIT §5.1](../MAP_SYSTEM_AUDIT.md#51-路线-a增量升级-)）；route B 1-2 周重写代价与 179 测试破坏，MAP-02 不承担此范围。本 ADR §7 embed-and-compose 给出 route A 下「哈希可独立验证」的能力。

### SHA-256 / xxHash64

- **否决理由**：与 [ADR-0001 §Alternatives](../ADR/ADR-0001-core-data-model-and-hash.md#alternatives-considered) 同——零依赖优先、Replay 验证每回合 1 次非瓶颈。FNV-1a 64 与 [ADR-0001](./ADR-0001-core-data-model-and-hash.md) 同算法便于代码复用与心智模型统一。

### `BattleState.PostStateHash` 平铺 `MapState` 字段

- **否决理由**：把 §4 字段直接插入 [ADR-0001 §3](./ADR-0001-core-data-model-and-hash.md#3-哈希字段包含表顺序固定不得在原-adr-内-patch) 会破坏该表「顺序固定」原则；任何 `MapState` 字段变更都要 patch [ADR-0001](./ADR-0001-core-data-model-and-hash.md)，违反 ADR 自身的不变式。embed-and-compose（§7）保留了两个 ADR 的独立性。

### `MapDefinition` 嵌套哈希（先算 ulong 再嵌入）

- **否决理由**：见 §6 决策理由 #1——扁平字节流、单一职责、无双重 FNV-1a 嵌套。

---

## Known Follow-ups（不在本 ADR 范围）

| # | 项 | 关联任务 | 来源 |
| --- | --- | --- | --- |
| F1 | `AnchorZone` 旋转 / 镜像不变规范化（doc2 §12） | MAP-12 锚点连线 | [MAP_SYSTEM_AUDIT §3.3](../MAP_SYSTEM_AUDIT.md#33-冲突与兼容性) |
| F2 | 锚点整数定点 `Contains`（doc2 §14.4） | MAP-12 | [MAP_SYSTEM_AUDIT §3.3](../MAP_SYSTEM_AUDIT.md#33-冲突与兼容性) |
| F3 | `AnchorZone` 凸 / 凹多边形 + 自相交检测 | MAP-12 | [MAP_SYSTEM_AUDIT §3.3](../MAP_SYSTEM_AUDIT.md#33-冲突与兼容性) |
| F4 | `RegionId` / `ObjectId` 由 `string` 升级为 `IComparable<T>` 强类型 | MAP-09 / MAP-10 | 本 ADR §5 |
| F5 | `MapDefinition` 字段表扩展（5 张地图表，doc2 §19.2） | MAP-13 | [MAP_SYSTEM_FORWARD_PLAN §3.3](../../Docs/MAP_SYSTEM_FORWARD_PLAN.md#33-不在范围防止提前实现) |
| F6 | 双层维度字段（MAP-07 `Layer` 进入 `GridCoord` 哈希路径） | MAP-07 | [MAP_SYSTEM_FORWARD_PLAN §3.3](../../Docs/MAP_SYSTEM_FORWARD_PLAN.md#33-不在范围防止提前实现) |
| F7 | 性能基准（20×28 双层 / 48×64 逻辑压力地图） | MAP-18 | [MAP_SYSTEM_AUDIT §7.3](../MAP_SYSTEM_AUDIT.md#73-性能与规模) |

---

## References

- [ADR-0001-core-data-model-and-hash.md](./ADR-0001-core-data-model-and-hash.md) — FNV-1a 64 算法 + `GridPos.CompareTo` 排序基础
- [ADR-0002-presenter-sync-contract.md](./ADR-0002-presenter-sync-contract.md) — `BattleState` 快照驱动 Render（`MapState` 作为快照一部分）
- [MAP_SYSTEM_FORWARD_PLAN §3.2](../../Docs/MAP_SYSTEM_FORWARD_PLAN.md#32-范围严格-route-a) — route A `MapState` / `MapDefinition` / `MapStateCloner` / `MapStateHasher` 范围规范
- [MAP_SYSTEM_FORWARD_PLAN §3.4](../../Docs/MAP_SYSTEM_FORWARD_PLAN.md#34-验收标准) — Gate 验证标准（EditMode ≥ 272、ClonerTests 14 PASS、FNV-1a 100× 一致）
- [MAP_SYSTEM_AUDIT §3.3](../MAP_SYSTEM_AUDIT.md#33-冲突与兼容性) — `MapState` 嵌入 `BattleState` 的 route A 决策 + 邻居顺序 / 多边形冲突清单
- [MAP_SYSTEM_AUDIT §6.5](../MAP_SYSTEM_AUDIT.md#65-关键修复无论选哪条路线都必须做) — `BattleStateCloner` 不复制 `Statuses / Anchors / Decrees` 的已知修复（MAP-02 一并解决嵌入层）
- [MAP_SYSTEM_AUDIT §7.1](../MAP_SYSTEM_AUDIT.md#71-命名冲突) — `GridCoord` vs `GridPos`、`MapState` vs `BoardState`、`TileState` 命名空间隔离
- [AGENTS.md §10.1](../AGENTS.md) — Core 不引用 `UnityEngine` / `UnityEditor`
- [AGENTS.md §11](../AGENTS.md) — 确定性规则（集合稳定排序、跨运行同哈希）
- [Assets/Starfall/Core/Anchor/AnchorZone.cs](../../Assets/Starfall/Core/Anchor/AnchorZone.cs) — `AnchorZone` 当前 `(Y, X)` 排序实现
- FNV-1a 参考：<http://www.isthe.com/chongo/tech/comp/fnv/>