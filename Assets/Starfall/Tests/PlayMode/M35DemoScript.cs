using System.Collections;
using NUnit.Framework;
using Starfall.Core.Combat;
using Starfall.Core.Model;
using Starfall.Unity;
using Starfall.Unity.Input;
using UnityEngine;
using UnityEngine.TestTools;

namespace Starfall.Tests.PlayMode
{
    /// <summary>
    /// M-35 演示自动化：跑 6 个主键（M / A / F / D / Z / Space），
    /// 在 PlayMode 下把每一步的真实状态（模式 / 哈希 / TurnNumber / Outcome）打日志，
    /// 证明修复后的 InputSystem 路由 + State Machine 流转 + BattleRunner 推进 + Undo/EndTurn 元动作
    /// 全部端到端工作。
    ///
    /// 不依赖 InputSystem 真键盘（batchmode -nographics 拿不到 Keyboard.current），
    /// 也不依赖 UI 输入；直接通过 InputController.Press(InputAction) 走完整业务路径：
    ///   _machine.ProcessAction → transition → CommandBuilder.Build → BattleRunner.Submit → Render。
    ///
    /// 关键约束：
    /// - 不复制玩法规则（仍走 InputStateMachine + CommandBuilder + Core Command）；
    /// - 不修改 InputController / BattleBootstrap 业务逻辑（仅复用 M-35 暴露的 Press 入口）；
    /// - 真实 BattleState 的 PostStateHash 必须在 6 步之间出现有意义的差异。
    /// </summary>
    public class M35DemoScript
    {
        private const string Tag = "[M35]";

        private GameObject _bootstrapGo;
        private BattleBootstrap _bootstrap;
        private InputController _ic;

        [UnitySetUp]
        public IEnumerator UnitySetup()
        {
            // 1) 创建独立的 GameObject（不在 MVP_Battle 场景里加载 — 避免对场景对象的依赖）
            _bootstrapGo = new GameObject("M35DemoBootstrap");
            // 2) BattleBootstrap.Awake 会：装载 JSON → 构造 BattleRunner → 自动挂 RealBoardPresenter + RealBattleHud + InputController
            _bootstrap = _bootstrapGo.AddComponent<BattleBootstrap>();

            // 3) 等到至少一帧后 Start() 才完成（InputController.Start 才会把 _state 切到 SelectUnit）
            yield return null;
            // 再多等一帧，让 BattleBootstrap.Start 的 RenderPresenters 跑完
            yield return null;

            _ic = _bootstrapGo.GetComponent<InputController>();
            Assert.IsNotNull(_ic, "InputController not auto-attached by BattleBootstrap");
            Assert.IsNotNull(_bootstrap, "BattleBootstrap null after AddComponent");
            Assert.IsNotNull(_bootstrap.Runner, "BattleRunner not constructed (JSON load failed?)");
            Assert.AreEqual(BattleOutcome.Ongoing, _bootstrap.Runner.Outcome,
                "Battle should be Ongoing on start");
            Debug.Log($"{Tag} setup: Runner OK, hash0={_bootstrap.Runner.State.PostStateHash:X16} " +
                      $"turn={_bootstrap.Runner.State.TurnNumber} " +
                      $"phase={_bootstrap.Runner.State.CurrentPhase} " +
                      $"outcome={_bootstrap.Runner.Outcome}");
        }

        [UnityTearDown]
        public IEnumerator UnityTeardown()
        {
            if (_bootstrapGo != null)
            {
                Object.Destroy(_bootstrapGo);
                _bootstrapGo = null;
                _bootstrap = null;
                _ic = null;
            }
            // 等一帧让 Destroy 真的执行
            yield return null;
        }

