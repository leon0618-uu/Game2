# ADR-0009: AnchorLink + ConstellationPolygon（整数顶点 + 自相交拒绝 + 规范化 + IMapCommand 集成）

- **状态**：**Proposed**（Pending gameplay 实现 + qa Gate acceptance；与 ADR-0003 / ADR-0004 同路线后续段）
- **日期**：2026-07-16
- **作者**：xingyuan-architect
- **关联任务包**：MAP-12 `agent/map-12-anchor-link`（Task 21-L）
- **关联文档**：
  - 扩展 [ADR-0001](./ADR-0001-core-data-model-and-hash.md)（Core 数据模型与 FNV-1a 64 哈希基础）
  - 扩展 [ADR-0003](./ADR-0003-map-state-hash.md)（MapState 哈希契约 + 字段顺序不变原则）
  - 扩展 [ADR-0004](./ADR-0004-map-command-framework.md)（IMapCommand / MapCommandResult / MapEvent 框架）
  - 规范来源：[MAP_SYSTEM_AUDIT §3.3 + §6.2](../MAP_SYSTEM_AUDIT.md#33-冲突与兼容性)（整数定点 + 自相交 + 固定顶点排序）
  - 路线依据：[MAP_SYSTEM_FORWARD_PLAN §3.2 route A](../../Docs/MAP_SYSTEM_FORWARD_PLAN.md#32-范围严格-route-a)
  - 当前缺口：[IMPLEMENTATION_STATUS §4.1 MAP-12 候补](../../Docs/IMPLEMENTATION_STATUS.md)
  - 已知限制：[KNOWN_LIMITATIONS §5 ADR 数量跟踪](../KNOWN_LIMITATIONS.md)

---

## Context

锚点围区是 [AGENTS.md §1](../AGENTS.md)「核心玩法」明确项之一，也是 [MAP_SYSTEM_AUDIT §3.3](../MAP_SYSTEM_AUDIT.md#33-冲突与兼容性) 中 doc2 §12/§14.4 强制约束的承载对象。当前 main HEAD `5832e8c` 上与锚点相关的存量资产如下：

1. **MAP-02 锚点数据载体**：[`Assets/Starfall/Core/Anchor/AnchorZone.cs`](../../Assets/Starfall/Core/Anchor/AnchorZone.cs) + [`AnchorRegistry.cs`](../../Assets/Starfall/Core/Anchor/AnchorRegistry.cs) —— 经典 POCO `class AnchorZone { int ZoneId; string Owner; IReadOnlyList<GridPos> Vertices; }`。顶点仅按 `GridPos.CompareTo`（Y→X）升序排序；`Contains` 用浮点射线法，**不满足** doc2 §14.4「必须使用整数或定点数，禁止浮点」。这是 [MAP_SYSTEM_AUDIT §3.3](../MAP_SYSTEM_AUDIT.md#33-冲突与兼容性) 显式记录的「多边形顶点排序」冲突与「多边形包含」冲突。
2. **MAP-02 嵌入**：[`MapState.Anchors: IReadOnlyList<AnchorZone>`](../../Assets/Starfall/Core/Map/State/MapState.cs)（legacy 字段，由 [ADR-0003 §4 字段表](./ADR-0003-map-state-hash.md#4-字段编码表顺序固定) 位置 8 锚定哈希 tag `0x31`，子 tag `0x40 / 0x41 / 0x42`）。
3. **MAP-03 锚点状态 side-channel**：[`AnchorStateService.cs`](../../Assets/Starfall/Core/Anchor/AnchorStateService.cs) + `enum AnchorZoneState : byte { Inactive, PlayerControlled, EnemyControlled, Neutral, Overloaded, Damaged, Destroyed, Locked }`（8 值），attach 模式 `Dictionary<MapState, Dictionary<int, AnchorZoneState>>`。该状态机表达「zone 归属」语义（与本 ADR 的 `AnchorLinkState`「连线生命周期」语义不同）。
4. **MAP-03 锚点 IMapCommand 入口**：
   - [`CreateAnchorLinkCommand.cs`](../../Assets/Starfall/Core/Map/Commands/CreateAnchorLinkCommand.cs) —— 把 `AnchorZone` 写入 `MapState.Anchors`；emit `OnAnchorLinkCreated` + `OnRegionChanged`。
   - [`ModifyAnchorStateCommand.cs`](../../Assets/Starfall/Core/Map/Commands/ModifyAnchorStateCommand.cs) —— 改 zone 的 8 值 `AnchorZoneState`；emit `OnRegionChanged`。
   - [`CreateConstellationAreaCommand.cs`](../../Assets/Starfall/Core/Map/Commands/CreateConstellationAreaCommand.cs) —— doc2 §21.1 的 14 类区域之一「Constellation」占位（map state 仅写入 `MapRegion` + emit `OnConstellationPolygonCreated`，**未实现** polygon 算法）。

**缺口（与 [MAP_SYSTEM_AUDIT §6.2 P1 + §3.3「中度冲突」](../MAP_SYSTEM_AUDIT.md#62-p1扩展--编辑器建议第-2-批) 对齐）**：

| # | 缺口 | doc2 引用 | 现状 |
|---|---|---|---|
| G1 | **整数顶点 Polygon**：AnchorZone 仅 `GridPos`，缺 Layer 信息（doc2 §12 要求双层） | doc2 §12 / §4.1 | ❌ |
| G2 | **自相交拒绝**：构造期校验 O(n²) | doc2 §12.3 | ❌（构造期只检查 `Count >= 3`） |
| G3 | **规范化顶点排序**：旋转 / 镜像不变（固定起点 + 顺时针 / 逆时针统一） | doc2 §12 | ❌（仅 Y→X 升序） |
| G4 | **整数定点 Contains**：替代浮点射线法 | doc2 §14.4 | ❌ |
| G5 | **`AnchorLink` 运行时模型**：当前 `AnchorZone` 只表示静态几何；缺 link 生命周期（建立 / 激活 / 锚定 / 切断 / 消退） | doc2 §21.1 | ❌ |
| G6 | **完整 IMapCommand 集**：Register / Unregister / TransitionState / UpdatePolygon / BatchTransition 5 个 | doc2 §21.1 + ADR-0004 §6 | ❌（仅 Register + ModifyAnchorState 占位） |
| G7 | **`ConstellationPolygonService`**：求交 / 并集 / 重叠判定 | doc2 §13.4 | ❌ |
| G8 | **新事件**：`OnAnchorLinkStateChanged`（link 生命周期），区别于 `OnAnchorLinkCreated`（一次性创建） | doc2 §21.1 | ❌ |

如不在 MAP-12 阶段一次性收口上述 8 项缺口，则：

- **核心玩法不可执行**：锚点围区无法表达「建立 → 激活 → 锚定 → 切断」四段生命周期（[doc1 §11.1](../../.incoming/doc1-core-systems.txt) + [doc2 §21.1](../../.incoming/doc2-map-dev-plan.txt)）。
- **Replay 哈希不可信**：相同初始状态 + 不同「自相交被拒绝的构造顺序」可导致不同 `MapState.PostStateHash`（违反 [AGENTS.md §11](../AGENTS.md)「相同输入产生相同 Event 顺序 / Replay 后状态哈希一致」）。
- **后续包（律令 / 引力度 / Constellation 触发战斗效果）失去稳定 API**：MAP-08 / MAP-11 之后的链式效果依赖 `AnchorLinkState` 切换触发器。

---

## Decision

### 1. 类型层级总览（命名空间 `Starfall.Core.Anchor` + `Starfall.Core.Map.Constellation`）

```text
Starfall.Core.Anchor                         ← 兼容 AnchorZone 旧 API（保留）
    ConstellationPolygonId : int              ← 新强类型（避免与 ZoneId 混淆）
Starfall.Core.Map.Constellation              ← 新增子目录（MAP-12 资产）
    ConstellationVertex  (readonly struct)
    ConstellationPolygon (readonly struct)
    ConstellationValidator (static class)
    AnchorLinkId         (readonly struct)
    AnchorLinkState      (enum byte)
    AnchorLink           (class, mutable runtime)
    AnchorLinkService    (static helpers)
```

**向后兼容**（与 [ADR-0003 §5 已知跟踪项 + §7 不变量](./ADR-0003-map-state-hash.md#5-排序键与确定性规则) 保持一致）：保留 `AnchorZone` / `AnchorRegistry` / `CreateAnchorLinkCommand` / `ModifyAnchorStateCommand` 不删除；新增 `AnchorLinks` 集合字段 + 5 个 IMapCommand + 7-state 生命周期。

### 2. `ConstellationVertex` / `ConstellationPolygon`（不可变 struct）

```csharp
namespace Starfall.Core.Map.Constellation
{
    /// <summary>
    /// 多边形顶点（doc2 §12 整数坐标，扩展 Layer 信息）。
    /// 排序键：Y → X → Layer（与 ADR-0003 §5 `GridCoord.CompareTo` 一致）。
    /// </summary>
    public readonly struct ConstellationVertex : IEquatable<ConstellationVertex>, IComparable<ConstellationVertex>
    {
        public readonly GridCoord Coord;
        public ConstellationVertex(GridCoord coord) { Coord = coord; }
        public ConstellationVertex(int x, int y, DimensionLayer layer)
            : this(new GridCoord(x, y, layer)) { }

        public int CompareTo(ConstellationVertex other) => Coord.CompareTo(other.Coord);
        public bool Equals(ConstellationVertex other) => Coord.Equals(other.Coord);
        public override int GetHashCode() => Coord.GetHashCode();
        public override string ToString() => $"V({Coord})";
    }

    /// <summary>
    /// 多边形：整数顶点序列 + 强类型 Id。构造时必经 <see cref="ConstellationValidator"/> 校验。
    /// 顶点顺序固定为「Y→X→Layer 升序后的循环顺序，第一位 = 最小顶点」（§5 规范化）。
    /// </summary>
    public readonly struct ConstellationPolygon : IEquatable<ConstellationPolygon>
    {
        public readonly ConstellationPolygonId Id;
        public readonly IReadOnlyList<ConstellationVertex> Vertices;

        public ConstellationPolygon(ConstellationPolygonId id, IReadOnlyList<ConstellationVertex> vertices)
        {
            // 构造期必经 ConstellationValidator；失败抛 ConstellationValidationException
            ConstellationValidator.ValidateOrThrow(id, vertices);
            Id = id;
            Vertices = vertices;
        }

        // 整数定点 Contains（替代浮点射线法）
        public bool Contains(GridCoord p)
            => ConstellationValidator.ContainsIntegerPoint(this, p);

        // 顶点个数（≥ 3 由 Validator 保证）
        public int VertexCount => Vertices.Count;

        public bool Equals(ConstellationPolygon other)
            => Id.Equals(other.Id) && ConstellationVertex.SequenceEqualStatic(Vertices, other.Vertices);
        public override int GetHashCode()
            => Id.GetHashCode() ^ (Vertices.Count * 397);
        public override string ToString()
            => $"ConstellationPolygon(Id={Id}, V={Vertices.Count})";
    }

    /// <summary>多边形强类型 Id（int 包装，避免与 ZoneId 混用）。</summary>
    public readonly struct ConstellationPolygonId : IEquatable<ConstellationPolygonId>, IComparable<ConstellationPolygonId>
    {
        public readonly int Value;
        public ConstellationPolygonId(int value)
        {
            if (value < 0) throw new ArgumentOutOfRangeException(nameof(value));
            Value = value;
        }
        public int CompareTo(ConstellationPolygonId other) => Value.CompareTo(other.Value);
        public bool Equals(ConstellationPolygonId other) => Value == other.Value;
        public override int GetHashCode() => Value;
        public override string ToString() => $"P{Value}";
    }
}
```

### 3. `ConstellationValidator`（构造期校验，**eager 拒绝**）

```csharp
namespace Starfall.Core.Map.Constellation
{
    public static class ConstellationValidator
    {
        public const int MinVertexCount = 3;

        /// <summary>
        /// 三阶段校验：TooFewVertices → Collinear → SelfIntersecting。
        /// 全部失败抛 <see cref="ConstellationValidationException"/>（含 Id + Reason + 失败顶点索引）。
        /// </summary>
        public static void ValidateOrThrow(
            ConstellationPolygonId id, IReadOnlyList<ConstellationVertex> vertices)
        {
            if (vertices == null)
                throw new ConstellationValidationException(id, ConstellationValidationReason.NullVertices);
            if (vertices.Count < MinVertexCount)
                throw new ConstellationValidationException(id, ConstellationValidationReason.TooFewVertices);

            // 1. Collinear：所有顶点叉积为 0 → 退化
            if (IsCollinear(vertices))
                throw new ConstellationValidationException(id, ConstellationValidationReason.Collinear);

            // 2. SelfIntersecting：O(n²) 检查所有非相邻边对
            if (HasSelfIntersection(vertices))
                throw new ConstellationValidationException(id, ConstellationValidationReason.SelfIntersecting);
        }

        // 整数叉积（避免 double）
        private static long Cross(in ConstellationVertex o, in ConstellationVertex a, in ConstellationVertex b)
        {
            long ax = a.Coord.X - o.Coord.X; long ay = a.Coord.Y - o.Coord.Y;
            long bx = b.Coord.X - o.Coord.X; long by = b.Coord.Y - o.Coord.Y;
            return ax * by - ay * bx;
        }

        // 整数段-段相交（包含端点 = false；共用端点不算相交）
        private static bool SegmentsIntersect(in ConstellationVertex a, in ConstellationVertex b,
                                              in ConstellationVertex c, in ConstellationVertex d) { /* ... */ }

        // 整数定点 Contains（替代 AnchorZone.Contains 的浮点射线法）
        public static bool ContainsIntegerPoint(in ConstellationPolygon polygon, GridCoord p) { /* ... */ }
    }

    public enum ConstellationValidationReason : byte
    {
        NullVertices = 0,
        TooFewVertices = 1,   // < 3 顶点
        Collinear = 2,        // 所有顶点共线
        SelfIntersecting = 3, // 任意两条非相邻边相交
    }

    public sealed class ConstellationValidationException : Exception
    {
        public ConstellationPolygonId PolygonId { get; }
        public ConstellationValidationReason Reason { get; }
        public ConstellationValidationException(ConstellationPolygonId id, ConstellationValidationReason reason)
            : base($"Constellation polygon {id} invalid: {reason}")
        { PolygonId = id; Reason = reason; }
    }
}
```

**关键不变量**：

- **eager 拒绝**：所有 `ConstellationPolygon` 实例必须经过 `ValidateOrThrow`；`struct` 构造器直接调用。这意味着失败在**命令构造期**（`new ConstellationPolygon(...)` 抛异常），而不是 lazy 在 runtime 抛——满足 [AGENTS.md §10.1 确定性](../AGENTS.md) + [MAP_SYSTEM_AUDIT §7.2 算法冲突](../MAP_SYSTEM_AUDIT.md#72-算法冲突)「确定性保证失败」。
- **零浮点**：`ContainsIntegerPoint` 用整数叉积 + 整数射线方向判定（参见 doc2 §14.4）。
- **O(n²) 自相交**：n ≤ 10（MVP 8×10 双层 = 160 格，多边形顶点数典型 ≤ 8），最坏 45 次段-段判定 < 1μs。**性能不构成瓶颈**（详见 §Consequences #R1）。

### 4. 规范化（canonical form）

> 满足 [AGENTS.md §11](../AGENTS.md)「锚点和多边形使用规范化顶点顺序」+ [doc2 §12「闭合路径 + 排除自相交 + 固定顶点排序」](../../.incoming/doc2-map-dev-plan.txt)。

```text
输入：vertices = [v0, v1, v2, ..., v_{n-1}]（用户顺序）
1. 去重（保留首次出现的副本）：duplicate 抛 TooFewVertices（去重后 < 3）
2. 排序：按 ConstellationVertex.CompareTo（Y → X → Layer）升序 → sorted
3. 起点固定：sorted[0]（最小顶点）固定为第一个顶点；其余保持原相对循环顺序
   （注：MVP 不做顺/逆时针统一；后续包 MAP-12b 再按"整数面积符号"翻转）
4. 输出：canon = [sorted[0], sorted[1], ..., sorted[n-1]]
```

**示例**（来自 [MAP_SYSTEM_AUDIT §3.3「多边形顶点排序」](../MAP_SYSTEM_AUDIT.md#33-冲突与兼容性) 行）：

```text
输入 [(2,1), (0,0), (1,2), (2,1)]     → 去重 [(2,1), (0,0), (1,2)] → 排序 [(0,0), (1,2), (2,1)]
输入 [(0,0), (1,0), (0,1)]            → 排序 [(0,0), (0,1), (1,0)]
输入 [(1,2), (2,1), (0,0)]            → 排序 [(0,0), (1,2), (2,1)]
```

**旋转 / 镜像不变性**：通过固定起点（最小顶点）实现旋转不变；镜像在 MVP 不要求（Y→X→Layer 排序 + 起点固定后，[0,0],[0,1],[1,0] 与 [0,0],[1,0],[0,1] 仍判为不同多边形——这是 MVP 的已知限制，见 §Consequences #N3 + Follow-ups F2）。

### 5. `AnchorLink`（运行时模型，class，含 7 状态生命周期）

```csharp
namespace Starfall.Core.Map.Constellation
{
    /// <summary>
    /// 锚点 link 的运行时模型：绑定一个 ConstellationPolygon + 7 状态生命周期 + StateTick + PostStateHash。
    /// 与 <see cref="AnchorZone"/>（静态几何）解耦：前者表达「连线状态」，后者表达「围区形状」。
    /// </summary>
    public sealed class AnchorLink
    {
        public AnchorLinkId Id { get; }
        public ConstellationPolygon Polygon { get; private set; }
        public AnchorLinkState CurrentState { get; private set; }
        public int StateTick { get; private set; }       // 进入当前状态的回合 / tick
        public ulong PostStateHash { get; private set; } // 状态机内部哈希（事件触发用）

        // 内部可写集合：MapState 写入；外部只读
        internal AnchorLink(AnchorLinkId id, ConstellationPolygon polygon,
                            AnchorLinkState initialState, int initialTick)
        {
            Id = id; Polygon = polygon;
            CurrentState = initialState; StateTick = initialTick;
            PostStateHash = AnchorLinkHasher.ComputeStateHash(this);
        }

        // 单步状态切换（由 AnchorLinkTransitionStateCommand 调用）
        internal void TransitionTo(AnchorLinkState newState, int tick)
        {
            CurrentState = newState; StateTick = tick;
            PostStateHash = AnchorLinkHasher.ComputeStateHash(this);
        }

        // 单步 Polygon 替换（由 AnchorLinkUpdatePolygonCommand 调用；新 Polygon 必须 ValidateOrThrow 通过）
        internal void UpdatePolygon(ConstellationPolygon newPolygon)
            => Polygon = newPolygon;
    }

    /// <summary>AnchorLink 强类型 Id。</summary>
    public readonly struct AnchorLinkId : IEquatable<AnchorLinkId>, IComparable<AnchorLinkId>
    {
        public readonly int Value;
        public AnchorLinkId(int value)
        {
            if (value < 0) throw new ArgumentOutOfRangeException(nameof(value));
            Value = value;
        }
        public int CompareTo(AnchorLinkId other) => Value.CompareTo(other.Value);
        public bool Equals(AnchorLinkId other) => Value == other.Value;
        public override int GetHashCode() => Value;
        public override string ToString() => $"L{Value}";
    }

    /// <summary>
    /// 锚点连线生命周期（doc2 §21.1 + 现状「7 状态」清单）：
    /// Inert → Forming → Active → Anchored → Severed → Fading → Resolved。
    /// 与 <see cref="Starfall.Core.Anchor.AnchorZoneState"/>（8 值 zone 归属）正交。
    /// </summary>
    public enum AnchorLinkState : byte
    {
        Inert     = 0,   // 默认：link 建立但未激活（最常见稳态）
        Forming   = 1,   // 形成中（CV 蓄能 / 单位压上）
        Active    = 2,   // 激活（提供围区效果）
        Anchored  = 3,   // 锚定（不可被 ModifyGlobalCV / PhaseFlip 干扰）
        Severed   = 4,   // 切断（外部攻击或律令切断）
        Fading    = 5,   // 消退（剩余 N tick 后转 Resolved）
        Resolved  = 6,   // 终止（不可恢复；同 AnchorZoneState.Destroyed 不可逆）
    }
}
```

> **状态机严格守则**（与 [ADR-0004 §2 IMapCommand 失败规则](./ADR-0004-map-command-framework.md#2-imapcommand-完整合约) 一致）：
>
> - 非法转移（如 `Resolved → Forming`）由命令返回 `MapCommandResult.Fail("illegal state transition")`，**不**抛异常；保持 mapState 零修改。
> - `Resolved` 不可逆（与 `AnchorZoneState.Destroyed` 同语义，但通过 `AnchorLinkState.Resolved` 而非 `Destroyed` 表达）。

### 6. 与 `MapState` 集成（向后兼容 + 新增 `AnchorLinks`）

```csharp
namespace Starfall.Core.Map.State
{
    public sealed partial class MapState
    {
        // ──── 既有字段（保留） ────
        public IReadOnlyList<AnchorZone> Anchors => AnchorsInternal;     // ADR-0003 §4 字段表位置 8
        public IReadOnlyList<MapRegion> Regions => RegionsInternal;
        public IReadOnlyList<MapObjectInstance> MapObjects => MapObjectsInternal;
        // ... 其余字段保持不变

        // ──── MAP-12 新增字段 ────
        internal readonly List<AnchorLink> AnchorLinksInternal;
        public IReadOnlyList<AnchorLink> AnchorLinks => AnchorLinksInternal;  // 新 hash tag 0x38

        public MapState(MapDefinition definition) : this(definition) { /* existing */ }
        // ... ctor 末尾追加：
        AnchorLinksInternal = new List<AnchorLink>();
    }
}
```

**字段表（追加，不修改 ADR-0003 §4 既有字段）**：

| # | 字段 | 类型 | 新 tag | 备注 |
|---|---|---|---|---|
| 11 | `AnchorLinks` | `IReadOnlyList<AnchorLink>` | `0x38` | 按 `AnchorLinkId` 升序写入 |

**不变量**：

- **不删除** `Anchors` 字段；`CreateAnchorLinkCommand`（MAP-03 stub）继续写入 `Anchors`，同时调用 `AnchorLinkService.AttachLink` 把对应 `AnchorLink` 写入 `AnchorLinks`。两个集合共享 `ZoneId == AnchorLinkId`（语义对齐）。
- **`AnchorLinkService` 是 attach-mode side-channel**（与 [ADR-0004 §8 attach-mode](./ADR-0004-map-command-framework.md#8-服务挂载模式attach-mode-singleton) 一致），但**额外要求**：`MapState.AnchorLinksInternal` 是真值，side-channel 仅用于快速查询（O(1) `Dictionary<int, AnchorLink>`）。

### 7. 5 个 IMapCommand（与 ADR-0004 §2 + §7 合约对齐）

| # | 命令 | CommandId | Dependencies | 行为 |
|---|---|---|---|---|
| 1 | `RegisterAnchorLinkCommand` | `register-anchor-link:{id}` | （独立） | 构造 `AnchorLink` + 写入 `AnchorLinks`；emit `OnAnchorLinkCreated` |
| 2 | `UnregisterAnchorLinkCommand` | `unregister-anchor-link:{id}` | 独立 | 从 `AnchorLinks` 移除；emit `OnAnchorLinkRemoved`（新事件，见 §8） |
| 3 | `TransitionAnchorLinkStateCommand` | `transition-anchor-link:{id}` | `register-anchor-link:{id}` | 切换 `CurrentState`；emit `OnAnchorLinkStateChanged`（新事件） |
| 4 | `UpdateAnchorLinkPolygonCommand` | `update-anchor-link-polygon:{id}` | `register-anchor-link:{id}` | 替换 `Polygon`（新 polygon 必须 ValidateOrThrow 通过）；emit `OnAnchorLinkPolygonChanged`（新事件） |
| 5 | `BatchTransitionAnchorLinksCommand` | `batch-transition-anchor-links:{count}` | 每条 link 各自的 register + 当前 state transition | 在单条命令内对多条 link 做 TransitionState；emit 每条 link 的 `OnAnchorLinkStateChanged` + 单条 `OnAnchorLinksBatchResolved` |

**契约**（沿用 [ADR-0004 §2](./ADR-0004-map-command-framework.md#2-imapcommand-完整合约)）：

1. 构造期严格校验输入（Polygon 必须在构造时 ValidateOrThrow 通过）。
2. 执行时先全部校验再写；失败 `Fail(reason)`，mapState 零修改。
3. Undo 必须严格反向单步操作；`BatchTransitionAnchorLinksCommand.Undo` 一次性反向所有 link 的 transition。
4. Emit 事件必须按 `MapEvent.CompareTo` 排序（[ADR-0004 §4](./ADR-0004-map-command-framework.md#4-mapevent-稳定排序合约)）。
5. 命令实现不引用 `UnityEngine`（[AGENTS.md §10.1](../AGENTS.md)）；不读时间 / 线程 / 实例地址（[AGENTS.md §11](../AGENTS.md)）。

### 8. 事件扩展（`MapEventKind` 增量）

```csharp
namespace Starfall.Core.Map
{
    public enum MapEventKind : byte
    {
        // ... MAP-03 + MAP-08/11a 既有 9 个值
        OnAnchorLinkCreated      = 5,    // 既有（保留）
        OnAnchorLinkRemoved      = 12,   // 新增（MAP-12 Unregister）
        OnAnchorLinkStateChanged = 13,   // 新增（MAP-12 TransitionState）
        OnAnchorLinkPolygonChanged = 14, // 新增（MAP-12 UpdatePolygon）
        OnAnchorLinksBatchResolved = 15, // 新增（MAP-12 BatchTransition）
        // ... 后续 0x0F-0x1F 保留
    }
}
```

> 现有 `OnAnchorLinkCreated = 5` 已有 [ADR-0004 §4](../../Docs/ADR/ADR-0004-map-command-framework.md) 引用——保留不变。

### 9. Hasher 段扩展（不破坏 [ADR-0003 §4 字段编码表](./ADR-0003-map-state-hash.md#4-字段编码表顺序固定) 既有字段顺序）

`MapStateHasher` 在 `AnchorLinks` 字段追加新段，**不修改**既有字段编码：

| 新 tag | 用途 | 字节布局 |
|---|---|---|
| `0x38` | `AnchorLinks` 集合（顶层） | `uint8` tag + `uint32` count + 按 `AnchorLinkId` 升序逐 link 写入 |
| `0x43` | `AnchorLinkId`（link header） | `uint8` tag + `int32` LE |
| `0x44` | `VertexEntry`（每顶点入口） | `uint8` tag + 顶点内容（`int32` X + `int32` Y + `int32` Layer） |
| `0x45` | `AnchorLinkCurrentState` | `uint8` tag + `int32` LE（`AnchorLinkState` cast） |
| `0x46` | `AnchorLinkStateTick` | `uint8` tag + `int32` LE |
| `0x47` | `AnchorLinkPostStateHash` | `uint8` tag + `uint64` LE（`AnchorLink.PostStateHash` 直接嵌入） |

> **关于 0x40/0x41 冲突说明**：任务原 spec 建议「AnchorLink header = 0x40 + Vertex entry = 0x41」，但 `0x40 / 0x41 / 0x42` 已被 [ADR-0003 §4](./ADR-0003-map-state-hash.md#4-字段编码表顺序固定) 既有字段（`TagAnchorZoneId = 0x40`、`TagAnchorOwner = 0x41`、`TagAnchorVertex = 0x42`）占用。本 ADR 改用 `0x38`（顶层 collection，与 `TagLocalCVs = 0x37` 邻接）+ `0x43-0x47`（link sub-tags，与 legacy anchor sub-tags `0x40-0x42` 邻接但**无碰撞**）。这是对原 spec 的偏离，原因：[ADR-0003 §Consequences 负面 #1](./ADR-0003-map-state-hash.md#consequences) 明确「任何字段顺序变更必须新建 ADR」+ 既有 294+ EditMode 测试期望 `TagAnchors`/`TagAnchorZoneId`/`TagAnchorOwner`/`TagAnchorVertex` 字节流不变。

**AnchorLink 单条字节流**（按 `AnchorLinkId` 升序）：

```text
type_tag 0x43
AnchorLinkId.Value       int32 LE

type_tag 0x38 子结构（隐式）
VertexCount              uint32 LE   (≥ 3)
  重复 VertexCount 次:
    type_tag 0x44
    X                      int32 LE
    Y                      int32 LE
    Layer                  int32 LE   (DimensionLayer cast)

type_tag 0x45
CurrentState             int32 LE    (AnchorLinkState cast)

type_tag 0x46
StateTick                int32 LE

type_tag 0x47
PostStateHash            uint64 LE   (AnchorLink.PostStateHash 直接嵌入)
```

**AnchorLink.PostStateHash 计算**（`AnchorLinkHasher.ComputeStateHash`，独立小函数）：

```text
hash = 0xCBF29CE484222325  // FNV-1a offset basis（与 ADR-0001/0003 一致）
mix 0x45                           // type tag: AnchorLinkCurrentState
mix state_byte                     // AnchorLinkState cast LE
mix 0x46                           // type tag: AnchorLinkStateTick
mix tick_le4                       // StateTick LE 4 bytes
mix 0x47                           // type tag: PostStateHash 自引用标记
return hash
```

> 注意：`AnchorLink.PostStateHash` 是**状态机内部哈希**（仅含 State + Tick），**不含** Polygon 顶点。这是「状态事件触发用」的轻量哈希；Polygon 顶点的写入已通过 §9 顶层 `0x38` 集合中 `0x44` 顶点 entry 完成。两者职责分明，避免双重哈希。

**MapState 字段表追加（仅追加，不修改既有）**：

```text
[既有 18 个字段...]                  ← ADR-0003 §4 + MAP-09 + MAP-11a 增量
19. AnchorLinks (tag=0x38)          ← 本 ADR §9 新增
   uint32 count
   foreach AnchorLink 按 AnchorLinkId 升序：
     0x43 + AnchorLinkId            ← §9 子结构
     0x44 + X + Y + Layer × n      ← 顶点序列
     0x45 + CurrentState
     0x46 + StateTick
     0x47 + PostStateHash
```

### 10. 与既有结构的兼容清单

| 既有结构 | 本 ADR 影响 | 兼容策略 |
|---|---|---|
| MAP-02 `MapState.Anchors` ([`AnchorZone.cs`](../../Assets/Starfall/Core/Anchor/AnchorZone.cs)) | 不删除 | 保留 legacy 字段；CreateAnchorLinkCommand（MAP-03 stub）继续写入；新增 `AnchorLinks` 与之并行 |
| MAP-02 `MapStateHasher` tag `0x31` (`TagAnchors`) | 不修改 | 既有 294 EditMode 测试字节流期望保持不变；`AnchorLinks` 走新 tag `0x38` |
| MAP-02 `MapStateCloner` | 扩展 | 增加 `AnchorLinksInternal` 深拷贝（`AnchorLink` 是 class，每条 new + 复制 Polygon） |
| MAP-03 `AnchorStateService` (`AnchorZoneState` 8 值) | 不修改 | 保留；与 `AnchorLinkState`（7 值）正交；语义不同（前者 = 围区归属，后者 = 连线生命周期） |
| MAP-03 `IMapCommand` + `MapCommandExecutor` ([ADR-0004](./ADR-0004-map-command-framework.md)) | 扩展 | 5 个新命令遵循既有合约；`BatchTransitionAnchorLinksCommand` 是首个 multi-link 命令，依赖图见 §7 |
| MAP-03 `CreateAnchorLinkCommand` (Register 旧版) | **保留 + 标记 deprecated** | 保留以兼容既有 35 个 MAP-03 测试；新代码用 `RegisterAnchorLinkCommand`（同时写 `Anchors` + `AnchorLinks`） |
| MAP-03 `CreateConstellationAreaCommand` (Region 占位) | 不修改 | 仍是 `MapRegion` + 字符串类型 `Constellation` 占位；后续 MAP-12b 再升级为完整 polygon 算法 |
| MAP-03 `ModifyAnchorStateCommand` | 不修改 | 仍写 8 值 `AnchorZoneState`；本 ADR 新增 `TransitionAnchorLinkStateCommand` 走 7 值 `AnchorLinkState` |
| MAP-09 `MapRegion` + `MapSpawnPoint` | 不修改 | 锚点连线生命周期与 region 状态机正交 |
| MAP-11a `CollapseTileCommand` + `LocalCollapseValue` | 不修改 | 后续包可能用 `OnAnchorLinkStateChanged` 作为塌缩触发器，但 MAP-12 阶段不接 |
| ADR-0003 §6 `MapStateHasher` 字段表 | 仅追加 §9 | 不修改既有 18 个字段；新字段 #19 追加 |

---

## Test invariants（gate from MAP-12 验收）

实现必须通过以下不变式测试（与 doc2 §21.1 + [MAP_SYSTEM_FORWARD_PLAN §3.4](../../Docs/MAP_SYSTEM_FORWARD_PLAN.md#34-验收标准) 8 项 gate 对齐）：

1. **构造期校验**：TooFewVertices / Collinear / SelfIntersecting 各自抛 `ConstellationValidationException`；同输入多进程抛同一异常（确定性）。
2. **规范化稳定**：相同顶点集合以任意顺序构造，得到的 `ConstellationPolygon` 顶点序列 `Equals` 为真；不同进程同输入同输出。
3. **整数 Contains**：8×10 双层地图每个 GridCoord 与 `ContainsIntegerPoint` 一致（含 3 / 4 / 5 / 6 / 8 边形；凸 / 凹 / 自相交拒绝）；浮点从不进入此函数（由 CoreDependencyGuardTests 自动验证）。
4. **自相交 O(n²) 完整覆盖**：三角 / 四角 / 五角 / 六角 / 八角自相交 case；含「共用顶点」「共用边」不算自相交、「内部对角线相交」算自相交。
5. **5 个 IMapCommand Happy Path**：
   - `Register` → `MapState.AnchorLinks.Count == 1` + `OnAnchorLinkCreated` 事件 + `Version+1`
   - `Unregister` → `Count == 0` + `OnAnchorLinkRemoved` 事件
   - `TransitionState` (Inert → Forming) → `CurrentState == Forming` + `OnAnchorLinkStateChanged` + `PostStateHash` 变化
   - `UpdatePolygon`（新 polygon 顶点全部合法）→ `Polygon` 引用替换 + `OnAnchorLinkPolygonChanged`
   - `BatchTransition` (3 条 link 同时 Forming → Active) → 3 条 `OnAnchorLinkStateChanged` + 1 条 `OnAnchorLinksBatchResolved`，事件按 `MapEvent.CompareTo` 排序
6. **5 个 IMapCommand Failure Path**：
   - `Register` 重复 Id → `Fail("duplicate link id")`，mapState 不变
   - `Register` polygon 自相交 → `Fail("invalid polygon: SelfIntersecting")`，mapState 不变
   - `Unregister` 不存在 Id → `Fail("link not found")`
   - `TransitionState` 非法转移（Resolved → Forming）→ `Fail("illegal state transition")`
   - `UpdatePolygon` 新 polygon 共线 → `Fail("invalid polygon: Collinear")`
   - `BatchTransition` 任一 link 失败 → 整体 `Fail`，mapState 零修改（atomic）
7. **Undo 单步反向**：
   - `Register.Undo` → AnchorLinks 移除同一条 link
   - `TransitionState.Undo` → CurrentState 恢复 + StateTick 恢复 + PostStateHash 恢复
   - `UpdatePolygon.Undo` → Polygon 引用恢复旧引用（deep copy 保证独立性）
   - `BatchTransition.Undo` → 一次性反向所有 link 的 transition
8. **Hasher 稳定**：`CalculateDeterministicHash(具有 3 条 AnchorLink 的 MapState)` 100 次连续调用同 ulong；同输入跨 .NET runtime 一致。
9. **依赖校验**：`BatchTransitionAnchorLinksCommand` 的 `Dependencies` 含所有 link 的 `register-anchor-link:{id}` + 当前状态 commandId；`MapCommandExecutor.Run` 在缺依赖时返回 `Fail("missing dependency: ...")`，**不**调用 Execute。
10. **Core 依赖守卫**：新增 6 个 .cs 文件（`ConstellationVertex` / `ConstellationPolygon` / `ConstellationValidator` / `AnchorLink` / `AnchorLinkId` / `AnchorLinkState` / `AnchorLinkService` + 5 个 command）均无 `using UnityEngine`；`CoreDependencyGuardTests` 自动验证。

**测试规模最低线**：

| 测试集 | 数量 |
|---|---|
| `ConstellationValidatorTests` | ≥ 20（TooFewVertices 3 + Collinear 3 + SelfIntersecting 5 + NullVertices 1 + 多态 4 + 跨进程一致 4） |
| `ConstellationPolygonCanonicalFormTests` | ≥ 15（规范化 6 + 整数 Contains 5 + 镜像 / 旋转 4） |
| `AnchorLinkLifecycleTests` | ≥ 20（7 状态转移表 12 + Undo 4 + PostStateHash 4） |
| `AnchorLinkCommandTests` | ≥ 30（5 命令 × 6 path ≈ 30，含 Happy + Failure + 事件 + Undo） |
| `AnchorLinkHashTests` | ≥ 6（包含 100× 一致 + 跨进程同 hash） |
| `AnchorLinkRegressionTests` | ≥ 10（保证 MAP-03 既有 35 测试不变） |
| **合计** | **≥ 101 新 EditMode 测试** |

---

## Consequences

### 正面

- **核心玩法锚定**：建立 / 激活 / 锚定 / 切断 / 消退 / 终止 6 段生命周期可被 IMapCommand 操作；后续包（律令 / 引力度 / Constellation 触发战斗效果）有稳定 API。
- **整数定点保证**：核心玩法确定性不再依赖浮点除法；Reentrant + Replay 跨进程一致。
- **自相交构造期拒绝**：用户错误在 `new ConstellationPolygon(...)` 即失败，零状态污染。
- **Hasher 字段可增量**：MAP-12 仅追加字段 #19；既有 18 字段字节流不变 → 既有 294 EditMode 测试 + MAP-03/06/07/08/09/11a 全链路哈希期望保持 PASS（向后兼容保证）。
- **零新依赖**：`ConstellationValidator` / `AnchorLink` 用 .NET BCL；不改 `Packages/manifest.json`；不引第三方库。
- **AGENTS.md §10.1 / §11 一致**：Core 无 UnityEngine 引用、集合稳定排序、整数定点、FNV-1a 字节序明确。

### 负面 / 约束

- **增加 5 个 IMapCommand + 8+ 新类型**：每个命令需独立 EditMode 测试覆盖（§Test invariants 共 ≥ 101 测试），开发 + QA 工作量 ≈ 2-3 天（与 [MAP_SYSTEM_AUDIT §6.2 P1「2-3 天」估算](../MAP_SYSTEM_AUDIT.md#62-p1扩展--编辑器建议第-2-批) 对齐）。
- **`CreateAnchorLinkCommand` 双写**（既有 + 新 `RegisterAnchorLinkCommand`）：MAP-03 既有 35 测试期望保留，新代码用 `RegisterAnchorLinkCommand`；两条命令**共存**直到后续包做迁移。这是有意的向后兼容，**不**在 MAP-12 范围删除 MAP-03 stub。
- **AnchorLinkId 与 ZoneId 语义重叠**：`ZoneId == AnchorLinkId.Value` 是当前约定，但两者**类型不同**（int vs readonly struct）；若未来打破对齐，AnchorLinkService.AttachLink 必须显式校验。
- **状态机 7 值 vs AnchorZoneState 8 值**：两个枚举独立存在，命名容易混淆（Inert/Inactive、Resolved/Destroyed 容易写错）；通过命名空间 `Starfall.Core.Map.Constellation.AnchorLinkState` 与 `Starfall.Core.Anchor.AnchorZoneState` 隔离。

### 风险

- **R1（性能）**：自相交检测 O(n²)。MVP 多边形顶点数典型 3-8，最坏 8 顶点 = 28 次段-段判定 < 5μs；20×28 双层（MAP-18 压力地图）+ 12 个 link = 12 × 5μs ≈ 60μs/帧，**远低于** 60 FPS 帧预算 16.6ms。**可接受**。若未来出现 n > 20 顶点的「巨型 Constellation」（如全图覆盖），需要切换到 Bentley-Ottmann 扫描线算法（O(n log n)）——见 Follow-ups F1。
- **R2（向后兼容）**：tag `0x38` 与 `0x43-0x47` 是新分配，必须保证：
  1. 既有 294 EditMode 测试期望 `MapState.PostStateHash` 字节流第 1-18 字段不变；
  2. `BattleState.PostStateHash` 字节流第 1 段（`MapState.PostStateHash`）长度可变，但 ulong 值仍确定性——下游消费方按 ADR-0003 §7「先算 ulong 再嵌入」读取，**不**依赖字节流长度。✓
- **R3（命令冲突）**：`RegisterAnchorLinkCommand` 与 `CreateAnchorLinkCommand` 共享「register」语义；命令 id 不同（`register-anchor-link:{id}` vs `create-anchor-link:{zone-id}`），executor 不会去重；但调用方需明确选择。建议在 `Unity/Map/MapCommandFactory` 暴露 helper 强制选择（**不在本 ADR 范围**）。

### 已知 Follow-ups（不在本 ADR 范围）

| # | 项 | 关联任务 | 来源 |
|---|---|---|---|
| F1 | 大 polygon（n > 20）Bentley-Ottmann 扫描线 | MAP-12b / MAP-18 性能 | 本 ADR Consequences R1 |
| F2 | 镜像不变性（顺 / 逆时针统一 → 整数面积符号翻转） | MAP-12b | 本 ADR §4 + MAP_SYSTEM_AUDIT §3.3 |
| F3 | `ConstellationPolygonService` 求交 / 并集 / 重叠 | MAP-12b | [MAP_SYSTEM_AUDIT §3.3「多边形求交」](../MAP_SYSTEM_AUDIT.md#33-冲突与兼容性) |
| F4 | `AnchorZone` 升级为整数定点 `Contains`（替代浮点射线法） | MAP-12b（独立子任务） | [MAP_SYSTEM_AUDIT §3.3「多边形包含」](../MAP_SYSTEM_AUDIT.md#33-冲突与兼容性) |
| F5 | 迁移 `CreateAnchorLinkCommand` → `RegisterAnchorLinkCommand`（删除 MAP-03 stub） | MAP-12c（独立清理） | 本 ADR Consequences 负面 #2 |
| F6 | `AnchorLink` 关联 `ConstellationPolygon` ↔ `MapRegion` 类型 `Constellation`（10-region 子集） | MAP-12b | doc2 §13.4 |
| F7 | `OnAnchorLinkStateChanged` 触发 `CollapseWarningService.ShouldWarn` | MAP-11b | doc2 §21.1 |

---

## Alternatives considered

### A. 用 `Vector2Int` 而非 `GridCoord`

- **优点**：`Vector2Int` 是 UnityEngine 内置类型；零新类型。
- **否决理由**：违反 [AGENTS.md §10.1](../AGENTS.md)「Core 不引用 UnityEngine」+ 失去 Layer 信息无法处理 MAP-07 双层维度。`GridCoord` 已由 MAP-01 提供且与 `GridPos.CompareTo` 行为一致，零成本复用。

### B. 自相交检测放运行时（lazy）

- **优点**：构造期快；运行时按需计算。
- **否决理由**：违反 [AGENTS.md §10.1 确定性](../AGENTS.md) + [MAP_SYSTEM_AUDIT §7.2 算法冲突「确定性保证失败」](../MAP_SYSTEM_AUDIT.md#72-算法冲突)——runtime lazy 检测意味着同一初始状态在不同执行路径下可能 late-fail，Replay 哈希会不一致。eager 拒绝是唯一可重现路径。

### C. Polygon 用 class 不用 struct

- **优点**：可空 + 继承扩展。
- **否决理由**：`ConstellationPolygon` 是不可变值类型（Id + 顶点序列在构造后不变）；struct 性能更优（零分配比较 + Equals）+ 语义更准确（值相等而非引用相等）。运行时需要可变的部分已由 `AnchorLink` class 承载（§5）。

### D. 保留 `AnchorZone` 而不引入 `ConstellationPolygon`（只升级 Contains + 顶点排序）

- **优点**：最小改动；不引入新类型。
- **否决理由**：无法承载「AnchorLink 生命周期」语义——`AnchorZone` 是静态几何 + 8 值 zone 归属，**无法**表达「连线 Forming → Anchored」7 状态。`ConstellationPolygon` 是不可变几何，配套 `AnchorLink`（class，可变 runtime）是 doc2 §21.1 的强制分层。

### E. `AnchorLinkState` 用 8 值与 `AnchorZoneState` 对齐（合并两个枚举）

- **优点**：少一个枚举；调用方少一次思考。
- **否决理由**：语义正交——`AnchorZoneState` 表达「zone 归属」（谁的围区）；`AnchorLinkState` 表达「连线生命周期」（从建立到终止）。合并会模糊语义，并破坏 [AGENTS.md §11](../AGENTS.md)「类型枚举按业务职责分层」。

### F. Hasher 用 `0x40` + `0x41`（任务原 spec）

- **优点**：与任务 spec 一致。
- **否决理由**：冲突 [ADR-0003 §4 + §6](./ADR-0003-map-state-hash.md) 既有 tag 表 `0x40 / 0x41 / 0x42`（`TagAnchorZoneId / TagAnchorOwner / TagAnchorVertex`）；破坏既有 294 EditMode 测试期望。本 ADR §9 已说明改用 `0x38 + 0x43-0x47` 的理由。

---

## References

- [ADR-0001-core-data-model-and-hash.md](./ADR-0001-core-data-model-and-hash.md) — Core 数据模型与 FNV-1a 64 哈希基础
- [ADR-0003-map-state-hash.md](./ADR-0003-map-state-hash.md) — MapState 哈希契约 + 字段顺序不变原则
- [ADR-0004-map-command-framework.md](./ADR-0004-map-command-framework.md) — IMapCommand / MapCommandResult / MapEvent 框架
- [AGENTS.md §1](../AGENTS.md) — 核心玩法（锚点围区）
- [AGENTS.md §10.1](../AGENTS.md) — Core 不引用 `UnityEngine` / `UnityEditor`
- [AGENTS.md §11](../AGENTS.md) — 确定性规则（集合稳定排序、跨运行同哈希、锚点规范化顶点顺序）
- [MAP_SYSTEM_AUDIT §3.3](../MAP_SYSTEM_AUDIT.md#33-冲突与兼容性) — 多边形冲突清单（自相交 + 固定排序 + 整数定点）
- [MAP_SYSTEM_AUDIT §6.2 P1](../MAP_SYSTEM_AUDIT.md#62-p1扩展--编辑器建议第-2-批) — MAP-12 优先级与 2-3 天估算
- [MAP_SYSTEM_AUDIT §7.2](../MAP_SYSTEM_AUDIT.md#72-算法冲突) — 算法冲突（多边形包含浮点 → 整数定点）
- [MAP_SYSTEM_FORWARD_PLAN §3.2](../../Docs/MAP_SYSTEM_FORWARD_PLAN.md#32-范围严格-route-a) — route A 严格 scope
- [MAP_SYSTEM_FORWARD_PLAN §3.4](../../Docs/MAP_SYSTEM_FORWARD_PLAN.md#34-验收标准) — Gate 验证标准（EditMode 总数、ClonerTests、FNV-1a 100× 一致）
- [IMPLEMENTATION_STATUS §4.1](../../Docs/IMPLEMENTATION_STATUS.md) — MAP-12 候补 + 下一步候选
- [KNOWN_LIMITATIONS §5](../KNOWN_LIMITATIONS.md) — ADR 数量跟踪
- [`Assets/Starfall/Core/Anchor/AnchorZone.cs`](../../Assets/Starfall/Core/Anchor/AnchorZone.cs) — legacy `AnchorZone` POCO（保留）
- [`Assets/Starfall/Core/Anchor/AnchorRegistry.cs`](../../Assets/Starfall/Core/Anchor/AnchorRegistry.cs) — legacy `AnchorRegistry`（保留）
- [`Assets/Starfall/Core/Anchor/AnchorStateService.cs`](../../Assets/Starfall/Core/Anchor/AnchorStateService.cs) — 8 值 `AnchorZoneState` side-channel（与本 ADR 7 值 `AnchorLinkState` 正交）
- [`Assets/Starfall/Core/Map/State/MapState.cs`](../../Assets/Starfall/Core/Map/State/MapState.cs) — `MapState.Anchors` legacy 字段（保留）+ `AnchorLinks` 新增字段
- [`Assets/Starfall/Core/Map/State/MapStateHasher.cs`](../../Assets/Starfall/Core/Map/State/MapStateHasher.cs) — 既有 tag 表 `0x10-0x37` + `0x40-0x42`（保留）；本 ADR §9 追加 `0x38 + 0x43-0x47`
- [`Assets/Starfall/Core/Map/Commands/CreateAnchorLinkCommand.cs`](../../Assets/Starfall/Core/Map/Commands/CreateAnchorLinkCommand.cs) — MAP-03 Register stub（保留，标记 deprecated）
- [`Assets/Starfall/Core/Map/Commands/ModifyAnchorStateCommand.cs`](../../Assets/Starfall/Core/Map/Commands/ModifyAnchorStateCommand.cs) — MAP-03 8 值 zone 归属 state 命令（保留）
- [`Assets/Starfall/Core/Map/Commands/CreateConstellationAreaCommand.cs`](../../Assets/Starfall/Core/Map/Commands/CreateConstellationAreaCommand.cs) — MAP-03 Constellation region 占位（保留）
- [`Assets/Starfall/Core/Map/MapEvent.cs`](../../Assets/Starfall/Core/Map/MapEvent.cs) — `MapEvent` + `MapEventKind` 既有 9 值（保留）+ 本 ADR §8 追加 4 值
- FNV-1a 参考：<http://www.isthe.com/chongo/tech/comp/fnv/>