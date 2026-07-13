using Starfall.Core.Model;

namespace Starfall.Core.Command
{
    /// <summary>
    /// 战斗命令接口。所有状态变化必须经由 Command。
    /// Command 是不可变的；Executor 负责应用并产生 BattleEvent。
    /// </summary>
    public interface ICommand
    {
        int CommandId { get; }  // 确定性序号（用于 replay）
        bool CanExecute(BattleState state);
        CommandResult Execute(BattleState state, out System.Collections.Generic.IReadOnlyList<BattleEvent> events);
    }
}