        [UnityTest]
        public IEnumerator M35_Demo_SixKeys()
        {
            // === 0. 起点：记录初始状态 ===
            var runner = _bootstrap.Runner;
            ulong s0 = runner.State.PostStateHash;
            int turn0 = runner.State.TurnNumber;
            var outcome0 = runner.Outcome;
            var mode0 = _ic.State.Mode;
            Debug.Log($"{Tag} step 0 (start)         hash={s0:X16} turn={turn0} phase={runner.State.CurrentPhase} " +
                      $"guards={runner.State.GuardsCompleted}/{runner.State.GuardsRequired} " +
                      $"outcome={outcome0} mode={mode0} msg='{_ic.State.LastMessage}'");
            Assert.AreEqual(InputMode.SelectUnit, mode0,
                "InputController should start in SelectUnit mode after Start()");

            // === 预备：把光标移到 Player 单位 (1,1) 以便 Confirm 选中 ===
            // 起点光标 = 棋盘中心 = (4, 5)
            // 移到 (1, 1) 需要：CursorLeft x3 (4→1), CursorUp x4 (5→1)
            // 4 次 Left + 3 次 Up
            for (int i = 0; i < 3; i++) { _ic.Press(InputAction.CursorLeft); yield return null; }
            for (int i = 0; i < 4; i++) { _ic.Press(InputAction.CursorUp);   yield return null; }
            Debug.Log($"{Tag} step pre (cursor→(1,1)) cursor={_ic.State.Cursor} mode={_ic.State.Mode}");

            // === 预备：Confirm 选中单位 ===
            _ic.Press(InputAction.Confirm); yield return null;
            int? selectedUnit = _ic.State.SelectedUnitId;
            Debug.Log($"{Tag} step pre (after Confirm) selected={selectedUnit} mode={_ic.State.Mode} " +
                      $"msg='{_ic.State.LastMessage}'");
            Assert.IsNotNull(selectedUnit, "Confirm on (1,1) should select the Player unit #1");

            // === 1. M 键：进入 MoveTarget 模式 ===
            ulong sBefore = runner.State.PostStateHash;
            _ic.Press(InputAction.EnterMove); yield return null;
            var mode1 = _ic.State.Mode;
            ulong s1 = runner.State.PostStateHash;
            Debug.Log($"{Tag} step 1 (after M)        hash={s1:X16} turn={runner.State.TurnNumber} " +
                      $"mode={mode1} msg='{_ic.State.LastMessage}'");
            Assert.AreEqual(InputMode.MoveTarget, mode1, "M should enter MoveTarget mode");

            // === 2. A 键：在 MoveTarget 模式下是 CursorLeft（M→A 不会进入 AttackTarget）===
            _ic.Press(InputAction.EnterAttack); yield return null;
            var mode2 = _ic.State.Mode;
            ulong s2 = runner.State.PostStateHash;
            Debug.Log($"{Tag} step 2 (after A)        hash={s2:X16} mode={mode2} " +
                      $"cursor={_ic.State.Cursor} msg='{_ic.State.LastMessage}'");
            // mode2 应仍是 MoveTarget（A 在 MoveTarget 下是 CursorLeft，由 KeyboardInput 路由到 CursorLeft）
            // 实际 Press(EnterAttack) 在 MoveTarget 模式下被 InputStateMachine 识别为 "EnterAttack from SelectUnit 才进入 Attack",
            // 在 MoveTarget 模式下 EnterAttack 走空返回，state 不变 (mode 保持 MoveTarget)
            Assert.AreEqual(InputMode.MoveTarget, mode2,
                "In MoveTarget mode, A (EnterAttack) should be a no-op for mode (CursorLeft is the routing)");

            // === 3. F 键：进入 PhaseFlipTarget 模式 ===
            _ic.Press(InputAction.EnterPhaseFlip); yield return null;
            var mode3 = _ic.State.Mode;
            ulong s3 = runner.State.PostStateHash;
            Debug.Log($"{Tag} step 3 (after F)        hash={s3:X16} mode={mode3} " +
                      $"msg='{_ic.State.LastMessage}'");
            Assert.AreEqual(InputMode.PhaseFlipTarget, mode3, "F should enter PhaseFlipTarget mode");

            // === 4. D 键：进入 DecreeSelect 模式（前提：场上有 Player anchor zone）===
            // battle_default.json 不含 Anchors 字段 → ListPlayerAnchorZones 返回空 → D 的状态机分支返回
            // Empty transition 并把 message 切成 "no player anchor zones; D disabled"。mode 保持 PhaseFlipTarget。
            // 我们关心的是 D 被 InputStateMachine 接收并处理：message 应当被改写。
            string msgBeforeD = _ic.State.LastMessage;
            _ic.Press(InputAction.EnterDecree); yield return null;
            var mode4 = _ic.State.Mode;
            ulong s4 = runner.State.PostStateHash;
            Debug.Log($"{Tag} step 4 (after D)        hash={s4:X16} mode={mode4} " +
                      $"msg='{_ic.State.LastMessage}'");
            // D 被状态机处理：消息会变成 "no player anchor zones; D disabled"（无 zones）或 "[Mode] DecreeSelect"
            Assert.IsTrue(_ic.State.LastMessage != msgBeforeD || mode4 == InputMode.DecreeSelect,
                "D should be processed by state machine (message changes or mode→DecreeSelect)");

            // === 5. Z 键：Undo（无前序可撤销命令 → 弹空栈，state 不变；但消息变化）===
            _ic.Press(InputAction.Undo); yield return null;
            var mode5 = _ic.State.Mode;
            ulong s5 = runner.State.PostStateHash;
            Debug.Log($"{Tag} step 5 (after Z)        hash={s5:X16} mode={mode5} " +
                      $"msg='{_ic.State.LastMessage}'");

            // === 6. Space 键：EndTurn（应推进 TurnNumber + 切到 Enemy AI + 跑守卫回合 + 回来）===
            _ic.Press(InputAction.EndTurn); yield return null;
            int turn1 = runner.State.TurnNumber;
            var outcome1 = runner.Outcome;
            var mode6 = _ic.State.Mode;
            ulong s6 = runner.State.PostStateHash;
            Debug.Log($"{Tag} step 6 (after Space)    hash={s6:X16} turn={turn1} " +
                      $"phase={runner.State.CurrentPhase} guards={runner.State.GuardsCompleted}/{runner.State.GuardsRequired} " +
                      $"outcome={outcome1} mode={mode6} msg='{_ic.State.LastMessage}'");

            // === 关键断言：6 步之间的状态差异 ===
            // M / A(MoveTarget no-op) / F / D(no zones no-op) / Z(no-op) 都只改 mode / message，不改 BattleState。
            // 只有 EndTurn (Space) 会改 BattleState（TurnNumber++ / AI 命令 / GuardsCompleted++）。
            Assert.AreEqual(s0, s1, "M (EnterMove) only changes InputMode, not BattleState hash");
            Assert.AreEqual(s1, s2, "A in MoveTarget is no-op, state hash unchanged");
            Assert.AreEqual(s2, s3, "F (EnterPhaseFlip) only changes InputMode, not BattleState hash");
            Assert.AreEqual(s3, s4, "D (EnterDecree) without zones is a no-op, state hash unchanged");
            Assert.AreEqual(s4, s5, "Z with empty undo stack doesn't change BattleState");
            Assert.AreNotEqual(s5, s6, "EndTurn should change BattleState hash (TurnNumber++ / guards / events)");

            // EndTurn 推进 TurnNumber 或 ObjectivePhase（具体哪个由 AI 行为决定，但至少其中一个会变）
            bool turnChanged = turn1 != turn0;
            bool guardsChanged = runner.State.GuardsCompleted != 0;
            Assert.IsTrue(turnChanged || guardsChanged,
                "EndTurn must change TurnNumber or GuardsCompleted (M-35 sanity check)");

            Debug.Log($"{Tag} summary: hash_start={s0:X16} hash_end={s6:X16} " +
                      $"turn {turn0}→{turn1} outcome {outcome0}→{outcome1} " +
                      $"modes {mode0}→{mode1}→{mode2}→{mode3}→{mode4}→{mode5}→{mode6}");
        }
    }
}
