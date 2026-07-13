using System.Collections.Generic;
using Starfall.Core.Model;

namespace Starfall.Unity.Input
{
    /// <summary>
    /// InputStateMachine 的状态：模式 + 光标 + 已选单位 + 律令候选。
    /// </summary>
    /// <remarks>
    /// 字段全部 immutable（get-only）；状态变化通过 InputStateMachine 返回新对象
    /// 实现（AGENTS.md §10.3：Presenter / Input 不直接改写 Core；此处同样不持有 BattleState 真值）。
    /// </remarks>
    public sealed class InputState
    {
        public InputMode Mode { get; }
        public GridPos? Cursor { get; }      // 当前光标指向的格子（不在棋盘内 = null）
        public int? SelectedUnitId { get; }  // 当前命令的目标单位（Move / PhaseFlip / Attack）
        public int DecreeZoneCursor { get; } // 律令模式下：当前选中的 anchor zone 索引（按 ZoneId 升序）
        public string LastMessage { get; }   // HUD 提示（"请选择移动目标" 等）

        public InputState(
            InputMode mode,
            GridPos? cursor,
            int? selectedUnitId,
            int decreeZoneCursor = 0,
            string lastMessage = null)
        {
            Mode = mode;
            Cursor = cursor;
            SelectedUnitId = selectedUnitId;
            DecreeZoneCursor = decreeZoneCursor;
            LastMessage = lastMessage;
        }

        public static InputState Initial()
        {
            return new InputState(
                InputMode.None,
                cursor: null,
                selectedUnitId: null,
                decreeZoneCursor: 0,
                lastMessage: "[Initializing]");
        }

        public InputState WithMode(InputMode newMode, string message = null)
            => new InputState(newMode, Cursor, SelectedUnitId, DecreeZoneCursor, message ?? LastMessage);

        public InputState WithCursor(GridPos? newCursor)
            => new InputState(Mode, newCursor, SelectedUnitId, DecreeZoneCursor, LastMessage);

        public InputState WithSelectedUnit(int? unitId, string message = null)
            => new InputState(Mode, Cursor, unitId, DecreeZoneCursor, message ?? LastMessage);

        public InputState WithDecreeCursor(int idx, string message = null)
            => new InputState(Mode, Cursor, SelectedUnitId, idx, message ?? LastMessage);

        public InputState WithMessage(string message)
            => new InputState(Mode, Cursor, SelectedUnitId, DecreeZoneCursor, message);
    }

    /// <summary>
    /// InputStateMachine.ProcessAction 的返回结果：状态 + 0..N 个待执行命令。
    /// 命令由 InputController 拿去丢给 BattleRunner / CommandExecutor 执行；
    /// 状态机自身不持有 BattleRunner，遵循 AGENTS.md §10.3 分层。
    /// </summary>
    public sealed class InputTransition
    {
        public InputState Next { get; }
        public IReadOnlyList<ICommandPlan> Commands { get; }  // 待执行 Command 蓝图（CommandBuilder 负责生成具体 Command 实例）
        public bool ShouldEndTurn { get; }    // Space 触发：需要跑 BattleRunner.EndTurn（包含 Tick + AI）
        public bool ShouldUndo { get; }       // Z 触发：需要 UndoStack.TryUndo（需要 Core 提供 BattleRunner.SetState）

        public InputTransition(
            InputState next,
            IReadOnlyList<ICommandPlan> commands = null,
            bool shouldEndTurn = false,
            bool shouldUndo = false)
        {
            Next = next ?? throw new System.ArgumentNullException(nameof(next));
            Commands = commands ?? System.Array.Empty<ICommandPlan>();
            ShouldEndTurn = shouldEndTurn;
            ShouldUndo = shouldUndo;
        }

        public static InputTransition Empty(InputState next)
            => new InputTransition(next);
    }

    /// <summary>
    /// Command 蓝图：状态机不直接 new Command（避免引入 Starfall.Core.Command 之外的依赖图）；
    /// 由 CommandBuilder 把 ICommandPlan 翻译为 MoveCommand / AttackCommand / ... 具体实例并填 CommandId。
    /// 这样状态机保持纯函数、可在 EditMode 单测中直接断言。
    /// </summary>
    public interface ICommandPlan
    {
        InputCommandKind Kind { get; }
    }

    public enum InputCommandKind : byte
    {
        None = 0,
        Move = 1,
        Attack = 2,
        PhaseFlip = 3,
        DecreeHold = 4,   // MVP: D 模式默认发 Hold
    }

    public sealed class MovePlan : ICommandPlan
    {
        public InputCommandKind Kind => InputCommandKind.Move;
        public int UnitId { get; }
        public GridPos To { get; }
        public MovePlan(int unitId, GridPos to) { UnitId = unitId; To = to; }
    }

    public sealed class AttackPlan : ICommandPlan
    {
        public InputCommandKind Kind => InputCommandKind.Attack;
        public int AttackerId { get; }
        public int TargetId { get; }
        public AttackPlan(int attackerId, int targetId) { AttackerId = attackerId; TargetId = targetId; }
    }

    public sealed class PhaseFlipPlan : ICommandPlan
    {
        public InputCommandKind Kind => InputCommandKind.PhaseFlip;
        public int SourceUnitId { get; }
        public int TargetUnitId { get; }
        public PhaseFlipPlan(int sourceUnitId, int targetUnitId) { SourceUnitId = sourceUnitId; TargetUnitId = targetUnitId; }
    }

    public sealed class DecreeHoldPlan : ICommandPlan
    {
        public InputCommandKind Kind => InputCommandKind.DecreeHold;
        public int ZoneId { get; }
        public Owner IssuingPlayer { get; }
        public DecreeHoldPlan(int zoneId, Owner issuingPlayer) { ZoneId = zoneId; IssuingPlayer = issuingPlayer; }
    }
}