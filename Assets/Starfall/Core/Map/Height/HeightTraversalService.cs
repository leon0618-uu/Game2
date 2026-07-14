using System;
using System.Collections.Generic;

namespace Starfall.Core.Map.Height
{
    /// <summary>
    /// doc2 MAP-06 §4.4 高度遍历服务：判定单位能否从一格踏到另一格。
    ///
    /// <para/>
    /// **判定规则**：
    /// <list type="bullet">
    /// <item><see cref="MovementProfile.CanFly"/> 为 true → 无视高度差，恒为 true。</item>
    /// <item>同高度（Δh = 0）→ 恒为 true。</item>
    /// <item>上升（Δh &gt; 0）→ Δh ≤ <see cref="MovementProfile.MaxAscend"/>。</item>
    /// <item>下降（Δh &lt; 0）→ |Δh| ≤ <see cref="MovementProfile.MaxDescend"/>。</item>
    /// </list>
    ///
    /// <para/>
    /// **服务边界**：
    /// <list type="bullet">
    /// <item>纯静态方法 + 纯函数，无状态依赖；可并发调用。</item>
    /// <item>不引用 <c>UnityEngine</c>（AGENTS.md §10.1）；属于 Starfall.Core 程序集。</item>
    /// <item>不读 <see cref="State.MapState"/>；调用方自行把地图高度字典传入（Data 层未实现）。</item>
    /// </list>
    ///
    /// <para/>
    /// **稳定排序**：<see cref="SortByHeightAscending"/> 严格按 Y → X 排序
    /// （与 <see cref="Coordinates.GridCoord.CompareTo"/> 一致），避免依赖
    /// <c>Dictionary</c> 的内部哈希顺序；这是 AGENTS.md §11 强制要求。
    /// </summary>
    public static class HeightTraversalService
    {
        /// <summary>判定单位能否从 <paramref name="from"/> 高度踏到 <paramref name="to"/> 高度。</summary>
        /// <param name="from">源地块高度。</param>
        /// <param name="to">目标地块高度。</param>
        /// <param name="profile">单位的移动配置。</param>
        /// <returns>true = 可通行；false = 高度差超过 profile 限制。</returns>
        /// <exception cref="ArgumentException"><paramref name="profile"/> 为 default（MVP 防御性校验）。</exception>
        public static bool CanTraverse(HeightLevel from, HeightLevel to, MovementProfile profile)
        {
            // 飞行单位短路：无视所有 Δh。
            if (profile.CanFly) return true;

            int delta = to - from;
            if (delta == 0) return true;
            if (delta > 0)
            {
                // 上升：Δh ≤ MaxAscend
                return delta <= profile.MaxAscend;
            }
            // 下降：|Δh| ≤ MaxDescend
            int descent = -delta;
            return descent <= profile.MaxDescend;
        }

        /// <summary>最大可上升 Δh（飞行 = int.MaxValue，否则 = <see cref="MovementProfile.MaxAscend"/>）。</summary>
        public static int MaxAscendHeight(MovementProfile profile)
        {
            if (profile.CanFly) return int.MaxValue;
            return profile.MaxAscend;
        }

        /// <summary>最大可下降 |Δh|（飞行 = int.MaxValue，否则 = <see cref="MovementProfile.MaxDescend"/>）。</summary>
        public static int MaxDescendHeight(MovementProfile profile)
        {
            if (profile.CanFly) return int.MaxValue;
            return profile.MaxDescend;
        }

        /// <summary>判定 source profile 能否在 from → to 跨越（含 0 高度差）。同 <see cref="CanTraverse"/>。</summary>
        public static bool CanTraverse(
            HeightLevel from,
            HeightLevel to,
            bool canFly,
            int maxAscend,
            int maxDescend)
        {
            return CanTraverse(from, to, new MovementProfile(canFly, maxAscend, maxDescend, false));
        }

        /// <summary>
        /// 稳定排序：按 (height, Y, X) 升序输出 <paramref name="entries"/> 的拷贝。
        /// <para/>
        /// 排序键：先 height，再按 <see cref="Coordinates.GridCoord.CompareTo"/>（Y → X）。
        /// 用于显示"最矮到最高"或"从地面到塔顶"等确定性序列。
        /// </summary>
        public static List<KeyValuePair<Coordinates.GridCoord, HeightLevel>> SortByHeightAscending(
            IEnumerable<KeyValuePair<Coordinates.GridCoord, HeightLevel>> entries)
        {
            if (entries == null) throw new ArgumentNullException(nameof(entries));
            var list = new List<KeyValuePair<Coordinates.GridCoord, HeightLevel>>(entries);
            // KeyValuePair 不能直接 CompareTo，自己写 List.Sort(IComparer<>)。
            list.Sort(HeightCoordComparer.Instance);
            return list;
        }

        private sealed class HeightCoordComparer : IComparer<KeyValuePair<Coordinates.GridCoord, HeightLevel>>
        {
            public static readonly HeightCoordComparer Instance = new HeightCoordComparer();

            public int Compare(
                KeyValuePair<Coordinates.GridCoord, HeightLevel> a,
                KeyValuePair<Coordinates.GridCoord, HeightLevel> b)
            {
                int c = a.Value.CompareTo(b.Value);
                if (c != 0) return c;
                return a.Key.CompareTo(b.Key);
            }
        }
    }
}
