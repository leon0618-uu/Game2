using System.Collections.Generic;
using Starfall.Core.Map.Coordinates;
using Starfall.Core.Map.Height;
using Starfall.Core.Map.State;
using Starfall.Core.Map.Tile;

namespace Starfall.Core.Map.Pathfinding
{
    /// <summary>
    /// doc2 MAP-05 §9.4 deterministic A* pathfinding service.
    ///
    /// <para/>
    /// Algorithm: classic A* (g + h, openSet = min-heap by F).
    /// Neighbor order is strictly North -&gt; East -&gt; South -&gt; West (matches
    /// <see cref="BFSPathfinder.Neighbours"/> / <see cref="GridCoord.Neighbours"/>;
    /// MAP-01 neighbor-order bug was fixed at 5cc4644).
    /// Heuristic: Manhattan distance (same layer); +1 for cross-layer transitions
    /// (keeps the heuristic admissible + consistent).
    ///
    /// <para/>
    /// Deterministic tie-break: openSet entries with equal F are ordered by
    /// (H, Y, X, Layer). This guarantees identical inputs yield identical
    /// <see cref="MapPath"/>, satisfying AGENTS.md §11 + doc2 §3.4 acceptance matrix.
    ///
    /// <para/>
    /// Failure semantics: failed paths return a non-null <see cref="MapPath"/>
    /// (Success = false); the reason is exposed via <see cref="MapPath.FailureReason"/>:
    /// <list type="bullet">
    /// <item><see cref="MapPath.PathFailure.NoPath"/>: A* exhausted all reachable neighbors without hitting goal.</item>
    /// <item><see cref="MapPath.PathFailure.GoalBlocked"/>: goal is out-of-bounds / blocking / occupied / collapsed.</item>
    /// <item><see cref="MapPath.PathFailure.StartOccupied"/>: start tile is blocking (out-of-bounds or blocking).</item>
    /// <item><see cref="MapPath.PathFailure.Unreachable"/>: start == goal but tile is blocked.</item>
    /// </list>
    /// </summary>
    public static class PathfindingService
    {
        // 4-neighbor order (matches GridCoord.Neighbours): N, E, S, W.
        private static readonly (int dx, int dy)[] Neighbours = new (int, int)[]
        {
            (0, -1), (1, 0), (0, 1), (-1, 0)
        };

