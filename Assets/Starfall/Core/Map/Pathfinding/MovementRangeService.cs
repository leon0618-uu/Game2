using System.Collections.Generic;
using Starfall.Core.Map.Coordinates;
using Starfall.Core.Map.Height;
using Starfall.Core.Map.State;
using Starfall.Core.Map.Tile;

namespace Starfall.Core.Map.Pathfinding
{
    /// <summary>
    /// doc2 MAP-05 §9.4 移动范围服务（每回合 AP 范围内的可达 tile）。
    ///
    /// <para/>
    /// **算法**：BFS-based 多源扩展；以 <see cref="GridCoord"/> 上累计
    /// <see cref="TileDefinition.BaseMoveCost"/> 作为到达成本，受
    /// <see cref="MapMovementProfile.MaxMovementPoints"/> 约束。
    ///
    /// <para/>
    /// **确定性**：open frontier 按 (Cost asc, Y, X, Layer) 升序出列；
    /// **输出排序**：<see cref="GetReachableTiles"/> 返回的列表按
    /// <see cref="GridCoord.CompareTo"/>（Y → X → Layer）升序排列，
    /// 满足 AGENTS.md §11。
    ///
    /// <para/>
    /// **包含 origin**：返回的列表中第一个元素是 origin 自身（cost = 0）。
    /// </summary>
    public static class MovementRangeService
    {
        private static readonly (int dx, int dy)[] Neighbours = new (int, int)[]
        {
            (0, -1), (1, 0), (0, 1), (-1, 0)
        };

        /// <summary>
        /// 计算 <paramref name="origin"/> 在 <see cref="MapMovementProfile.MaxMovementPoints"/>
        /// 范围内的所有可达 tile 列表（按 <see cref="GridCoord.CompareTo"/> 升序；含自身）。
        /// </summary>
        public static IReadOnlyList<GridCoord> GetReachableTiles(
            MapState state,
            GridCoord origin,
            MapMovementProfile profile)
        {
            if (state == null) throw new System.ArgumentNullException(nameof(state));

            if (!origin.IsInBounds(state.Definition.Size))
                throw new System.ArgumentOutOfRangeException(nameof(origin), origin,
                    $"Origin {origin} is out of bounds for map {state.Definition.Size}.");

            // 不能飞行 / 不能跨层 → 仅同层 BFS 扩
            if (profile.MaxMovementPoints == 0)
            {
                return new List<GridCoord> { origin };
            }

            var registry = TileOccupancyService.TryGetAttachedRegistry(state);

            // frontier list (sortedList by Cost asc → Y → X → Layer)
            var frontier = new List<FrontierEntry>();
            // best cost cache
            var bestCost = new Dictionary<GridCoord, int>();
            // visited set（确保不重复 push）
            var reachable = new HashSet<GridCoord>();

            frontier.Add(new FrontierEntry(origin, 0));
            bestCost[origin] = 0;
            reachable.Add(origin);

            // 起点 tile 阻挡 → 仅 origin 自身（fallback）
            if (registry != null && registry.TryGetByCoord(origin, out var originDef) && originDef.BlocksMovement)
            {
                return SortedByCoord(reachable, state, registry);
            }

            // frontier 排水（替代 priority queue 但同样确定性）
            while (frontier.Count > 0)
            {
                // 出列最小 cost 的 entry（线性最小查找）
                int minIdx = 0;
                for (int i = 1; i < frontier.Count; i++)
                {
                    if (frontier[i].Cost < frontier[minIdx].Cost) minIdx = i;
                    else if (frontier[i].Cost == frontier[minIdx].Cost)
                    {
                        // Tie-break by Y → X → Layer (GridCoord.CompareTo)
                        if (frontier[i].Coord.CompareTo(frontier[minIdx].Coord) < 0) minIdx = i;
                    }
                }
                var current = frontier[minIdx];
                frontier.RemoveAt(minIdx);

                if (current.Cost >= profile.MaxMovementPoints) continue;

                foreach (var (dx, dy) in Neighbours)
                {
                    var neighbour = new GridCoord(current.Coord.X + dx, current.Coord.Y + dy, current.Coord.Layer);
                    if (!neighbour.IsInBounds(state.Definition.Size)) continue;

                    var pass = MapPassabilityService.CanEnter(state, current.Coord, neighbour, profile, Footprint.SingleCell);
                    if (!pass.IsPassable) continue;

                    int edgeCost = 1;
                    if (registry != null && registry.TryGetByCoord(neighbour, out var ndef) && ndef.BaseMoveCost > 0)
                        edgeCost = ndef.BaseMoveCost;

                    int newCost = current.Cost + edgeCost;
                    if (newCost > profile.MaxMovementPoints) continue;
                    if (bestCost.TryGetValue(neighbour, out int prev) && newCost >= prev) continue;

                    bestCost[neighbour] = newCost;
                    reachable.Add(neighbour);
                    frontier.Add(new FrontierEntry(neighbour, newCost));
                }
            }

            return SortedByCoord(reachable, state, registry);
        }

        private static IReadOnlyList<GridCoord> SortedByCoord(
            HashSet<GridCoord> set,
            MapState state,
            TileDefinitionRegistry registry)
        {
            var list = new List<GridCoord>(set);
            list.Sort();
            return list;
        }

        private readonly struct FrontierEntry
        {
            public readonly GridCoord Coord;
            public readonly int Cost;

            public FrontierEntry(GridCoord coord, int cost)
            {
                Coord = coord;
                Cost = cost;
            }
        }
    }
}
