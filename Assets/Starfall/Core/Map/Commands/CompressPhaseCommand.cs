using System;
using System.Collections.Generic;
using Starfall.Core.Map;
using Starfall.Core.Map.Commands.Compression;
using Starfall.Core.Map.Coordinates;
using Starfall.Core.Map.State;
using Starfall.Core.Map.Tile;

namespace Starfall.Core.Map.Commands
{
    /// <summary>
    /// doc2 MAP-03 §21.1 / MAP-08 §6.1 相位挤压命令（<see cref="PhaseCompressionResolutionService"/> 包装）。
    /// <para/>
    /// **范围**：当同一 <see cref="GridCoord"/> 上 ≥ 2 个单位时被触发；
    /// 找出被弹 unit（默认 = <c>unitIdsAtCoord[Count-1]</c>）并按 service
    /// 给定的位移规则（4 邻居 → 8 邻居 Manhattan=2 环）将其弹到一个空 cell。
    /// <para/>
    /// **实现路径**：
    /// <list type="number">
    /// <item>调 <see cref="PhaseCompressionResolutionService.Resolve"/> 取得 <c>(displacedUnitId, newCoord)</c>。</item>
    /// <item>同步更新 <c>MapTileState.OccupyingUnitId</c>（from / to 两侧）。</item>
    /// <li>Emits <see cref="MapEventKind.OnPhaseCompressed"/> + <see cref="MapEventKind.OnTileChanged"/> × 2。</li>
    /// </list>
    /// <para/>
    /// **失败条件**：
    /// <list type="bullet">
    /// <item>unitIdsAtCoord.Count &lt; 2 → <c>"compression requires >= 2 units"</c>。</item>
    /// <item>runtime states 未 attach → <c>"no runtime states attached"</c>。</item>
    /// <item>PhaseCompressionResolutionService.Resolve 返回 null（无空 cell）→ <c>"no free neighbor cell"</c>。</item>
    /// </list>
    /// <para/>
    /// **依赖**：本命令可独立执行；但业务通常在 unit 移动 / phase flip 后由
    /// BattleRunner 或 scenario manager 触发。
    /// </summary>
    public sealed class CompressPhaseCommand : IMapCommand
    {
        public GridCoord Coord { get; }
        public IReadOnlyList<int> UnitIdsAtCoord { get; }

        public CompressPhaseCommand(GridCoord coord, IReadOnlyList<int> unitIdsAtCoord)
        {
            if (unitIdsAtCoord == null) throw new ArgumentNullException(nameof(unitIdsAtCoord));
            if (unitIdsAtCoord.Count == 0) throw new ArgumentException(
                "UnitIdsAtCoord must not be empty.", nameof(unitIdsAtCoord));
            for (int i = 0; i < unitIdsAtCoord.Count; i++)
            {
                if (unitIdsAtCoord[i] < 1)
                    throw new ArgumentOutOfRangeException(nameof(unitIdsAtCoord), unitIdsAtCoord[i],
                        "UnitIds must be >= 1.");
            }
            Coord = coord;
            UnitIdsAtCoord = unitIdsAtCoord;
        }

        public MapCommandResult Execute(MapState mapState)
        {
            if (mapState == null) throw new ArgumentNullException(nameof(mapState));
            int previousVersion = mapState.Version;
            int newVersion = previousVersion + 1;

            if (UnitIdsAtCoord.Count < 2)
                return MapCommandResult.Fail("compression requires >= 2 units");

            var runtimeStates = PhaseFlipStateService.GetRuntimeStates(mapState);
            if (runtimeStates == null)
                return MapCommandResult.Fail("no runtime states attached");

            var resolved = PhaseCompressionResolutionService.Resolve(mapState, Coord, UnitIdsAtCoord);
            if (!resolved.HasValue)
                return MapCommandResult.Fail("no free neighbor cell");

            int displacedId = resolved.Value.displacedUnitId;
            GridCoord newCoord = resolved.Value.newCoord;

            // 找出原 tile 和目标 tile runtime states
            MapTileState fromTile = null;
            MapTileState toTile = null;
            foreach (var kv in runtimeStates)
            {
                if (kv.Value.Coord == Coord) fromTile = kv.Value;
                else if (kv.Value.Coord == newCoord) toTile = kv.Value;
                if (fromTile != null && toTile != null) break;
            }
            if (fromTile == null || toTile == null)
                return MapCommandResult.Fail("runtime state missing for from/to tile");

            if (fromTile.OccupyingUnitId != displacedId)
                return MapCommandResult.Fail("source tile occupancy mismatch");

            _previousFromOccupant = fromTile.OccupyingUnitId;
            _previousToOccupant = toTile.OccupyingUnitId;
            _executedCoord = newCoord;
            _executedUnitId = displacedId;

            fromTile.OccupyingUnitId = null;
            toTile.OccupyingUnitId = displacedId;
            _executed = true;

            var events = new List<MapEvent>(3)
            {
                MapEvent.TileChanged(Coord, $"compressed-from:{Coord}"),
                MapEvent.TileChanged(newCoord, $"compressed-to:{newCoord}"),
                MapEvent.PhaseCompressed(Coord, displacedId, $"phase-compressed:{displacedId}")
            };
            events.Sort();
            return MapCommandResult.Ok(events, newVersion);
        }

        public void Undo(MapState mapState)
        {
            if (mapState == null) throw new ArgumentNullException(nameof(mapState));
            if (!_executed)
                throw new InvalidOperationException("CompressPhaseCommand.Undo called without prior Execute.");
            var runtimeStates = PhaseFlipStateService.GetRuntimeStates(mapState);
            if (runtimeStates != null && _executedCoord.HasValue && _executedUnitId.HasValue)
            {
                MapTileState fromTile = null;
                MapTileState toTile = null;
                foreach (var kv in runtimeStates)
                {
                    if (kv.Value.Coord == Coord) fromTile = kv.Value;
                    else if (kv.Value.Coord == _executedCoord.Value) toTile = kv.Value;
                    if (fromTile != null && toTile != null) break;
                }
                if (fromTile != null && toTile != null)
                {
                    toTile.OccupyingUnitId = _previousToOccupant;
                    fromTile.OccupyingUnitId = _previousFromOccupant;
                }
            }
            _executed = false;
            _executedCoord = null;
            _executedUnitId = null;
        }

        public int Version => 1;
        public string CommandId => $"compress-phase:{Coord}";
        public IReadOnlyList<string> Dependencies => Array.Empty<string>();

        private bool _executed;
        private GridCoord? _executedCoord;
        private int? _executedUnitId;
        private int? _previousFromOccupant;
        private int? _previousToOccupant;

        public override string ToString()
            => $"CompressPhaseCommand(Coord={Coord}, Units={UnitIdsAtCoord.Count})";
    }
}
