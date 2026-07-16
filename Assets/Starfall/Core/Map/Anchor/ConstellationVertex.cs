using System;
using Starfall.Core.Map.Coordinates;

namespace Starfall.Core.Map.Anchor
{
    /// <summary>
    /// doc2 MAP-12 星座多边形顶点（锚点围区 / 律令多边形共用）。
    /// <para/>
    /// **不可变** <c>readonly struct</c>；通过 <see cref="IEquatable{T}"/> +
    /// <see cref="IComparable{T}"/> 自手实现，避免依赖 <c>record struct</c>
    /// （langversion=9.0）。所有跨集合 / 哈希 / 排序逻辑均依赖此结构。
    /// <para/>
    /// **排序规则**（AGENTS.md §11 确定性）：
    /// <list type="number">
    /// <item>先 <c>Y</c>；</item>
    /// <item>再 <c>X</c>；</item>
    /// <item>最后 <c>Layer</c>（Reality=0 &lt; Astral=1）。</item>
    /// </list>
    /// 这与 <see cref="GridCoord.CompareTo"/> 完全一致——结构上是同一排序键。
    /// 之所以不直接复用 <see cref="GridCoord"/>，是因为 ConstellationVertex 是
    /// 锚点 / 律令域的"业务顶点"，未来可能挂载附加字段（例如顶点权重 / 法向量），
    /// 与纯网格坐标解耦更安全。
    /// </summary>
    public readonly struct ConstellationVertex : IEquatable<ConstellationVertex>, IComparable<ConstellationVertex>
    {
        /// <summary>顶点所在的网格坐标（X / Y / Layer）。</summary>
        public GridCoord Coord { get; }

        public ConstellationVertex(GridCoord coord)
        {
            Coord = coord;
        }

        public ConstellationVertex(int x, int y, DimensionLayer layer)
            : this(new GridCoord(x, y, layer))
        {
        }

        // ──────────── 等值 / 哈希 ────────────

        public bool Equals(ConstellationVertex other) => Coord.Equals(other.Coord);

        public override bool Equals(object obj) => obj is ConstellationVertex other && Equals(other);

        public override int GetHashCode() => Coord.GetHashCode();

        public static bool operator ==(ConstellationVertex a, ConstellationVertex b) => a.Equals(b);

        public static bool operator !=(ConstellationVertex a, ConstellationVertex b) => !a.Equals(b);

        // ──────────── 排序 ────────────

        public int CompareTo(ConstellationVertex other) => Coord.CompareTo(other.Coord);

        // ──────────── 字符串 ────────────

        public override string ToString() => $"ConstellationVertex({Coord})";
    }
}