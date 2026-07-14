using System;

namespace Starfall.Core.Map.Height
{
    /// <summary>
    /// doc2 §9.4 移动配置：单位能跨越的最大高度差 + 是否飞行 / 跨维。
    ///
    /// <para/>
    /// 字段语义：
    /// <list type="bullet">
    /// <item><see cref="CanFly"/>：飞行单位，无视所有高度差（同时视为可以跨维下降）。</item>
    /// <item><see cref="MaxAscend"/>：最大可上升的 Δh（向下兼容 ≤ 0 视为不能上升）。
    ///       典型玩家步兵 1，重装 0（不能上梯），双足机甲 2。</item>
    /// <item><see cref="MaxDescend"/>：最大可下降的 Δh（绝对值；负值方向）。
    ///       典型玩家步兵 2，重装 1，飞行 ∞（由 CanFly 短路）。</item>
    /// <item><see cref="CanCrossDimension"/>：是否允许在 Reality / Astral 间跨越。
    ///       doc2 §9.4 仅飞行单位可跨维。</item>
    /// </list>
    ///
    /// <para/>
    /// 是 <c>readonly struct</c>，便于在 <see cref="UnitState"/> / 配置 JSON /
    /// <see cref="HeightTraversalService"/> 入口处传值而不分配。提供两个内置 profile：
    /// <see cref="Standard"/>（标准步兵）+ <see cref="Flyer"/>（飞行单位）。
    /// </summary>
    public readonly struct MovementProfile : IEquatable<MovementProfile>
    {
        public readonly bool CanFly;
        public readonly int MaxAscend;
        public readonly int MaxDescend;
        public readonly bool CanCrossDimension;

        public MovementProfile(bool canFly, int maxAscend, int maxDescend, bool canCrossDimension)
        {
            if (maxAscend < 0)
                throw new ArgumentOutOfRangeException(nameof(maxAscend), maxAscend,
                    "MaxAscend must be >= 0 (doc2 §9.4: -1 不允许；飞行单位置 0 即可).");
            if (maxDescend < 0)
                throw new ArgumentOutOfRangeException(nameof(maxDescend), maxDescend,
                    "MaxDescend must be >= 0 (doc2 §9.4: 表示可下降的 |Δh|).");
            CanFly = canFly;
            MaxAscend = maxAscend;
            MaxDescend = maxDescend;
            CanCrossDimension = canCrossDimension;
        }

        /// <summary>标准玩家步兵：不能飞，上 1 下 2，不可跨维。</summary>
        public static readonly MovementProfile Standard = new MovementProfile(
            canFly: false, maxAscend: 1, maxDescend: 2, canCrossDimension: false);

        /// <summary>飞行单位：飞 0 0（由 CanFly 短路所有 Δh），可跨维。</summary>
        public static readonly MovementProfile Flyer = new MovementProfile(
            canFly: true, maxAscend: 0, maxDescend: 0, canCrossDimension: true);

        // ──────────── 等值 / 哈希 ────────────

        public bool Equals(MovementProfile other)
            => CanFly == other.CanFly
               && MaxAscend == other.MaxAscend
               && MaxDescend == other.MaxDescend
               && CanCrossDimension == other.CanCrossDimension;

        public override bool Equals(object obj) => obj is MovementProfile other && Equals(other);

        public override int GetHashCode()
        {
            unchecked
            {
                int h = CanFly ? 1 : 0;
                h = (h * 397) ^ MaxAscend;
                h = (h * 397) ^ MaxDescend;
                h = (h * 397) ^ (CanCrossDimension ? 1 : 0);
                return h;
            }
        }

        public static bool operator ==(MovementProfile a, MovementProfile b) => a.Equals(b);

        public static bool operator !=(MovementProfile a, MovementProfile b) => !a.Equals(b);

        public override string ToString()
            => $"MovementProfile(fly={CanFly}, up={MaxAscend}, down={MaxDescend}, crossDim={CanCrossDimension})";
    }
}
