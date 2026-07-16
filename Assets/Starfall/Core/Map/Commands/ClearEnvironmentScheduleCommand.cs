using System;
using System.Collections.Generic;
using Starfall.Core.Map;
using Starfall.Core.Map.Environment;
using Starfall.Core.Map.State;

namespace Starfall.Core.Map.Commands
{
    /// <summary>
    /// doc2 MAP-11b ClearEnvironmentScheduleCommand（IMapCommand；ADR-0008）。
    ///
    /// <para/>
    /// **作用**：清空当前 <see cref="MapState.ActiveSchedule"/>（替换为 Empty），
    /// 并清空 <see cref="MapState.PendingEvents"/>。
    ///
    /// <para/>
    /// **Undo**：恢复 Clear 前的 ActiveSchedule + PendingEvents 完整快照。
    ///
    /// <para/>
    /// **范围**：仅清理 schedule 状态；不影响 <see cref="MapState.LocalCVs"/> /
    /// <see cref="MapState.GlobalCV"/>（这些是 map 状态机的副作用，需单独通过
    /// <see cref="MapCollapse.ReconstructTileCommand"/> /
    /// <see cref="MapCollapse.ModifyGlobalCollapseValueCommand"/> undo）。
    /// </summary>
    public sealed class ClearEnvironmentScheduleCommand : IMapCommand
    {
        public ClearEnvironmentScheduleCommand()
        {
        }

        public MapCommandResult Execute(MapState mapState)
        {
            if (mapState == null) throw new ArgumentNullException(nameof(mapState));
            int previousVersion = mapState.Version;
            int newVersion = previousVersion + 1;

            // 保存旧值（用于 Undo）
            _previousSchedule = mapState.ActiveSchedule;
            _previousPendingEvents = new List<MapEnvironmentEvent>(mapState.PendingEvents);
            _executed = true;

            // 应用：清空
            mapState.SetActiveSchedule(MapEnvironmentSchedule.Empty(mapState.EnvironmentTickAccumulator));
            mapState.ClearPendingEvents();

            return MapCommandResult.Ok(Array.Empty<MapEvent>(), newVersion);
        }

        public void Undo(MapState mapState)
        {
            if (mapState == null) throw new ArgumentNullException(nameof(mapState));
            if (!_executed)
                throw new InvalidOperationException(
                    "ClearEnvironmentScheduleCommand.Undo called without prior Execute.");
            mapState.SetActiveSchedule(_previousSchedule);
            // Restore pending events
            mapState.ClearPendingEvents();
            for (int i = 0; i < _previousPendingEvents.Count; i++)
                mapState.AddPendingEvent(_previousPendingEvents[i]);
            _executed = false;
            _previousPendingEvents = null;
        }

        public int Version => 1;
        public string CommandId => "clear-environment-schedule";
        public IReadOnlyList<string> Dependencies => Array.Empty<string>();

        private bool _executed;
        private MapEnvironmentSchedule _previousSchedule;
        private List<MapEnvironmentEvent> _previousPendingEvents;

        public override string ToString() => "ClearEnvironmentScheduleCommand()";
    }
}
