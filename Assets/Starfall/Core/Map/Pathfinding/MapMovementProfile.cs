using System;

namespace Starfall.Core.Map.Pathfinding
{
    /// <summary>
    /// doc2 MAP-05 §9.4 寻路用移动配置（readonly struct）。
    ///
    /// <para/>
    /// 与既有 <see cref="Starfall.Core.Map.Height.MovementProfile"/>（MAP-06 高度判定）的区别：
    /// <list type="bullet">
    /// <item>本结构面向"寻路可达范围"：含 <see cref="MaxMovementPoints"/>（AP）、
    ///       <see cref="CanCrossDimension"/>，以及 map path 性能优化所需字段。</item>
    /// <item>既有 <c>Height.MovementProfile</c> 面向"高度判定"：含
    ///       <c>MaxAscend</c> / <c>MaxDescend</c>。
    ///       两者并存不冲突（不同命名空间 + 不同字段语义）。</item>
    /// </list>
    ///
    /// <para/>
    /// 字段语义：
    /// <list type="bullet">
    /// <item><see cref="MaxAscendHeight"/>：最大可上升 Δh（飞行单位视作 int.MaxValue）。
    ///       与既有 <c>Height.MovementProfile.MaxAscend</c> 含义一致；命名带 'Height' 后缀
    ///       避免与同名 <c>MaxAscend</c> 字段歧义。</item>
    /// <item><see cref="MaxDescendHeight"/>：最大可下降 |Δh|（飞行单位 = int.MaxValue）。</item>
    /// <item><see cref="CanFly"/>：飞行单位短路所有 Δh，并允许跨维（与既有字段语义一致）。</item>
    /// <item><see cref="CanCrossDimension"/>：允许在 Reality ↔ Astral 间跨越；
    ///       <see cref="PathfindingService"/> 在跨 layer 邻居处使用。</item>
    /// <item><see cref="MaxMovementPoints"/>：单回合最大移动成本（AP）。与
    ///       <see cref="TileDefinition.BaseMoveCost"/> 相加，<see cref="MovementRangeService"/>
    ///       用 BFS 累计到达每个 tile 的移动成本，限制 &lt;= MaxMovementPoints。</item>
    /// </list>
    ///
    /// <para/>
    /// 三个内置预设：<see cref="Standard"/>（步兵）、<see cref="Flyer"/>（飞行）、<see cref="Heavy"/>（重装/机甲）。
    /// 三者语义在 ADR-0005 中明确：飞行可无视 Δh 与跨维；重装可上升低 + 下降低，但 AP 较少；标准是大多数 player 单位的折衷。
    /// </summary>
    public readonly struct MapMovementProfile : IEquatable<MapMovementProfile>
    {
        /// <summary>默认最大上升 Δh（飞行不计）。</summary>
        public const int DefaultMaxAscendHeight = 1;

        /// <summary>默认最大下降 |Δh|。</summary>
        public const int DefaultMaxDescendHeight = 2;

        /// <summary>默认行动点（AP）。</summary>
        public const int DefaultMaxMovementPoints = 6;

        public readonly int MaxAscendHeight;
        public readonly int MaxDescendHeight;
        public readonly bool CanFly;
        public readonly bool CanCrossDimension;
        public readonly int MaxMovementPoints;

        public MapMovementProfile(
            int maxAscendHeight,
            int maxDescendHeight,
            bool canFly,
            bool canCrossDimension,
            int maxMovementPoints)
        {
            if (maxAscendHeight < 0)
                throw new ArgumentOutOfRangeException(nameof(maxAscendHeight), maxAscendHeight,
                    "MaxAscendHeight must be >= 0 (飞行单位请传 0 并设置 CanFly=true).");
            if (maxDescendHeight < 0)
                throw new ArgumentOutOfRangeException(nameof(maxDescendHeight), maxDescendHeight,
                    "MaxDescendHeight must be >= 0 (表示可下降的 |Δh|).");
            if (maxMovementPoints < 0)
                throw new ArgumentOutOfRangeException(nameof(maxMovementPoints), maxMovementPoints,
                    "MaxMovementPoints must be >= 0 (0 视为禁止移动).");

            MaxAscendHeight = maxAscendHeight;
            MaxDescendHeight = maxDescendHeight;
            CanFly = canFly;
            CanCrossDimension = canCrossDimension;
            MaxMovementPoints = maxMovementPoints;
        }

        /// <summary>默认 / 玩家步兵：上 1 / 下 2 / AP 6 / 不可飞 / 不可跨维。</summary>
        public static readonly MapMovementProfile Standard = new MapMovementProfile(
            maxAscendHeight: DefaultMaxAscendHeight,
            maxDescendHeight: DefaultMaxDescendHeight,
            canFly: false,
            canCrossDimension: false,
            maxMovementPoints: DefaultMaxMovementPoints);

        /// <summary>飞行单位：Δh 短路 / AP 6 / 可跨维。</summary>
        public static readonly MapMovementProfile Flyer = new MapMovementProfile(
            maxAscendHeight: 0,
            maxDescendHeight: 0,
            canFly: true,
            canCrossDimension: true,
            maxMovementPoints: DefaultMaxMovementPoints);

        /// <summary>重装 / 机甲方舟：上 0 / 下 1 / AP 4 / 不可飞 / 不可跨维。
        /// （可上升 0 等同"不能爬梯子，但能在平地间走"。）</summary>
        public static readonly MapMovementProfile Heavy = new MapMovementProfile(
            maxAscendHeight: 0,
            maxDescendHeight: 1,
            canFly: false,
            canCrossDimension: false,
            maxMovementPoints: 4);

        // ──────────── 等值 / 哈希 ────────────

        public bool Equals(MapMovementProfile other)
            => MaxAscendHeight == other.MaxAscendHeight
               && MaxDescendHeight == other.MaxDescendHeight
               && CanFly == other.CanFly
               && CanCrossDimension == other.CanCrossDimension
               && MaxMovementPoints == other.MaxMovementPoints;

        public override bool Equals(object obj) => obj is MapMovementProfile other && Equals(other);

        public override int GetHashCode()
        {
            unchecked
            {
                int h = MaxAscendHeight;
                h = (h * 397) ^ MaxDescendHeight;
                h = (h * 397) ^ (CanFly ? 1 : 0);
                h = (h * 397) ^ (CanCrossDimension ? 1 : 0);
                h = (h * 397) ^ MaxMovementPoints;
                return h;
            }
        }

        public static bool operator ==(MapMovementProfile a, MapMovementProfile b) => a.Equals(b);

        public static bool operator !=(MapMovementProfile a, MapMovementProfile b) => !a.Equals(b);

        public override string ToString()
            => $"MapMovementProfile(ascend={MaxAscendHeight}, descend={MaxDescendHeight}, fly={CanFly}, crossDim={CanCrossDimension}, ap={MaxMovementPoints})";
    }
}
