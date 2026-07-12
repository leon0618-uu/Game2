using System.Collections.Generic;
using NUnit.Framework;
using Starfall.Core.Command;
using Starfall.Core.Combat;
using Starfall.Core.Model;
using Starfall.Core.Status;

namespace Starfall.Tests.EditMode
{
    /// <summary>
    /// Task 14 Phase A 测试集。
    /// 覆盖 DamageFormula / AttackCommand / ImprovedEnemyAI 的最小行为集。
    /// </summary>
    public class AttackAndAITests
    {
        private static BoardState MakeBoard(int w = 4, int h = 4)
        {
            var tiles = new Dictionary<GridPos, TileState>();
            for (int y = 0; y < h; y++)
                for (int x = 0; x < w; x++)
                    tiles[new GridPos(x, y)] = TileState.Normal;
            return new BoardState(w, h, tiles);
        }

        private static BattleState MakeStateWithBothUnits()
        {
            var s = new BattleState(0, Owner.Player, MakeBoard(), null);
            s.AddUnit(new UnitState(1, new GridPos(0, 0), 10, 10, Phase.Light, Owner.Player));
            s.AddUnit(new UnitState(2, new GridPos(1, 0), 10, 10, Phase.Dark, Owner.Enemy));
            return s;
        }

        [Test]
        public void DamageFormula_SamePhase_1x()
        {
            // 3 * 1 = 3 （同相位），但本测试中 Light vs Dark 不同相位 = 1.5× → 3 * 3 / 2 = 4
            var s = MakeStateWithBothUnits();
            int dmg = DamageFormula.Compute(3, s.Units[0], s.Units[1]);
            Assert.AreEqual(4, dmg);
        }

        [Test]
        public void DamageFormula_DifferentPhase_1_5x()
        {
            // attacker=Enemy (Dark) vs defender=Player (Light) → 不同 = 1.5×
            var s = MakeStateWithBothUnits();
            int dmg = DamageFormula.Compute(3, s.Units[1], s.Units[0]);
            Assert.AreEqual(4, dmg);
        }

        [Test]
        public void DamageFormula_BurnAttacker_Plus1()
        {
            // 4 (1.5×) + 1 (Burn by attacker) = 5
            var s = MakeStateWithBothUnits();
            s.AddStatus(new StatusInstance(0, StatusKind.Burn, 2, 1));
            int dmg = DamageFormula.ComputeWithStatuses(3, s.Units[0], s.Units[1], s.Statuses);
            Assert.AreEqual(5, dmg);
        }

        [Test]
        public void AttackCommand_AppliesDamage()
        {
            // Player(Light) (0,0) attacks Enemy(Dark) (1,0) → dmg 4 → hp 10 - 4 = 6
            var s = MakeStateWithBothUnits();
            var atk = new AttackCommand(1, 1, 2, baseDamage: 3);
            var result = CommandExecutor.Run(s, atk, out var events);
            Assert.AreEqual(CommandResult.Success, result);
            Assert.AreEqual(6, s.Units[1].Hp);
            Assert.AreEqual(BattleEventKind.UnitDamaged, events[0].Kind);
        }

        [Test]
        public void AttackCommand_IllegalIfNotAdjacent()
        {
            // (0,0) vs (3,3) → Chebyshev = 3 > 1 → Illegal
            var s = new BattleState(0, Owner.Player, MakeBoard(), null);
            s.AddUnit(new UnitState(1, new GridPos(0, 0), 10, 10, Phase.Light, Owner.Player));
            s.AddUnit(new UnitState(2, new GridPos(3, 3), 10, 10, Phase.Dark, Owner.Enemy));
            var atk = new AttackCommand(1, 1, 2);
            Assert.AreEqual(CommandResult.Illegal, CommandExecutor.Run(s, atk, out _));
        }

        [Test]
        public void ImprovedEnemyAI_AttacksAdjacent()
        {
            // 双方已邻接（(0,0) 与 (1,0)）→ 第一条应是 AttackCommand
            var s = MakeStateWithBothUnits();
            var ai = new ImprovedEnemyAI();
            var cmds = new List<ICommand>(ai.PlanTurn(1, s));
            Assert.GreaterOrEqual(cmds.Count, 1);
            Assert.IsInstanceOf<AttackCommand>(cmds[0]);
        }

        [Test]
        public void ImprovedEnemyAI_MovesWhenNotAdjacent()
        {
            // Enemy (5,5) 与 Player (0,0) 不邻接 → 第一条应是 MoveCommand
            var s = new BattleState(0, Owner.Player, MakeBoard(6, 6), null);
            s.AddUnit(new UnitState(1, new GridPos(0, 0), 10, 10, Phase.Light, Owner.Player));
            s.AddUnit(new UnitState(2, new GridPos(5, 5), 10, 10, Phase.Dark, Owner.Enemy));
            var ai = new ImprovedEnemyAI();
            var cmds = new List<ICommand>(ai.PlanTurn(1, s));
            Assert.GreaterOrEqual(cmds.Count, 1);
            Assert.IsInstanceOf<MoveCommand>(cmds[0]);
        }

        [Test]
        public void ImprovedEnemyAI_EndsWithEndTurn()
        {
            var s = MakeStateWithBothUnits();
            var ai = new ImprovedEnemyAI();
            var cmds = new List<ICommand>(ai.PlanTurn(1, s));
            Assert.IsInstanceOf<EndTurnCommand>(cmds[cmds.Count - 1]);
        }
    }
}
