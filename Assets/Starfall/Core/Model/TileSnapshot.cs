namespace Starfall.Core.Model
{
    /// <summary>给 Presenter 用的瓦片快照（ADR-0002 §Decision 1）。</summary>
    public readonly record struct TileSnapshot(GridPos Pos, TileState State);
}
