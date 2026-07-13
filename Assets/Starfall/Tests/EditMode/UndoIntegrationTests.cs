using System.Collections.Generic;
using NUnit.Framework;
using Starfall.Core.Command;
using Starfall.Core.Combat;
using Starfall.Core.Model;
using Starfall.Core.Undo;

namespace Starfall.Tests.EditMode
{
    /// <summary>
    /// Undo 集成测试（Task 21-B）：验证 <see cref="BattleRunner.RestoreState"/> + <see cref="UndoStack"/>
    /// 链路真正生效。
    ///
    /// 覆盖范围：
    /// - MoveCommand 撤销后单位位置回到原位；
    /// - AttackCommand 撤销后目标 HP 回到原值；
    /// - EndTurn 撤销后 TurnNumber / ActivePlayer 回到原状；
    /// - 多步 Undo 只回退最近一步（保留 Multi-Level Undo 语义）；
    /// - Undo 后 BattleRunner 可以继续正常 Submit / EndTurn（行为可逆）。
    /// - RestoreState 对 null 参数抛 ArgumentNullException（防御性 API）。
    ///
    /// 硬约束（AGENTS.md §10.1）：
    /// - 全部为 EditMode 测试，不引用 UnityEngine；
    /// - 全部确定性（无 Random / 时间 / 线程依赖）；
    /// - 全部基于已存在的 Core 类型（MoveCommand / AttackCommand / EndTurnCommand），
    ///   不复制玩法规则。
    /// </summary>
    public class UndoIntegrationTests
    {
        // ============================================================
        // Helpers
        // ============================================================

        private static BoardState MakeBoard(int w = 4, int h = 4)
        {
            var tiles = new Dictionary<GridPos, TileState>();
            for (int y = 0; y < h; y++)
                for (int x = 0; x < w; x++)
                    tiles[new GridPos(x, y)] = TileState.Normal;
            return new BoardState(w, h, tiles);
        }

        /// <summary>
        /// 构造一个最小可战斗的 BattleState：1 个 Player 在 (0,0)，1 个 Enemy 在 (3,0)。
        /// 距离 3（Chebyshev），可被 1-2 步 Move 接近后再 Attack。
        /// </summary>
        private static BattleState MakeState()
        {
            var s = new BattleState(0, Owner.Player, MakeBoard(), null);
            s.AddUnit(new UnitState(1, new GridPos(0, 0), 10, 10, Phase.Light, Owner.Player));
            s.AddUnit(new UnitState(2, new GridPos(3, 0), 10, 10, Phase.Dark, Owner.Enemy));
            return s;
        }

        private static BattleRunner MakeRunner() => new BattleRunner(MakeState());

        // ============================================================
        // 1. Undo MoveCommand restores position
        // ============================================================

        [Test]
        public void Undo_MoveCommand_RestoresPreviousState()
        {
            var runner = MakeRunner();
            var undo = new UndoStack();
            var startHash = runner.State.PostStateHash;
            var startPos = runner.State.Units[0].Pos;

            // 提交 Move 前先 Snapshot（与 InputController.Apply 顺序一致）
            undo.Push(runner.State);
            var path = new List<GridPos> { new GridPos(0, 0), new GridPos(1, 0) };
            var move = new MoveCommand(1, 1, new GridPos(0, 0), new GridPos(1, 0), path);
            var result = runner.Submit(move);
            Assert.AreEqual(CommandResult.Success, result);
            Assert.AreEqual(new GridPos(1, 0), runner.State.Units[0].Pos);
            Assert.AreNotEqual(startHash, runner.State.PostStateHash);

            // Undo
            Assert.IsTrue(undo.TryUndo(out var prev));
            Assert.IsNotNull(prev);
            runner.RestoreState(prev);

            // 位置 + 状态哈希都应回到起点
            Assert.AreEqual(startPos, runner.State.Units[0].Pos);
            Assert.AreEqual(startHash, runner.State.PostStateHash);
        }

