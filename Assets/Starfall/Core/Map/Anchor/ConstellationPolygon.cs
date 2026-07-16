using System;
using System.Collections.Generic;
using Starfall.Core.Map.Coordinates;

namespace Starfall.Core.Map.Anchor
{
    /// <summary>
    /// doc2 MAP-12 星座多边形（锚点围区 / 律令多边形共用）。
    /// <para/>
    /// **不可变** <c>readonly struct</c>。构造期调用 <see cref="ConstellationValidator"/>：
    /// <list type="bullet">
    /// <item>顶点数 &lt; 3 → 抛 <see cref="ArgumentException"/>。</item>
    /// <item>所有顶点共线 → 抛 <see cref="ArgumentException"/>（退化）。</item>
    /// <item>自相交 → 抛 <see cref="ArgumentException"/>。</item>
    /// </list>
    /// 顶点会被 <see cref="ConstellationValidator.NormalizeVertices"/> 规范化
    /// （Y→X→Layer 排序，保持循环顺序），存入内部 <see cref="Vertices"/> 字段。
    /// <para/>
    /// **<see cref="Contains(GridCoord)"/> 算法**：整数射线法（horizontal ray casting，
    /// AGENTS.md §11 + doc2 §14.4 整数定点击中测试）。输入点位于边上视为 **inside = false**
    /// （约定半开 / 半闭区间，规则统一即可）。
    /// <para/>
    /// **等值规则**：按规范化顶点序列比较（<see cref="Vertices"/> 已经规范化）。
    /// 即相同顶点、不同输入顺序的两个 polygon 视为相等。
    /// <para/>
    /// **邻居顺序 N→E→S→W 确定性**：射线法内部边遍历走 i = 0..n-1 升序，
    /// 与 <see cref="GridCoord.Neighbours"/> 的 N→E→S→W 不直接相关（边是隐式
    /// 相邻顶点对），但任何依赖此结构顺序的算法都遵守同样的固定顺序。
    /// </summary>
    public readonly struct ConstellationPolygon : IEquatable<ConstellationPolygon>
    {
        /// <summary>多边形 ID（业务名 / 字符串 ID）。</summary>
        public ConstellationPolygonId Id { get; }

        /// <summary>规范化顶点列表（Y→X→Layer 排序后保持循环顺序）。</summary>
        public IReadOnlyList<ConstellationVertex> Vertices { get; }

        public ConstellationPolygon(ConstellationPolygonId id, IReadOnlyList<ConstellationVertex> vertices)
        {
            if (vertices == null) throw new ArgumentNullException(nameof(vertices));

            var validation = ConstellationValidator.Validate(vertices);
            if (validation != ConstellationValidator.ConstellationValidationError.None)
            {
                throw new ArgumentException(
                    $"Invalid ConstellationPolygon (Id={id}, error={validation}). " +
                    "Polygon must be non-degenerate and non-self-intersecting.",
                    nameof(vertices));
            }

            Id = id;
            Vertices = ConstellationValidator.NormalizeVertices(vertices);
        }

        // ──────────── 等值 ────────────

        public bool Equals(ConstellationPolygon other)
        {
            if (!Id.Equals(other.Id)) return false;
            return VerticesEqual(Vertices, other.Vertices);
        }

        public override bool Equals(object obj) => obj is ConstellationPolygon other && Equals(other);

        public override int GetHashCode()
        {
            unchecked
            {
                int h = Id.GetHashCode();
                if (Vertices != null)
                {
                    // 顺序无关哈希（FNV-1a 风格）：每顶点 hash XOR 自身 + 自身左移 7。
                    for (int i = 0; i < Vertices.Count; i++)
                    {
                        int vh = Vertices[i].GetHashCode();
                        h = (h * 397) ^ vh;
                    }
                }
                return h;
            }
        }

        public static bool operator ==(ConstellationPolygon a, ConstellationPolygon b) => a.Equals(b);

        public static bool operator !=(ConstellationPolygon a, ConstellationPolygon b) => !a.Equals(b);

        // ──────────── 几何查询 ────────────

        /// <summary>
        /// 整数射线法（horizontal ray casting）判断点是否在多边形内。
        /// 顶点 <paramref name="c"/> 位于边上返回 false（半开约定）。
        /// 顶点退化（&lt; 3）时永远返回 false。
        /// </summary>
        public bool Contains(GridCoord c)
        {
            if (Vertices == null || Vertices.Count < 3) return false;
            int n = Vertices.Count;
            bool inside = false;
            for (int i = 0, j = n - 1; i < n; j = i++)
            {
                var vi = Vertices[i].Coord;
                var vj = Vertices[j].Coord;
                // 半开射线：(vi.Y > c.Y) != (vj.Y > c.Y) 严格不等；
                // 边上点（Y 相等）会被忽略（"顶点位于边上" → false）。
                // 由于 Coordinate 是整数、无浮点，半开等价于 (c.Y - vi.Y) / (vj.Y - vi.Y) 不含端点。
                if (((vi.Y > c.Y) != (vj.Y > c.Y)) &&
                    (c.X < (long)(vj.X - vi.X) * (c.Y - vi.Y) / (vj.Y - vi.Y) + vi.X))
                {
                    inside = !inside;
                }
            }
            return inside;
        }

        // ──────────── 内部工具 ────────────

        private static bool VerticesEqual(
            IReadOnlyList<ConstellationVertex> a,
            IReadOnlyList<ConstellationVertex> b)
        {
            if (ReferenceEquals(a, b)) return true;
            if (a == null || b == null) return false;
            if (a.Count != b.Count) return false;
            for (int i = 0; i < a.Count; i++)
            {
                if (!a[i].Equals(b[i])) return false;
            }
            return true;
        }

        public override string ToString()
            => $"ConstellationPolygon(Id={Id}, Vertices={Vertices?.Count ?? 0})";
    }
}