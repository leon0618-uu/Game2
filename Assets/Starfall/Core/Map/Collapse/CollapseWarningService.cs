using System;
using System.Collections.Generic;
using Starfall.Core.Map.Coordinates;
using Starfall.Core.Map.State;

namespace Starfall.Core.Map.Collapse
{
    /// <summary>
    /// doc2 MAP-11a 预警 4 等级枚举（ADR-0007）。
    /// <list type="bullet">
    /// <item><c>None</c>    — CV ∈ [0, 39]</item>
    /// <item><c>Caution</c> — CV ∈ [40, 59]（Fracturing 进入）</item>
    /// <item><c>Danger</c>  — CV ∈ [60, 79]（Collapsing 进入）</item>
    /// <item><c>Critical</c> — CV ∈ [80, 100]（GateFault 进入）</item>
    /// </list>
    /// </summary>
    public enum CollapseWarningLevel : byte
    {
        /// <summary>无预警。</summary>
        None = 0,

        /// <summary>注意：CV 进入 Fracturing。</summary>
        Caution = 1,

        /// <summary>危险：CV 进入 Collapsing。</summary>
        Danger = 2,

        /// <summary>严重：CV 进入 GateFault（终态）。</summary>
        Critical = 3,
    }

    /// <summary>
    /// doc2 MAP-11a 预警 API（Core 纯 C#，无 Unity 表现层；MAP-14 接入 HUD）。
    ///
    /// <para/>
    /// **职责**：
    /// <list type="bullet">
    /// <item><see cref="EvaluateWarningLevel"/> — 把 <see cref="GlobalCollapseValue"/> 映射为 4 等级。</item>
    /// <item><see cref="ShouldWarn"/> — 是否应该触发 UI 预警（true 当 GlobalCV 跨过阈值）。</item>
    /// <item><see cref="GetHotspots"/> — Top N 高 CV 格子（按 Value DESC, Coord ASC 排序）。</item>
    /// </list>
    ///
    /// <para/>
    /// **阈值表**（与 <see cref="CollapseStage"/> 同步）：
    /// <list type="bullet">
    /// <item>Caution  ≥ 40</item>
    /// <item>Danger   ≥ 60</item>
    /// <item>Critical ≥ 80</item>
    /// </list>
    /// </summary>
    public sealed class CollapseWarningService
    {
        /// <summary>Caution 阈值（Fracturing 进入）。</summary>
        public const int CautionThreshold = 40;

        /// <summary>Danger 阈值（Collapsing 进入）。</summary>
        public const int DangerThreshold = 60;

        /// <summary>Critical 阈值（GateFault 进入）。</summary>
        public const int CriticalThreshold = 80;

        // ──────────── 等级评估 ────────────

        /// <summary>把 <see cref="GlobalCollapseValue"/> 映射为 4 等级。</summary>
        public CollapseWarningLevel EvaluateWarningLevel(GlobalCollapseValue gcv)
        {
            int v = gcv.Value;
            if (v < CautionThreshold) return CollapseWarningLevel.None;
            if (v < DangerThreshold) return CollapseWarningLevel.Caution;
            if (v < CriticalThreshold) return CollapseWarningLevel.Danger;
            return CollapseWarningLevel.Critical;
        }

        /// <summary>
        /// 给定 map state，判断当前是否应该触发 UI 预警（true 当 GlobalCV ≥ threshold）。
        /// 阈值 <see cref="CautionThreshold"/> 推荐用于基础触发；
        /// 自定义阈值可通过 <paramref name="threshold"/> 覆盖。
        /// </summary>
        public bool ShouldWarn(MapState mapState, int threshold = CautionThreshold)
        {
            if (mapState == null) throw new ArgumentNullException(nameof(mapState));
            if (threshold < 0 || threshold > 100)
                throw new ArgumentOutOfRangeException(nameof(threshold), threshold,
                    "threshold must be in [0, 100].");
            return mapState.GlobalCV.Value >= threshold;
        }

        /// <summary>
        /// 给定 map state + 上一次 GlobalCV（用于跨阈值检测）：
        /// 当 <c>oldValue &lt; threshold &amp;&amp; newValue &gt;= threshold</c> 时返回 true。
        /// 适用于"刚跨越阈值"的一次性预警。
        /// </summary>
        public bool ShouldWarnOnTransition(int oldValue, int newValue, int threshold)
        {
            if (threshold < 0 || threshold > 100)
                throw new ArgumentOutOfRangeException(nameof(threshold), threshold,
                    "threshold must be in [0, 100].");
            return oldValue < threshold && newValue >= threshold;
        }

        // ──────────── 热点查询 ────────────

        /// <summary>
        /// Top N 高 CV 格子（按 Value DESC，同值按 GridCoord.CompareTo ASC）。
        /// topN ≤ 0 返回所有。
        /// </summary>
        public IReadOnlyList<LocalCollapseValue> GetHotspots(MapState mapState, int topN = 10)
        {
            if (mapState == null) throw new ArgumentNullException(nameof(mapState));
            if (topN < 0)
                throw new ArgumentOutOfRangeException(nameof(topN), topN, "topN must be >= 0.");

            var list = new List<LocalCollapseValue>(mapState.LocalCVs.Count);
            foreach (var lcv in mapState.LocalCVs.Values)
                list.Add(lcv);
            list.Sort((a, b) =>
            {
                int c = b.Value.CompareTo(a.Value);
                if (c != 0) return c;
                return a.Coord.CompareTo(b.Coord);
            });
            if (topN > 0 && list.Count > topN)
                list.RemoveRange(topN, list.Count - topN);
            return list;
        }
    }
}
