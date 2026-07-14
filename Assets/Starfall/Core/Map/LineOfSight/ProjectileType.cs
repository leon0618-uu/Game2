namespace Starfall.Core.Map.LineOfSight
{
    /// <summary>
    /// doc2 MAP-06 §4.3 弹道分类。
    ///
    /// <para/>
    /// 数值固定：Direct=0, Arc=1, Beam=2, Chain=3, GroundPropagation=4, CrossPhase=5。
    /// 不同类型决定 <see cref="LineOfSightService"/> 的具体行为：
    /// <list type="bullet">
    /// <item><see cref="Direct"/>：单格直线；Full Cover 必挡，Half Cover 给 50% 命中。</item>
    /// <item><see cref="Arc"/>：抛物线；忽略 Half Cover（从天而降），仍被 Full Cover 挡。</item>
    /// <item><see cref="Beam"/>：链式 / 光束；命中首目标后衰减至下一格（实现留给上层）。</item>
    /// <item><see cref="Chain"/>：弹跳；最多 N 个目标，按优先级遍历（本服务只暴露 LOS 结果）。</item>
    /// <item><see cref="GroundPropagation"/>：地表传播；只看 Ground 层 TileDef.BlocksLineOfSight。</item>
    /// <item><see cref="CrossPhase"/>：跨相位；先走 Reality，再走 Astral（同 (X,Y) 跨层）。</item>
    /// </list>
    ///
    /// <para/>
    /// 数值顺序由 AGENTS.md §11 强制约束：**禁止重排或跳号**，否则 Replay 字节流会失稳。
    /// </summary>
    public enum ProjectileType : byte
    {
        /// <summary>单格直线。</summary>
        Direct = 0,

        /// <summary>抛物线（忽略 Half Cover）。</summary>
        Arc = 1,

        /// <summary>链式 / 光束。</summary>
        Beam = 2,

        /// <summary>弹跳。</summary>
        Chain = 3,

        /// <summary>地表传播。</summary>
        GroundPropagation = 4,

        /// <summary>跨相位（双层）。</summary>
        CrossPhase = 5,
    }
}
