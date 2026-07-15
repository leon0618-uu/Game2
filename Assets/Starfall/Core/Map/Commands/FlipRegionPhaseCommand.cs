using System;
using System.Collections.Generic;
using Starfall.Core.Map;
using Starfall.Core.Map.Coordinates;
using Starfall.Core.Map.State;
using Starfall.Core.Map.Tile;

namespace Starfall.Core.Map.Commands
{
    /// <summary>
    /// doc2 MAP-03 / MAP-08 §6.1 区域相位翻转命令。
    /// <para/>
    /// **粒度**（公开签名不变）：
    /// <list type="bullet">
    /// <item>目标：在 <see cref="MapState"/> 中找到含 <see cref="RegionAnchorTileId"/> 的
    ///       <see cref="MapRegion"/>；校验 region 内每 cell 的 PhaseLocked / PhaseFlippable /
    ///       NotAtTargetLayer 条件；全部通过则对每个 cell
    ///       调用 <see cref="PhaseFlipStateService.SetActiveDimension"/>。</item>
    /// <item>**MAP-07 路径**：写操作直接修改 <see cref="MapTileState.ActiveDimension"/>
    ///       字段，替代旧 <c>PhaseFlipState.SetFlippedLayer</c> dict。</item>
    /// </list>
    /// <para/>
    /// **MAP-03 完整化**：
    /// <list type="bullet">
    /// <item>实现 <see cref="Undo(MapState)"/>：记录 region 内原 layer，Undo 时反向 flip。</item>
    /// <li><see cref="Version"/> = 2。</li>
    /// <li><see cref="CommandId"/> = <c>"flip-region-phase:{RegionAnchorTileId}"</c>。</li>
    /// <li><see cref="Dependencies"/> = 空（独立命令）。</li>
    /// <li>Emit <see cref="MapEventKind.OnTileChanged"/> 事件（region 内每 cell 一个）。</li>
    /// </list>
    /// <para/>
    /// **失败条件**（任一则整 region Fail，无副作用）：
    /// <list type="bullet">
    /// <item>no region found → <c>"region not found"</c>。</item>
    /// <item>region 为空（0 cells）→ <c>"empty region"</c>。</item>
    /// <item>region 内任一 cell 已是 TargetLayer 同层 → <c>"already at target layer (in region)"</c>。</item>
    /// <item>region 内任一 cell PhaseLocked → <c>"phase locked (in region)"</c>。</item>
    /// <item>region 内任一 cell 无 PhaseFlippable 标签 → <c>"not phase flippable (in region)"</c>。</item>
    /// </list>
    /// <para/>
    /// **原子性**：失败时不写任何 flip 状态；成功时整 region 同步翻转。
    /// </summary>
    public sealed class FlipRegionPhaseCommand : IMapCommand
    {
        /// <summary>用于查 <see cref="MapState.Regions"/> 的 anchor tileId（>= 1）。</summary>
        public int RegionAnchorTileId { get; }

        /// <summary>切换到的目标维度。</summary>
        public DimensionLayer TargetLayer { get; }

        // ──────────── Undo 跟踪（MPRecords：region 内所有 cell 的 prevLayer）────────────

        private bool _executed;
        private readonly List<(int tileId, DimensionLayer prevLayer)> _undoRecords
            = new List<(int, DimensionLayer)>(8);

        public FlipRegionPhaseCommand(int regionAnchorTileId, DimensionLayer targetLayer)
        {
            if (regionAnchorTileId < 1)
                throw new ArgumentOutOfRangeException(nameof(regionAnchorTileId), regionAnchorTileId,
                    "RegionAnchorTileId must be >= 1.");
            RegionAnchorTileId = regionAnchorTileId;
            TargetLayer = targetLayer;
        }

        public MapCommandResult Execute(MapState mapState)
        {
            if (mapState == null) throw new ArgumentNullException(nameof(mapState));

            int previousVersion = mapState.Version;
            int newVersion = previousVersion + 1;

            var runtimeStates = PhaseFlipStateService.GetRuntimeStates(mapState);
            if (runtimeStates == null)
            {
                return MapCommandResult.Fail("no runtime states attached");
            }

            var registry = PhaseFlipStateService.GetAttachedRegistry(mapState);
            if (registry == null)
                return MapCommandResult.Fail("no tile registry attached");

            if (!registry.TryGetById(RegionAnchorTileId, out var anchorDef))
                return MapCommandResult.Fail("tile not found");

            // 1) 查找包含该 tile 的 region（按 RegionId 升序，第一个命中即胜）。
            MapRegion region = null;
            foreach (var r in mapState.Regions)
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

            // 2) 预校验：region 内所有 cell 必须通过 PhaseLocked / PhaseFlippable / NotYetTargetLayer 校验。
            //    任何一项失败 → 原子性返回 Fail，mapState 完全不变。
            foreach (var coord in region.TileCoords)
            {
                if (!registry.TryGetByCoord(coord, out var def))
                    return MapCommandResult.Fail("not phase flippable (in region: coord not in registry)");

                if ((def.Tags & TileTags.PhaseLocked) != 0)
                    return MapCommandResult.Fail("phase locked (in region)");

                if ((def.Tags & TileTags.PhaseFlippable) == 0)
                    return MapCommandResult.Fail("not phase flippable (in region)");

                // MAP-07：通过 per-tile ActiveDimension 字段读当前层（fallback 字典）。
                PhaseFlipStateService.TryGetActiveDimension(mapState, def.TileId, out var currentLayer);

                if (currentLayer == TargetLayer)
                    return MapCommandResult.Fail("already at target layer (in region)");
            }

            // 3) 全部通过 → 写翻状态并收集 affected events。
            _undoRecords.Clear();
            var events = new List<MapEvent>(region.TileCoords.Count);

            foreach (var coord in region.TileCoords)
            {
                if (!registry.TryGetByCoord(coord, out var def)) continue;
                PhaseFlipStateService.TryGetActiveDimension(mapState, def.TileId, out var currentLayer);
                _undoRecords.Add((def.TileId, currentLayer));
                PhaseFlipStateService.SetActiveDimension(mapState, def.TileId, TargetLayer);
                events.Add(MapEvent.TileChanged(def.Coord));
            }

            // 稳定排序（按 CompareTo：GridCoord -> Y -> X -> Layer）。
            events.Sort();

            _executed = true;
            return MapCommandResult.Ok(events, newVersion);
        }

        public void Undo(MapState mapState)
        {
            if (mapState == null) throw new ArgumentNullException(nameof(mapState));
            if (!_executed)
                throw new InvalidOperationException(
                    "FlipRegionPhaseCommand.Undo called without prior successful Execute; " +
                    "history stack violation (executor should not invoke Undo in this state).");

            // 反向恢复：直接从 undoRecords 应用 prevLayer（顺序不重要）。
            for (int i = _undoRecords.Count - 1; i >= 0; i--)
            {
                var (tileId, prevLayer) = _undoRecords[i];
                PhaseFlipStateService.SetActiveDimension(mapState, tileId, prevLayer);
            }
            _executed = false;
            _undoRecords.Clear();
        }

        /// <inheritdoc />
        public int Version => 2;

        /// <inheritdoc />
        public string CommandId => $"flip-region-phase:{RegionAnchorTileId}";

        /// <inheritdoc />
        public IReadOnlyList<string> Dependencies => Array.Empty<string>();

        public override string ToString()
            => $"FlipRegionPhaseCommand(AnchorTileId={RegionAnchorTileId}, Target={TargetLayer})";
    }
}
