using System.Collections.Generic;
using NUnit.Framework;
using Starfall.Core.Command;
using Starfall.Core.Model;
using Starfall.Core.Rules;
using Starfall.Core.Status;

namespace Starfall.Tests.EditMode
{
    public class RulesTests
    {
        private static BoardState MakeBoard(int w = 4, int h = 4)
        {
            var tiles = new Dictionary<GridPos, TileState>();
            for (int y = 0; y < h; y++)
                for (int x = 0; x < w; x++)
                    tiles[new GridPos(x, y)] = TileState.Normal;
            return new BoardState(w, h, tiles);
        }

        private static BattleState MakeState()
        {
            var s = new BattleState(0, Owner.Player, MakeBoard(), null);
            s.AddUnit(new UnitState(1, new GridPos(0, 0), 10, 10, Phase.Light, Owner.Player));
            return s;
        }

        [Test]
        public void FallingCommand_ReducesHp()
        {
            var s = MakeState();
            var fall = new FallingCommand(1, 1, fallDamage: 3);
            var result = CommandExecutor.Run(s, fall, out var events);
            Assert.AreEqual(CommandResult.Success, result);
            Assert.AreEqual(7, s.Units[0].Hp);
            Assert.AreEqual(BattleEventKind.UnitDamaged, events[0].Kind);
        }

        [Test]
        public void FallingCommand_IllegalOnMissingUnit()
        {
            var s = MakeState();
            var fall = new FallingCommand(1, 999);
            Assert.AreEqual(CommandResult.Illegal, CommandExecutor.Run(s, fall, out _));
        }

        [Test]
        public void CrushResolver_DetectsAndDamages()
        {
            var s = MakeState();
            s.AddUnit(new UnitState(2, new GridPos(0, 0), 5, 5, Phase.Dark, Owner.Enemy));
            var outcome = CrushResolver.DetectAndApply(s, damagePerUnit: 1);
            Assert.IsTrue(outcome.CrushDetected);
            Assert.AreEqual(2, outcome.AffectedUnitIds.Count);
            Assert.AreEqual(9, s.Units[0].Hp);
            Assert.AreEqual(4, s.Units[1].Hp);
        }

        [Test]
        public void CrushResolver_NoCrushIfSeparated()
        {
            var s = MakeState();
            s.AddUnit(new UnitState(2, new GridPos(3, 3), 5, 5, Phase.Dark, Owner.Enemy));
            var outcome = CrushResolver.DetectAndApply(s, damagePerUnit: 1);
            Assert.IsFalse(outcome.CrushDetected);
            Assert.AreEqual(10, s.Units[0].Hp);
            Assert.AreEqual(5, s.Units[1].Hp);
        }

        [Test]
        public void PhaseFlipValidator_BlocksDoubleFlip()
        {
            var s = MakeState();
            var firstFlip = new ApplyStatusCommand(1, 1, StatusKind.PhaseInvert, 3, 1);
            CommandExecutor.Run(s, firstFlip, out _);
            Assert.IsFalse(PhaseFlipValidator.CanFlipPhase(s, 1));

            var secondFlip = new ApplyStatusCommand(2, 1, StatusKind.PhaseInvert, 3, 1);
            Assert.AreEqual(CommandResult.Illegal, CommandExecutor.Run(s, secondFlip, out _));
        }

        [Test]
        public void PhaseFlipValidator_AllowsAfterExpiry()
        {
            var s = MakeState();
            var firstFlip = new ApplyStatusCommand(1, 1, StatusKind.PhaseInvert, 1, 1);
            CommandExecutor.Run(s, firstFlip, out _);
            // 强制设为 0 模拟已过期
            s.Statuses[0].RemainingTurns = 0;
            Assert.IsTrue(PhaseFlipValidator.CanFlipPhase(s, 1));
        }

        [Test]
        public void CrushResolver_SkipsDeadUnits()
        {
            var s = MakeState();
            s.AddUnit(new UnitState(2, new GridPos(0, 0), 0, 5, Phase.Dark, Owner.Enemy));
            var outcome = CrushResolver.DetectAndApply(s, damagePerUnit: 1);
            Assert.IsFalse(outcome.CrushDetected);
        }
    }
}