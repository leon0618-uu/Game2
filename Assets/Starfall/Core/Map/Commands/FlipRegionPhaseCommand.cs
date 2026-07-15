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
    /// **契约**（公开签名不变；MAP-07 重写内部使用 <see cref="MapTileState.ActiveDimension"/>）：
    /// <list type="bullet">
    /// <item>目标：在 <see cref="MapState"/> 上找到含 <see cref="RegionAnchorTileId"/> 的
    ///       <see cref="MapRegion"/>；校验 region 内每 cell 的 PhaseLocked /
    ///       PhaseFlippable / NotAtTargetLayer 条件；全部通过则对每个 cell
    ///       调用 <see cref="PhaseFlipStateService.SetActiveDimension"/>。</item>
    /// <item>**MAP-07 路径**：写操作直接修改 <see cref="MapTileState.ActiveDimension"/>
    ///       字段，替代旧 <c>PhaseFlipState.SetFlippedLayer</c> dict。</item>
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

            // 2) 预检：region 内所有 cell 必须通过 PhaseLocked / PhaseFlippable / NotYetTargetLayer 校验。
            foreach (var coord in region.TileCoords)
            {
                if (!registry.TryGetByCoord(coord, out var def))
                    return MapCommandResult.Fail("not phase flippable (in region: coord not in registry)");

                if ((def.Tags & TileTags.PhaseLocked) != 0)
                    return MapCommandResult.Fail("phase locked (in region)");

                if ((def.Tags & TileTags.PhaseFlippable) == 0)
                    return MapCommandResult.Fail("not phase flippable (in region)");

                // MAP-07：通过 per-tile ActiveDimension 字段读当前层（fallback 字典）。
                DimensionLayer currentLayer = map.ActiveLayer;
                PhaseFlipStateService.TryGetActiveDimension(map, def.TileId, out currentLayer);

                if (currentLayer == TargetLayer)
                    return MapCommandResult.Fail("already at target layer (in region)");
            }

            // 3) 全部通过 → 写翻转状态并收集 affected。
            var affected = new List<GridCoord>(region.TileCoords.Count);
            foreach (var coord in region.TileCoords)
            {
                if (!registry.TryGetByCoord(coord, out var def)) continue;
                PhaseFlipStateService.SetActiveDimension(map, def.TileId, TargetLayer);
                affected.Add(def.Coord);
            }
            affected.Sort();
            return MapCommandResult.Ok(affected);
        }

        public override string ToString()
            => $"FlipRegionPhaseCommand(AnchorTileId={RegionAnchorTileId}, Target={TargetLayer})";
    }
}
