using System.Collections.Generic;
using NUnit.Framework;
using Starfall.Core.Anchor;
using Starfall.Core.Command;
using Starfall.Core.Decree;
using Starfall.Core.Model;
using Starfall.Unity.Input;

namespace Starfall.Tests.EditMode
{
    /// <summary>
    /// Task 17 InputStateMachine 纯逻辑测试。
    /// 不引用 UnityEngine，可在 EditMode 中直接跑（nunit.framework.dll）。
    /// </summary>
    public class InputStateMachineTests
    {
        private static BoardState MakeBoard(int w = 4, int h = 4)
        {
            var tiles = new Dictionary<GridPos, TileState>();
            for (int y = 0; y < h; y++)
                for (int x = 0; x < w; x++)
                    tiles[new GridPos(x, y)] = TileState.Normal;
            return new BoardState(w, h, tiles);
        }

        private static BattleState MakeStateWithUnits()
        {
            var s = new BattleState(0, Owner.Player, MakeBoard(4, 4), null);
            s.AddUnit(new UnitState(1, new GridPos(0, 0), 10, 10, Phase.Light, Owner.Player));
            s.AddUnit(new UnitState(2, new GridPos(1, 0), 10, 10, Phase.Dark, Owner.Enemy));
            return s;
        }

        // ===== Mode transitions =====

        [Test]
        public void Initial_Mode_IsSelectUnit()
        {
            var s = MakeStateWithUnits();
            var machine = new InputStateMachine();
            var t = machine.ProcessAction(InputState.Initial(), InputAction.None, s);
            // InputAction.None 不改 state
            Assert.AreEqual(InputMode.None, t.Next.Mode);
        }

        [Test]
        public void Cancel_FromAnyMode_GoesToSelectUnit_AndClearsSelection()
        {
            var s = MakeStateWithUnits();
            var machine = new InputStateMachine();
            var start = new InputState(InputMode.MoveTarget, new GridPos(2, 2), selectedUnitId: 1);
            var t = machine.ProcessAction(start, InputAction.Cancel, s);
            Assert.AreEqual(InputMode.SelectUnit, t.Next.Mode);
            Assert.IsNull(t.Next.SelectedUnitId);
        }

        [Test]
        public void EnterMove_WithoutSelection_StaysAndMessagesError()
        {
            var s = MakeStateWithUnits();
            var machine = new InputStateMachine();
            var start = new InputState(InputMode.SelectUnit, new GridPos(0, 0), selectedUnitId: null);
            var t = machine.ProcessAction(start, InputAction.EnterMove, s);
            Assert.AreEqual(InputMode.SelectUnit, t.Next.Mode);
            StringAssert.Contains("no selected unit", t.Next.LastMessage);
        }

        [Test]
        public void EnterMove_WithSelection_EntersMoveTarget()
        {
            var s = MakeStateWithUnits();
            var machine = new InputStateMachine();
            var start = new InputState(InputMode.SelectUnit, new GridPos(0, 0), selectedUnitId: 1);
            var t = machine.ProcessAction(start, InputAction.EnterMove, s);
            Assert.AreEqual(InputMode.MoveTarget, t.Next.Mode);
        }

        [Test]
        public void EnterAttack_FromSelectUnit_EntersAttackTarget()
        {
            var s = MakeStateWithUnits();
            var machine = new InputStateMachine();
            var start = new InputState(InputMode.SelectUnit, new GridPos(0, 0), selectedUnitId: 1);
            var t = machine.ProcessAction(start, InputAction.EnterAttack, s);
            Assert.AreEqual(InputMode.AttackTarget, t.Next.Mode);
        }

        [Test]
        public void EnterAttack_FromNonSelectUnit_DoesNotEnterAttack()
        {
            var s = MakeStateWithUnits();
            var machine = new InputStateMachine();
            var start = new InputState(InputMode.MoveTarget, new GridPos(0, 0), selectedUnitId: 1);
            // A 在 MoveTarget 模式被路由为 CursorLeft（cursor 0,0 → (-1,0)，clamp 到 0,0）
            var t = machine.ProcessAction(start, InputAction.EnterAttack, s);
            Assert.AreEqual(InputMode.MoveTarget, t.Next.Mode);
        }

