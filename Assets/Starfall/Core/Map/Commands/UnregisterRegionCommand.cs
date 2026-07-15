using System;
using System.Collections.Generic;
using Starfall.Core.Map;
using Starfall.Core.Map.Regions;
using Starfall.Core.Map.State;

namespace Starfall.Core.Map.Commands
{
    /// <summary>
    /// doc2 MAP-09 注销 region 命令。
    ///
    /// <para/>
    /// **范围**：从 <see cref="MapState.RegionStates"/> 中按 RegionId 移除一个 region。
    ///
    /// <para/>
    /// **校验**：
    /// <list type="bullet">
    /// <item>RegionId 必须存在于 <see cref="MapState.RegionStates"/>。</item>
    /// </list>
    ///
    /// <para/>
    /// **Emit**：单 <see cref="MapEventKind.OnRegionChanged"/> 事件（含 RegionId + OldValue=pre-state, NewValue=-1=removed）。
    /// </summary>
    public sealed class UnregisterRegionCommand : IMapCommand
    {
        public int RegionId { get; }

        public UnregisterRegionCommand(int regionId)
        {
            if (regionId < 0)
                throw new ArgumentOutOfRangeException(nameof(regionId), regionId,
                    "RegionId must be >= 0.");
            RegionId = regionId;
        }

        public MapCommandResult Execute(MapState mapState)
        {
            if (mapState == null) throw new ArgumentNullException(nameof(mapState));
            int previousVersion = mapState.Version;
            int newVersion = previousVersion + 1;

            MapRegionState prev = null;
            int prevIdx = -1;
            for (int i = 0; i < mapState.RegionStates.Count; i++)
            {
                if (mapState.RegionStates[i].Definition.RegionIdValue.Value == RegionId)
                {
                    prev = mapState.RegionStates[i];
                    prevIdx = i;
                    break;
                }
            }
            if (prev == null)
                return MapCommandResult.Fail($"region {RegionId} not found");

            _previousState = prev.State;
            _capturedDefinition = prev.Definition;
            _capturedTick = prev.TickEntered;
            mapState.RegionStatesInternal.RemoveAt(prevIdx);
            _executed = true;

            var events = new List<MapEvent>(1)
            {
                MapRegionService.MakeStateChangedEvent(RegionId, prev.State, RegionState.Disabled, "unregistered")
            };
            events.Sort();
            return MapCommandResult.Ok(events, newVersion);
        }

        public void Undo(MapState mapState)
        {
            if (mapState == null) throw new ArgumentNullException(nameof(mapState));
            if (!_executed)
                throw new InvalidOperationException(
                    "UnregisterRegionCommand.Undo called without prior Execute.");
            // 重新构造同 Definition 的 region state
            var rs = new MapRegionState(_capturedDefinition, _capturedTick);
            rs.SetStateInternal(_previousState, _capturedTick);
            mapState.AddRegionState(rs);
            _executed = false;
        }

        public int Version => 1;
        public string CommandId => $"unregister-region:{RegionId}";
        public IReadOnlyList<string> Dependencies => Array.Empty<string>();

        private bool _executed;
        private RegionState _previousState;
        // Undo 需要恢复的 Definition（从 Execute 中捕获）
        internal MapRegionDefinition _capturedDefinition;
        internal int _capturedTick;

        public override string ToString()
            => $"UnregisterRegionCommand(RegionId={RegionId})";
    }
}