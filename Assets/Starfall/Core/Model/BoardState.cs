using System.Collections.Generic;
using System.Linq;

namespace Starfall.Core.Model
{
    public sealed class BoardState
    {
        public int Width { get; }
        public int Height { get; }
        private readonly Dictionary<GridPos, TileState> _tiles;

        public IReadOnlyDictionary<GridPos, TileState> Tiles => _tiles;

        public BoardState(int width, int height, IDictionary<GridPos, TileState> tiles)
        {
            if (width <= 0 || width > byte.MaxValue)
                throw new System.ArgumentException("Width must be 1..255", nameof(width));
            if (height <= 0 || height > byte.MaxValue)
                throw new System.ArgumentException("Height must be 1..255", nameof(height));
            Width = width;
            Height = height;
            _tiles = new Dictionary<GridPos, TileState>(tiles);
        }

        /// <summary>按 (Y, X) 升序迭代瓦片（AGENTS.md §11 确定性规则）。</summary>
        public IEnumerable<KeyValuePair<GridPos, TileState>> TilesInDeterministicOrder()
            => _tiles.OrderBy(kv => kv.Key, GridPosComparer.Instance);
    }
}
