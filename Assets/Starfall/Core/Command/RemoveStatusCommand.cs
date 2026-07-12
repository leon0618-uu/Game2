using System.Collections.Generic;
using Starfall.Core.Model;

namespace Starfall.Core.Command
{
    public sealed class RemoveStatusCommand : ICommand
    {
        public int CommandId { get; }
        public int StatusInstanceId { get; }

        public RemoveStatusCommand(int commandId, int statusInstanceId)
        {
            CommandId = commandId;
            StatusInstanceId = statusInstanceId;
        }

        public bool CanExecute(BattleState state)
        {
            foreach (var s in state.Statuses)
                if (s.InstanceId == StatusInstanceId) return true;
            return false;
        }

        public CommandResult Execute(BattleState state, out IReadOnlyList<BattleEvent> events)
        {
            events = System.Array.Empty<BattleEvent>();
            if (!CanExecute(state)) return CommandResult.Illegal;

            state.RemoveStatus(StatusInstanceId);
            events = new[]
            {
                new BattleEvent(BattleEventKind.StatusRemoved, -1, null, null)
            };
            return CommandResult.Success;
        }
    }
}