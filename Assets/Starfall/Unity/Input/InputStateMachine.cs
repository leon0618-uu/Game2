using System.Collections.Generic;
using Starfall.Core.Anchor;
using Starfall.Core.Decree;
using Starfall.Core.Model;

namespace Starfall.Unity.Input
{
    /// <summary>
    /// 纯 C# 输入状态机（Task 17）：接受 InputAction，返回 InputTransition。
    /// 不引用 UnityEngine，可在 EditMode 测试中直接构造并断言。
    /// </summary>
    /// <remarks>
    /// 设计目标：
    /// 1. <b>不持有 BattleState 真值</b>（AGENTS.md §10.3）；调用方在 ProcessAction 时传入只读 BattleState 引用，
    ///    返回的 Plan 里只包含「目标格子 / 目标单位 id」，由 CommandBuilder / InputController 真正构造 Command；
    /// 2. <b>键位解析顺序稳定</b>（AGENTS.md §11）：
    ///    - 在 SelectUnit 模式下，A 键进入 Attack 模式；
    ///    - 在 MoveTarget / PhaseFlipTarget / AttackTarget 模式下，A 键移动光标向左；
    ///    判定顺序为「先看是否在 SelectUnit，再看是否模式专属快捷键」；
    /// 3. <b>邻居顺序</b>：格子校验使用 BFSPathfinder 一致顺序（下、左、右、上），
    ///    这里把 attack / phaseflip 的「邻格」用相同 Chebyshev=1 判定（8 邻居含自身）。
    ///
    /// 状态转换图：
    /// <code>
    ///   None ──BattleStart──▶ SelectUnit
    ///   SelectUnit ──Confirm(cursor on own unit)──▶ SelectUnit (选中)
    ///   SelectUnit ──M──▶ MoveTarget
    ///   SelectUnit ──F──▶ PhaseFlipTarget
    ///   SelectUnit ──A──▶ AttackTarget
    ///   SelectUnit ──D──▶ DecreeSelect
    ///   SelectUnit ──Esc──▶ SelectUnit (取消选中)
    ///   MoveTarget/PhaseFlipTarget/AttackTarget ──Confirm──▶ (build Command) ──▶ SelectUnit
    ///   DecreeSelect ──Confirm──▶ (build DecreeHold Command) ──▶ SelectUnit
    ///   any ──Z──▶ undo
    ///   any ──Space──▶ EndTurn
    ///   any ──Esc──▶ SelectUnit
    /// </code>
    /// </remarks>
    public sealed class InputStateMachine
    {
        /// <summary>初始模式：battle 开始后从 None 进入 SelectUnit。</summary>
        public const InputMode InitialMode = InputMode.SelectUnit;

        // ===== Mode switch helpers (public for testability) =====

        public static bool IsTargetingMode(InputMode m)
            => m == InputMode.MoveTarget || m == InputMode.PhaseFlipTarget || m == InputMode.AttackTarget;

        public static InputMode NextTargetingMode(InputMode current)
        {
            switch (current)
            {
                case InputMode.SelectUnit: return InputMode.MoveTarget;
                case InputMode.MoveTarget: return InputMode.PhaseFlipTarget;
                case InputMode.PhaseFlipTarget: return InputMode.AttackTarget;
                case InputMode.AttackTarget: return InputMode.DecreeSelect;
                case InputMode.DecreeSelect: return InputMode.SelectUnit;
                default: return InputMode.SelectUnit;
            }
        }

        // ===== ProcessAction =====

