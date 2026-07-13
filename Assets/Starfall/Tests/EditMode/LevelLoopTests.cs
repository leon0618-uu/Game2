using System.Collections.Generic;
using NUnit.Framework;
using Starfall.Core.Combat;
using Starfall.Core.Command;
using Starfall.Core.Model;

namespace Starfall.Tests.EditMode
{
    /// <summary>
    /// Task 19 关卡闭环（Guard → Retreat）EditMode 测试集。
    /// <para/>
    /// 覆盖：
    /// <list type="number">
    /// <item>GuardsCompleted 在 Guard 阶段累加正确。</item>
    /// <item>达到 GuardsRequired 自动切 Retreat。</item>
    /// <item>任意阶段 Player 全灭 → EnemyWins。</item>
    /// <item>任意阶段 Enemy 全灭 → PlayerWins。</item>
    /// <item>Retreat 阶段所有 Player 到达 ExitTile 邻接 → PlayerWins。</item>
    /// <item>确定性：相同初始 + 相同 commands → 相同 Outcome + GuardsCompleted。</item>
    /// </list>
    /// <para/>
    /// 不依赖 UnityEngine / 时间 / 线程 —— 全部确定性。
    /// </summary>
    public class LevelLoopTests
    {
        private static BoardState MakeBoard(int w = 4, int h = 4)
        {
            var tiles = new Dictionary<GridPos, TileState>();
            for (int y = 0; y < h; y++)
                for (int x = 0; x < w; x++)
                    tiles[new GridPos(x, y)] = TileState.Normal;
            return new BoardState(w, h, tiles);
        }

        private static BattleState MakeMixedState(int guardsRequired = 1)
        {
            var s = new BattleState(0, Owner.Player, MakeBoard(), null)
            {
                GuardsRequired = guardsRequired,
            };
            s.AddUnit(new UnitState(1, new GridPos(0, 0), 10, 10, Phase.Light, Owner.Player));
            s.AddUnit(new UnitState(2, new GridPos(3, 3), 5, 5, Phase.Dark, Owner.Enemy));
            return s;
        }

        [Test]
        public void GuardsCompleted_IncrementsByExactlyOnePerUpdate()
        {
            var s = MakeMixedState(guardsRequired: 5);
            Assert.AreEqual(ObjectivePhase.Guard, s.CurrentPhase);
            Assert.AreEqual(0, s.GuardsCompleted);

            var (outcome, advanced, retreated) = ObjectivePhaseUpdater.Update(s);
            Assert.AreEqual(BattleOutcome.Ongoing, outcome);
            Assert.IsFalse(advanced);
            Assert.IsFalse(retreated);
            Assert.AreEqual(1, s.GuardsCompleted);
            Assert.AreEqual(ObjectivePhase.Guard, s.CurrentPhase);

            ObjectivePhaseUpdater.Update(s);
            ObjectivePhaseUpdater.Update(s);
            Assert.AreEqual(3, s.GuardsCompleted);
            Assert.AreEqual(ObjectivePhase.Guard, s.CurrentPhase);
        }

        [Test]
        public void GuardsCompleted_ReachesRequired_AdvancesToRetreat()
        {
            var s = MakeMixedState(guardsRequired: 2);
            ObjectivePhaseUpdater.Update(s);  // GuardsCompleted 1
            Assert.AreEqual(ObjectivePhase.Guard, s.CurrentPhase);

            var (outcome, advanced, retreated) = ObjectivePhaseUpdater.Update(s);
            Assert.AreEqual(BattleOutcome.Ongoing, outcome);
            Assert.IsTrue(advanced, "应推进到 Retreat 阶段");
            Assert.IsFalse(retreated);
            Assert.AreEqual(ObjectivePhase.Retreat, s.CurrentPhase);
            Assert.AreEqual(2, s.GuardsCompleted);
        }

        [Test]
        public void GuardPhase_PlayerDead_EnemyWins_AndEnded()
        {
            var s = MakeMixedState(guardsRequired: 5);
            s.Units[0].Hp = 0;  // Player 单位死
            var (outcome, advanced, retreated) = ObjectivePhaseUpdater.Update(s);
            Assert.AreEqual(BattleOutcome.EnemyWins, outcome);
            Assert.IsFalse(advanced);
            Assert.IsFalse(retreated);
            Assert.AreEqual(ObjectivePhase.Ended, s.CurrentPhase);
        }

