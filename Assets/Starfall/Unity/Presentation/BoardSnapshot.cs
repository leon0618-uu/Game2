using System.Collections.Generic;
using Starfall.Core.Model;

namespace Starfall.Unity.Presentation
{
    /// <summary>给 Presenter 用的棋盘快照（按 (Y,X) 升序迭代）。</summary>
    /// <remarks>
    /// Task 16 扩展：加入 Units + Anchors（Task 16 之前仅含 Tiles）。
    /// Presenter 从单一 BoardSnapshot 即可获取棋盘 / 单位 / 锚点三视图，
    /// 避免拆分接口引发的「单帧多 Render」竞态。
    /// AGENTS.md §11 确定性：Units 按 UnitId 升序、Anchors 按 ZoneId 升序。
    /// </remarks>
    public readonly struct BoardSnapshot
    {
        public int Width { get; }
        public int Height { get; }
        public IReadOnlyList<TileSnapshot> Tiles { get; }
        public IReadOnlyList<UnitSnapshot> Units { get; }
        public IReadOnlyList<AnchorSnapshot> Anchors { get; }

        public BoardSnapshot(int width, int height, IReadOnlyList<TileSnapshot> tiles)
            : this(width, height, tiles, System.Array.Empty<UnitSnapshot>(), System.Array.Empty<AnchorSnapshot>())
        {
        }

        public BoardSnapshot(
            int width,
            int height,
            IReadOnlyList<TileSnapshot> tiles,
            IReadOnlyList<UnitSnapshot> units,
            IReadOnlyList<AnchorSnapshot> anchors)
        {
            Width = width;
            Height = height;
            Tiles = tiles ?? System.Array.Empty<TileSnapshot>();
            Units = units ?? System.Array.Empty<UnitSnapshot>();
            Anchors = anchors ?? System.Array.Empty<AnchorSnapshot>();
        }

        public static BoardSnapshot FromState(BattleState state)
        {
            // Tiles: 按 (Y, X) 升序（Board.TilesInDeterministicOrder）
            var tiles = new List<TileSnapshot>();
            foreach (var kv in state.Board.TilesInDeterministicOrder())
                tiles.Add(new TileSnapshot(kv.Key, kv.Value));

            // Units: 按 UnitId 升序（BattleState.PostStateHash §5 一致）
            var sortedUnits = new List<UnitState>(state.Units);
            sortedUnits.Sort((a, b) => a.UnitId.CompareTo(b.UnitId));
            var units = new List<UnitSnapshot>(sortedUnits.Count);
            foreach (var u in sortedUnits)
                units.Add(new UnitSnapshot(u.UnitId, u.Pos, u.Hp, u.Phase, u.Owner));

            // Anchors: 按 ZoneId 升序（AnchorRegistry.ZonesInOrder）
            var anchors = new List<AnchorSnapshot>();
            foreach (var z in state.Anchors.ZonesInOrder)
                anchors.Add(new AnchorSnapshot(z.ZoneId, z.Owner, z.Vertices));

            return new BoardSnapshot(state.Board.Width, state.Board.Height, tiles, units, anchors);
        }
    }
}