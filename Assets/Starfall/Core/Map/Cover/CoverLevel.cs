namespace Starfall.Core.Map.Cover
{
    /// <summary>
    /// doc2 MAP-06 §4.2 掩体等级。
    ///
    /// <para/>
    /// 数值固定：None=0, Half=1, Full=2。
    /// 该顺序与"遮挡强度"严格一致（None &lt; Half &lt; Full），便于直接用
    /// <c>byte</c> 比较决定命中修正 / 掩体附加防御。
    ///
    /// <para/>
    /// 数值由 AGENTS.md §11 强制约束：任何排序遍历 / 哈希编码 /
    /// Replay 字节流都依赖此顺序，**禁止重排或跳号**。
    /// </summary>
    public enum CoverLevel : byte
    {
        /// <summary>无掩体（空地 / 完全暴露）。</summary>
        None = 0,

        /// <summary>半掩体（半身墙 / 低掩体；命中率受 50% 修正）。</summary>
        Half = 1,

        /// <summary>全掩体（整墙 / 大型障碍物；攻击无法直接命中目标）。</summary>
        Full = 2,
    }
}
