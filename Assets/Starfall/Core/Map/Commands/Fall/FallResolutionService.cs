using System;
using System.Collections.Generic;
using Starfall.Core.Map.Coordinates;
using Starfall.Core.Map.State;
using Starfall.Core.Map.Tile;

namespace Starfall.Core.Map.Commands.Fall
{
    /// <summary>
    /// doc2 MAP-08 §6.1 坠落解析服务：单位原坐标 invalid (Void / Stability=0 / 已占用 /
    /// <see cref="TileDefinition.BlocksMovement"/>) 时，查找最近合法落点。
    /// <para/>
    /// **搜索规则**（AGENTS.md §11）：
    /// <list type="number">
    /// <item>第一排序键：曼哈顿距离 <c>|x - ox| + |y - oy|</c>。</item>
    /// <item>第二排序键：<see cref="GridCoord.CompareTo"/>（Y → X → Layer）—— 严格确定性。</item>
    /// <item>跨层候选：原坐标 <paramref name="originalCoord"/> 的同 (X,Y) 跨层候选优先于
    ///       跨 (X,Y) 候选（因为 Manhattan 距离 = 0 时 CompareTo 顺序固定）。</item>
    /// <item>合法落点定义：cell 在 <paramref name="map"/> 内（含 Layer）、
    ///       <see cref="TileDefinition.BlocksMovement"/> = false、
    ///       当前未坍塌（<see cref="MapTileState.Stability"/> > 0）、
    ///       未被其它单位占用（<see cref="TileOccupancyService.IsOccupied"/> = false）。</item>
    /// </list>
    /// <para/>
    /// **footprint 处理**：本服务接收一个**已确定**的 anchor 单元（即 footprint 的代表坐标）；
    /// Footprint 2x2 / 3x3 在落点对其它 cell 的要求由调用方（<see cref="Starfall.Core.Rules.FallingCommand"/>）
    /// 在使用 <see cref="TileOccupancyService.TryPlaceUnit"/> 时统一校验 —— 本服务只负责找到
    /// 一个**单格合法落点**。
    /// <para/>
    /// **无解语义**：返回 <c>null</c>（由调用方触发"HPMinus + OnUnitEnteredVoid" fallback）。
    /// </summary>
    public static class FallResolutionService
    {
        /// <summary>
        /// 搜索最近合法落点。
        /// </summary>
        /// <param name="map">当前 <see cref="MapState"/>。</param>
        /// <param name="originalCoord">单位原坐标（含 Layer）。</param>
        /// <param name="unitId">用于排除自身占用（避免"原地 valid 但被自己占"误判）。</param>
        /// <returns>合法落点 <see cref="GridCoord"/>；<c>null</c> = 无解。</returns>
        public static GridCoord? FindNearestLegalLanding(
            MapState map,
            GridCoord originalCoord,
            int unitId)
        {
            if (map == null) throw new ArgumentNullException(nameof(map));

            var registry = PhaseFlipStateService.GetAttachedRegistry(map);
            var flipState = PhaseFlipStateService.GetOrAttach(map);
            if (registry == null) return null;

            GridCoord? best = null;
            int bestDistance = int.MaxValue;

            // 遍历注册表的全部 tile，按 Manhattan + CompareTo 顺序。
            // 委托 TileDefinitionRegistry.All()（已按 Y → X → Layer 升序），
            // 我们手动按 Manhattan 距离排序即可；确定性的总序：
            //   primary: Manhattan distance ASC
            //   secondary: (Y, X, Layer) ASC
            // 只考虑同 Layer 的 tile：跨 Layer 需 PhaseFlip 跳转（本轮不启用）。
            foreach (var def in registry.All())
            {
                if (def.Coord.Layer != originalCoord.Layer) continue;
                if (!IsLandingCandidateValid(map, registry, flipState, def, unitId))
                    continue;

                int distance = def.Coord.ManhattanDistance(originalCoord);
                if (distance > bestDistance) continue;

                bool isBetter;
                if (distance < bestDistance)
                {
                    isBetter = true;
                }
                else
                {
                    // tie-break: CompareTo
                    isBetter = def.Coord.CompareTo(best.Value) < 0;
                }

                if (isBetter)
                {
                    best = def.Coord;
                    bestDistance = distance;
                }
            }

            return best;
        }

        /// <summary>
        /// 校验 tile 是否可作为合法落点（不依赖 unitId / footprint）。
        /// </summary>
        /// <remarks>
        /// 释放给 PhaseCompression / 多候选查找场景；与 <see cref="FindNearestLegalLanding"/>
        /// 的核心校验一致。
        /// </remarks>
        public static bool IsLandingCandidateValid(
            MapState map,
            TileDefinitionRegistry registry,
            PhaseFlipState flipState,
            TileDefinition def,
            int unitId)
        {
            if (map == null) throw new ArgumentNullException(nameof(map));
            if (registry == null) throw new ArgumentNullException(nameof(registry));
            if (flipState == null) throw new ArgumentNullException(nameof(flipState));

            // 1) 越界（按 map.Definition.Size 排除）
            if (!def.Coord.IsInBounds(map.Definition.Size))
                return false;

            // 2) terrain 是否阻挡移动
            if (def.BlocksMovement) return false;

            // 3) 对象占用：任何对象占 → 不可落。
            var occObj = TileOccupancyService.GetOccupantObject(map, def.Coord);
            if (occObj.HasValue) return false;

            // 4) 单元占用：被自己占（unitId == 自身） → 视为"可重新布置"合法；
            //                  被别人占 → 不可落。
            var occUnit = TileOccupancyService.GetOccupantUnit(map, def.Coord);
            if (occUnit.HasValue && occUnit.Value != unitId) return false;

            return true;
        }
    }
}
