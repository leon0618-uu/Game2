using System.Collections.Generic;
using Starfall.Core.Model;

namespace Starfall.Core.Pathfinding
{
    /// <summary>
    /// BFS 4 邻居寻路（AGENTS.md §11 确定性：North → East → South → West）。
    /// 距离相等时按 (Y, X) 升序 tie-break（GridPosComparer）。
    /// 阻塞格：TileState.Blocked 或越界。
    /// 坐标系：X 向右为正（East），Y 向下为正（South）。
    /// </summary>
    public sealed class BFSPathfinder : IPathfinder
    {
        // 邻居顺序固定：North (0,-1) → East (1,0) → South (0,1) → West (-1,0)
        // 与 doc2 §4.5 / MAP-05 §9.4 平局规则一致；AGENTS.md §11 已同步。
        private static readonly (int dx, int dy)[] Neighbors = new (int, int)[]
        {
            (0, -1), (1, 0), (0, 1), (-1, 0)
        };

        public IReadOnlyList<GridPos> FindPath(BoardState board, GridPos from, GridPos to)
        {
            if (board == null) return null;
            if (!IsWalkable(board, from)) return null;
            if (!IsWalkable(board, to)) return null;
            if (from == to) return new[] { from };

            var cameFrom = new Dictionary<GridPos, GridPos>();
            var queue = new Queue<GridPos>();
            queue.Enqueue(from);
            cameFrom[from] = from;

            bool found = false;
            while (queue.Count > 0)
            {
                var cur = queue.Dequeue();
                if (cur == to) { found = true; break; }

                foreach (var (dx, dy) in Neighbors)
                {
                    var next = new GridPos(cur.X + dx, cur.Y + dy);
                    if (!IsWalkable(board, next)) continue;
                    if (cameFrom.ContainsKey(next)) continue;
                    cameFrom[next] = cur;
                    queue.Enqueue(next);
                }
            }

            if (!found) return null;

            // 回溯路径
            var path = new List<GridPos>();
            var p = to;
            while (p != from)
            {
                path.Add(p);
                p = cameFrom[p];
            }
            path.Add(from);
            path.Reverse();
            return path;
        }

        private static bool IsWalkable(BoardState board, GridPos p)
        {
            if (p.X < 0 || p.Y < 0 || p.X >= board.Width || p.Y >= board.Height) return false;
            if (board.Tiles.TryGetValue(p, out var t) && t == TileState.Blocked) return false;
            return true;
        }
    }
}