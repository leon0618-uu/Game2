using System.Collections.Generic;
using Starfall.Core.Map.Coordinates;

namespace Starfall.Core.Map.Pathfinding
{
    /// <summary>
    /// doc2 MAP-05 §9.4 寻路结果（成功路径 / 失败描述）。
    ///
    /// <para/>
    /// <see cref="PathfindingService.FindPath"/> 始终返回非 null 实例；
    /// 失败时 <see cref="Success"/> = false 并填入 <see cref="FailureReason"/>，
    /// <see cref="Tiles"/> 为空列表，<see cref="TotalCost"/> = 0；
    /// 成功时 <see cref="Tiles"/> 含起点 + 终点（顺序一致）。
    ///
    /// <para/>
    /// <see cref="RiskTags"/>：路径被分段打上的"风险标签"，用于 Presenter 高亮或
    /// 单位 AI 评估。 当前支持：
    /// <list type="bullet">
    /// <item><c>CrossPhase</c>：路径穿过了 Reality ↔ Astral（PhasePair 跨层）。</item>
    /// <item><c>Hazard</c>：路径至少一段经过了危险地块（hazardousDamagePerTurn &gt; 0）。</item>
    /// <item><c>OverHeight</c>：路径涉及高度差 &gt; 1 的跨越（标准步兵）。</item>
    /// </list>
    ///
    /// **顺序**：<see cref="RiskTags"/> 按字典序升序，便于哈希稳定。
    /// </summary>
    public sealed class MapPath
    {
        /// <summary>路径节点序列（包含起点 + 终点；顺序从起点到终点）。空 = 失败。</summary>
        public IReadOnlyList<GridCoord> Tiles { get; }

        /// <summary>路径总成本（sum of TileDefinition.BaseMoveCost for each edge）。失败 = 0。</summary>
        public int TotalCost { get; }

        /// <summary>true = <see cref="Tiles"/> 合法且非空；false = 见 <see cref="FailureReason"/>。</summary>
        public bool Success { get; }

        /// <summary>失败原因（成功时为空串）；由 <see cref="FailureReason"/> 提供常量值。</summary>
        public string FailureReason { get; }

        /// <summary>风险标签（字典序升序；可能为空集合）。</summary>
        public IReadOnlyList<string> RiskTags { get; }

        private MapPath(
            IReadOnlyList<GridCoord> tiles,
            int totalCost,
            bool success,
            string failureReason,
            IReadOnlyList<string> riskTags)
        {
            Tiles = tiles;
            TotalCost = totalCost;
            Success = success;
            FailureReason = failureReason ?? string.Empty;
            RiskTags = riskTags;
        }

        // ──────────── 失败原因常量（doc2 §9.4 + AGENTS.md §11）────────────

        public static class PathFailure
        {
            /// <summary>完全不可达（A* 探索完所有可达邻居仍未找到 goal）。</summary>
            public const string NoPath = "NoPath";

            /// <summary>终点本身被阻挡 / 越界 / 占用，无法作为路径终点。</summary>
            public const string GoalBlocked = "GoalBlocked";

            /// <summary>起点本身被阻挡 / 越界 / 占用，无法出发。</summary>
            public const string StartOccupied = "StartOccupied";

            /// <summary>起点 == 终点但 tile 上无法落座（被占用、坍塌等）。</summary>
            public const string Unreachable = "Unreachable";
        }

        // ──────────── 工厂 ────────────

        /// <summary>构造一个代表失败结果的 <see cref="MapPath"/>。</summary>
        public static MapPath Null(string reason)
        {
            return new MapPath(
                tiles: System.Array.Empty<GridCoord>(),
                totalCost: 0,
                success: false,
                failureReason: reason ?? PathFailure.NoPath,
                riskTags: System.Array.Empty<string>());
        }

        /// <summary>构造一个代表成功结果的 <see cref="MapPath"/>。</summary>
        /// <param name="tiles">路径节点序列（≥ 1；含起点 + 终点）。</param>
        /// <param name="totalCost">总移动成本。</param>
        /// <param name="riskTags">可选风险标签集合；传 null 视为空集合，并按字典序排序。</param>
        public static MapPath From(IReadOnlyList<GridCoord> tiles, int totalCost, IReadOnlyList<string> riskTags = null)
        {
            if (tiles == null) throw new System.ArgumentNullException(nameof(tiles));
            if (tiles.Count == 0)
                throw new System.ArgumentException("Successful MapPath must contain at least one tile (start == goal).", nameof(tiles));

            // 复制 + 排序风险标签。
            string[] sortedTags;
            if (riskTags == null || riskTags.Count == 0)
            {
                sortedTags = System.Array.Empty<string>();
            }
            else
            {
                var list = new List<string>(riskTags);
                list.Sort(System.StringComparer.Ordinal);
                sortedTags = list.ToArray();
            }

            return new MapPath(
                tiles: tiles,
                totalCost: totalCost,
                success: true,
                failureReason: string.Empty,
                riskTags: sortedTags);
        }

        public override string ToString()
            => Success
                ? $"MapPath(success=True, tiles={Tiles.Count}, cost={TotalCost}, tags=[{string.Join(",", RiskTags)}])"
                : $"MapPath(success=False, reason={FailureReason})";
    }
}
