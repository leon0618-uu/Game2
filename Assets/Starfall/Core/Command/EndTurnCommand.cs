using System.Collections.Generic;
using Starfall.Core.Model;

namespace Starfall.Core.Command
{
    public sealed class EndTurnCommand : ICommand
    {
        public int CommandId { get; }
        public Owner ExpectedActivePlayer { get; }

        public EndTurnCommand(int commandId, Owner expectedActivePlayer)
        {
            CommandId = commandId;
            ExpectedActivePlayer = expectedActivePlayer;
        }

        public bool CanExecute(BattleState state) => state.ActivePlayer == ExpectedActivePlayer;

        public CommandResult Execute(BattleState state, out IReadOnlyList<BattleEvent> events)
        {
            events = System.Array.Empty<BattleEvent>();
            if (!CanExecute(state)) return CommandResult.Illegal;

            state.TurnNumber++;
            state.ActivePlayer = state.ActivePlayer == Owner.Player ? Owner.Enemy : Owner.Player;
            events = new[] { new BattleEvent(BattleEventKind.TurnEnded, -1, null, null) };
            return CommandResult.Success;
        }
    }
}