        /// <summary>
        /// 主入口：接受一个 InputAction + 当前 BattleState 只读引用，返回 InputTransition。
        /// 调用方负责把 Commands 拿去 BattleRunner.Submit，把 ShouldEndTurn 拿去 BattleRunner.EndTurn，把 ShouldUndo 拿去 UndoStack。
        /// </summary>
        public InputTransition ProcessAction(InputState state, InputAction action, BattleState s)
        {
            if (state == null) state = InputState.Initial();
            // s 可能为 null（战斗未加载 / 已结束）；这种情形只允许 Cancel / EndTurn。

            switch (action)
            {
                case InputAction.None:
                    return InputTransition.Empty(state);

                // ===== 元动作：Esc / Z / Space 在任何模式都生效 =====
                case InputAction.Cancel:
                    return CancelToSelectUnit(state, message: "[Cancel] → SelectUnit");

                case InputAction.Undo:
                    return new InputTransition(state.WithMessage("[Undo] pop UndoStack"), shouldUndo: true);

                case InputAction.EndTurn:
                    return new InputTransition(state.WithMessage("[EndTurn] BattleRunner.EndTurn"), shouldEndTurn: true);

                // ===== 光标移动：所有模式都生效 =====
                case InputAction.CursorUp:
                case InputAction.CursorDown:
                case InputAction.CursorLeft:
                case InputAction.CursorRight:
                    return MoveCursor(state, action, s);

                // ===== 命令模式进入：A 键在 SelectUnit 进入 Attack，在其他模式走光标 =====
                case InputAction.EnterMove:
                    return EnterMode(state, InputMode.MoveTarget, message: "[Mode] MoveTarget — choose destination");

                case InputAction.EnterAttack:
                    if (state.Mode == InputMode.SelectUnit)
                        return EnterMode(state, InputMode.AttackTarget, message: "[Mode] AttackTarget — choose adjacent enemy");
                    // 在目标选择模式里 A 是 CursorLeft（已在上方 Cursor* 处理）
                    return InputTransition.Empty(state);

                case InputAction.EnterPhaseFlip:
                    return EnterMode(state, InputMode.PhaseFlipTarget, message: "[Mode] PhaseFlipTarget — choose adjacent tile");

                case InputAction.EnterDecree:
                    return EnterDecreeMode(state, s);

                // ===== 确认：在 SelectUnit 选中单位 / 在目标模式构建 Command =====
                case InputAction.Confirm:
                    return Confirm(state, s);

                // ===== DecreeSelect 内部循环 =====
                case InputAction.DecreeCyclePrev:
                    return CycleDecree(state, s, delta: -1);
                case InputAction.DecreeCycleNext:
                    return CycleDecree(state, s, delta: +1);

                default:
                    return InputTransition.Empty(state);
            }
        }

        // ===== Mode transitions =====

        private static InputTransition EnterMode(InputState state, InputMode target, string message)
        {
            if (state.Mode == target)
                return InputTransition.Empty(state.WithMessage(message));
            // 进入目标模式需要先有一个已选单位
            if (state.SelectedUnitId == null)
                return InputTransition.Empty(state.WithMessage("[Mode] no selected unit; click your unit first"));
            return InputTransition.Empty(state.WithMode(target, message));
        }

        private static InputTransition EnterDecreeMode(InputState state, BattleState s)
        {
            if (s == null) return InputTransition.Empty(state.WithMessage("[Mode] battle not loaded"));
            var zoneIds = ListPlayerAnchorZones(s);
            if (zoneIds.Count == 0)
                return InputTransition.Empty(state.WithMessage("[Mode] no player anchor zones; D disabled"));
            return InputTransition.Empty(
                new InputState(
                    InputMode.DecreeSelect,
                    state.Cursor,
                    state.SelectedUnitId,
                    decreeZoneCursor: 0,
                    lastMessage: $"[Mode] DecreeSelect — {zoneIds.Count} zone(s)"));
        }

        private static InputTransition CancelToSelectUnit(InputState state, string message)
        {
            if (state.Mode == InputMode.SelectUnit)
                return InputTransition.Empty(state.WithMessage("[Cancel] already in SelectUnit"));
            return InputTransition.Empty(
                new InputState(
                    InputMode.SelectUnit,
                    state.Cursor,
                    selectedUnitId: null,
                    decreeZoneCursor: state.DecreeZoneCursor,
                    lastMessage: message));
        }

        // ===== Cursor =====

        private static InputTransition MoveCursor(InputState state, InputAction action, BattleState s)
        {
            if (s == null) return InputTransition.Empty(state);
            var cur = state.Cursor ?? CenterOfBoard(s);
            int x = cur.X, y = cur.Y;
            // 顺序固定：Up=-1, Down=+1, Left=-1, Right=+1
            switch (action)
            {
                case InputAction.CursorUp:    y -= 1; break;
                case InputAction.CursorDown:  y += 1; break;
                case InputAction.CursorLeft:  x -= 1; break;
                case InputAction.CursorRight: x += 1; break;
            }
            // 钳到棋盘内
            if (x < 0) x = 0;
            if (y < 0) y = 0;
            if (x >= s.Board.Width)  x = s.Board.Width  - 1;
            if (y >= s.Board.Height) y = s.Board.Height - 1;
            return InputTransition.Empty(state.WithCursor(new GridPos(x, y)));
        }

        private static GridPos CenterOfBoard(BattleState s)
            => new GridPos(s.Board.Width / 2, s.Board.Height / 2);

        // ===== Confirm =====

