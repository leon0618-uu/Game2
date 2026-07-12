using System.Collections.Generic;
using Starfall.Core.Model;

namespace Starfall.Unity.Presentation
{
    /// <summary>给 Presenter 用的棋盘快照（按 (Y,X) 升序迭代）。</summary>
    public readonly struct BoardSnapshot
    {
        public int Width { get; }
        public int Height { get; }
        public IReadOnlyList<TileSnapshot> Tiles { get; }

        public BoardSnapshot(int width, int height, IReadOnlyList<TileSnapshot> tiles)
        {
            Width = width;
            Height = height;
            Tiles = tiles;
        }

        public static BoardSnapshot FromState(BattleState state)
        {
            var tiles = new List<TileSnapshot>();
            foreach (var kv in state.Board.TilesInDeterministicOrder())
                tiles.Add(new TileSnapshot(kv.Key, kv.Value));
            return new BoardSnapshot(state.Board.Width, state.Board.Height, tiles);
        }
    }
}