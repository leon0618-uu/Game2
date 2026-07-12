using System;

namespace Starfall.Core.Model
{
    /// <summary>
    /// 网格坐标。Y 是主排序键，X 是次排序键（AGENTS.md §11 确定性规则）。
    /// </summary>
    public readonly record struct GridPos(int X, int Y) : IComparable<GridPos>
    {
        public int CompareTo(GridPos other)
        {
            int c = Y.CompareTo(other.Y);
            if (c != 0) return c;
            return X.CompareTo(other.X);
        }
    }
}