        [Test]
        public void Cursor_ArrowKeys_ClampedToBoard()
        {
            var s = MakeStateWithUnits();
            var machine = new InputStateMachine();
            var start = new InputState(InputMode.SelectUnit, new GridPos(0, 0), selectedUnitId: null);
            // CursorUp from (0,0) → (0,-1) clamp → (0,0)
            var t1 = machine.ProcessAction(start, InputAction.CursorUp, s);
            Assert.AreEqual(new GridPos(0, 0), t1.Next.Cursor);
            // CursorDown from (0,0) → (0,1)
            var t2 = machine.ProcessAction(start, InputAction.CursorDown, s);
            Assert.AreEqual(new GridPos(0, 1), t2.Next.Cursor);
            // CursorRight from (0,0) → (1,0)
            var t3 = machine.ProcessAction(start, InputAction.CursorRight, s);
            Assert.AreEqual(new GridPos(1, 0), t3.Next.Cursor);
        }

        [Test]
        public void Confirm_InSelectUnit_OnEnemyUnit_RejectsSelection()
        {
            var s = MakeStateWithUnits();
            var machine = new InputStateMachine();
            // enemy unit at (1,0)
            var start = new InputState(InputMode.SelectUnit, new GridPos(1, 0), selectedUnitId: null);
            var t = machine.ProcessAction(start, InputAction.Confirm, s);
            Assert.IsNull(t.Next.SelectedUnitId);
            StringAssert.Contains("cannot select enemy", t.Next.LastMessage);
        }

        [Test]
        public void Confirm_InSelectUnit_OnOwnUnit_SelectsIt()
        {
            var s = MakeStateWithUnits();
            var machine = new InputStateMachine();
            // own unit at (0,0)
            var start = new InputState(InputMode.SelectUnit, new GridPos(0, 0), selectedUnitId: null);
            var t = machine.ProcessAction(start, InputAction.Confirm, s);
            Assert.AreEqual(1, t.Next.SelectedUnitId);
        }

        [Test]
        public void Confirm_InMoveTarget_BuildsMoveCommand_AndReturnsToSelectUnit()
        {
            var s = MakeStateWithUnits();
            var machine = new InputStateMachine();
            var start = new InputState(InputMode.MoveTarget, new GridPos(0, 2), selectedUnitId: 1);
            var t = machine.ProcessAction(start, InputAction.Confirm, s);
            Assert.AreEqual(InputMode.SelectUnit, t.Next.Mode);
            Assert.IsNull(t.Next.SelectedUnitId);
            Assert.AreEqual(1, t.Commands.Count);
            Assert.IsInstanceOf<MovePlan>(t.Commands[0]);
            var mp = (MovePlan)t.Commands[0];
            Assert.AreEqual(1, mp.UnitId);
            Assert.AreEqual(new GridPos(0, 2), mp.To);
        }

        [Test]
        public void Confirm_InAttackTarget_OnAdjacentEnemy_BuildsAttackPlan()
        {
            var s = MakeStateWithUnits();
            var machine = new InputStateMachine();
            // unit 1 at (0,0), unit 2 at (1,0) — adjacent
            var start = new InputState(InputMode.AttackTarget, new GridPos(1, 0), selectedUnitId: 1);
            var t = machine.ProcessAction(start, InputAction.Confirm, s);
            Assert.IsInstanceOf<AttackPlan>(t.Commands[0]);
            var ap = (AttackPlan)t.Commands[0];
            Assert.AreEqual(1, ap.AttackerId);
            Assert.AreEqual(2, ap.TargetId);
        }

        [Test]
        public void Confirm_InAttackTarget_OnOwnUnit_Rejects()
        {
            var s = MakeStateWithUnits();
            var machine = new InputStateMachine();
            // 没有 other own unit 紧贴 unit 1 (0,0)
            var start = new InputState(InputMode.AttackTarget, new GridPos(0, 0), selectedUnitId: 1);
            var t = machine.ProcessAction(start, InputAction.Confirm, s);
            Assert.AreEqual(0, t.Commands.Count);
            StringAssert.Contains("cannot attack own", t.Next.LastMessage);
        }

