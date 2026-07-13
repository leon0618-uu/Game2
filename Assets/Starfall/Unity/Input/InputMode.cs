namespace Starfall.Unity.Input
{
    /// <summary>
    /// 键盘输入模式状态机（Task 17）。
    /// 决定下一次 Confirm/Cancel 键如何被解释（移动 / 攻击 / 律令 ...）。
    /// 模式之间的合法转换见 <see cref="InputStateMachine"/>。
    /// </summary>
    /// <remarks>
    /// 顺序按 AGENTS.md §11 确定性规则：从最不具攻击性的模式（None / SelectUnit）
    /// 到最具攻击性的（AttackTarget / DecreeSelect）。
    /// 枚举顺序也用于 mode 比较，避免引入魔法数字。
    /// </remarks>
    public enum InputMode : byte
    {
        /// <summary>未就绪（Runner 不可用 / 战斗结束）。</summary>
        None = 0,

        /// <summary>默认模式：光标在棋盘上移动，回车/点击选中一个己方单位进入命令菜单。</summary>
        SelectUnit = 1,

        /// <summary>移动目标选择：选合法落点 → MoveCommand。</summary>
        MoveTarget = 2,

        /// <summary>相位翻转目标选择：选邻格 → ApplyStatusCommand(PhaseInvert)。</summary>
        PhaseFlipTarget = 3,

        /// <summary>攻击目标选择：选邻格敌对单位 → AttackCommand。</summary>
        AttackTarget = 4,

        /// <summary>律令选择：循环浏览可用 Anchor zone → ApplyDecreeCommand。</summary>
        DecreeSelect = 5,

        /// <summary>已锚定（最近一次动作把单位送入了锚点围区）。短暂停留后自动回到 SelectUnit。</summary>
        Anchored = 6,
    }
}