        [Test]
        public void GuardPhase_EnemyDead_PlayerWins_AndEnded()
        {
            var s = MakeMixedState(guardsRequired: 5);
            s.Units[1].Hp = 0;  // Enemy 单位死
            var (outcome, advanced, retreated) = ObjectivePhaseUpdater.Update(s);
            Assert.AreEqual(BattleOutcome.PlayerWins, outcome);
            Assert.IsFalse(advanced);
            Assert.IsFalse(retreated);
            Assert.AreEqual(ObjectivePhase.Ended, s.CurrentPhase);
        }

        [Test]
        public void RetreatPhase_AllPlayersAdjacentToExit_PlayerWins()
        {
            // 4x4 board, ExitTile = (1, 1)；4 邻居 = (1,2), (0,1), (2,1), (1,0)
            var s = MakeMixedState(guardsRequired: 1);
            s.ExitTile = new GridPos(1, 1);
            // 先到达 GuardRequired
            ObjectivePhaseUpdater.Update(s);
            Assert.AreEqual(ObjectivePhase.Retreat, s.CurrentPhase);

            // 把 Player 移到 (1, 2) —— ExitTile 的"下"邻居
            s.Units[0].Pos = new GridPos(1, 2);
            // 但 Enemy 也存在 -> WinConditionChecker 仍然 Ongoing
            var (outcome, advanced, retreated) = ObjectivePhaseUpdater.Update(s);
            Assert.AreEqual(BattleOutcome.PlayerWins, outcome, "Retreat + 邻接撤离 → PlayerWins 即使有 Enemy");
            Assert.IsTrue(retreated);
            Assert.AreEqual(ObjectivePhase.Ended, s.CurrentPhase);
        }

        [Test]
        public void RetreatPhase_PlayerDead_EnemyWins()
        {
            var s = MakeMixedState(guardsRequired: 1);
            s.ExitTile = new GridPos(1, 1);
            ObjectivePhaseUpdater.Update(s);  // → Retreat
            s.Units[0].Hp = 0;  // Player 单位死
            var (outcome, advanced, retreated) = ObjectivePhaseUpdater.Update(s);
            Assert.AreEqual(BattleOutcome.EnemyWins, outcome);
            Assert.IsFalse(advanced);
            Assert.IsFalse(retreated);
            Assert.AreEqual(ObjectivePhase.Ended, s.CurrentPhase);
        }

        [Test]
        public void RetreatPhase_NotAllPlayersAdjacent_NoWin()
        {
            var s = MakeMixedState(guardsRequired: 1);
            s.ExitTile = new GridPos(1, 1);
            ObjectivePhaseUpdater.Update(s);  // → Retreat
            // Player 在 (0,0) —— 不是 ExitTile 邻居
            s.Units[0].Pos = new GridPos(0, 0);
            var (outcome, advanced, retreated) = ObjectivePhaseUpdater.Update(s);
            Assert.AreEqual(BattleOutcome.Ongoing, outcome, "未抵达撤离点 → 不算完成");
            Assert.IsFalse(retreated);
            Assert.AreEqual(ObjectivePhase.Retreat, s.CurrentPhase, "未完成撤离应保持在 Retreat");
        }

        [Test]
        public void Determinism_SameInputs_SameOutcomeAndGuards()
        {
            // 构造两个相同初始状态的 BattleRunner 跑同样的 EndTurn
            var s1 = MakeMixedState(guardsRequired: 1);
            var r1 = new BattleRunner(s1);
            var s2 = MakeMixedState(guardsRequired: 1);
            var r2 = new BattleRunner(s2);

            r1.EndTurn();
            r2.EndTurn();

            Assert.AreEqual(s1.CurrentPhase, s2.CurrentPhase, "CurrentPhase 必须一致");
            Assert.AreEqual(s1.GuardsCompleted, s2.GuardsCompleted, "GuardsCompleted 必须一致");
            Assert.AreEqual(r1.Outcome, r2.Outcome, "Outcome 必须一致");
            Assert.AreEqual(s1.PostStateHash, s2.PostStateHash, "PostStateHash 必须一致");
        }

