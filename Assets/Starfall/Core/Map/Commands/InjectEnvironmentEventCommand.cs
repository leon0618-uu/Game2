using System;
using System.Collections.Generic;
using Starfall.Core.Map;
using Starfall.Core.Map.Environment;
using Starfall.Core.Map.State;

namespace Starfall.Core.Map.Commands
{
    /// <summary>
    /// doc2 MAP-11b InjectEnvironmentEventCommand（IMapCommand；ADR-0008）。
    ///
    /// <para/>
    /// **作用**：把单个 <see cref="MapEnvironmentEvent"/> 插入到
    /// <see cref="MapState.ActiveSchedule"/>。
    ///
    /// <para/>
    /// **执行语义**：
    /// <list type="number">
    /// <item>读取当前 <see cref="MapState.ActiveSchedule"/>。</item>
    /// <item>把 <paramref name="Event"/> 附加到 schedule.Events（按 phase 顺序保持）。</item>
    /// <item>用 <see cref="MapEnvironmentSchedule.FromEvents"/> 重建 schedule。</item>
    /// <item>写回 <see cref="MapState.ActiveSchedule"/>。</item>
    /// </list>
    ///
    /// <para/>
    /// **Undo**：恢复旧 schedule。
    ///
    /// <para/>
    /// **失败条件**：
    /// <list type="bullet">
    /// <item><paramref name="Event"/> 为 null → <c>"event is null"</c>。</item>
    /// <item>插入后存在"重复 event"（按 <see cref="MapEnvironmentEvent.Equals"/> 完全相同）
    ///       → <c>"duplicate event"</c>。</item>
    /// </list>
    /// </summary>
    public sealed class InjectEnvironmentEventCommand : IMapCommand
    {
        public MapEnvironmentEvent Event { get; }

        public InjectEnvironmentEventCommand(MapEnvironmentEvent ev)
        {
            if (ev == null) throw new ArgumentNullException(nameof(ev));
            Event = ev;
        }

        public MapCommandResult Execute(MapState mapState)
        {
            if (mapState == null) throw new ArgumentNullException(nameof(mapState));
            int previousVersion = mapState.Version;
            int newVersion = previousVersion + 1;

            var oldSchedule = mapState.ActiveSchedule;
            // 校验事件 phase index 可分配
            int phaseIdx = MapEnvironmentEventToPhaseMap.GetPhaseIndex(Event.Kind);
            if (phaseIdx < 0 || phaseIdx > 9)
                return MapCommandResult.Fail("event kind cannot be assigned to any phase");

            // 构造新 events 列表（包含旧 + 新）
            var newEvents = new List<MapEnvironmentEvent>(oldSchedule.Count + 1);
            for (int i = 0; i < oldSchedule.Count; i++)
                newEvents.Add(oldSchedule.Events[i]);

            // 重复 event 检测（按 Equals 全等比较）
            for (int i = 0; i < newEvents.Count; i++)
            {
                if (newEvents[i].Equals(Event))
                    return MapCommandResult.Fail("duplicate event");
            }
            newEvents.Add(Event);
            var newSchedule = MapEnvironmentSchedule.FromEvents(
                newEvents, oldSchedule.ScheduleId, oldSchedule.CreatedTick);

            // 保存旧值
            _previousSchedule = oldSchedule;
            _executed = true;

            // 应用
            mapState.SetActiveSchedule(newSchedule);

            return MapCommandResult.Ok(Array.Empty<MapEvent>(), newVersion);
        }

        public void Undo(MapState mapState)
        {
            if (mapState == null) throw new ArgumentNullException(nameof(mapState));
            if (!_executed)
                throw new InvalidOperationException(
                    "InjectEnvironmentEventCommand.Undo called without prior Execute.");
            mapState.SetActiveSchedule(_previousSchedule);
            _executed = false;
        }

        public int Version => 1;
        public string CommandId => $"inject-environment-event:{(int)Event.Kind}:{Event.TriggerTick}";
        public IReadOnlyList<string> Dependencies => Array.Empty<string>();

        private bool _executed;
        private MapEnvironmentSchedule _previousSchedule;

        public override string ToString()
            => $"InjectEnvironmentEventCommand(Kind={Event.Kind})";
    }
}
