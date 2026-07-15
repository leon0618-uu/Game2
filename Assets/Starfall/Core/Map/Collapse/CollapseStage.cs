namespace Starfall.Core.Map.Collapse
{
    /// <summary>
    /// doc2 MAP-11a 全局坍塌值 5 阶段状态机（ADR-0007）。
    ///
    /// <para/>
    /// 范围映射（[<see cref="MinValue"/>, <see cref="MaxValue"/>]）：
    /// <list type="bullet">
    /// <item><c>Stable</c>     ∈ [0, 19]  — 默认；无效果</item>
    /// <item><c>Anomalous</c>  ∈ [20, 39] — 随机生成 OnAnomalyDetected</item>
    /// <item><c>Fracturing</c> ∈ [40, 59] — 高频生成 OnTileFractured（基于本地 CV ≥ 50）</item>
    /// <item><c>Collapsing</c> ∈ [60, 79] — EnvironmentalHazard / Restricted 标 Contested</item>
    /// <item><c>GateFault</c>  ∈ [80, 100] — 终态；Emit OnGateFaultTriggered（游戏结束条件之一）</item>
    /// </list>
    ///
    /// <para/>
    /// 位序固定（AGENTS.md §11）：禁止重排或跳号；任何序列化 / 哈希 / 网络协议
    /// 都依赖此位序。
    /// </summary>
    public enum CollapseStage : byte
    {
        /// <summary>稳定：CV ∈ [0, 19]。无效果。</summary>
        Stable = 0,

        /// <summary>异常：CV ∈ [20, 39]。Emit OnAnomalyDetected。</summary>
        Anomalous = 1,

        /// <summary>断裂：CV ∈ [40, 59]。高频 OnTileFractured（本地 CV ≥ 50 的格子）。</summary>
        Fracturing = 2,

        /// <summary>坍塌：CV ∈ [60, 79]。EnvironmentalHazard / Restricted 标 Contested。</summary>
        Collapsing = 3,

        /// <summary>闸门失稳：CV ∈ [80, 100]。终态；Emit OnGateFaultTriggered。</summary>
        GateFault = 4,
    }

    /// <summary>
    /// <see cref="CollapseStage"/> 静态映射工具。
    /// <para/>
    /// **约定**（与 <c>FromValue</c> / <c>MinValue</c> / <c>MaxValue</c> 一致）：
    /// <list type="bullet">
    /// <item><see cref="FromValue"/> 接受任意 <c>int</c>，先 clamp 到 [0, 100] 再映射阶段。</item>
    /// <item><see cref="MinValue"/> / <see cref="MaxValue"/> 返回每个阶段的范围端点（闭区间）。</item>
    /// <item>值 = 阶段上限时仍属于该阶段（<c>MaxValue</c> 是闭区间上界，不是开区间）。</item>
    /// </list>
    /// </summary>
    public static class CollapseStageMapping
    {
        /// <summary>把任意 int CV 值映射到 <see cref="CollapseStage"/>（先 clamp 到 [0, 100]）。</summary>
        public static CollapseStage FromValue(int cv)
        {
            if (cv < 0) cv = 0;
            if (cv > 100) cv = 100;
            if (cv <= 19) return CollapseStage.Stable;
            if (cv <= 39) return CollapseStage.Anomalous;
            if (cv <= 59) return CollapseStage.Fracturing;
            if (cv <= 79) return CollapseStage.Collapsing;
            return CollapseStage.GateFault;
        }

        /// <summary>该阶段的下限（含）。</summary>
        public static int MinValue(CollapseStage stage)
        {
            switch (stage)
            {
                case CollapseStage.Stable: return 0;
                case CollapseStage.Anomalous: return 20;
                case CollapseStage.Fracturing: return 40;
                case CollapseStage.Collapsing: return 60;
                case CollapseStage.GateFault: return 80;
                default:
                    // 未知 byte 视为最小；保持确定性而不是抛异常（Gameplay 风格）。
                    return 0;
            }
        }

        /// <summary>该阶段的上限（含）。</summary>
        public static int MaxValue(CollapseStage stage)
        {
            switch (stage)
            {
                case CollapseStage.Stable: return 19;
                case CollapseStage.Anomalous: return 39;
                case CollapseStage.Fracturing: return 59;
                case CollapseStage.Collapsing: return 79;
                case CollapseStage.GateFault: return 100;
                default:
                    return 100;
            }
        }
    }
}
