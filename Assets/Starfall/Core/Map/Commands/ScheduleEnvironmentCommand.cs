using System;
using System.Collections.Generic;
using Starfall.Core.Map;
using Starfall.Core.Map.Environment;
using Starfall.Core.Map.State;

namespace Starfall.Core.Map.Commands
{
    /// <summary>
    /// doc2 MAP-11b ScheduleEnvironmentCommand（IMapCommand；ADR-0008）。
    ///
    /// <para/>
    /// **作用**：把给定的 <see cref="MapEnvironmentSchedule"/> 设为
    /// <see cref="MapState.ActiveSchedule"/>，并顺序执行该 schedule 的 phase 0..9。
    ///
    /// <para/>
    /// **执行语义**：
    /// <list type="number">
    /// <item>保存旧 <see cref="MapState.ActiveSchedule"/> 和 <see cref="MapState.EnvironmentTickAccumulator"/>。</item>
    /// <item>把 <paramref name="Schedule"/> 写入 <see cref="MapState.ActiveSchedule"/>。</item>
    /// <item>调 <see cref="EnvironmentPhaseResolver.ExecuteAll"/>，返回 phase 0..9 累计事件。</item>
    /// <item>每次 ExecuteAll +1 <see cref="MapState.EnvironmentTickAccumulator"/>。</item>
    /// </list>
    ///
    /// <para/>
    /// **Undo 语义**：恢复旧的 ActiveSchedule + TickAccumulator；
    /// 不撤销 phase 内副作用（如 LocalCVs / GlobalCV 变化）———
    /// phase 副作用按设计是"当前生效对 map 状态的写"；
    /// 调用方应在需要 undo 时，使用更细粒度的 IMapCommand
    /// （如 <see cref="MapCollapse.ModifyGlobalCollapseValueCommand"/> /
    /// <see cref="MapCollapse.CollapseTileCommand"/>）单独 undo。
    ///
    /// <para/>
    /// **失败条件**：
    /// <list type="bullet">
    /// <item><paramref name="Schedule"/> 为 null → <c>"schedule is null"</c>。</item>
    /// <item><paramref name="Schedule"/> 的事件顺序错乱（phase index 不单调） → <c>"schedule out of order"</c>。</item>
    /// <item><see cref="MapState"/> 为 null → 抛 <see cref="ArgumentNullException"/>。</item>
    /// </list>
    /// </summary>
    public sealed class ScheduleEnvironmentCommand : IMapCommand
    {
        public MapEnvironmentSchedule Schedule { get; }

        public ScheduleEnvironmentCommand(MapEnvironmentSchedule schedule)
        {
            if (schedule.ScheduleId < 0)
                throw new ArgumentOutOfRangeException(nameof(schedule), schedule.ScheduleId,
                    "ScheduleId must be >= 0.");
            Schedule = schedule;
        }

        public MapCommandResult Execute(MapState mapState)
        {
            if (mapState == null) throw new ArgumentNullException(nameof(mapState));
            int previousVersion = mapState.Version;
            int newVersion = previousVersion + 1;

            // 校验 schedule 内部顺序
            var resolver = new EnvironmentPhaseResolver();
            int validateCode = resolver.ValidateSchedule(Schedule);
            if (validateCode != 0)
                return MapCommandResult.Fail($"schedule out of order (code={validateCode})");

            // 保存旧值（用于 Undo）
            _previousSchedule = mapState.ActiveSchedule;
            _previousTick = mapState.EnvironmentTickAccumulator;
            _executed = true;

            // 应用：写入新 schedule
            mapState.SetActiveSchedule(Schedule);
            mapState.EnvironmentTickAccumulator = mapState.EnvironmentTickAccumulator + 1;

            // 顺序执行 phase 0..9
            // 注：ExecuteAll 不直接修改 version，但 ScheduleEnvironmentCommand 自身
            // 实现版号递增。
            var events = resolver.ExecuteAll(mapState, Schedule);
            return MapCommandResult.Ok(events, newVersion);
        }

        public void Undo(MapState mapState)
        {
            if (mapState == null) throw new ArgumentNullException(nameof(mapState));
            if (!_executed)
                throw new InvalidOperationException(
                    "ScheduleEnvironmentCommand.Undo called without prior Execute.");
            mapState.SetActiveSchedule(_previousSchedule);
            mapState.EnvironmentTickAccumulator = _previousTick;
            _executed = false;
        }

        public int Version => 1;
        public string CommandId => $"schedule-environment:{Schedule.ScheduleId}";
        public IReadOnlyList<string> Dependencies => Array.Empty<string>();

        private bool _executed;
        private MapEnvironmentSchedule _previousSchedule;
        private int _previousTick;

        public override string ToString()
            => $"ScheduleEnvironmentCommand(ScheduleId={Schedule.ScheduleId}, Events={Schedule.Count})";
    }
}