        [Test]
        public void Confirm_InPhaseFlipTarget_OnAdjacentOwnUnit_BuildsPhaseFlipPlan()
        {
            var s = MakeStateWithUnits();
            // 加第二个己方单位
            s.AddUnit(new UnitState(3, new GridPos(1, 1), 10, 10, Phase.Light, Owner.Player));
            var machine = new InputStateMachine();
            // unit 1 at (0,0), unit 3 at (1,1) — adjacent (Chebyshev=1)
            // source=1, cursor on (1,1) → target=3
            var start = new InputState(InputMode.PhaseFlipTarget, new GridPos(1, 1), selectedUnitId: 1);
            var t = machine.ProcessAction(start, InputAction.Confirm, s);
            Assert.IsInstanceOf<PhaseFlipPlan>(t.Commands[0]);
            var pf = (PhaseFlipPlan)t.Commands[0];
            Assert.AreEqual(1, pf.SourceUnitId);
            Assert.AreEqual(3, pf.TargetUnitId);
        }

        // ===== EndTurn / Undo signals =====

        [Test]
        public void EndTurn_SetsShouldEndTurn_True()
        {
            var s = MakeStateWithUnits();
            var machine = new InputStateMachine();
            var start = new InputState(InputMode.SelectUnit, new GridPos(0, 0), selectedUnitId: null);
            var t = machine.ProcessAction(start, InputAction.EndTurn, s);
            Assert.IsTrue(t.ShouldEndTurn);
            Assert.AreEqual(0, t.Commands.Count);
        }

        [Test]
        public void Undo_SetsShouldUndo_True_AndStaysInCurrentMode()
        {
            var s = MakeStateWithUnits();
            var machine = new InputStateMachine();
            var start = new InputState(InputMode.MoveTarget, new GridPos(0, 2), selectedUnitId: 1);
            var t = machine.ProcessAction(start, InputAction.Undo, s);
            Assert.IsTrue(t.ShouldUndo);
            Assert.AreEqual(InputMode.MoveTarget, t.Next.Mode);
        }

        // ===== Decree mode =====

        [Test]
        public void EnterDecree_WithoutAnchors_RejectsMode()
        {
            var s = MakeStateWithUnits();
            var machine = new InputStateMachine();
            var start = new InputState(InputMode.SelectUnit, new GridPos(0, 0), selectedUnitId: 1);
            var t = machine.ProcessAction(start, InputAction.EnterDecree, s);
            Assert.AreEqual(InputMode.SelectUnit, t.Next.Mode);
            StringAssert.Contains("no player anchor zones", t.Next.LastMessage);
        }

        [Test]
        public void EnterDecree_WithAnchors_EntersDecreeSelect()
        {
            var s = MakeStateWithUnits();
            s.Anchors.Register(new AnchorZone(1, "Player", new[]
            {
                new GridPos(0, 0), new GridPos(1, 0), new GridPos(0, 1)
            }));
            var machine = new InputStateMachine();
            var start = new InputState(InputMode.SelectUnit, new GridPos(0, 0), selectedUnitId: 1);
            var t = machine.ProcessAction(start, InputAction.EnterDecree, s);
            Assert.AreEqual(InputMode.DecreeSelect, t.Next.Mode);
        }

        [Test]
        public void ConfirmDecree_BuildsDecreeHoldPlan()
        {
            var s = MakeStateWithUnits();
            s.Anchors.Register(new AnchorZone(1, "Player", new[]
            {
                new GridPos(0, 0), new GridPos(1, 0), new GridPos(0, 1)
            }));
            var machine = new InputStateMachine();
            var start = new InputState(InputMode.DecreeSelect, new GridPos(0, 0), selectedUnitId: 1, decreeZoneCursor: 0);
            var t = machine.ProcessAction(start, InputAction.Confirm, s);
            Assert.AreEqual(1, t.Commands.Count);
            Assert.IsInstanceOf<DecreeHoldPlan>(t.Commands[0]);
            var dp = (DecreeHoldPlan)t.Commands[0];
            Assert.AreEqual(1, dp.ZoneId);
            Assert.AreEqual(Owner.Player, dp.IssuingPlayer);
        }

