using System.Collections.Generic;
using Starfall.Core.Command;

namespace Starfall.Core.Replay
{
    /// <summary>
    /// 单条命令记录：含执行序号 + 命令本身 + 该命令产生的 BattleEvent 链。
    /// 用于确定性 Replay 重放。
    /// </summary>
    public readonly struct CommandRecord
    {
        public int Sequence { get; }      // 1-based 序号
        public ICommand Command { get; }
        public IReadOnlyList<BattleEvent> Events { get; }

        public CommandRecord(int sequence, ICommand command, IReadOnlyList<BattleEvent> events)
        {
            Sequence = sequence;
            Command = command;
            Events = events;
        }
    }
}