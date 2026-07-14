using System;
using System.Collections.Generic;
using Starfall.Core.Map.Coordinates;
using Starfall.Core.Map.State;
using Starfall.Core.Map.Tile;

namespace Starfall.Core.Map.Commands
{
    /// <summary>
    /// doc2 MAP-08 §6.1 区域相位翻转命令。
    /// <para/>
    /// **区域识别（MAP-08 妥协方案）**：
    /// <list type="bullet">
    /// <item>MAP-09 完整 <see cref="MapRegion"/> 已挂占位结构（<see cref="MapRegion.TileCoords"/>）。</item>
    /// <item>本命令接受 <paramref name="RegionAnchorTileId"/>，在 <see cref="MapState.Regions"/>
    ///       内查找包含该 tile 的 region。未找到 → 返回 Fail。</item>
    /// <item>查找算法：线性扫描 <see cref="MapState.Regions"/>，按 <see cref="MapRegion.RegionId"/>
    ///       升序，第一个命中即作为目标 region（确定性强）。</item>
    /// </list>
    /// <para/>
    /// **失败条件**（任一即整体 Fail，无副作用）：
    /// <list type="bullet">
    /// <item>no region found → <c>"region not found"</c>。</item>
    /// <item>region 为空（0 cells） → <c>"empty region"</c>。</item>
    /// <item>region 内任一 cell 已与 TargetLayer 同层 → <c>"already at target layer (in region)"</c>。</item>
    /// <item>region 内任一 cell PhaseLocked → <c>"phase locked (in region)"</c>。</item>
    /// <item>region 内任一 cell 无 PhaseFlippable 标签 → <c>"not phase flippable (in region)"</c>。</item>
    /// </list>
    /// <para/>
    /// **原子性**：失败时不写任何 flip 状态；成功时整 region 同步翻转。
    /// <para/>
    /// **影响面**：成功 → AffectedTiles = region 内全部 cell，按 <see cref="GridCoord.CompareTo"/>
    /// 升序。
    /// </summary>
    public sealed class FlipRegionPhaseCommand : IMapCommand
    {
        /// <summary>用于查 <see cref="MapState.Regions"/> 的 anchor tileId（>= 1）。</summary>
        public int RegionAnchorTileId { get; }

        /// <summary>切换到的目标维度。</summary>
        public DimensionLayer TargetLayer { get; }

        public FlipRegionPhaseCommand(int regionAnchorTileId, DimensionLayer targetLayer)
        {
            if (regionAnchorTileId < 1)
                throw new ArgumentOutOfRangeException(nameof(regionAnchorTileId), regionAnchorTileId,
                    "RegionAnchorTileId must be >= 1.");
            RegionAnchorTileId = regionAnchorTileId;
            TargetLayer = targetLayer;
        }

        public MapCommandResult Execute(MapState map)
        {
            if (map == null) throw new ArgumentNullException(nameof(map));

            var registry = PhaseFlipStateService.GetAttachedRegistry(map);
            if (registry == null)
                return MapCommandResult.Fail("no tile registry attached");

            if (!registry.TryGetById(RegionAnchorTileId, out var anchorDef))
                return MapCommandResult.Fail("tile not found");

            // 1) 查找包含该 tile 的 region（按 RegionId 升序，第一个命中）。
            MapRegion region = null;
            foreach (var r in map.Regions)
            {
                foreach (var c in r.TileCoords)
                {
                    if (c == anchorDef.Coord)
                    {
                        region = r;
                        break;
                    }
                }
                if (region != null) break;
            }
            if (region == null)
                return MapCommandResult.Fail("region not found");

            if (region.TileCoords.Count == 0)
                return MapCommandResult.Fail("empty region");

            var phaseState = PhaseFlipStateService.GetOrAttach(map);

            // 2) 预检：region 内所有 cell 必须通过 PhaseLocked / PhaseFlippable / NotYetTargetLayer 校验。
            //    全部通过才执行写。任一失败 → 整命令 Fail。
            foreach (var coord in region.TileCoords)
            {
                if (!registry.TryGetByCoord(coord, out var def))
                    return MapCommandResult.Fail("not phase flippable (in region: coord not in registry)");

                if ((def.Tags & TileTags.PhaseLocked) != 0)
                    return MapCommandResult.Fail("phase locked (in region)");

                if ((def.Tags & TileTags.PhaseFlippable) == 0)
                    return MapCommandResult.Fail("not phase flippable (in region)");

                DimensionLayer currentLayer = phaseState.TryGetFlippedLayer(def.TileId, out var cur)
                    ? cur
                    : map.ActiveLayer;

                if (currentLayer == TargetLayer)
                    return MapCommandResult.Fail("already at target layer (in region)");
            }

            // 3) 全部通过 → 写翻转状态并收集 affected。
            var affected = new List<GridCoord>(region.TileCoords.Count);
            foreach (var coord in region.TileCoords)
            {
                if (!registry.TryGetByCoord(coord, out var def)) continue;
                phaseState.SetFlippedLayer(def.TileId, TargetLayer);
                affected.Add(def.Coord);
            }
            affected.Sort();
            return MapCommandResult.Ok(affected);
        }

        public override string ToString()
            => $"FlipRegionPhaseCommand(AnchorTileId={RegionAnchorTileId}, Target={TargetLayer})";
    }
}
