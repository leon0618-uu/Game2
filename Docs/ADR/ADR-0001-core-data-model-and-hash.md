# ADR-0001: Core 数据模型与哈希契约

- **状态**：Accepted（Task 02 起草）
- **日期**：2026-07-12
- **作者**：xingyuan-architect
- **关联任务包**：Task 02 Phase A
- **后续落地任务**：Task 03、Task 04、Task 12

---

## Context

Task 03 之后的战斗逻辑层（BattleState / Command / Pathfinder / Resolver / Status / Replay / Undo）需要一个稳定的数据契约：

1. `BattleState` 必须能在多次运行之间被稳定地序列化与比较；
2. Task 12 Replay 必须能用一个独立的 `PostStateHash` 验证「相同初始状态 + 相同 Command 序列 = 相同结束状态」；
3. AGENTS.md §10.1 与 §11 要求所有影响结果的集合遍历稳定排序，所有哈希必须跨平台/版本一致。

如果 Core 数据模型的字段顺序、哈希算法与序列化字节序不在最早一个 ADR 里钉死，下游每一个实现都会引入「漂移」，Task 12 的 Replay 一致性测试就无从验证。

本 ADR 必须先于 Task 03 / Task 04 落地，避免后续修改需重写大量测试。

---

## Decision

### 1. GridPos

```csharp
namespace Starfall.Core.Model
{
    /// <summary>
    /// 逻辑网格坐标。X 为列、Y 为行；原点 (0,0) = 地图左上角。
    /// 必须是 readonly record struct，避免装箱与不可变语义。
    /// </summary>
    public readonly record struct GridPos(int X, int Y) : IComparable<GridPos>
    {
        public int CompareTo(GridPos other)
        {
            // 规范排序：先 Y（升序），相等时比 X（升序）
            // 与 AGENTS.md §11「网格排序：先 y，后 x」一致
            int yCmp = Y.CompareTo(other.Y);
            if (yCmp != 0) return yCmp;
            return X.CompareTo(other.X);
        }
    }
}
```

- `GridPos` 不可变，`readonly record struct` 保证零分配比较。
- 所有 Core 内部集合使用 `GridPos.CompareTo` 作为排序键。

### 2. PostStateHash 算法

| 项 | 值 |
| --- | --- |
| 算法 | FNV-1a 64 位 |
| Offset basis | `0xCBF29CE484222325`（14695981039346656037） |
| Prime | `0x100000001B3`（1099511628211） |
| 字节序 | 小端序（little-endian） |
| 字段拼接 | 按本 ADR §3「哈希字段包含表」顺序链式 `hash = (hash ^ byte) * prime` |

伪代码：

```text
hash = 0xCBF29CE484222325
for each field in order:
    write field as little-endian bytes
    for each byte:
        hash = (hash XOR byte) * 0x100000001B3
        // 使用 ulong 无符号溢出回卷（与 .NET 默认一致）
return hash
```

### 3. 哈希字段包含表（顺序固定，不得在原 ADR 内 patch）

| 序号 | 字段 | 类型 | 字节布局 |
| --- | --- | --- | --- |
| 1 | `turnNumber` | int32 | LE 4 bytes |
| 2 | `activePlayer` | byte | 1 byte |
| 3 | `gridWidth` | byte | 1 byte |
| 4 | `gridHeight` | byte | 1 byte |
| 5 | `units` | 链式 | 按 `UnitId` 升序排列的 `UnitSnapshot` 哈希链 |
| 6 | `tileStates` | 链式 | 按 `(Y, X)` 升序排列的 `TileSnapshot` 哈希链 |
| 7 | `statuses` | 链式 | 按 `(StatusId, RemainingTurns, InstanceId)` 升序排列的 `StatusInstance` 哈希链 |
| 8 | `pendingDecrees` | 链式 | 按 `InstanceId` 升序排列 |

子结构（`UnitSnapshot` / `TileSnapshot` / `StatusInstance`）的字段顺序在各自记录结构声明中固定：

```csharp
public readonly record struct UnitSnapshot(
    UnitId UnitId, int X, int Y, int Hp, byte Phase, byte Direction);
// 字段顺序即哈希顺序，不得调换

public readonly record struct TileSnapshot(
    int X, int Y, byte Terrain, byte State);
// 同上

public readonly record struct StatusInstance(
    StatusId StatusId, int RemainingTurns, ulong InstanceId);
// 同上；与 AGENTS.md §11「状态顺序：StatusId、剩余回合、实例 ID」一致
```

### 4. BattleState 接口契约

