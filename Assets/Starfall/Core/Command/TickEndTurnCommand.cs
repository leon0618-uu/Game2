using System.Collections.Generic;
using Starfall.Core.Model;
using Starfall.Core.Status;

namespace Starfall.Core.Command
{
    /// <summary>
    /// 回合末 tick：递减所有 Status.RemainingTurns，归零则移除；
    /// Burn 状态扣 1 HP；PhaseInvert 翻转单位相位；
    /// Root 仅在 MoveCommand.CanExecute 时阻止移动（不在 tick 中处理）。
    /// </summary>
    public sealed class TickEndTurnCommand : ICommand
    {
        public int CommandId { get; }

        public TickEndTurnCommand(int commandId)
        {
            CommandId = commandId;
        }

        public bool CanExecute(BattleState state) => true;

        public CommandResult Execute(BattleState state, out IReadOnlyList<BattleEvent> events)
        {
            var evs = new List<BattleEvent>();

            // Burn tick: 扣 1 HP
            foreach (var s in state.Statuses)
            {
                if (s.Kind != StatusKind.Burn) continue;
                foreach (var u in state.Units)
                {
                    if (u.UnitId == s.SourceUnitId)
                    {
                        u.Hp = System.Math.Max(0, u.Hp - 1);
                        evs.Add(new BattleEvent(BattleEventKind.UnitDamaged, u.UnitId, null, null));
                        break;
                    }
                }
            }

            // PhaseInvert tick: 翻转相位
            foreach (var s in state.Statuses)
            {
                if (s.Kind != StatusKind.PhaseInvert) continue;
                foreach (var u in state.Units)
                {
                    if (u.UnitId == s.SourceUnitId)
                    {
                        u.Phase = u.Phase == Phase.Light ? Phase.Dark : Phase.Light;
                        evs.Add(new BattleEvent(BattleEventKind.UnitPhaseInverted, u.UnitId, null, null));
                        break;
                    }
                }
            }

            // 递减 remaining；归零移除
            var toRemove = new List<int>();
            foreach (var s in state.Statuses)
            {
                s.RemainingTurns--;
                if (s.RemainingTurns <= 0) toRemove.Add(s.InstanceId);
            }
            foreach (var id in toRemove) state.RemoveStatus(id);

            events = evs;
            return CommandResult.Success;
        }
    }
}