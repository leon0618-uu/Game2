namespace Starfall.Core.Map.Tile
{
    /// <summary>
    /// doc2 MAP-04 §4.1 基础地形类型枚举（11 类）。
    ///
    /// <para/>
    /// 数值顺序固定：Plain = 0 → AnchorTile = 10，且对应 doc2 §3.4 验收矩阵的枚举序号。
    /// 任何按 byte 值排序 / 序列化 / Replay 字节流都依赖此顺序，**禁止重排或跳号**
    /// （AGENTS.md §11）。
    ///
    /// <para/>
    /// 与 <see cref="Starfall.Core.Model.TileState"/>（Normal / Blocked / Hazard / Objective）共存：
    /// <list type="bullet">
    /// <item><c>Core.Model.TileState</c> 是 doc1 MVP 的 4 类旧枚举，仍由
    ///       <see cref="BoardState"/> 与 <see cref="BattleState"/> 使用；</item>
    /// <item><c>TerrainType</c> 是 doc2 MAP-04 引入的 11 类新枚举，作为
    ///       <see cref="TerrainDefinition"/> 与 <see cref="TileDefinition"/> 的真实数据源；</item>
    /// <item>两者由 <see cref="LegacyTileStateAdapter"/> 桥接，旧 enum 自动映射为新
    ///       <c>TileDefinition</c>（Normal → Plain，Blocked → Wall，等等）。</item>
    /// </list>
    ///
    /// <para/>
    /// 地形类型 → 典型语义：
    /// <list type="table">
    /// <listheader><term>枚举</term><description>默认用途</description></listheader>
    /// <item><term><see cref="Plain"/></term><description>开阔地 / 默认地形</description></item>
    /// <item><term><see cref="Rough"/></term><description>碎石 / 缓慢地形（额外移动成本）</description></item>
    /// <item><term><see cref="Ruins"/></term><description>废墟，提供 Half 掩体</description></item>
    /// <item><term><see cref="Wall"/></term><description>整墙，阻挡移动 + 视线 + 弹道</description></item>
    /// <item><term><see cref="BrokenBridge"/></term><description>断桥，不稳定可坍塌</description></item>
    /// <item><term><see cref="LightBridge"/></term><description>光桥，允许通过但提供少量掩体</description></item>
    /// <item><term><see cref="Void"/></term><description>虚空（不可通过，但不下视线阻挡）</description></item>
    /// <item><term><see cref="ShallowAstralTide"/></term><description>浅层相位潮（轻度伤害）</description></item>
    /// <item><term><see cref="DeepAstralTide"/></term><description>深层相位潮（重度伤害）</description></item>
    /// <item><term><see cref="GateTile"/></term><description>相位门（跨相位通道）</description></item>
    /// <item><term><see cref="AnchorTile"/></term><description>锚点 tile（地图胜利条件之一）</description></item>
    /// </list>
    /// </summary>
    public enum TerrainType : byte
    {
        /// <summary>开阔地 / 默认地形。BaseMoveCost=1，不阻挡。</summary>
        Plain = 0,

        /// <summary>碎石地形。BaseMoveCost ≥ 2，不阻挡。</summary>
        Rough = 1,

        /// <summary>废墟。提供 Half 掩体。</summary>
        Ruins = 2,

        /// <summary>整墙。阻挡移动 + 视线 + 弹道，提供 Full 掩体。</summary>
        Wall = 3,

        /// <summary>断桥。初始可通过，但 Stability 会随时间下降。</summary>
        BrokenBridge = 4,

        /// <summary>光桥。临时可通过，由律令控制显隐。</summary>
        LightBridge = 5,

        /// <summary>虚空。不可通过但**不阻挡**视线（视觉可见却不可踏上）。</summary>
        Void = 6,

        /// <summary>浅层相位潮。每个回合对站在其上的单位造成少量伤害。</summary>
        ShallowAstralTide = 7,

        /// <summary>深层相位潮。每个回合对站在其上的单位造成大量伤害。</summary>
        DeepAstralTide = 8,

        /// <summary>相位门（跨相位通道）。允许 Reality / Astral 间的相位切换。</summary>
        GateTile = 9,

        /// <summary>锚点 tile。地图胜利条件之一；初始锁定（不可站立），被激活后开放。</summary>
        AnchorTile = 10,
    }
}