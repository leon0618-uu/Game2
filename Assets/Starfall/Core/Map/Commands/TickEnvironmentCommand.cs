using System;
using System.Collections.Generic;
using Starfall.Core.Map;
using Starfall.Core.Map.Environment;
using Starfall.Core.Map.State;

namespace Starfall.Core.Map.Commands
{
    /// <summary>
    /// doc2 MAP-11b TickEnvironmentCommand（IMapCommand；ADR-0008）。
    ///
    /// <para/>
    /// **作用**：单步执行 phase N（<paramref name="PhaseIndex"/>），
    /// 并把 <see cref="MapState.EnvironmentTickAccumulator"/> 增加 <paramref name="TickDelta"/>。
    ///
    /// <para/>
    /// **执行语义**：调 <see cref="EnvironmentPhaseResolver.ExecutePhase"/>（仅单个 phase），
    /// 不修改 <see cref="MapState.ActiveSchedule"/>。
    ///
    /// <para/>
    /// **Undo 语义**：回滚 EnvironmentTickAccumulator；不回滚 phase 副作用
    /// （理由与 <see cref="ScheduleEnvironmentCommand"/> 一致）。
    ///
    /// <para/>
    /// **失败条件**：
    /// <list type="bullet">
    /// <item><paramref name="PhaseIndex"/> ∉ [0, 9] → <c>"phase index out of range"</c>。</item>
    /// <item><paramref name="TickDelta"/> < 0 → 构造时拒。</item>
    /// <item>EnvironmentTickAccumulator + TickDelta 溢出 <see cref="int.MaxValue"/> → <c>"tick overflow"</c>。</item>
    /// </list>
    /// </summary>
    public sealed class TickEnvironmentCommand : IMapCommand
    {
        public int PhaseIndex { get; }
        public int TickDelta { get; }

        public TickEnvironmentCommand(int phaseIndex, int tickDelta = 1)
        {
            if (phaseIndex < 0 || phaseIndex > 9)
                throw new ArgumentOutOfRangeException(nameof(phaseIndex), phaseIndex,
                    "PhaseIndex must be in [0, 9].");
            if (tickDelta < 0)
                throw new ArgumentOutOfRangeException(nameof(tickDelta), tickDelta,
                    "TickDelta must be >= 0.");
            PhaseIndex = phaseIndex;
            TickDelta = tickDelta;
        }

        public MapCommandResult Execute(MapState mapState)
        {
            if (mapState == null) throw new ArgumentNullException(nameof(mapState));
            int previousVersion = mapState.Version;
            int newVersion = previousVersion + 1;

            if (PhaseIndex < 0 || PhaseIndex > 9)
                return MapCommandResult.Fail("phase index out of range");

            // Tick overflow 校验（防止 max int 翻转）
            long projected = (long)mapState.EnvironmentTickAccumulator + TickDelta;
            if (projected > int.MaxValue)
                return MapCommandResult.Fail("tick overflow");

            // 保存旧值（用于 Undo）
            _previousTick = mapState.EnvironmentTickAccumulator;
            _executed = true;

            // 应用 tick 累加
            mapState.EnvironmentTickAccumulator = mapState.EnvironmentTickAccumulator + TickDelta;

            // 执行单步 phase
            var resolver = new EnvironmentPhaseResolver();
            var events = resolver.ExecutePhase(mapState, PhaseIndex, mapState.ActiveSchedule);
            return MapCommandResult.Ok(events, newVersion);
        }

        public void Undo(MapState mapState)
        {
            if (mapState == null) throw new ArgumentNullException(nameof(mapState));
            if (!_executed)
                throw new InvalidOperationException(
                    "TickEnvironmentCommand.Undo called without prior Execute.");
            mapState.EnvironmentTickAccumulator = _previousTick;
            _executed = false;
        }

        public int Version => 1;
        public string CommandId => $"tick-environment:{PhaseIndex}";
        public IReadOnlyList<string> Dependencies => Array.Empty<string>();

        private bool _executed;
        private int _previousTick;

        public override string ToString()
            => $"TickEnvironmentCommand(Phase={PhaseIndex}, TickDelta={TickDelta})";
    }
}
