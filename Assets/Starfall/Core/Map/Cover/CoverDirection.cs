namespace Starfall.Core.Map.Cover
{
    /// <summary>
    /// doc2 MAP-06 §4.2 掩体方向（攻击者相对 defender 的方向）。
    ///
    /// <para/>
    /// 数值固定：North=0, East=1, South=2, West=3, All=4。
    /// 前 4 个值与 <see cref="Starfall.Core.Map.Coordinates.GridDirection"/> 严格对应
    /// （同一 byte 序号），便于按同一 byte 索引查找 4 邻居掩体。
    ///
    /// <para/>
    /// <see cref="All"/> 表示"该 tile 对任何方向都暴露"，例如开阔地；
    /// 调用方需要在 4 邻居平均时把 All 视为 None。
    ///
    /// <para/>
    /// 数值顺序由 AGENTS.md §11 强制约束：**禁止重排或插入中间值**，
    /// 否则与现有 GridDirection / GridCoord.Neighbours() 的契约会全部失效。
    /// </summary>
    public enum CoverDirection : byte
    {
        /// <summary>北方（Y+1）攻击者方向；掩体在该 tile 北侧。</summary>
        North = 0,

        /// <summary>东方（X+1）攻击者方向。</summary>
        East = 1,

        /// <summary>南方（Y-1）攻击者方向。</summary>
        South = 2,

        /// <summary>西方（X-1）攻击者方向。</summary>
        West = 3,

        /// <summary>该 tile 对所有方向都暴露（开阔地 / 平台中央）。</summary>
        All = 4,
    }
}
