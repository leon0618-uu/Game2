using System;

namespace Starfall.Core.Model
{
    /// <summary>
    /// 网格坐标。Y 是主排序键，X 是次排序键（AGENTS.md §11 确定性规则）。
    /// 用 readonly struct + IEquatable + IComparable 手写实现，
    /// 不依赖 C# 10 的 record struct 语法（项目 langversion=9.0）。
    /// </summary>
    public readonly struct GridPos : IEquatable<GridPos>, IComparable<GridPos>
    {
        public int X { get; }
        public int Y { get; }

        public GridPos(int x, int y)
        {
            X = x;
            Y = y;
        }

        public int CompareTo(GridPos other)
        {
            int c = Y.CompareTo(other.Y);
            if (c != 0) return c;
            return X.CompareTo(other.X);
        }

        public bool Equals(GridPos other) => X == other.X && Y == other.Y;

        public override bool Equals(object obj) => obj is GridPos other && Equals(other);

        public override int GetHashCode() => unchecked((Y * 397) ^ X);

        public static bool operator ==(GridPos a, GridPos b) => a.Equals(b);

        public static bool operator !=(GridPos a, GridPos b) => !a.Equals(b);

        public override string ToString() => $"({X},{Y})";
    }
}