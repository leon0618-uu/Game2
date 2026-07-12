using System.Collections.Generic;
using Starfall.Core.Model;

namespace Starfall.Core.Undo
{
    /// <summary>
    /// Undo 栈：每次 BattleState 变化前 push 当前快照；
    /// Undo 弹出最近快照。
    /// 深度限制：最大 50 层（防止内存爆炸）。
    /// </summary>
    public sealed class UndoStack
    {
        private readonly Stack<BattleState> _stack = new Stack<BattleState>();
        public int MaxDepth { get; }
        public int Count => _stack.Count;

        public UndoStack(int maxDepth = 50)
        {
            MaxDepth = maxDepth;
        }

        public void Push(BattleState snapshot)
        {
            if (snapshot == null) throw new System.ArgumentNullException(nameof(snapshot));
            _stack.Push(BattleStateCloner.Clone(snapshot));
            while (_stack.Count > MaxDepth)
            {
                // 弹出最旧条目（栈底）
                var temp = new Stack<BattleState>(_stack.Count);
                while (_stack.Count > 1) temp.Push(_stack.Pop());
                _stack.Pop();
                while (temp.Count > 0) _stack.Push(temp.Pop());
            }
        }

        public bool TryUndo(out BattleState restored)
        {
            if (_stack.Count == 0) { restored = null; return false; }
            restored = _stack.Pop();
            return true;
        }

        public void Clear() => _stack.Clear();
    }
}