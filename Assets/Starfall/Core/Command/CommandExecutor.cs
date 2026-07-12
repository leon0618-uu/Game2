using System.Collections.Generic;
using Starfall.Core.Model;

namespace Starfall.Core.Command
{
    /// <summary>
    /// 命令执行器：接收 BattleState + ICommand，返回 CommandResult + 事件链。
    /// 失败命令不修改 state；成功命令原子应用并派发事件。
    /// </summary>
    public static class CommandExecutor
    {
        public static CommandResult Run(BattleState state, ICommand command,
            out IReadOnlyList<BattleEvent> events)
        {
            if (command == null) { events = System.Array.Empty<BattleEvent>(); return CommandResult.Illegal; }
            return command.Execute(state, out events);
        }
    }
}