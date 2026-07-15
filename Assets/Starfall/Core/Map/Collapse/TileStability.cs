namespace Starfall.Core.Map.Collapse
{
    /// <summary>
    /// doc2 MAP-11a 单 tile 稳定性 6 值枚举（ADR-0007）。
    ///
    /// <para/>
    /// 位序固定（AGENTS.md §11）：禁止重排或跳号；任何序列化 / 哈希 / 网络协议
    /// 都依赖此位序。新增类别一律追加到末尾，并通过 ADR 升级。
    ///
    /// <para/>
    /// **Passability 规则**（<see cref="TileStabilityExtensions.IsPassable"/>）：
    /// <list type="bullet">
    /// <item><see cref="Stable"/> / <see cref="Unstable"/> / <see cref="Reconstructed"/> = true</item>
    /// <item><see cref="Fractured"/> / <see cref="Collapsing"/> / <see cref="Collapsed"/> = false</item>
    /// </list>
    ///
    /// **Destroyed 规则**（<see cref="TileStabilityExtensions.IsDestroyed"/>）：
    /// 仅 <see cref="Collapsed"/> = true。
    /// </summary>
    public enum TileStability : byte
    {
        /// <summary>稳定：默认；可通行，未受影响。</summary>
        Stable = 0,

        /// <summary>不稳：仍可通行，但已受 CV 累积影响。</summary>
        Unstable = 1,

        /// <summary>断裂：不可通行，CV 累积超过 50 的临界点。</summary>
        Fractured = 2,

        /// <summary>坍塌中：不可通行，处于向 Collapsed 过渡。</summary>
        Collapsing = 3,

        /// <summary>已坍塌：不可通行；终态（除非 Reconstructed）。</summary>
        Collapsed = 4,

        /// <summary>已重建：可通行，由 <c>ReconstructTileCommand</c> 从 Collapsed 恢复。</summary>
        Reconstructed = 5,
    }

    /// <summary>
    /// <see cref="TileStability"/> 行为扩展。
    /// </summary>
    public static class TileStabilityExtensions
    {
        /// <summary>该稳定性值是否允许单位站立 / 寻路通过。</summary>
        public static bool IsPassable(this TileStability s)
        {
            switch (s)
            {
                case TileStability.Stable:
                case TileStability.Unstable:
                case TileStability.Reconstructed:
                    return true;
                default:
                    return false;
            }
        }

        /// <summary>该稳定性值是否表示 tile 已物理销毁（终态）。</summary>
        public static bool IsDestroyed(this TileStability s)
        {
            return s == TileStability.Collapsed;
        }
    }
}
