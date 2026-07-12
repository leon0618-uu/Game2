using System.Collections.Generic;
using Starfall.Core.Model;

namespace Starfall.Core.Pathfinding
{
    public interface IPathfinder
    {
        /// <summary>
        /// 计算从 from 到 to 的最短路径（含 from 与 to 端点）。
        /// 不可达返回 null。
        /// </summary>
        IReadOnlyList<GridPos> FindPath(BoardState board, GridPos from, GridPos to);
    }
}