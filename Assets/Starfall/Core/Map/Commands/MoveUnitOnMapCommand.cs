using System;
using System.Collections.Generic;
using Starfall.Core.Map;
using Starfall.Core.Map.Coordinates;
using Starfall.Core.Map.State;
using Starfall.Core.Map.Tile;

namespace Starfall.Core.Map.Commands
{
    /// <summary>
    /// doc2 MAP-03 §21.1 单位在地图上移动命令（**仅触发 MapEvent，不替代 BattleRunner.MoveCommand**）。
    /// <para/>
    /// **重要**：本命令**不**与 BattleRunner.MoveCommand 竞争——
    /// <list type="bullet">
    /// <li><see cref="Starfall.Core.Command.MoveCommand"/>：战斗规则（HP / 占用 / 移动力消耗）；写 <see cref="Starfall.Core.Command.BattleEvent"/>。</li>
    /// <li>本命令：地图状态层（tile occupancy 标记）；写 <see cref="MapEvent"/>。</item>
    /// <item>业务用法：BFS / pathfinding → PlayMode 中根据寻路结果先运行本命令
    ///       （同步 tile.OccupyingUnitId 字段），再让 BattleRunner.Run(MoveCommand) 推进战斗规则。</li>
    /// </list>
    /// <para/>
    /// **当前实现**：仅修改 tile runtime state 的 <see cref="TileState.OccupyingUnitId"/>
    /// 字段（如未 attach 则 no-op 返 Fail），并发射 <see cref="MapEventKind.OnUnitMovedOnMap"/>。
    /// 不触碰 unit.HP / Status 等战斗状态。
    /// <para/>
    /// **校验**：
    /// <list type="bullet">
    /// <item><paramref name="unitId"/> >= 1。</item>
    /// <li>runtime states 必须 attach（<c>"no runtime states attached"</c>）。</item>
    /// <li><paramref name="from"/> 已 attach 到 runtime states + 当前 OccupyingUnitId == unitId
    ///     （"source tile not owned by this unit"）。</li>
    /// <li><paramref name="to"/> in-bounds（<see cref="MapState.Definition"/>）。</item>
    /// <li><paramref name="to"/> 当前无占用单位（<c>"target tile occupied"</c>）。</li>
    /// </list>
    /// </summary>
    public sealed class MoveUnitOnMapCommand : IMapCommand
    {
        public int UnitId { get; }
        public GridCoord From { get; }
        public GridCoord To { get; }

        public MoveUnitOnMapCommand(int unitId, GridCoord from, GridCoord to)
        {
            if (unitId < 1)
                throw new ArgumentOutOfRangeException(nameof(unitId), unitId,
                    "UnitId must be >= 1.");
            From = from;
            To = to;
            UnitId = unitId;
        }

        public MapCommandResult Execute(MapState mapState)
        {
            if (mapState == null) throw new ArgumentNullException(nameof(mapState));
            int previousVersion = mapState.Version;
            int newVersion = previousVersion + 1;

            var runtimeStates = PhaseFlipStateService.GetRuntimeStates(mapState);
            if (runtimeStates == null)
                return MapCommandResult.Fail("no runtime states attached");

            // 1) from tile 必须存在并被该 unit 占用
            MapTileState fromTile = null;
            foreach (var kv in runtimeStates)
            {
                if (kv.Value.Coord == From) { fromTile = kv.Value; break; }
            }
            if (fromTile == null)
                return MapCommandResult.Fail("from coord has no runtime state");
            if (fromTile.OccupyingUnitId != UnitId)
                return MapCommandResult.Fail("source tile not owned by this unit");

            // 2) to 必须在 bounds
            if (!To.IsInBounds(mapState.Definition.Size))
                return MapCommandResult.Fail($"to {To} out of bounds");

            // 3) to 必须空闲
            MapTileState toTile = null;
            foreach (var kv in runtimeStates)
            {
                if (kv.Value.Coord == To) { toTile = kv.Value; break; }
            }
            if (toTile == null)
                return MapCommandResult.Fail("to coord has no runtime state");
            if (toTile.OccupyingUnitId.HasValue)
                return MapCommandResult.Fail("target tile occupied");

            // 4) 移动
            _previousFromOccupant = fromTile.OccupyingUnitId;
            _previousToOccupant = toTile.OccupyingUnitId;

            fromTile.OccupyingUnitId = null;
            toTile.OccupyingUnitId = UnitId;
            _executed = true;

            // 5) Emit events：2 个 OnTileChanged（from / to） + 1 个 OnUnitMovedOnMap（聚合）。
            var events = new List<MapEvent>(3)
            {
                MapEvent.TileChanged(From, $"unit-departed:{UnitId}"),
                MapEvent.TileChanged(To, $"unit-arrived:{UnitId}"),
                MapEvent.UnitMovedOnMap(UnitId, From, To, $"unit-moved:{UnitId}")
            };
            events.Sort();
            return MapCommandResult.Ok(events, newVersion);
        }

        public void Undo(MapState mapState)
        {
            if (mapState == null) throw new ArgumentNullException(nameof(mapState));
            if (!_executed)
                throw new InvalidOperationException("MoveUnitOnMapCommand.Undo called without prior Execute.");

            var runtimeStates = PhaseFlipStateService.GetRuntimeStates(mapState);
            if (runtimeStates != null)
            {
                // 反向：把 unit 放回 from；把 to 还原 to previous occupant
                foreach (var kv in runtimeStates)
                {
                    if (kv.Value.Coord == From) kv.Value.OccupyingUnitId = _previousFromOccupant;
                    else if (kv.Value.Coord == To) kv.Value.OccupyingUnitId = _previousToOccupant;
                }
            }
            _executed = false;
        }

        public int Version => 1;
        public string CommandId => $"move-unit-on-map:{UnitId}:{To}";
        public IReadOnlyList<string> Dependencies => Array.Empty<string>();

        private bool _executed;
        private int? _previousFromOccupant;
        private int? _previousToOccupant;

        public override string ToString()
            => $"MoveUnitOnMapCommand(UnitId={UnitId}, {From}->{To})";
    }
}