        private static InputTransition Confirm(InputState state, BattleState s)
        {
            if (s == null) return InputTransition.Empty(state.WithMessage("[Confirm] battle not loaded"));
            if (state.Cursor == null)
                return InputTransition.Empty(state.WithMessage("[Confirm] no cursor; move with arrows/WASD"));

            switch (state.Mode)
            {
                case InputMode.SelectUnit:
                    return ConfirmSelectUnit(state, s);

                case InputMode.MoveTarget:
                    return ConfirmMove(state, s);

                case InputMode.PhaseFlipTarget:
                    return ConfirmPhaseFlip(state, s);

                case InputMode.AttackTarget:
                    return ConfirmAttack(state, s);

                case InputMode.DecreeSelect:
                    return ConfirmDecree(state, s);

                case InputMode.Anchored:
                    // Anchored 是瞬时态：Confirm 直接回 SelectUnit
                    return InputTransition.Empty(state.WithMode(InputMode.SelectUnit, "[Anchored] → SelectUnit"));

                default:
                    return InputTransition.Empty(state);
            }
        }

        private static InputTransition ConfirmSelectUnit(InputState state, BattleState s)
        {
            var cursor = state.Cursor.Value;
            int? unitId = FindUnitAt(s, cursor);
            if (unitId == null)
                return InputTransition.Empty(state.WithMessage("[Select] no unit at cursor"));
            if (!IsOwnUnit(s, unitId.Value))
                return InputTransition.Empty(state.WithMessage("[Select] cannot select enemy unit"));
            return InputTransition.Empty(
                state.WithSelectedUnit(unitId, message: $"[Select] unit #{unitId.Value} ready (M/F/A/D or Esc)"));
        }

        private static InputTransition ConfirmMove(InputState state, BattleState s)
        {
            if (state.SelectedUnitId == null)
                return InputTransition.Empty(state.WithMode(InputMode.SelectUnit, "[Mode] lost selection → SelectUnit"));

            var cursor = state.Cursor.Value;
            int unitId = state.SelectedUnitId.Value;
            var unit = FindUnitById(s, unitId);
            if (unit == null)
                return InputTransition.Empty(state.WithMode(InputMode.SelectUnit, "[Mode] unit gone → SelectUnit"));

            var cmd = new MovePlan(unitId, cursor);
            var next = state.WithSelectedUnit(null, message: $"[Move] unit #{unitId} → {cursor}");
            next = next.WithMode(InputMode.SelectUnit, next.LastMessage);
            return new InputTransition(next, new ICommandPlan[] { cmd });
        }

        private static InputTransition ConfirmPhaseFlip(InputState state, BattleState s)
        {
            if (state.SelectedUnitId == null)
                return InputTransition.Empty(state.WithMode(InputMode.SelectUnit, "[Mode] lost selection → SelectUnit"));

            var cursor = state.Cursor.Value;
            int sourceId = state.SelectedUnitId.Value;
            // 目标：在 cursor 上的单位（若有）；或与 cursor 邻接的己方单位（若 cursor 是空格）。
            int targetId = FindUnitAt(s, cursor) ?? FindAdjacentOwnUnit(s, sourceId, cursor);
            if (targetId == 0)
                return InputTransition.Empty(state.WithMessage("[PhaseFlip] no adjacent target at cursor"));

            var cmd = new PhaseFlipPlan(sourceId, targetId);
            var next = state.WithSelectedUnit(null, message: $"[PhaseFlip] target #{targetId}");
            next = next.WithMode(InputMode.SelectUnit, next.LastMessage);
            return new InputTransition(next, new ICommandPlan[] { cmd });
        }

        private static InputTransition ConfirmAttack(InputState state, BattleState s)
        {
            if (state.SelectedUnitId == null)
                return InputTransition.Empty(state.WithMode(InputMode.SelectUnit, "[Mode] lost selection → SelectUnit"));

            var cursor = state.Cursor.Value;
            int attacker = state.SelectedUnitId.Value;
            int? target = FindUnitAt(s, cursor);
            if (target == null)
                return InputTransition.Empty(state.WithMessage("[Attack] no unit at cursor"));
            if (!IsAdjacent(s, attacker, target.Value))
                return InputTransition.Empty(state.WithMessage("[Attack] target not adjacent (Chebyshev > 1)"));
            if (IsOwnUnit(s, target.Value))
                return InputTransition.Empty(state.WithMessage("[Attack] cannot attack own unit"));

            var cmd = new AttackPlan(attacker, target.Value);
            var next = state.WithSelectedUnit(null, message: $"[Attack] {attacker} → {target.Value}");
            next = next.WithMode(InputMode.SelectUnit, next.LastMessage);
            return new InputTransition(next, new ICommandPlan[] { cmd });
        }