        // ============================================================
        // 2. Undo AttackCommand restores HP
        // ============================================================

        [Test]
        public void Undo_AttackCommand_RestoresHP()
        {
            var runner = MakeRunner();
            var undo = new UndoStack();

            // 把 Player 移到 (2,0) 与 Enemy 相邻（距离 1）
            var path = new List<GridPos> { new GridPos(0, 0), new GridPos(1, 0), new GridPos(2, 0) };
            runner.Submit(new MoveCommand(1, 1, new GridPos(0, 0), new GridPos(2, 0), path));
            Assert.AreEqual(new GridPos(2, 0), runner.State.Units[0].Pos);

            var hpBefore = runner.State.Units[1].Hp;
            var startHash = runner.State.PostStateHash;

            // 攻击前 Snapshot
            undo.Push(runner.State);
            var attack = new AttackCommand(2, 1, 2, baseDamage: 3);
            var result = runner.Submit(attack);
            Assert.AreEqual(CommandResult.Success, result);
            // 实际伤害由 DamageFormula 决定：Light vs Dark = 1.5x → 3*3/2 = 4（无 Burn 加成）。
            // 显式用公式算期望值，避免硬编码被未来公式调整影响。
            int expectedDamage = DamageFormula.ComputeWithStatuses(
                3, runner.State.Units[0], runner.State.Units[1], runner.State.Statuses);
            Assert.AreEqual(hpBefore - expectedDamage, runner.State.Units[1].Hp);
            Assert.AreNotEqual(startHash, runner.State.PostStateHash);

            // Undo
            Assert.IsTrue(undo.TryUndo(out var prev));
            runner.RestoreState(prev);

            Assert.AreEqual(hpBefore, runner.State.Units[1].Hp);
            Assert.AreEqual(startHash, runner.State.PostStateHash);
        }

        // ============================================================
        // 3. Undo EndTurn restores turn number + active player
        // ============================================================

        [Test]
        public void Undo_EndTurn_RestoresTurnNumberAndActivePlayer()
        {
            var runner = MakeRunner();
            var undo = new UndoStack();
            var turnBefore = runner.State.TurnNumber;
            var activeBefore = runner.State.ActivePlayer;
            var hashBefore = runner.State.PostStateHash;

            undo.Push(runner.State);
            var r = runner.EndTurn();
            Assert.AreEqual(CommandResult.Success, r);
            // EndTurn：玩家 EndTurn + Tick + Enemy EndTurn(AI) → TurnNumber +2
            Assert.AreEqual(turnBefore + 2, runner.State.TurnNumber);
            Assert.AreNotEqual(hashBefore, runner.State.PostStateHash);

            Assert.IsTrue(undo.TryUndo(out var prev));
            runner.RestoreState(prev);

            Assert.AreEqual(turnBefore, runner.State.TurnNumber);
            Assert.AreEqual(activeBefore, runner.State.ActivePlayer);
            Assert.AreEqual(hashBefore, runner.State.PostStateHash);
        }

        // ============================================================
        // 4. Multi-level Undo: 2 moves, 2 undos, each step reverts independently
        // ============================================================

        [Test]
        public void Undo_MultiLevel_OnlyLastStepReverts()
        {
            var runner = MakeRunner();
            var undo = new UndoStack();
            var originPos = runner.State.Units[0].Pos;

            // Step 1: Move (0,0) → (1,0)
            undo.Push(runner.State);
            var p1 = new List<GridPos> { new GridPos(0, 0), new GridPos(1, 0) };
            runner.Submit(new MoveCommand(1, 1, new GridPos(0, 0), new GridPos(1, 0), p1));
            Assert.AreEqual(new GridPos(1, 0), runner.State.Units[0].Pos);
            var after1Hash = runner.State.PostStateHash;

            // Step 2: Move (1,0) → (2,0)
            undo.Push(runner.State);
            var p2 = new List<GridPos> { new GridPos(1, 0), new GridPos(2, 0) };
            runner.Submit(new MoveCommand(2, 1, new GridPos(1, 0), new GridPos(2, 0), p2));
            Assert.AreEqual(new GridPos(2, 0), runner.State.Units[0].Pos);
            var after2Hash = runner.State.PostStateHash;
            Assert.AreNotEqual(after1Hash, after2Hash);

            // Undo #1: 回到 Step 1 后的状态
            Assert.IsTrue(undo.TryUndo(out var prev2));
            runner.RestoreState(prev2);
            Assert.AreEqual(new GridPos(1, 0), runner.State.Units[0].Pos);
            Assert.AreEqual(after1Hash, runner.State.PostStateHash);

            // Undo #2: 回到起点
            Assert.IsTrue(undo.TryUndo(out var prev1));
            runner.RestoreState(prev1);
            Assert.AreEqual(originPos, runner.State.Units[0].Pos);

            // Undo #3: 栈空
            Assert.IsFalse(undo.TryUndo(out _));
        }

