using System;
using System.Collections.Generic;
using Starfall.Core.Map.Commands.Fall;
using Starfall.Core.Map.Coordinates;
using Starfall.Core.Map.State;
using Starfall.Core.Map.Tile;

namespace Starfall.Core.Map.Commands.Compression
{
    /// <summary>
    /// doc2 MAP-08 §6.1 相位挤压解析（与已有 <see cref="Starfall.Core.Rules.CrushResolver"/>
    /// 共存：CrushResolver 是"HP damage"语义，本服务是"弹回位移"语义 —— 两者互补，
    /// 不互相替代）。
    /// <para/>
    /// **触发条件**：同一 <see cref="GridCoord"/> 上 ≥2 个单位，且这些单位都在目标
    /// layer 上（即 ActiveDimension 全部一致）。
    /// <para/>
    /// **挤压规则**：
    /// <list type="number">
    /// <item>取 <paramref name="unitIdsAtCoord"/> 的最后一个 unitId 作为"被弹"目标
    ///       （具有最高 unitId 的 unit = 最后一个被放进 = 最容易被挤压出去，
    ///       与既有 <c>unitIdsAtCoord</c> 顺序约定一致）。</item>
    /// <item>优先弹到 8 邻居（曼哈顿距离 1）中第一个合法且未被占的 cell。</item>
    /// <item>8 邻居全不可用 → 弹到曼哈顿距离 2 的 cell。</item>
    /// <item>全部不可达 → 返回 <c>null</c>。</item>
    /// </list>
    /// <para/>
    /// **8 邻居顺序**（AGENTS.md §11）：N → E → S → W（同 <see cref="GridCoord.Neighbours"/>）。
    /// **曼哈顿距离 2 圈遍历**：先 dy ∈ {-2, +2} → 横条（X 范围 = original.X ± 2）；
    /// 再 dx ∈ {-2, +2} → 纵条（Y 范围 = original.Y ± 2）。同距离再按 CompareTo 升序。
    /// <para/>
    /// **不修改占用**：本服务**只**计算 `(displacedUnitId, newCoord)`；
    /// 实际占用迁移由调用方（<see cref="Starfall.Core.Rules.FallingCommand"/> 或将来的
    /// CompressionCommand）通过 <see cref="TileOccupancyService"/> 完成。
    /// </summary>
    public static class PhaseCompressionResolutionService
    {
        /// <summary>
        /// 解析阶段：找出被弹 unit 的最远空邻居。
        /// </summary>
        /// <param name="map">当前 <see cref="MapState"/>。</param>
        /// <param name="coord">多 unit 共同占用的坐标。</param>
        /// <param name="unitIdsAtCoord">占用该坐标的所有 unitId 列表（>= 2 才会触发，本服务不强制此约束）。</param>
        /// <returns>(被弹 unitId, 新坐标)；<c>null</c> = 无任何可达邻居。</returns>
        public static (int displacedUnitId, GridCoord newCoord)? Resolve(
            MapState map,
            GridCoord coord,
            IReadOnlyList<int> unitIdsAtCoord)
        {
            if (map == null) throw new ArgumentNullException(nameof(map));
            if (unitIdsAtCoord == null) throw new ArgumentNullException(nameof(unitIdsAtCoord));
            if (unitIdsAtCoord.Count < 2) return null;

            int displaced = unitIdsAtCoord[unitIdsAtCoord.Count - 1];

            var registry = PhaseFlipStateService.GetAttachedRegistry(map);
            var flipState = PhaseFlipStateService.GetOrAttach(map);
            if (registry == null) return null;

            // 1) 曼哈顿距离 1：4 邻居（按 N → E → S → W 确定性顺序）。
            var neighbours = new List<GridCoord>
            {
                new GridCoord(coord.X, coord.Y + 1, coord.Layer),
                new GridCoord(coord.X + 1, coord.Y, coord.Layer),
                new GridCoord(coord.X, coord.Y - 1, coord.Layer),
                new GridCoord(coord.X - 1, coord.Y, coord.Layer),
            };

            foreach (var n in neighbours)
            {
                if (TryFindFreeCell(map, registry, flipState, n, displaced, out var free))
                {
                    return (displaced, free);
                }
            }

            // 2) 曼哈顿距离 2：仅枚举 |ΔX|+|ΔY|==2 的格子（与 Manhattan 距离严格一致）。
            //    顺序：CompareTo Y → X → Layer 升序。
            var ring2 = new List<GridCoord>();
            // Manhattan=2 形式：
            //   dx=±2, dy=0：两侧外跳 2 列
            //   dx=±1, dy=±1：四角对角
            //   dx=0, dy=±2：上下 2 行
            ring2.Add(new GridCoord(coord.X + 2, coord.Y, coord.Layer));
            ring2.Add(new GridCoord(coord.X + 1, coord.Y + 1, coord.Layer));
            ring2.Add(new GridCoord(coord.X, coord.Y + 2, coord.Layer));
            ring2.Add(new GridCoord(coord.X - 1, coord.Y + 1, coord.Layer));
            ring2.Add(new GridCoord(coord.X - 2, coord.Y, coord.Layer));
            ring2.Add(new GridCoord(coord.X - 1, coord.Y - 1, coord.Layer));
            ring2.Add(new GridCoord(coord.X, coord.Y - 2, coord.Layer));
            ring2.Add(new GridCoord(coord.X + 1, coord.Y - 1, coord.Layer));
            // 按 Y → X → Layer 升序
            ring2.Sort();

            foreach (var n in ring2)
            {
                if (TryFindFreeCell(map, registry, flipState, n, displaced, out var free))
                {
                    return (displaced, free);
                }
            }

            return null;
        }

        /// <summary>
        /// 检查指定 <paramref name="coord"/> 是否能停 displaced unit。
        /// </summary>
        /// <remarks>
        /// 复用 <see cref="FallResolutionService.IsLandingCandidateValid"/> 的逻辑：
        /// 越界 + 阻挡 + ActiveDimension 不一致 + Stability=0 + 占用（包括自身不算）任一拒绝。
        /// </remarks>
        private static bool TryFindFreeCell(
            MapState map,
            TileDefinitionRegistry registry,
            PhaseFlipState flipState,
            GridCoord coord,
            int displacedUnitId,
            out GridCoord freeCell)
        {
            freeCell = default;

            if (!registry.TryGetByCoord(coord, out var def))
                return false;

            if (!FallResolutionService.IsLandingCandidateValid(map, registry, flipState, def, displacedUnitId))
                return false;

            freeCell = coord;
            return true;
        }
    }
}
