using System.Collections.Generic;
using Starfall.Core.Model;
using Starfall.Core.Status;

namespace Starfall.Core.Command
{
    /// <summary>
    /// 对单位施加状态。InstanceId 由 BattleState.NextStatusInstanceId 分配（确定性）。
    /// </summary>
    public sealed class ApplyStatusCommand : ICommand
    {
        public int CommandId { get; }
        public int TargetUnitId { get; }
        public StatusKind Kind { get; }
        public int RemainingTurns { get; }
        public int SourceUnitId { get; }

        public ApplyStatusCommand(int commandId, int targetUnitId, StatusKind kind, int remainingTurns, int sourceUnitId)
        {
            CommandId = commandId;
            TargetUnitId = targetUnitId;
            Kind = kind;
            RemainingTurns = remainingTurns;
            SourceUnitId = sourceUnitId;
        }

        public bool CanExecute(BattleState state)
        {
            foreach (var u in state.Units)
                if (u.UnitId == TargetUnitId) return true;
            return false;
        }

        public CommandResult Execute(BattleState state, out IReadOnlyList<BattleEvent> events)
        {
            events = System.Array.Empty<BattleEvent>();
            if (!CanExecute(state)) return CommandResult.Illegal;

            var inst = new StatusInstance(state.NextStatusInstanceId, Kind, RemainingTurns, SourceUnitId);
            state.NextStatusInstanceId++;
            state.AddStatus(inst);
            events = new[]
            {
                new BattleEvent(BattleEventKind.StatusApplied, TargetUnitId, null, null)
            };
            return CommandResult.Success;
        }
    }
}