using System;
using System.Collections.Generic;

namespace Starfall.Core.Map.Coordinates
{
    /// <summary>
    /// 三维逻辑网格坐标 (X, Y, Layer)。doc2 MAP-01 §4.1。
    ///
    /// 与 MVP 的 <see cref="Starfall.Core.Model.GridPos"/>（仅 X/Y）共存：
    ///   - GridPos 继续用于现有战斗 / Replay / Undo 系统，保持向后兼容；
    ///   - GridCoord 是 doc2 地图系统的标准坐标，新增地图相关代码统一使用此类型。
    /// 同 (X, Y) 不同 Layer 视为不同地块，禁止跨层操作（除非显式 Flip）。
    ///
    /// 确定性排序：Y → X → Layer（与 GridPos 一致，再追加 Layer 作为第三键）。
    /// 这是 AGENTS.md §11 强制要求的迭代顺序，所有依赖 GridCoord 顺序的逻辑
    /// （锚点 / 律令 / Hash / 序列化）都必须遵守。
    /// </summary>
    public readonly struct GridCoord : IEquatable<GridCoord>, IComparable<GridCoord>
    {
        public readonly int X;
        public readonly int Y;
        public readonly DimensionLayer Layer;

        public GridCoord(int x, int y, DimensionLayer layer)
        {
            X = x;
            Y = y;
            Layer = layer;
        }

        public GridCoord(int x, int y) : this(x, y, DimensionLayer.Reality)
        {
        }

        // ──────────── 等值 / 哈希 ────────────

        public bool Equals(GridCoord other) => X == other.X && Y == other.Y && Layer == other.Layer;

        public override bool Equals(object obj) => obj is GridCoord other && Equals(other);

        public override int GetHashCode()
        {
            // 三维 Fold 哈希，避开 object/string.GetHashCode 不稳定实现。
            unchecked
            {
                int h = (Y * 397) ^ X;
                h = (h * 397) ^ (int)Layer;
                return h;
            }
        }

        public static bool operator ==(GridCoord a, GridCoord b) => a.Equals(b);

        public static bool operator !=(GridCoord a, GridCoord b) => !a.Equals(b);

        // ──────────── 排序 ────────────

        /// <summary>
        /// 排序键：先 Y，后 X，最后 Layer。
        /// 与 <see cref="Starfall.Core.Model.GridPos"/> 一致 + 追加 Layer 第三键，
        /// 便于跨程序集统一调用 List&lt;GridCoord&gt;.Sort()。
        /// </summary>
        public int CompareTo(GridCoord other)
        {
            int c = Y.CompareTo(other.Y);
            if (c != 0) return c;
            c = X.CompareTo(other.X);
            if (c != 0) return c;
            return ((byte)Layer).CompareTo((byte)other.Layer);
        }

        // ──────────── 字符串 ────────────

        public override string ToString() => $"({X}, {Y}, {Layer})";

        // ──────────── 4 邻居（doc2 §4.5 + AGENTS.md §11）────────────

        /// <summary>
        /// 4 邻居，按固定顺序 North → East → South → West 输出。
        /// 不做越界过滤——越界判断由调用方结合 <see cref="IsInBounds"/> 完成。
        /// </summary>
        public IEnumerable<GridCoord> Neighbours()
        {
            // 与 GridDirection 枚举顺序严格一致：North, East, South, West。
            yield return new GridCoord(X, Y + 1, Layer);
            yield return new GridCoord(X + 1, Y, Layer);
            yield return new GridCoord(X, Y - 1, Layer);
            yield return new GridCoord(X - 1, Y, Layer);
        }

        /// <summary>按指定方向位移一格（不越界检查）。</summary>
        public GridCoord Neighbour(GridDirection dir)
        {
            switch (dir)
            {
                case GridDirection.North: return new GridCoord(X, Y + 1, Layer);
                case GridDirection.East:  return new GridCoord(X + 1, Y, Layer);
                case GridDirection.South: return new GridCoord(X, Y - 1, Layer);
                case GridDirection.West:  return new GridCoord(X - 1, Y, Layer);
                default:
                    throw new ArgumentOutOfRangeException(nameof(dir), dir, "Unknown GridDirection.");
            }
        }

        // ──────────── 距离 ────────────

        /// <summary>曼哈顿距离，跨层距离按 |ΔX| + |ΔY| 计算（Layer 不参与距离）。</summary>
        public int ManhattanDistance(GridCoord other)
        {
            int dx = Math.Abs(X - other.X);
            int dy = Math.Abs(Y - other.Y);
            return dx + dy;
        }

        // ──────────── 越界检查 ────────────

        /// <summary>判断坐标是否落在指定 MapSize 内（仅 X/Y，Layer 不参与边界）。</summary>
        public bool IsInBounds(MapSize size)
        {
            if (X < 0 || X >= size.Width) return false;
            if (Y < 0 || Y >= size.Height) return false;
            return true;
        }
    }
}