        [Test]
        public void BattleRunner_EndTurn_AdvancesPhaseAndEmitsObjectiveAdvancedEvent()
        {
            var s = MakeMixedState(guardsRequired: 1);
            var runner = new BattleRunner(s);

            // 玩家 EndTurn（玩家 EndTurn → Enemy EndTurn AI 都会触发）
            // EffectiveGuardsCompleted 推进一次 → 达到门槛 → 切 Retreat（在 Enemy EndTurn 末尾）
            runner.EndTurn();

            Assert.AreEqual(ObjectivePhase.Retreat, s.CurrentPhase, "应进入 Retreat");
            // 检查 Events 中至少有一个 ObjectiveAdvanced
            bool foundAdvanced = false;
            for (int i = 0; i < runner.Events.Events.Count; i++)
                if (runner.Events.Events[i].Kind == BattleEventKind.ObjectiveAdvanced) { foundAdvanced = true; break; }
            Assert.IsTrue(foundAdvanced, "应发出 BattleEventKind.ObjectiveAdvanced 事件");
        }

        [Test]
        public void BattleState_Cloner_PreservesNewFields()
        {
            var s = MakeMixedState(guardsRequired: 5);
            s.CurrentPhase = ObjectivePhase.Retreat;
            s.GuardsCompleted = 2;
            s.ExitTile = new GridPos(2, 2);

            var clone = BattleStateCloner.Clone(s);
            Assert.AreEqual(s.CurrentPhase, clone.CurrentPhase);
            Assert.AreEqual(s.GuardsCompleted, clone.GuardsCompleted);
            Assert.AreEqual(s.GuardsRequired, clone.GuardsRequired);
            Assert.IsTrue(clone.ExitTile.HasValue);
            Assert.AreEqual(2, clone.ExitTile.Value.X);
            Assert.AreEqual(2, clone.ExitTile.Value.Y);

            // 修改原 state 的 ExitTile 不应影响 clone（深拷贝独立性）
            s.ExitTile = new GridPos(9, 9);
            Assert.AreEqual(2, clone.ExitTile.Value.X);

            // 修改 GuardsCompleted 不应影响 clone
            s.GuardsCompleted = 99;
            Assert.AreEqual(2, clone.GuardsCompleted);
        }

        [Test]
        public void BattleState_Comparer_DetectsFieldDifferences()
        {
            var s1 = MakeMixedState(guardsRequired: 2);
            var s2 = MakeMixedState(guardsRequired: 2);
            Assert.IsTrue(BattleStateComparer.Equals(s1, s2), "相同初始应相等");

            s2.GuardsCompleted = 1;
            Assert.IsFalse(BattleStateComparer.Equals(s1, s2), "GuardsCompleted 不同应不相等");
            s2.GuardsCompleted = 0;

            s2.CurrentPhase = ObjectivePhase.Retreat;
            Assert.IsFalse(BattleStateComparer.Equals(s1, s2), "CurrentPhase 不同应不相等");
            s2.CurrentPhase = ObjectivePhase.Guard;

            s2.ExitTile = new GridPos(0, 0);
            Assert.IsFalse(BattleStateComparer.Equals(s1, s2), "ExitTile 不同应不相等");
        }

        [Test]
        public void PostStateHash_ChangesWithGuardFields()
        {
            var s1 = MakeMixedState(guardsRequired: 2);
            var h1 = s1.PostStateHash;
            s1.GuardsCompleted = 1;
            var h2 = s1.PostStateHash;
            Assert.AreNotEqual(h1, h2, "GuardsCompleted 不同 → Hash 不同");

            s1.GuardsCompleted = 0;
            s1.GuardsRequired = 5;
            Assert.AreNotEqual(h1, s1.PostStateHash, "GuardsRequired 不同 → Hash 不同");

            s1.GuardsRequired = 2;
            s1.ExitTile = new GridPos(1, 1);
            Assert.AreNotEqual(h1, s1.PostStateHash, "ExitTile 不同 → Hash 不同");
        }
    }
}
