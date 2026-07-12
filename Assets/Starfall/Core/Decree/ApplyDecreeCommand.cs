using Starfall.Core.Model;

namespace Starfall.Core.Decree
{
    /// <summary>
    /// 颁布律令。DecreeId 由 caller 提供（确定性）。
    /// </summary>
    public sealed class ApplyDecreeCommand : Starfall.Core.Command.ICommand
    {
        public int CommandId { get; set; }  // 通过 BattleRunner 显式传入
        public Decree Decree { get; }

        public ApplyDecreeCommand(int commandId, Decree decree)
        {
            CommandId = commandId;
            Decree = decree ?? throw new System.ArgumentNullException(nameof(decree));
        }

        public bool CanExecute(BattleState state) => true;

        public CommandResult Execute(BattleState state, out System.Collections.Generic.IReadOnlyList<Starfall.Core.Command.BattleEvent> events)
        {
            events = System.Array.Empty<Starfall.Core.Command.BattleEvent>();
            return CommandResult.Success;  // 注册表由外部管理（本任务不内嵌）
        }
    }
}
