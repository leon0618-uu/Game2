using System.Collections.Generic;
using Starfall.Core.Map.Coordinates;

namespace Starfall.Core.Map.Anchor
{
    /// <summary>
    /// doc2 MAP-12 <see cref="ConstellationPolygon"/> 构造期验证器。
    /// <para/>
    /// **错误枚举 <see cref="ConstellationValidationError"/>**：
    /// <list type="bullet">
    /// <item><c>None</c> = 合法；</item>
    /// <item><c>TooFewVertices</c> = 顶点数 &lt; 3；</item>
    /// <item><c>Collinear</c> = 所有顶点共线（退化多边形）；</item>
    /// <item><c>SelfIntersecting</c> = 多边形边自相交（"蝴蝶"或"8 字"形）。</item>
    /// </list>
    /// <para/>
    /// **算法约定**（AGENTS.md §11 确定性）：
    /// <list type="number">
    /// <item>全部以 <c>long</c> 整数叉积（避免浮点，doc2 §14.4）；</item>
    /// <item><see cref="NormalizeVertices"/>：先按 <see cref="ConstellationVertex.CompareTo"/>
    ///       （Y→X→Layer）排序，但保持循环顺序不变；</item>
    /// <item>邻居顺序 N→E→S→W 由 <see cref="GridCoord.Neighbours"/> 保证 ——
    ///       validator 内部依赖 <see cref="ConstellationVertex"/> 的固定排序以消除抖动。</item>
    /// </list>
    /// <para/>
    /// **不在范围内**：
    /// <list type="bullet">
    /// <item>不自检顶点是否越界（由调用方 / <see cref="GridCoord.IsInBounds"/> 负责）。</item>
    /// <item>不自检 Layer 一致性（混合 Layer 视为合法，doc2 §4.5 允许跨层围区）。</item>
    /// </list>
    /// </summary>
    public static class ConstellationValidator
    {
        // ──────────── 错误枚举 ────────────

        public enum ConstellationValidationError
        {
            None = 0,
            TooFewVertices = 1,
            Collinear = 2,
            SelfIntersecting = 3,
        }

        // ──────────── 公开入口 ────────────

        /// <summary>顶点数 &lt; 3 → true。</summary>
        public static bool IsDegenerate(IReadOnlyList<ConstellationVertex> vertices)
        {
            if (vertices == null) return true;
            return vertices.Count < 3 || HasCollinearAllVertices(vertices);
        }

        /// <summary>多边形任意两条非相邻边相交 → true。</summary>
        public static bool IsSelfIntersecting(IReadOnlyList<ConstellationVertex> vertices)
        {
            if (vertices == null || vertices.Count < 4) return false;
            int n = vertices.Count;
            // 多边形 P = V[0..n-1]，边集 = {(i, i+1 mod n)}。
            // 检测非相邻边 (i, i+1) vs (j, j+1) 是否相交（i+1 != j, j+1 != i）。
            for (int i = 0; i < n; i++)
            {
                int iNext = (i + 1) % n;
                for (int j = i + 2; j < n; j++)
                {
                    int jNext = (j + 1) % n;
                    // 跳过相邻边（含最后一条边与第一条相邻）
                    if (i == 0 && jNext == 0) continue;
                    if (SegmentsIntersectStrict(vertices[i].Coord, vertices[iNext].Coord,
                                                  vertices[j].Coord, vertices[jNext].Coord))
                    {
                        return true;
                    }
                }
            }
            return false;
        }

