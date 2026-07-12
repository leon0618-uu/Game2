using System.Collections.Generic;
using Starfall.Core.Command;
using Starfall.Core.Model;

namespace Starfall.Core.Decree
{
    /// <summary>
    /// 颁布律令。DecreeId 由 caller 提供（确定性）。
    /// 实际注入 _decrees 由 caller 在 Submit 前完成；本 Command 仅发出 DecreeApplied 事件。
    /// </summary>
    public sealed class ApplyDecreeCommand : ICommand
    {
        public int CommandId { get; set; }
        public Decree Decree { get; }

        public ApplyDecreeCommand(int commandId, Decree decree)
        {
            CommandId = commandId;
            Decree = decree ?? throw new System.ArgumentNullException(nameof(decree));
        }

        public bool CanExecute(BattleState state) => true;

        public CommandResult Execute(BattleState state, out IReadOnlyList<BattleEvent> events)
        {
            events = new[] { new BattleEvent(BattleEventKind.DecreeApplied, -1, null, null) };
            return CommandResult.Success;
        }
    }
}