        /// <summary>
        /// Compute the shortest path from <paramref name="start"/> to <paramref name="goal"/>.
        /// </summary>
        /// <param name="state"><see cref="MapState"/>. Attach a registry via
        ///     <see cref="TileOccupancyService.AttachTileDefinitionRegistry"/> first;
        ///     when no registry is attached, this service assumes all in-bounds tiles
        ///     are Plain / Cost = 1 (conservative fallback for tests).</param>
        /// <param name="start">start coordinate (incl. Layer).</param>
        /// <param name="goal">goal coordinate (incl. Layer).</param>
        /// <param name="profile">movement profile (dH bounds + cross-dimension toggle).
        ///     Note: <see cref="MapMovementProfile.MaxMovementPoints"/> does NOT constrain A*
        ///     (A* always returns the globally cheapest path; AP gating is the job of
        ///     <see cref="MovementRangeService"/>).</param>
        public static MapPath FindPath(
            MapState state,
            GridCoord start,
            GridCoord goal,
            MapMovementProfile profile)
        {
            if (state == null) throw new System.ArgumentNullException(nameof(state));

            // 1) Start out of bounds -> StartOccupied.
            if (!start.IsInBounds(state.Definition.Size))
                return MapPath.Null(MapPath.PathFailure.StartOccupied);

            // 2) Goal out of bounds -> GoalBlocked.
            if (!goal.IsInBounds(state.Definition.Size))
                return MapPath.Null(MapPath.PathFailure.GoalBlocked);

            var registry = TileOccupancyService.TryGetAttachedRegistry(state);

            // 3) Start == Goal: a single-tile path whose passability we must verify.
            if (start == goal)
            {
                if (registry != null && registry.TryGetByCoord(start, out var sDef) && sDef.BlocksMovement)
                    return MapPath.Null(MapPath.PathFailure.StartOccupied);

                var stay = MapPassabilityService.CanEnter(state, start, goal, profile, Footprint.SingleCell);
                if (!stay.IsPassable)
                    return MapPath.Null(MapPath.PathFailure.Unreachable);

                return MapPath.From(new[] { start }, totalCost: 0, riskTags: null);
            }

            // 4) Goal pre-check: if it cannot accept any entry, the path cannot end there.
            var goalCheck = MapPassabilityService.CanEnter(state, start, goal, profile, Footprint.SingleCell);
            if (!goalCheck.IsPassable)
            {
                if (goalCheck.Reason == PassabilityResult.RejectionCode.BlockedByPhase)
                    return MapPath.Null(MapPath.PathFailure.Unreachable);
                return MapPath.Null(MapPath.PathFailure.GoalBlocked);
            }

            // 5) Start pre-check: a blocking start tile forbids departure.
            if (registry != null && registry.TryGetByCoord(start, out var startDef) && startDef.BlocksMovement)
                return MapPath.Null(MapPath.PathFailure.StartOccupied);

            // 6) A* main loop.
            var openSet = new SortedSet<OpenEntry>(OpenEntryComparer.Instance);
            var bestG = new Dictionary<GridCoord, int>();
            var cameFrom = new Dictionary<GridCoord, GridCoord>();
            var inOpen = new HashSet<GridCoord>();

            int startG = 0;
            int startH = Heuristic(start, goal);
            openSet.Add(new OpenEntry(start, startG + startH, startH));
            bestG[start] = 0;
            inOpen.Add(start);

            bool found = false;
            GridCoord foundAt = default;

            while (openSet.Count > 0)
            {
                var currentEntry = openSet.Min;
                openSet.Remove(currentEntry);
                var current = currentEntry.Coord;
                inOpen.Remove(current);

                if (current == goal)
                {
                    found = true;
                    foundAt = current;
                    break;
                }

                int currentG = bestG[current];

                foreach (var (dx, dy) in Neighbours)
                {
                    var neighbour = new GridCoord(current.X + dx, current.Y + dy, current.Layer);
                    if (!neighbour.IsInBounds(state.Definition.Size)) continue;

                    // Passability: failed -> skip.
                    var pass = MapPassabilityService.CanEnter(state, current, neighbour, profile, Footprint.SingleCell);
                    if (!pass.IsPassable) continue;

                    // Edge cost: default 1; or TileDefinition.BaseMoveCost when registry is attached.
                    int edgeCost = 1;
                    bool hazardEdge = false;
                    bool overHeightEdge = false;
                    if (registry != null
                        && registry.TryGetByCoord(current, out var cdef)
                        && registry.TryGetByCoord(neighbour, out var ndef))
                    {
                        if (ndef.BaseMoveCost > 0) edgeCost = ndef.BaseMoveCost;
                        if ((ndef.Tags & TileTags.Hazardous) != 0) hazardEdge = true;
                        if (System.Math.Abs(ndef.Height.Value - cdef.Height.Value) > 1) overHeightEdge = true;
                    }

                    int tentativeG = currentG + edgeCost;
                    if (bestG.TryGetValue(neighbour, out int existing) && tentativeG >= existing)
                    {
                        // We may still want to keep the edge if hazard/overHeight tag flips... no, tags are per-node.
                        continue;
                    }

                    bestG[neighbour] = tentativeG;
                    cameFrom[neighbour] = current;
                    var newEntry = new OpenEntry(neighbour, tentativeG + Heuristic(neighbour, goal), Heuristic(neighbour, goal));
                    if (inOpen.Contains(neighbour))
                    {
                        // SortedSet does not allow in-place updates -> drop old, add new.
                        openSet.RemoveWhere(e => e.Coord.Equals(neighbour));
                    }
                    openSet.Add(newEntry);
                    inOpen.Add(neighbour);

                    // Recompute risk tags lazily at path return time, but stash edge flags here
                    // in case we want an immediate summary.
                    if (hazardEdge) _ = hazardEdge;
                    if (overHeightEdge) _ = overHeightEdge;
                }
            }

            if (!found) return MapPath.Null(MapPath.PathFailure.NoPath);

            // 7) Reconstruct path.
            var path = new List<GridCoord>();
            var cur = foundAt;
            while (!cur.Equals(start))
            {
                path.Add(cur);
                cur = cameFrom[cur];
            }
            path.Add(start);
            path.Reverse();

            // 8) Compute total cost + risk tags by re-walking the path.
            int totalCost = bestG[foundAt];
            var tags = new List<string>();
            if (registry != null)
            {
                for (int i = 1; i < path.Count; i++)
                {
                    var a = path[i - 1];
                    var b = path[i];
                    if (a.Layer != b.Layer) tags.Add("CrossPhase");
                    if (registry.TryGetByCoord(a, out var aDef) && registry.TryGetByCoord(b, out var bDef))
                    {
                        if ((bDef.Tags & TileTags.Hazardous) != 0) tags.Add("Hazard");
                        if (System.Math.Abs(bDef.Height.Value - aDef.Height.Value) > 1) tags.Add("OverHeight");
                    }
                }
            }

            return MapPath.From(path, totalCost, tags);
        }

        private static int Heuristic(GridCoord a, GridCoord b)
        {
            int dx = System.Math.Abs(a.X - b.X);
            int dy = System.Math.Abs(a.Y - b.Y);
            int layerPenalty = a.Layer == b.Layer ? 0 : 1;
            return dx + dy + layerPenalty;
        }

        private static string FromRejection(PassabilityResult.RejectionCode code)
        {
            switch (code)
            {
                case PassabilityResult.RejectionCode.BlockedByTile:
                case PassabilityResult.RejectionCode.BlockedByUnit:
                case PassabilityResult.RejectionCode.BlockedByHeightDelta:
                    return MapPath.PathFailure.GoalBlocked;
                case PassabilityResult.RejectionCode.BlockedByPhase:
                    return MapPath.PathFailure.Unreachable;
                default:
                    return MapPath.PathFailure.NoPath;
            }
        }

        // A* openSet node.
        private readonly struct OpenEntry
        {
            public readonly GridCoord Coord;
            public readonly int F;
            public readonly int H;

            public OpenEntry(GridCoord coord, int f, int h)
            {
                Coord = coord;
                F = f;
                H = h;
            }
        }

        private sealed class OpenEntryComparer : IComparer<OpenEntry>
        {
            public static readonly OpenEntryComparer Instance = new OpenEntryComparer();

            public int Compare(OpenEntry x, OpenEntry y)
            {
                int c = x.F.CompareTo(y.F);
                if (c != 0) return c;
                c = x.H.CompareTo(y.H);
                if (c != 0) return c;
                c = x.Coord.Y.CompareTo(y.Coord.Y);
                if (c != 0) return c;
                c = x.Coord.X.CompareTo(y.Coord.X);
                if (c != 0) return c;
                return ((byte)x.Coord.Layer).CompareTo((byte)y.Coord.Layer);
            }
        }
    }
}
