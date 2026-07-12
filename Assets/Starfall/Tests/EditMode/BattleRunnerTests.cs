using System.Collections.Generic;
using NUnit.Framework;
using Starfall.Core.Combat;
using Starfall.Core.Command;
using Starfall.Core.Model;

namespace Starfall.Tests.EditMode
{
    /// <summary>
    /// BattleRunner / WinConditionChecker / EventSink EditMode 测试集。
    /// 覆盖：胜负判定 4 例、Submit 1 例、EndTurn 闭环 1 例、Outcome 锁定 2 例、EventSink 1 例。
    /// 不依赖 UnityEngine.Random / 系统时间 —— 全部确定性。
    /// </summary>
    public class BattleRunnerTests
    {
        private static BoardState MakeBoard(int w = 4, int h = 4)
        {
            var tiles = new Dictionary<GridPos, TileState>();
            for (int y = 0; y < h; y++)
                for (int x = 0; x < w; x++)
                    tiles[new GridPos(x, y)] = TileState.Normal;
            return new BoardState(w, h, tiles);
        }

        private static BattleState MakeStateWith2Units()
        {
            var s = new BattleState(0, Owner.Player, MakeBoard(), null);
            s.AddUnit(new UnitState(1, new GridPos(0, 0), 10, 10, Phase.Light, Owner.Player));
            s.AddUnit(new UnitState(2, new GridPos(3, 3), 10, 10, Phase.Dark, Owner.Enemy));
            return s;
        }

        [Test]
        public void WinCondition_PlayerWins_WhenEnemyDead()
        {
            var s = MakeStateWith2Units();
            s.Units[1].Hp = 0;
            Assert.AreEqual(BattleOutcome.PlayerWins, WinConditionChecker.Check(s));
        }

        [Test]
        public void WinCondition_EnemyWins_WhenPlayerDead()
        {
            var s = MakeStateWith2Units();
            s.Units[0].Hp = 0;
            Assert.AreEqual(BattleOutcome.EnemyWins, WinConditionChecker.Check(s));
        }

        [Test]
        public void WinCondition_Draw_WhenBothDead()
        {
            var s = MakeStateWith2Units();
            s.Units[0].Hp = 0;
            s.Units[1].Hp = 0;
            Assert.AreEqual(BattleOutcome.Draw, WinConditionChecker.Check(s));
        }

        [Test]
        public void WinCondition_Ongoing_WhenBothAlive()
        {
            var s = MakeStateWith2Units();
            Assert.AreEqual(BattleOutcome.Ongoing, WinConditionChecker.Check(s));
        }

        [Test]
        public void BattleRunner_SubmitMove_AppliesAndEmitsEvent()
        {
            var s = MakeStateWith2Units();
            var runner = new BattleRunner(s);
            var path = new List<GridPos> { new GridPos(0, 0), new GridPos(1, 0) };
            var move = new MoveCommand(1, 1, new GridPos(0, 0), new GridPos(1, 0), path);
            var result = runner.Submit(move);
            Assert.AreEqual(CommandResult.Success, result);
            Assert.AreEqual(1, runner.Events.Events.Count);
            Assert.AreEqual(BattleEventKind.UnitMoved, runner.Events.Events[0].Kind);
            Assert.AreEqual(new GridPos(1, 0), s.Units[0].Pos);
        }

        [Test]
        public void BattleRunner_EndTurn_SwitchesPlayerAndRunsEnemyAI()
        {
            var s = MakeStateWith2Units();
            var runner = new BattleRunner(s);
            var r = runner.EndTurn();
            Assert.AreEqual(CommandResult.Success, r);
            // EndTurn + Tick (no statuses -> 0 events) + Enemy EndTurn(AI) = 2 events minimum
            // 注：TickEndTurnCommand 在无 Status 时不发事件；SimpleEnemyAI 仅返回 EndTurnCommand。
            Assert.GreaterOrEqual(runner.Events.Events.Count, 2);
            // 回 Player
            Assert.AreEqual(Owner.Player, s.ActivePlayer);
            // 双方各 EndTurn 一次，TurnNumber 0→1→2
            Assert.AreEqual(2, s.TurnNumber);
        }

        [Test]
        public void BattleRunner_OutcomeSetAfterPlayerWins()
        {
            var s = MakeStateWith2Units();
            s.Units[1].Hp = 0;
            var runner = new BattleRunner(s);
            Assert.AreEqual(BattleOutcome.PlayerWins, runner.Outcome);
        }

        [Test]
        public void BattleRunner_RejectSubmitAfterOutcome()
        {
            var s = MakeStateWith2Units();
            s.Units[1].Hp = 0;
            var runner = new BattleRunner(s);
            var path = new List<GridPos> { new GridPos(0, 0), new GridPos(1, 0) };
            var move = new MoveCommand(1, 1, new GridPos(0, 0), new GridPos(1, 0), path);
            var result = runner.Submit(move);
            Assert.AreEqual(CommandResult.Illegal, result);
        }

        [Test]
        public void BattleRunner_EventSink_ClearWorks()
        {
            var s = MakeStateWith2Units();
            var runner = new BattleRunner(s);
            runner.EndTurn();
            Assert.Greater(runner.Events.Events.Count, 0);
            runner.Events.Clear();
            Assert.AreEqual(0, runner.Events.Events.Count);
        }
    }
}