        // ============================================================
        // 5. After Undo the runner keeps working (behaviour is reversible)
        // ============================================================

        [Test]
        public void Undo_ThenNewSubmit_BehavesLikeFreshRunner()
        {
            var runner = MakeRunner();
            var undo = new UndoStack();
            var startHash = runner.State.PostStateHash;

            // 一次 Move → Undo
            undo.Push(runner.State);
            var p = new List<GridPos> { new GridPos(0, 0), new GridPos(1, 0) };
            runner.Submit(new MoveCommand(1, 1, new GridPos(0, 0), new GridPos(1, 0), p));
            Assert.IsTrue(undo.TryUndo(out var prev));
            runner.RestoreState(prev);
            Assert.AreEqual(startHash, runner.State.PostStateHash);

            // Undo 后再次 Move，应正常生效（不可逆性：Undo 不应把 Runner 锁死）
            var p2 = new List<GridPos> { new GridPos(0, 0), new GridPos(1, 0) };
            var r2 = runner.Submit(new MoveCommand(2, 1, new GridPos(0, 0), new GridPos(1, 0), p2));
            Assert.AreEqual(CommandResult.Success, r2);
            Assert.AreEqual(new GridPos(1, 0), runner.State.Units[0].Pos);
        }

        // ============================================================
        // 6. RestoreState clears the EventSink
        // ============================================================

        [Test]
        public void RestoreState_ClearsEventSink()
        {
            var runner = MakeRunner();
            var path = new List<GridPos> { new GridPos(0, 0), new GridPos(1, 0) };
            runner.Submit(new MoveCommand(1, 1, new GridPos(0, 0), new GridPos(1, 0), path));
            Assert.Greater(runner.Events.Events.Count, 0, "Submit should have emitted at least 1 event");

            // 用初始 BattleState 的另一个 Runner 的快照来 Restore
            var freshSnapshot = BattleStateCloner.Clone(MakeState());
            runner.RestoreState(freshSnapshot);

            Assert.AreEqual(0, runner.Events.Events.Count, "RestoreState must clear EventSink");
        }

        // ============================================================
        // 7. RestoreState null guard
        // ============================================================

        [Test]
        public void RestoreState_NullSnapshot_Throws()
        {
            var runner = MakeRunner();
            Assert.Throws<System.ArgumentNullException>(() => runner.RestoreState(null));
        }

        // ============================================================
        // 8. RestoreState recomputes Outcome (e.g. revive dead enemy)
        // ============================================================

        [Test]
        public void RestoreState_RecomputesOutcome()
        {
            var runner = MakeRunner();
            // 击杀 Enemy
            runner.State.Units[1].Hp = 0;
            // 直接构造一个新 Runner 模拟"先前 Ongoing 状态的 BattleState"
            var ongoingSnapshot = MakeState();
            Assert.AreEqual(BattleOutcome.Ongoing, WinConditionChecker.Check(ongoingSnapshot));
            runner.RestoreState(ongoingSnapshot);
            Assert.AreEqual(BattleOutcome.Ongoing, runner.Outcome);
        }
    }
}