        /// <summary>
        /// **保持循环顺序**的规范化：以 Y→X→Layer 最小的顶点为起点旋转顶点列表。
        /// 这样既保证规范化（输入任何旋转 / 起始点得到相同输出），
        /// 又保留多边形的拓扑（不自相交、不破坏 Contains）。
        /// </summary>
        public static List<ConstellationVertex> NormalizeVertices(IReadOnlyList<ConstellationVertex> vertices)
        {
            if (vertices == null) return new List<ConstellationVertex>();
            if (vertices.Count == 0) return new List<ConstellationVertex>();

            // 1) 找 Y→X→Layer 最小的顶点索引
            int minIdx = 0;
            for (int i = 1; i < vertices.Count; i++)
            {
                if (vertices[i].CompareTo(vertices[minIdx]) < 0)
                {
                    minIdx = i;
                }
            }

            // 2) 以 minIdx 为起点旋转（保持循环顺序）
            var rotated = new List<ConstellationVertex>(vertices.Count);
            for (int i = 0; i < vertices.Count; i++)
            {
                rotated.Add(vertices[(minIdx + i) % vertices.Count]);
            }
            return rotated;
        }

        /// <summary>完整校验；返回首个错误。</summary>
        public static ConstellationValidationError Validate(IReadOnlyList<ConstellationVertex> vertices)
        {
            if (vertices == null || vertices.Count < 3) return ConstellationValidationError.TooFewVertices;
            if (HasCollinearAllVertices(vertices)) return ConstellationValidationError.Collinear;
            if (IsSelfIntersecting(vertices)) return ConstellationValidationError.SelfIntersecting;
            return ConstellationValidationError.None;
        }

        // ──────────── 内部工具 ────────────

        /// <summary>判断所有顶点是否共线（collinear / degenerate）。</summary>
        private static bool HasCollinearAllVertices(IReadOnlyList<ConstellationVertex> vertices)
        {
            int n = vertices.Count;
            if (n < 3) return true;
            // 任取前两个不重复点为基线；其余所有点必须落在该直线上。
            // 找第一个不等于 V[0] 的点作 V[1]。
            GridCoord base0 = vertices[0].Coord;
            GridCoord base1 = base0;
            for (int i = 1; i < n; i++)
            {
                if (!vertices[i].Coord.Equals(base0))
                {
                    base1 = vertices[i].Coord;
                    break;
                }
            }
            if (base1.Equals(base0)) return true; // 所有顶点重合

            for (int i = 0; i < n; i++)
            {
                if (!IsOnLine(vertices[i].Coord, base0, base1)) return false;
            }
            return true;
        }

        /// <summary>点 C 是否在直线 (A, B) 上（允许重合端点 / 在延长线上）。</summary>
        private static bool IsOnLine(GridCoord c, GridCoord a, GridCoord b)
        {
            // 叉积 (B - A) × (C - A) == 0
            long dx1 = b.X - a.X;
            long dy1 = b.Y - a.Y;
            long dx2 = c.X - a.X;
            long dy2 = c.Y - a.Y;
            return (dx1 * dy2 - dy1 * dx2) == 0L;
        }

        /// <summary>
        /// 严格线段相交（端点重合不算相交；共享端点不算相交）。
        /// 用跨立实验 + 整数叉积（AGENTS.md §11 + doc2 §14.4 整数射线）。
        /// </summary>
        private static bool SegmentsIntersectStrict(
            GridCoord p1, GridCoord p2, GridCoord p3, GridCoord p4)
        {
            long d1 = Cross(p3, p4, p1);
            long d2 = Cross(p3, p4, p2);
            long d3 = Cross(p1, p2, p3);
            long d4 = Cross(p1, p2, p4);

            // 跨立条件：(d1 与 d2 异号) 且 (d3 与 d4 异号)
            if (((d1 > 0 && d2 < 0) || (d1 < 0 && d2 > 0)) &&
                ((d3 > 0 && d4 < 0) || (d3 < 0 && d4 > 0)))
            {
                return true;
            }
            return false;
        }

        /// <summary>(B - A) × (C - A)。</summary>
        private static long Cross(GridCoord a, GridCoord b, GridCoord c)
        {
            long bx = b.X - a.X;
            long by = b.Y - a.Y;
            long cx = c.X - a.X;
            long cy = c.Y - a.Y;
            return bx * cy - by * cx;
        }
    }
}