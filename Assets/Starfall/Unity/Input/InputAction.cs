namespace Starfall.Unity.Input
{
    /// <summary>
    /// InputStateMachine 的输入事件（来自键盘 / 鼠标 / Gamepad 的统一抽象）。
    /// InputController 负责把 UnityEngine.InputSystem 的按键翻译成 InputAction。
    /// </summary>
    /// <remarks>
    /// 顺序与键盘扫描顺序一致（AGENTS.md §11 确定性）：
    /// 命令键 (Enter/Confirm) → 取消 (Esc/Cancel) → 移动 (Cursor) → 命令模式 (M/F/A/D) → 撤销 (Z) → 结束回合 (Space)。
    /// </remarks>
    public enum InputAction : byte
    {
        None = 0,

        // 确认 / 取消
        Confirm = 1,   // Enter / 鼠标左键
        Cancel  = 2,   // Esc

        // 光标移动
        CursorUp    = 10,   // ↑ / W
        CursorDown  = 11,   // ↓ / S
        CursorLeft  = 12,   // ← / A
        CursorRight = 13,   // → / D  （光标移动专用，不与 Attack 模式冲突；按下时由 InputController 路由）

        // 命令模式进入
        EnterMove       = 20,   // M
        EnterPhaseFlip  = 21,   // F
        EnterAttack     = 22,   // A  （与 CursorLeft 共键 A，由 InputController 路由：是否已按 Shift / 是否在 SelectUnit 模式）
        EnterDecree     = 23,   // D

        // 元动作
        Undo    = 30,   // Z
        EndTurn = 31,   // Space

        // 律令模式内：循环浏览 Anchor zone
        DecreeCyclePrev = 40,   // W（在 DecreeSelect 模式）
        DecreeCycleNext = 41,   // S（在 DecreeSelect 模式）
    }
}