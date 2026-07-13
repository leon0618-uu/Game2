using System.Collections.Generic;
using Starfall.Core.Command;
using Starfall.Core.Combat;
using Starfall.Core.Model;

namespace Starfall.Core.Replay
{
    /// <summary>
    /// 记录 BattleRunner 所有 Submit + EndTurn 产生的 CommandRecord。
    /// 严格按执行顺序追加。
    /// </summary>
    public sealed class CommandRecorder
    {
        private readonly List<CommandRecord> _records = new List<CommandRecord>();
        public IReadOnlyList<CommandRecord> Records => _records;
        public int NextSequence => _records.Count + 1;

        public void Record(ICommand command, IReadOnlyList<BattleEvent> events)
        {
            _records.Add(new CommandRecord(_records.Count + 1, command, events));
        }

        public void Clear() => _records.Clear();
    }
}