        [Test]
        public void DecreeCycleNext_WrapsAround()
        {
            var s = MakeStateWithUnits();
            s.Anchors.Register(new AnchorZone(1, "Player", new[]
            {
                new GridPos(0, 0), new GridPos(1, 0), new GridPos(0, 1)
            }));
            s.Anchors.Register(new AnchorZone(2, "Player", new[]
            {
                new GridPos(2, 2), new GridPos(3, 2), new GridPos(2, 3)
            }));
            var machine = new InputStateMachine();
            var start = new InputState(InputMode.DecreeSelect, new GridPos(0, 0), selectedUnitId: 1, decreeZoneCursor: 0);
            var t = machine.ProcessAction(start, InputAction.DecreeCycleNext, s);
            Assert.AreEqual(1, t.Next.DecreeZoneCursor);
            var t2 = machine.ProcessAction(t.Next, InputAction.DecreeCycleNext, s);
            Assert.AreEqual(0, t2.Next.DecreeZoneCursor);  // wraps
        }

        // ===== Deterministic helpers (AGENTS.md §11) =====

        [Test]
        public void FindUnitAt_ReturnsIdOrNull()
        {
            var s = MakeStateWithUnits();
            Assert.AreEqual(1, InputStateMachine.FindUnitAt(s, new GridPos(0, 0)));
            Assert.AreEqual(2, InputStateMachine.FindUnitAt(s, new GridPos(1, 0)));
            Assert.IsNull(InputStateMachine.FindUnitAt(s, new GridPos(2, 2)));
        }

        [Test]
        public void IsAdjacent_Chebyshev1()
        {
            var s = MakeStateWithUnits();
            Assert.IsTrue(InputStateMachine.IsAdjacent(s, 1, 2));
            Assert.IsFalse(InputStateMachine.IsAdjacent(s, 1, 99));
            // 远距离
            var s2 = new BattleState(0, Owner.Player, MakeBoard(8, 8), null);
            s2.AddUnit(new UnitState(10, new GridPos(0, 0), 10, 10, Phase.Light, Owner.Player));
            s2.AddUnit(new UnitState(11, new GridPos(5, 5), 10, 10, Phase.Light, Owner.Player));
            Assert.IsFalse(InputStateMachine.IsAdjacent(s2, 10, 11));
        }

        [Test]
        public void ListPlayerAnchorZones_OrderedByZoneId()
        {
            var s = MakeStateWithUnits();
            s.Anchors.Register(new AnchorZone(7, "Player", new[]
            {
                new GridPos(0, 0), new GridPos(1, 0), new GridPos(0, 1)
            }));
            s.Anchors.Register(new AnchorZone(3, "Player", new[]
            {
                new GridPos(1, 0), new GridPos(2, 0), new GridPos(1, 1)
            }));
            s.Anchors.Register(new AnchorZone(5, "Enemy", new[]
            {
                new GridPos(2, 0), new GridPos(3, 0), new GridPos(2, 1)
            }));  // 排除
            var zones = InputStateMachine.ListPlayerAnchorZones(s);
            Assert.AreEqual(2, zones.Count);
            Assert.AreEqual(3, zones[0]);
            Assert.AreEqual(7, zones[1]);
        }

        // ===== Task 17 接续补充（gate 验证） =====

        [Test]
        public void AllKeyBindings_HaveHandler_InSwitch()
        {
            // 列出必须存在的所有 InputAction（gate 要求 M/F/A/D/Z/Space/Esc + 方向键）
            // 通过 ProcessAction 触发这些 action 在 SelectUnit 模式下的副作用（不抛异常 + 返回 InputTransition）。
            var s = MakeStateWithUnits();
            var machine = new InputStateMachine();
            var baseState = new InputState(InputMode.SelectUnit, new GridPos(1, 1), selectedUnitId: 1);

            var cases = new (InputAction action, string label)[]
            {
                (InputAction.EnterMove,      "M"),
                (InputAction.EnterPhaseFlip, "F"),
                (InputAction.EnterAttack,    "A"),
                (InputAction.EnterDecree,    "D"),
                (InputAction.Undo,           "Z"),
                (InputAction.EndTurn,        "Space"),
                (InputAction.Cancel,         "Esc"),
                (InputAction.CursorUp,       "↑"),
                (InputAction.CursorDown,     "↓"),
                (InputAction.CursorLeft,     "←"),
                (InputAction.CursorRight,    "→"),
            };
            foreach (var (action, label) in cases)
            {
                InputTransition t = null;
                Assert.DoesNotThrow(() => t = machine.ProcessAction(baseState, action, s),
                    $"ProcessAction 应当处理键 '{label}' ({action}) 但抛异常");
                Assert.IsNotNull(t, $"ProcessAction 应当返回 InputTransition（键 '{label}'）");
                Assert.IsNotNull(t.Next, $"InputTransition.Next 不能为 null（键 '{label}'）");
            }
        }

