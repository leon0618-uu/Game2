using System.Collections.Generic;
using NUnit.Framework;
using Starfall.Core.Command;
using Starfall.Core.Model;
using Starfall.Core.Pathfinding;

namespace Starfall.Tests.EditMode
{
    public class CommandAndPathfinderTests
    {
        private static BoardState MakeBoard(int w = 4, int h = 4, System.Func<int, int, TileState> tileAt = null)
        {
            var tiles = new Dictionary<GridPos, TileState>();
            for (int y = 0; y < h; y++)
                for (int x = 0; x < w; x++)
                    tiles[new GridPos(x, y)] = tileAt != null ? tileAt(x, y) : TileState.Normal;
            return new BoardState(w, h, tiles);
        }

        private static BattleState MakeStateWithUnit(GridPos unitPos, int unitId = 1)
        {
            var board = MakeBoard();
            var s = new BattleState(0, Owner.Player, board, null);
            s.AddUnit(new UnitState(unitId, unitPos, 10, 10, Phase.Light, Owner.Player));
            return s;
        }

        [Test]
        public void BFSPathfinder_StraightPath()
        {
            var board = MakeBoard();
            var pf = new BFSPathfinder();
            var path = pf.FindPath(board, new GridPos(0, 0), new GridPos(3, 0));
            Assert.IsNotNull(path);
            Assert.AreEqual(4, path.Count);
            Assert.AreEqual(new GridPos(0, 0), path[0]);
            Assert.AreEqual(new GridPos(3, 0), path[3]);
        }

        [Test]
        public void BFSPathfinder_AvoidsBlockedTile()
        {
            var board = MakeBoard(4, 4, (x, y) => (x == 1 && y == 0) ? TileState.Blocked : TileState.Normal);
            var pf = new BFSPathfinder();
            var path = pf.FindPath(board, new GridPos(0, 0), new GridPos(2, 0));
            Assert.IsNotNull(path);
            // 绕过 (1,0) → 必须经过 (1,1) 或 (0,1)
            bool viaBelow = false;
            foreach (var p in path)
            {
                if (p == new GridPos(1, 1)) viaBelow = true;
            }
            Assert.IsTrue(viaBelow, "Path must route via (1,1) since (1,0) blocked");
        }

        [Test]
        public void BFSPathfinder_UnreachableReturnsNull()
        {
            // 中间一整列 Blocked
            var board = MakeBoard(4, 4, (x, y) => (x == 1) ? TileState.Blocked : TileState.Normal);
            var pf = new BFSPathfinder();
            var path = pf.FindPath(board, new GridPos(0, 0), new GridPos(3, 0));
            Assert.IsNull(path);
        }

        [Test]
        public void BFSPathfinder_Deterministic_SameStartEnd()
        {
            var board = MakeBoard();
            var pf = new BFSPathfinder();
            var p1 = pf.FindPath(board, new GridPos(0, 0), new GridPos(3, 3));
            var p2 = pf.FindPath(board, new GridPos(0, 0), new GridPos(3, 3));
            Assert.AreEqual(p1, p2);
        }

        [Test]
        public void MoveCommand_AppliesSuccessfully()
        {
            var s = MakeStateWithUnit(new GridPos(0, 0));
            var pf = new BFSPathfinder();
            var path = pf.FindPath(s.Board, new GridPos(0, 0), new GridPos(2, 0));
            var cmd = new MoveCommand(1, 1, new GridPos(0, 0), new GridPos(2, 0), path);
            var result = CommandExecutor.Run(s, cmd, out var events);
            Assert.AreEqual(CommandResult.Success, result);
            Assert.AreEqual(1, events.Count);
            Assert.AreEqual(BattleEventKind.UnitMoved, events[0].Kind);
            Assert.AreEqual(new GridPos(2, 0), s.Units[0].Pos);
        }

        [Test]
        public void MoveCommand_IllegalOnBlockedTarget()
        {
            var board = MakeBoard(4, 4, (x, y) => (x == 2 && y == 0) ? TileState.Blocked : TileState.Normal);
            var s = new BattleState(0, Owner.Player, board, null);
            s.AddUnit(new UnitState(1, new GridPos(0, 0), 10, 10, Phase.Light, Owner.Player));
            var path = new List<GridPos> { new GridPos(0, 0), new GridPos(1, 0), new GridPos(2, 0) };
            var cmd = new MoveCommand(1, 1, new GridPos(0, 0), new GridPos(2, 0), path);
            var result = CommandExecutor.Run(s, cmd, out var events);
            Assert.AreEqual(CommandResult.Illegal, result);
            Assert.AreEqual(0, events.Count);
            Assert.AreEqual(new GridPos(0, 0), s.Units[0].Pos);
        }

        [Test]
        public void MoveCommand_IllegalWhenUnitPositionMismatch()
        {
            var s = MakeStateWithUnit(new GridPos(5, 5));  // 单位实际在 (5,5)
            var path = new List<GridPos> { new GridPos(0, 0), new GridPos(1, 0) };
            var cmd = new MoveCommand(1, 1, new GridPos(0, 0), new GridPos(1, 0), path);
            var result = CommandExecutor.Run(s, cmd, out var events);
            Assert.AreEqual(CommandResult.Illegal, result);
        }

        [Test]
        public void EndTurnCommand_SwitchesActivePlayer()
        {
            var s = MakeStateWithUnit(new GridPos(0, 0));
            Assert.AreEqual(Owner.Player, s.ActivePlayer);
            var cmd = new EndTurnCommand(1, Owner.Player);
            var result = CommandExecutor.Run(s, cmd, out var events);
            Assert.AreEqual(CommandResult.Success, result);
            Assert.AreEqual(1, s.TurnNumber);
            Assert.AreEqual(Owner.Enemy, s.ActivePlayer);
            Assert.AreEqual(BattleEventKind.TurnEnded, events[0].Kind);
        }

        [Test]
        public void EndTurnCommand_IllegalOnPlayerMismatch()
        {
            var s = MakeStateWithUnit(new GridPos(0, 0));
            var cmd = new EndTurnCommand(1, Owner.Enemy);  // state is Player
            var result = CommandExecutor.Run(s, cmd, out var events);
            Assert.AreEqual(CommandResult.Illegal, result);
            Assert.AreEqual(0, s.TurnNumber);
        }
    }
}