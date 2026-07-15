using System;
using System.Collections.Generic;
using Starfall.Core.Map;
using Starfall.Core.Map.Coordinates;
using Starfall.Core.Map.State;
using Starfall.Core.Map.Tile;

namespace Starfall.Core.Map.Commands
{
    /// <summary>
    /// doc2 MAP-03 §21.1 相位解挤压命令（压缩命令的反向）。
    /// <para/>
    /// **角色**：与 <see cref="CompressPhaseCommand"/> 互补；用于"撤销一次压缩但保留历史" —
    /// 不同于 <see cref="CompressPhaseCommand.Undo"/>（仅由 executor 调用），本命令是一条
    /// 独立命令，业务可主动对其 apply（生成 OnPhaseDecompressed 事件）。
    /// <para/>
    /// **实现路径**：把指定 unitId 从 <paramref name="fromCoord"/> 移回 <paramref name="toCoord"/>；
    /// 这两条 Cell 必须在 runtime states 中存在且 fromCoord 当前占用 unitId、
    /// toCoord 当前无占用。
    /// <para/>
    /// **校验**：
    /// <list type="bullet">
    /// <item>runtime states 已 attach。</item>
    /// <item>fromCoord 当前 OccupyingUnitId == <paramref name="unitId"/>。</item>
    /// <li>toCoord 当前无单位占用。</li>
    /// </list>
    /// </summary>
    public sealed class DecompressPhaseCommand : IMapCommand
    {
        public int UnitId { get; }
        public GridCoord FromCoord { get; }
        public GridCoord ToCoord { get; }

        public DecompressPhaseCommand(int unitId, GridCoord fromCoord, GridCoord toCoord)
        {
            if (unitId < 1)
                throw new ArgumentOutOfRangeException(nameof(unitId), unitId,
                    "UnitId must be >= 1.");
            UnitId = unitId;
            FromCoord = fromCoord;
            ToCoord = toCoord;
        }

        public MapCommandResult Execute(MapState mapState)
        {
            if (mapState == null) throw new ArgumentNullException(nameof(mapState));
            int previousVersion = mapState.Version;
            int newVersion = previousVersion + 1;

            var runtimeStates = PhaseFlipStateService.GetRuntimeStates(mapState);
            if (runtimeStates == null)
                return MapCommandResult.Fail("no runtime states attached");

            MapTileState fromTile = null;
            MapTileState toTile = null;
            foreach (var kv in runtimeStates)
            {
                if (kv.Value.Coord == FromCoord) fromTile = kv.Value;
                else if (kv.Value.Coord == ToCoord) toTile = kv.Value;
                if (fromTile != null && toTile != null) break;
            }
            if (fromTile == null || toTile == null)
                return MapCommandResult.Fail("runtime state missing for from/to tile");

            if (fromTile.OccupyingUnitId != UnitId)
                return MapCommandResult.Fail("source tile not owned by this unit");
            if (toTile.OccupyingUnitId.HasValue)
                return MapCommandResult.Fail("target tile occupied");

            _previousFromOccupant = fromTile.OccupyingUnitId;
            _previousToOccupant = toTile.OccupyingUnitId;
            fromTile.OccupyingUnitId = null;
            toTile.OccupyingUnitId = UnitId;
            _executed = true;

            var events = new List<MapEvent>(3)
            {
                MapEvent.TileChanged(FromCoord, $"decompressed-from:{FromCoord}"),
                MapEvent.TileChanged(ToCoord, $"decompressed-to:{ToCoord}"),
                MapEvent.PhaseDecompressed(ToCoord, UnitId, $"phase-decompressed:{UnitId}")
            };
            events.Sort();
            return MapCommandResult.Ok(events, newVersion);
        }

        public void Undo(MapState mapState)
        {
            if (mapState == null) throw new ArgumentNullException(nameof(mapState));
            if (!_executed)
                throw new InvalidOperationException("DecompressPhaseCommand.Undo called without prior Execute.");
            var runtimeStates = PhaseFlipStateService.GetRuntimeStates(mapState);
            if (runtimeStates != null)
            {
                MapTileState fromTile = null;
                MapTileState toTile = null;
                foreach (var kv in runtimeStates)
                {
                    if (kv.Value.Coord == FromCoord) fromTile = kv.Value;
                    else if (kv.Value.Coord == ToCoord) toTile = kv.Value;
                    if (fromTile != null && toTile != null) break;
                }
                if (fromTile != null && toTile != null)
                {
                    toTile.OccupyingUnitId = _previousToOccupant;
                    fromTile.OccupyingUnitId = _previousFromOccupant;
                }
            }
            _executed = false;
        }

        public int Version => 1;
        public string CommandId => $"decompress-phase:{UnitId}:{ToCoord}";
        public IReadOnlyList<string> Dependencies => Array.Empty<string>();

        private bool _executed;
        private int? _previousFromOccupant;
        private int? _previousToOccupant;

        public override string ToString()
            => $"DecompressPhaseCommand(UnitId={UnitId}, {FromCoord}->{ToCoord})";
    }
}