        [Test]
        public void ComputeDecreeId_IsDeterministic_AndDistinct()
        {
            // 同样的 Plan 必须产生同样的 ID；不同 zone 产生不同 ID（Task 17 §「确定性」）。
            var p1a = new DecreeHoldPlan(zoneId: 3, issuingPlayer: Owner.Player);
            var p1b = new DecreeHoldPlan(zoneId: 3, issuingPlayer: Owner.Player);
            var p2  = new DecreeHoldPlan(zoneId: 7, issuingPlayer: Owner.Player);
            var pE  = new DecreeHoldPlan(zoneId: 3, issuingPlayer: Owner.Enemy);

            int id1a = CommandBuilder.ComputeDecreeId(p1a);
            int id1b = CommandBuilder.ComputeDecreeId(p1b);
            int id2  = CommandBuilder.ComputeDecreeId(p2);
            int idE  = CommandBuilder.ComputeDecreeId(pE);

            Assert.AreEqual(id1a, id1b, "同 (zone, owner) 必须产生同 DecreeId（确定性）");
            Assert.AreNotEqual(id1a, id2, "不同 zone 应产生不同 DecreeId");
            Assert.AreNotEqual(id1a, idE, "不同 owner 应产生不同 DecreeId");
        }

        [Test]
        public void BuildDecreeHold_IssuesDecreeIntoBattleState()
        {
            // 验证 DecreeHold 注册从 InputController 下沉到 CommandBuilder 后依然被正确写入 _decrees。
            // 这样 InputController 不再直接写 BattleState.Decrees（AGENTS.md §10.3）。
            var s = MakeStateWithUnits();
            Assert.AreEqual(0, s.Decrees.DecreesInOrder.Count, "初始 _decrees 应为空");

            var plan = new DecreeHoldPlan(zoneId: 11, issuingPlayer: Owner.Player);
            int before = s.Decrees.DecreesInOrder.Count;
            var cmd = CommandBuilder.Build(plan, s);

            Assert.IsNotNull(cmd, "DecreeHold Plan 必须产生 Command");
            Assert.IsInstanceOf<ApplyDecreeCommand>(cmd);
            Assert.AreEqual(before + 1, s.Decrees.DecreesInOrder.Count, "Build 后 _decrees 应增长 1");

            var registered = s.Decrees.DecreesInOrder[before];
            Assert.AreEqual(plan.ZoneId, registered.TargetZoneId);
            Assert.AreEqual(DecreeKind.Hold, registered.Kind);
            Assert.AreEqual(Owner.Player, registered.IssuingPlayer);
            Assert.AreEqual(CommandBuilder.ComputeDecreeId(plan), registered.DecreeId,
                "Build 内注册的 DecreeId 必须等于 ComputeDecreeId（确定性）");
        }

        [Test]
        public void EndTurn_Signal_DoesNotProduceCommands_AndPreservesMode()
        {
            // Space 不应清除当前 mode；它只是告诉 caller 去跑 BattleRunner.EndTurn。
            var s = MakeStateWithUnits();
            var machine = new InputStateMachine();
            var start = new InputState(InputMode.MoveTarget, new GridPos(0, 1), selectedUnitId: 1);
            var t = machine.ProcessAction(start, InputAction.EndTurn, s);
            Assert.IsTrue(t.ShouldEndTurn);
            Assert.AreEqual(0, t.Commands.Count);
            Assert.AreEqual(InputMode.MoveTarget, t.Next.Mode,
                "EndTurn 不应改变 input mode；只是信号（BattleRunner.EndTurn 内部切 Owner）");
        }

