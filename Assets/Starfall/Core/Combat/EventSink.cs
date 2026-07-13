using System.Collections.Generic;
using Starfall.Core.Command;

namespace Starfall.Core.Combat
{
    /// <summary>
    /// 收集所有 <see cref="BattleEvent"/>。
    /// 供 Presenter / 测试订阅（见 ADR-0002 单向同步）：
    /// - Presenter 在 Push 模型下作为事件拉取端；
    /// - 测试作为结果断言端（断言事件计数、种类、顺序）。
    ///
    /// 仅追加，不修改或重排已写入事件 —— 保证 Replay 与运行态事件顺序一致。
    /// </summary>
    public sealed class EventSink
    {
        private readonly List<BattleEvent> _events = new List<BattleEvent>();

        public IReadOnlyList<BattleEvent> Events => _events;

        public void Append(IReadOnlyList<BattleEvent> events)
        {
            if (events == null) return;
            for (int i = 0; i < events.Count; i++)
            {
                _events.Add(events[i]);
            }
        }

        public void Clear() => _events.Clear();
    }
}