        private static InputTransition ConfirmDecree(InputState state, BattleState s)
        {
            var zoneIds = ListPlayerAnchorZones(s);
            if (zoneIds.Count == 0)
                return InputTransition.Empty(state.WithMode(InputMode.SelectUnit, "[Decree] no zones → SelectUnit"));

            int idx = state.DecreeZoneCursor;
            if (idx < 0) idx = 0;
            if (idx >= zoneIds.Count) idx = zoneIds.Count - 1;
            int zoneId = zoneIds[idx];

            var cmd = new DecreeHoldPlan(zoneId, Owner.Player);
            var next = state.WithSelectedUnit(null, message: $"[Decree] Hold on zone #{zoneId}");
            next = next.WithMode(InputMode.SelectUnit, next.LastMessage);
            return new InputTransition(next, new ICommandPlan[] { cmd });
        }

        // ===== Helpers (pure) =====

        public static int? FindUnitAt(BattleState s, GridPos p)
        {
            foreach (var u in s.Units)
                if (u.Pos == p) return u.UnitId;
            return null;
        }

        private static UnitState FindUnitById(BattleState s, int unitId)
        {
            foreach (var u in s.Units)
                if (u.UnitId == unitId) return u;
            return null;
        }

        public static bool IsOwnUnit(BattleState s, int unitId)
        {
            var u = FindUnitById(s, unitId);
            return u != null && u.Owner == s.ActivePlayer;
        }

        public static bool IsAdjacent(BattleState s, int unitA, int unitB)
        {
            var a = FindUnitById(s, unitA);
            var b = FindUnitById(s, unitB);
            if (a == null || b == null) return false;
            int dx = System.Math.Abs(a.Pos.X - b.Pos.X);
            int dy = System.Math.Abs(a.Pos.Y - b.Pos.Y);
            return System.Math.Max(dx, dy) <= 1;
        }

        public static int FindAdjacentOwnUnit(BattleState s, int sourceId, GridPos cursor)
        {
            // 8 邻居顺序固定：下、左、右、上（与 BFSPathfinder 一致；AGENTS.md §11）。
            var source = FindUnitById(s, sourceId);
            if (source == null) return 0;
            var offsets = new (int dx, int dy)[] { (0, 1), (-1, 0), (1, 0), (0, -1) };
            // 先查 cursor 自身是否就是邻格
            int dxC = System.Math.Abs(source.Pos.X - cursor.X);
            int dyC = System.Math.Abs(source.Pos.Y - cursor.Y);
            if (System.Math.Max(dxC, dyC) <= 1)
            {
                int? atCursor = FindUnitAt(s, cursor);
                if (atCursor.HasValue && IsOwnUnit(s, atCursor.Value) && atCursor.Value != sourceId)
                    return atCursor.Value;
            }
            // 否则在 cursor 的 8 邻居里找一个己方单位
            for (int oy = -1; oy <= 1; oy++)
            {
                for (int ox = -1; ox <= 1; ox++)
                {
                    if (ox == 0 && oy == 0) continue;
                    var p = new GridPos(cursor.X + ox, cursor.Y + oy);
                    int? at = FindUnitAt(s, p);
                    if (at.HasValue && IsOwnUnit(s, at.Value) && at.Value != sourceId)
                        return at.Value;
                }
            }
            return 0;
        }

        public static IReadOnlyList<int> ListPlayerAnchorZones(BattleState s)
        {
            // 按 ZoneId 升序（与 AnchorRegistry.ZonesInOrder 一致）
            var list = new List<int>();
            foreach (var z in s.Anchors.ZonesInOrder)
                if (z.Owner == "Player") list.Add(z.ZoneId);
            return list;
        }

        private static InputTransition CycleDecree(InputState state, BattleState s, int delta)
        {
            if (state.Mode != InputMode.DecreeSelect)
                return InputTransition.Empty(state);
            var zoneIds = ListPlayerAnchorZones(s);
            if (zoneIds.Count == 0)
                return InputTransition.Empty(state.WithMode(InputMode.SelectUnit, "[Decree] no zones → SelectUnit"));

            int idx = state.DecreeZoneCursor + delta;
            // 循环 wrap
            idx = ((idx % zoneIds.Count) + zoneIds.Count) % zoneIds.Count;
            return InputTransition.Empty(
                state.WithDecreeCursor(idx, message: $"[Decree] zone #{zoneIds[idx]} ({idx + 1}/{zoneIds.Count})"));
        }
    }
}