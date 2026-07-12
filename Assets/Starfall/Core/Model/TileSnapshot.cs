using System;

namespace Starfall.Core.Model
{
    /// <summary>
    /// 给 Presenter 用的瓦片快照（ADR-0002 §Decision 1）。
    /// 用 readonly struct + IEquatable 手写实现，不依赖 C# 10 record struct 语法。
    /// </summary>
    public readonly struct TileSnapshot : IEquatable<TileSnapshot>
    {
        public GridPos Pos { get; }
        public TileState State { get; }

        public TileSnapshot(GridPos pos, TileState state)
        {
            Pos = pos;
            State = state;
        }

        public bool Equals(TileSnapshot other) => Pos.Equals(other.Pos) && State == other.State;

        public override bool Equals(object obj) => obj is TileSnapshot other && Equals(other);

        public override int GetHashCode() => unchecked((Pos.GetHashCode() * 397) ^ (int)State);

        public static bool operator ==(TileSnapshot a, TileSnapshot b) => a.Equals(b);

        public static bool operator !=(TileSnapshot a, TileSnapshot b) => !a.Equals(b);

        public override string ToString() => $"TileSnapshot({Pos},{State})";
    }
}