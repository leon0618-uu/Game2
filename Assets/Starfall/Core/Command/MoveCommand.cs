using System.Collections.Generic;
using Starfall.Core.Model;

namespace Starfall.Core.Command
{
    /// <summary>
    /// 移动命令：从 from 到 to（路径已由 Pathfinder 预算，Command 不重新算路）。
    /// </summary>
    public sealed class MoveCommand : ICommand
    {
        public int CommandId { get; }
        public int UnitId { get; }
        public GridPos From { get; }
        public GridPos To { get; }
        public IReadOnlyList<GridPos> Path { get; }

        public MoveCommand(int commandId, int unitId, GridPos from, GridPos to, IReadOnlyList<GridPos> path)
        {
            if (path == null || path.Count < 2)
                throw new System.ArgumentException("Path must have >= 2 points", nameof(path));
            if (path[0] != from) throw new System.ArgumentException("Path[0] must equal From", nameof(path));
            if (path[path.Count - 1] != to) throw new System.ArgumentException("Path[last] must equal To", nameof(path));
            CommandId = commandId;
            UnitId = unitId;
            From = from;
            To = to;
            Path = path;
        }

        public bool CanExecute(BattleState state)
        {
            var unit = FindUnit(state, UnitId);
            if (unit == null) return false;
            if (unit.Pos != From) return false;
            // Root 状态禁止移动
            foreach (var s in state.Statuses)
            {
                if (s.SourceUnitId == UnitId && s.Kind == Starfall.Core.Status.StatusKind.Root) return false;
            }
            // 目标格必须可站
            if (state.Board.Tiles.TryGetValue(To, out var tile) && tile == TileState.Blocked) return false;
            return true;
        }

        public CommandResult Execute(BattleState state, out IReadOnlyList<BattleEvent> events)
        {
            events = System.Array.Empty<BattleEvent>();
            if (!CanExecute(state)) return CommandResult.Illegal;

            var unit = FindUnit(state, UnitId);
            unit.Pos = To;
            events = new[] { new BattleEvent(BattleEventKind.UnitMoved, UnitId, From, To) };
            return CommandResult.Success;
        }

        private static UnitState FindUnit(BattleState s, int unitId)
        {
            foreach (var u in s.Units)
                if (u.UnitId == unitId) return u;
            return null;
        }
    }
}