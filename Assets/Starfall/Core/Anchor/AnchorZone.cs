using System.Collections.Generic;
using Starfall.Core.Model;

namespace Starfall.Core.Anchor
{
    /// <summary>
    /// 锚点围区：用规范化顶点顺序的多边形（顺时针/逆时针统一）。
    /// 顶点顺序按 GridPos.CompareTo 升序排序（确定性）。
    /// </summary>
    public sealed class AnchorZone
    {
        public int ZoneId { get; }
        public string Owner { get; }  // "Player" / "Enemy" / "Neutral"
        public IReadOnlyList<GridPos> Vertices { get; }

        public AnchorZone(int zoneId, string owner, IEnumerable<GridPos> vertices)
        {
            if (vertices == null) throw new System.ArgumentNullException(nameof(vertices));
            var list = new List<GridPos>(vertices);
            if (list.Count < 3)
                throw new System.ArgumentException("Polygon must have >= 3 vertices", nameof(vertices));
            list.Sort();  // GridPos.CompareTo: 先 Y 后 X
            ZoneId = zoneId;
            Owner = owner;
            Vertices = list;
        }

        /// <summary>射线法（horizontal ray casting）判断点是否在多边形内。MVP 阶段凸多边形假设足够。</summary>
        public bool Contains(GridPos p)
        {
            int n = Vertices.Count;
            bool inside = false;
            for (int i = 0, j = n - 1; i < n; j = i++)
            {
                var vi = Vertices[i];
                var vj = Vertices[j];
                if (((vi.Y > p.Y) != (vj.Y > p.Y)) &&
                    (p.X < (vj.X - vi.X) * (p.Y - vi.Y) / (vj.Y - vi.Y) + vi.X))
                {
                    inside = !inside;
                }
            }
            return inside;
        }
    }
}
