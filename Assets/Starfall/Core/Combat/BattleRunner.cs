using System.Collections.Generic;
using Starfall.Core.Command;
using Starfall.Core.Model;

namespace Starfall.Core.Combat
{
    /// <summary>
    /// 战斗主循环（M-14=A 最小化）：
    /// - <see cref="Submit"/>：外部提交一个 <see cref="ICommand"/>（玩家输入或 Replay 回放）；
    /// - <see cref="EndTurn"/>：结束当前玩家回合 → 派发 EndTurnCommand → TickEndTurnCommand →
    ///   切换 ActivePlayer → 若变为 Enemy 则触发 AI → 检查胜负；
    /// - 所有成功执行的 Command 的 events 写入 <see cref="EventSink"/>。
    ///
    /// Command 序号由 <see cref="_nextCommandId"/> 单调分配（1, 2, 3, ...），
    /// 用于内部命令（EndTurn / Tick / AI 命令）；外部提交的 Command 应在构造时显式指定
    /// CommandId（因 ICommand 不可变 —— Task 03 锁定接口为 get-only）。
    ///
    /// 该类不引用 UnityEngine、不读取时间/线程 —— 可在 EditMode 测试中跑通。
    /// </summary>
    public sealed class BattleRunner
    {
        public BattleState State { get; }
        public EventSink Events { get; } = new EventSink();
        public BattleOutcome Outcome { get; private set; } = BattleOutcome.Ongoing;

        private readonly IEnemyAI _enemyAI;
        private int _nextCommandId = 1;

        public BattleRunner(BattleState state, IEnemyAI enemyAI = null)
        {
            State = state ?? throw new System.ArgumentNullException(nameof(state));
            _enemyAI = enemyAI ?? new SimpleEnemyAI();
            // 初始判定：覆盖 "JSON 已含预死单位" 场景 —— 一般为 Ongoing
            Outcome = WinConditionChecker.Check(State);
        }

        /// <summary>
        /// 外部提交一个 Command（玩家输入或 replay）。
        /// - 战斗已结束（Outcome != Ongoing）时一律拒绝；
        /// - 调用方应在构造时显式指定 CommandId（ICommand 不可变，BattleRunner 不再回填）；
        /// - 执行失败不写 EventSink，不影响 Outcome。
        /// </summary>
        public CommandResult Submit(ICommand command)
        {
            if (Outcome != BattleOutcome.Ongoing)
                return CommandResult.Illegal;
            if (command == null)
                return CommandResult.Illegal;

            // 注：ICommand.CommandId 是 get-only（Task 03 锁定），不能回填。
            // 外部 caller 必须用具体 Command 构造函数显式传入 CommandId。
            // _nextCommandId 仍按内部命令（EndTurn / Tick / AI）单调递增。

            var result = CommandExecutor.Run(State, command, out var events);
            if (result == CommandResult.Success)
                Events.Append(events);
            return result;
        }

        /// <summary>
        /// 结束当前玩家回合：
        /// 1. 派发 <see cref="EndTurnCommand"/>（切换 ActivePlayer + TurnNumber++）；
        /// 2. 派发 <see cref="TickEndTurnCommand"/>（Burn/PhaseInvert/Remaining 衰减/移除）；
        /// 3. 若 ActivePlayer == Enemy，触发 AI 计划 → 顺序 Submit 每个 AI 命令；
        /// 4. 重新检查胜负 → 更新 Outcome。
        /// </summary>
        /// <returns>第一步 EndTurnCommand 的执行结果（成功 / 非法）。</returns>
        public CommandResult EndTurn()
        {
            if (Outcome != BattleOutcome.Ongoing) return CommandResult.Illegal;

            // 1. 玩家 EndTurn
            var endTurn = new EndTurnCommand(_nextCommandId++, State.ActivePlayer);
            var r1 = CommandExecutor.Run(State, endTurn, out var ev1);
            if (r1 == CommandResult.Success) Events.Append(ev1);

            // 2. Tick 状态（即使 EndTurn 失败也尝试 —— 但 MVP 约定二者皆可执行）
            var tick = new TickEndTurnCommand(_nextCommandId++);
            var r2 = CommandExecutor.Run(State, tick, out var ev2);
            if (r2 == CommandResult.Success) Events.Append(ev2);

            // 3. 若轮到 Enemy，触发 AI 计划
            if (State.ActivePlayer == Owner.Enemy)
            {
                foreach (var cmd in _enemyAI.PlanTurn(_nextCommandId++, State))
                {
                    if (cmd == null) continue;
                    Submit(cmd);
                }
            }

            // 4. 检查胜负（向后兼容的旧路径 —— 但 Task 19 关卡闭环以 ObjectivePhaseUpdater 为准）
            Outcome = WinConditionChecker.Check(State);

            // 5. 关卡阶段推进（Task 19 关卡闭环）
            //    - 任意胜负已定 → 锁定 Ended；
            //    - Guard 阶段且双方都有活单位 → GuardsCompleted++，达到门槛则切 Retreat；
            //    - Retreat 阶段且所有活 Player 都在 ExitTile 邻接 → 撤离完成 + PlayerWins。
            if (Outcome == BattleOutcome.Ongoing)
            {
                var update = ObjectivePhaseUpdater.Update(State);
                if (update.advancedToRetreat)
                    Events.Append(new[] { new BattleEvent(BattleEventKind.ObjectiveAdvanced, 0, null, null) });
                if (update.retreated)
                    Events.Append(new[] { new BattleEvent(BattleEventKind.RetreatComplete, 0, null, null) });
                Outcome = update.outcome;
            }
            else
            {
                // 胜负已定：ObjectivePhaseUpdater 会强制 Ended；调用以保证阶段字段一致
                ObjectivePhaseUpdater.Update(State);
            }

            return r1;
        }
    }
}