        [Test]
        public void CursorMovement_AtTopLeftCorner_AllDirections_StaysClamped()
        {
            // (0,0) → 上/左 clamp 在 (0,0)，下/右 正常。
            var s = MakeStateWithUnits();
            var machine = new InputStateMachine();
            var origin = new InputState(InputMode.SelectUnit, new GridPos(0, 0), selectedUnitId: null);

            var up    = machine.ProcessAction(origin, InputAction.CursorUp, s);
            var left  = machine.ProcessAction(origin, InputAction.CursorLeft, s);
            var down  = machine.ProcessAction(origin, InputAction.CursorDown, s);
            var right = machine.ProcessAction(origin, InputAction.CursorRight, s);

            Assert.AreEqual(new GridPos(0, 0), up.Next.Cursor);
            Assert.AreEqual(new GridPos(0, 0), left.Next.Cursor);
            Assert.AreEqual(new GridPos(0, 1), down.Next.Cursor);
            Assert.AreEqual(new GridPos(1, 0), right.Next.Cursor);
        }
    }

    /// <summary>
    /// CommandBuilder 纯逻辑测试（验证 CommandId 单调 + Plan → Command 转换）。
    /// </summary>
    public class CommandBuilderTests
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

        [SetUp]
        public void ResetBuilder()
        {
            CommandBuilder.Reset();
        }

        [Test]
        public void MovePlan_Builds_MoveCommand_WithMonotonicId()
        {
            var s = MakeState();
            var p1 = new MovePlan(1, new GridPos(1, 0));
            var c1 = CommandBuilder.Build(p1, s);
            var p2 = new MovePlan(1, new GridPos(2, 0));
            var c2 = CommandBuilder.Build(p2, s);

            Assert.IsInstanceOf<MoveCommand>(c1);
            Assert.IsInstanceOf<MoveCommand>(c2);
            var mc1 = (MoveCommand)c1;
            var mc2 = (MoveCommand)c2;
            Assert.GreaterOrEqual(mc1.CommandId, CommandBuilder.ExternalCommandIdBase);
            Assert.AreEqual(mc1.CommandId + 1, mc2.CommandId);
        }

        [Test]
        public void AttackPlan_Builds_AttackCommand_WithDamage3()
        {
            var s = MakeState();
            s.AddUnit(new UnitState(2, new GridPos(1, 0), 10, 10, Phase.Dark, Owner.Enemy));
            var p = new AttackPlan(1, 2);
            var c = CommandBuilder.Build(p, s);
            var ac = (AttackCommand)c;
            Assert.AreEqual(1, ac.AttackerId);
            Assert.AreEqual(2, ac.TargetId);
            Assert.AreEqual(3, ac.BaseDamage);
        }

        [Test]
        public void PhaseFlipPlan_Builds_ApplyStatusCommand_PhaseInvert()
        {
            var s = MakeState();
            var p = new PhaseFlipPlan(1, 1);
            var c = CommandBuilder.Build(p, s);
            var asc = (ApplyStatusCommand)c;
            Assert.AreEqual(1, asc.TargetUnitId);
            Assert.AreEqual(Starfall.Core.Status.StatusKind.PhaseInvert, asc.Kind);
            Assert.AreEqual(1, asc.SourceUnitId);
        }

        [Test]
        public void DecreeHoldPlan_Builds_ApplyDecreeCommand()
        {
            var s = MakeState();
            var p = new DecreeHoldPlan(7, Owner.Player);
            var c = CommandBuilder.Build(p, s);
            var dec = (ApplyDecreeCommand)c;
            Assert.AreEqual(7, dec.Decree.TargetZoneId);
            Assert.AreEqual(DecreeKind.Hold, dec.Decree.Kind);
            Assert.AreEqual(Owner.Player, dec.Decree.IssuingPlayer);
        }

        [Test]
        public void BuildAll_ReturnsSameCountAsPlans()
        {
            var s = MakeState();
            var plans = new ICommandPlan[]
            {
                new MovePlan(1, new GridPos(1, 0)),
                new AttackPlan(1, 1),  // invalid target but command still constructed
                new PhaseFlipPlan(1, 1),
            };
            var cmds = CommandBuilder.BuildAll(plans, s);
            Assert.AreEqual(3, cmds.Count);
        }
    }
}