```csharp
namespace Starfall.Core.Model
{
    public readonly record struct BattleState(
        int TurnNumber,
        byte ActivePlayer,
        byte GridWidth,
        byte GridHeight,
        IReadOnlyList<UnitSnapshot> Units,
        IReadOnlyList<TileSnapshot> TileStates,
        IReadOnlyList<StatusInstance> Statuses,
        IReadOnlyList<DecreeInstance> PendingDecrees)
    {
        /// <summary>
        /// FNV-1a 64 位哈希，按本 ADR §3 字段顺序链式计算。
        /// 相同 BattleState 内容 + 相同字段顺序 = 相同哈希（跨平台、跨 .NET 版本）。
        /// </summary>
        public ulong PostStateHash { get; }
    }

    /// <summary>深拷贝 BattleState；所有引用类型必须独立副本。</summary>
    public interface IBattleStateCloner
    {
        BattleState Clone(in BattleState source);
    }

    /// <summary>BattleState 等值比较与哈希。</summary>
    public interface IBattleStateComparer
    {
        bool Equals(in BattleState a, in BattleState b);
        int GetHashCode(in BattleState s);
    }
}
```

- `BattleStateCloner` 必须深拷贝 `IReadOnlyList<...>` 元素，副本之间不得共享引用。
- `BattleStateComparer.Equals` 必须返回 `a.PostStateHash == b.PostStateHash`（按字段等价实现）。
- `BattleState.PostStateHash` 属性是 computed property，调用时按 §3 实时计算，禁止缓存可变字段。

---

## Consequences

### 正面

- Task 04 可按本 ADR 直接实现 `IBattleStateCloner` / `IBattleStateComparer`，无需二次设计。
- Task 12 Replay 文件格式可定义为 `PostStateHash + CommandHistory`，独立可验证（无需回放即可确认最终状态）。
- AGENTS.md §11 的所有稳定排序规则在本 ADR 中以代码契约形式固化。
- FNV-1a 64 零依赖、跨平台一致，便于 EditMode 测试跨 .NET 版本运行。

### 负面 / 约束

- 任何对 §3 哈希字段表的修改必须新建 ADR（如 ADR-000X-hash-schema-v2），不得在原 ADR 内 patch，否则历史 Replay 文件失效。
- Task 04 必须在 `IBattleStateComparer` 中显式调用 `PostStateHash` 计算，禁止缓存。
- `GridPos` 字段顺序（`X, Y`）与 `CompareTo` 顺序（`Y, X`）不同，命名上易混淆；所有调用点必须遵循 `CompareTo` 排序规范而非字段声明顺序。

### 任务影响

| 任务 | 影响 |
| --- | --- |
| Task 03 | 按 §1 GridPos / §3 UnitSnapshot 等结构定义 `Starfall.Core.Model` 命名空间 |
| Task 04 | 实现 `IBattleStateCloner` / `IBattleStateComparer` / `PostStateHash` 计算 |
| Task 12 | Replay 文件 = `PostStateHash` + `CommandHistory`；可用 `Equals(in, in)` 验证 |
| QA 测试 | 跨进程同输入同哈希（参见 `Docs/05_Test_and_Acceptance.md`） |

---

## Alternatives considered

### xxHash64

- **优点**：速度比 FNV-1a 高数倍。
- **否决理由**：.NET 标准库未提供，需引入第三方 dll 或自实现；MVP 阶段零依赖优先；速度不是 Replay 验证瓶颈（每回合 1 次计算）。

### MurmurHash3

- **优点**：广泛使用，散列质量好。
- **否决理由**：MurmurHash3 与 FNV-1a 的字节序约定不同（最终化步骤差异），跨实现一致性文档不如 FNV-1a 完善；MVP 选行业默认更稳。

### System.HashCode

- **优点**：.NET Core 2.1+ 内置，零分配。
- **否决理由**：官方文档明确说明 `System.HashCode` 实现可能在不同 .NET 版本间变化（random seed per process），跨平台 / 跨运行不一致。AGENTS.md §10.1「不使用不稳定的 `object.GetHashCode()`」原则同样适用。

### 选用 FNV-1a 64（最终选择）

- **优点**：纯算法、零依赖、Wikipedia 与 RFC 均有参考实现、字节序明确（LE）、社区文档充分。
- **缺点**：散列质量弱于 xxHash/MurmurHash；但 BattleState 字段数小（通常 < 200），碰撞概率可忽略。

---

## References

- `AGENTS.md` §10.1（Core 硬约束）、§11（确定性规则）
- `Docs/02_Technical_Development_Manual.md`（待补 FNV-1a 一节，Task 03 前）
- `Docs/05_Test_and_Acceptance.md`（待补同输入同哈希测试规范）
- FNV-1a 参考：<http://www.isthe.com/chongo/tech/comp/fnv/>