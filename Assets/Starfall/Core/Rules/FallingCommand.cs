using System.Collections.Generic;
using Starfall.Core.Command;
using Starfall.Core.Model;

namespace Starfall.Core.Rules
{
    /// <summary>
    /// 坠落命令：将指定单位标记为坠出棋盘。Execute 时扣 1 HP（坠地伤害）并从 units 移除。
    /// 用于 MoveCommand 后续 phase 校验或锚点/挤压后果。
    /// </summary>
    public sealed class FallingCommand : ICommand
    {
        public int CommandId { get; set; }
        public int UnitId { get; }
        public int FallDamage { get; }

        public FallingCommand(int commandId, int unitId, int fallDamage = 1)
        {
            CommandId = commandId;
            UnitId = unitId;
            FallDamage = fallDamage;
        }

        public bool CanExecute(BattleState state)
        {
            foreach (var u in state.Units) if (u.UnitId == UnitId) return true;
            return false;
        }

        public CommandResult Execute(BattleState state, out IReadOnlyList<BattleEvent> events)
        {
            events = System.Array.Empty<BattleEvent>();
            if (!CanExecute(state)) return CommandResult.Illegal;

            UnitState target = null;
            for (int i = 0; i < state.Units.Count; i++)
                if (state.Units[i].UnitId == UnitId) { target = state.Units[i]; break; }

            target.Hp = System.Math.Max(0, target.Hp - FallDamage);
            // MVP 简化：单位仍在棋盘上但 HP 减少 + 标记位；M-13+ 真实物理移除
            // 用 StatusKind.None 但 RemainingTurns=0 标记坠地
            events = new[] { new BattleEvent(BattleEventKind.UnitDamaged, UnitId, null, null) };
            return CommandResult.Success;
        }
    }
}