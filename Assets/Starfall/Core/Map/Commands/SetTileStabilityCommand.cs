using System;
using System.Collections.Generic;
using Starfall.Core.Map;
using Starfall.Core.Map.Coordinates;
using Starfall.Core.Map.State;
using Starfall.Core.Map.Tile;

namespace Starfall.Core.Map.Commands
{
    /// <summary>
    /// doc2 MAP-03 §21.1 设置 tile 稳定性值命令。
    /// <para/>
    /// **范围**：[0, 100]（与 <see cref="MapTileState.Stability"/> 契约一致，0 = 已坍塌）。
    /// <para/>
    /// **实现路径**：
    /// <list type="bullet">
    /// <item>查 <see cref="TileDefinitionRegistry"/> + <see cref="PhaseFlipStateService.GetRuntimeStates"/>
    ///       获取 <see cref="MapTileState"/>。</li>
    /// <li>校验 <see cref="MapTileState.OccupyingUnitId"/> 有单位占用且新 stability = 0 时拒绝
    ///       （不主动抹除单位，由 attacker / FallingCommand 触发）。</li>
    /// <li>直接 set <see cref="MapTileState.Stability"/>。</li>
    /// </list>
    /// <para/>
    /// **失败条件**：
    /// <list type="bullet">
    /// <item>tileId 未注册 → <c>"tile not found"</c>。</item>
    /// <item>runtime states 未 attach → <c>"no runtime states attached"</c>。</item>
    /// <item>新值不在 [0, 100] → <c>"invalid stability"</c>。</item>
    /// <li>tile 有单位占用且 stability = 0 → <c>"tile occupied and unstable"</c>（避免误坍塌）。</item>
    /// </list>
    /// <para/>
    /// **Emit**：单 <see cref="MapEventKind.OnTileStabilityChanged"/> 事件，含 old / new value。
    /// </summary>
    public sealed class SetTileStabilityCommand : IMapCommand
    {
        public int TileId { get; }
        public int NewStability { get; }

        public SetTileStabilityCommand(int tileId, int newStability)
        {
            if (tileId < 1)
                throw new ArgumentOutOfRangeException(nameof(tileId), tileId,
                    "TileId must be >= 1.");
            if (newStability < 0 || newStability > 100)
                throw new ArgumentOutOfRangeException(nameof(newStability), newStability,
                    "NewStability must be in [0, 100].");
            TileId = tileId;
            NewStability = newStability;
        }

        public MapCommandResult Execute(MapState mapState)
        {
            if (mapState == null) throw new ArgumentNullException(nameof(mapState));
            int previousVersion = mapState.Version;
            int newVersion = previousVersion + 1;

            var runtimeStates = PhaseFlipStateService.GetRuntimeStates(mapState);
            if (runtimeStates == null)
                return MapCommandResult.Fail("no runtime states attached");

            if (!runtimeStates.TryGetValue(TileId, out var tileState))
                return MapCommandResult.Fail("tile runtime state not found");

            if (tileState.OccupyingUnitId.HasValue && NewStability == 0)
                return MapCommandResult.Fail("tile occupied and unstable");

            _previousStability = tileState.Stability;
            tileState.Stability = NewStability;
            _executed = true;

            var events = new List<MapEvent>(1)
            {
                MapEvent.TileStabilityChanged(tileState.Coord, _previousStability, NewStability, $"stability:{TileId}")
            };
            events.Sort();
            return MapCommandResult.Ok(events, newVersion);
        }

        public void Undo(MapState mapState)
        {
            if (mapState == null) throw new ArgumentNullException(nameof(mapState));
            if (!_executed)
                throw new InvalidOperationException("SetTileStabilityCommand.Undo called without prior Execute.");
            var runtimeStates = PhaseFlipStateService.GetRuntimeStates(mapState);
            if (runtimeStates != null && runtimeStates.TryGetValue(TileId, out var tileState))
            {
                tileState.Stability = _previousStability;
            }
            _executed = false;
        }

        public int Version => 1;
        public string CommandId => $"set-tile-stability:{TileId}";
        public IReadOnlyList<string> Dependencies => Array.Empty<string>();

        private bool _executed;
        private int _previousStability;

        public override string ToString()
            => $"SetTileStabilityCommand(TileId={TileId}, Stability={NewStability})";
    }
}
