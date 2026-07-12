using System.Collections.Generic;
using NUnit.Framework;
using Starfall.Core.Command;
using Starfall.Core.Model;
using Starfall.Core.Status;

namespace Starfall.Tests.EditMode
{
    public class StatusSystemTests
    {
        private static BoardState MakeBoard(int w = 4, int h = 4)
        {
            var tiles = new Dictionary<GridPos, TileState>();
            for (int y = 0; y < h; y++)
                for (int x = 0; x < w; x++)
                    tiles[new GridPos(x, y)] = TileState.Normal;
            return new BoardState(w, h, tiles);
        }

        private static BattleState MakeState(int unitId = 1, GridPos? pos = null)
        {
            var s = new BattleState(0, Owner.Player, MakeBoard(), null);
            s.AddUnit(new UnitState(unitId, pos ?? new GridPos(0, 0), 10, 10, Phase.Light, Owner.Player));
            return s;
        }

        [Test]
        public void ApplyStatusCommand_AddsStatus()
        {
            var s = MakeState();
            var cmd = new ApplyStatusCommand(1, 1, StatusKind.Burn, 3, 1);
            var result = CommandExecutor.Run(s, cmd, out var evs);
            Assert.AreEqual(CommandResult.Success, result);
            Assert.AreEqual(1, s.Statuses.Count);
            Assert.AreEqual(StatusKind.Burn, s.Statuses[0].Kind);
            Assert.AreEqual(3, s.Statuses[0].RemainingTurns);
            Assert.AreEqual(0, s.Statuses[0].InstanceId);
            Assert.AreEqual(1, s.NextStatusInstanceId);
        }

        [Test]
        public void ApplyStatusCommand_IllegalOnMissingUnit()
        {
            var s = MakeState();
            var cmd = new ApplyStatusCommand(1, 999, StatusKind.Burn, 3, 1);
            var result = CommandExecutor.Run(s, cmd, out _);
            Assert.AreEqual(CommandResult.Illegal, result);
            Assert.AreEqual(0, s.Statuses.Count);
        }

        [Test]
        public void RemoveStatusCommand_RemovesByInstanceId()
        {
            var s = MakeState();
            var add = new ApplyStatusCommand(1, 1, StatusKind.Burn, 3, 1);
            CommandExecutor.Run(s, add, out _);
            var rm = new RemoveStatusCommand(2, 0);
            var result = CommandExecutor.Run(s, rm, out _);
            Assert.AreEqual(CommandResult.Success, result);
            Assert.AreEqual(0, s.Statuses.Count);
        }

        [Test]
        public void TickEndTurnCommand_DecrementsRemaining()
        {
            var s = MakeState();
            new ApplyStatusCommand(1, 1, StatusKind.Burn, 3, 1).Execute(s, out _);
            new TickEndTurnCommand(2).Execute(s, out _);
            Assert.AreEqual(2, s.Statuses[0].RemainingTurns);
        }

        [Test]
        public void TickEndTurnCommand_RemovesExpiredStatus()
        {
            var s = MakeState();
            new ApplyStatusCommand(1, 1, StatusKind.Burn, 1, 1).Execute(s, out _);
            new TickEndTurnCommand(2).Execute(s, out _);
            Assert.AreEqual(0, s.Statuses.Count);
        }

        [Test]
        public void TickEndTurnCommand_BurnDealsOneDamage()
        {
            var s = MakeState();
            new ApplyStatusCommand(1, 1, StatusKind.Burn, 2, 1).Execute(s, out _);
            new TickEndTurnCommand(2).Execute(s, out _);
            Assert.AreEqual(9, s.Units[0].Hp);
        }

        [Test]
        public void TickEndTurnCommand_PhaseInvertFlipsPhase()
        {
            var s = MakeState();
            Assert.AreEqual(Phase.Light, s.Units[0].Phase);
            new ApplyStatusCommand(1, 1, StatusKind.PhaseInvert, 2, 1).Execute(s, out _);
            new TickEndTurnCommand(2).Execute(s, out _);
            Assert.AreEqual(Phase.Dark, s.Units[0].Phase);
        }

        [Test]
        public void MoveCommand_IllegalWhenRooted()
        {
            var s = MakeState();
            new ApplyStatusCommand(1, 1, StatusKind.Root, 2, 1).Execute(s, out _);
            var path = new List<GridPos> { new GridPos(0, 0), new GridPos(1, 0) };
            var move = new MoveCommand(2, 1, new GridPos(0, 0), new GridPos(1, 0), path);
            var result = CommandExecutor.Run(s, move, out _);
            Assert.AreEqual(CommandResult.Illegal, result);
            Assert.AreEqual(new GridPos(0, 0), s.Units[0].Pos);
        }

        [Test]
        public void StatusInstanceComparer_OrdersByKindThenTurnsThenId()
        {
            var list = new List<StatusInstance>
            {
                new StatusInstance(2, StatusKind.Root, 3, 1),
                new StatusInstance(0, StatusKind.Burn, 2, 1),
                new StatusInstance(1, StatusKind.Burn, 1, 1),
            };
            list.Sort(StatusInstanceComparer.Instance);
            // 期望：Burn(1,id1), Burn(2,id0), Root(3,id2)
            Assert.AreEqual(StatusKind.Burn, list[0].Kind);
            Assert.AreEqual(1, list[0].RemainingTurns);
            Assert.AreEqual(1, list[0].InstanceId);
            Assert.AreEqual(StatusKind.Burn, list[1].Kind);
            Assert.AreEqual(2, list[1].RemainingTurns);
            Assert.AreEqual(0, list[1].InstanceId);
            Assert.AreEqual(StatusKind.Root, list[2].Kind);
        }

        [Test]
        public void BattleState_HashChangesWithStatus()
        {
            var s1 = MakeState();
            ulong h1 = s1.PostStateHash;
            new ApplyStatusCommand(1, 1, StatusKind.Burn, 2, 1).Execute(s1, out _);
            ulong h2 = s1.PostStateHash;
            Assert.AreNotEqual(h1, h2);
        }
    }
}