namespace Starfall.Core.Status
{
    public enum StatusKind : byte
    {
        None = 0,
        Burn = 1,        // 回合末扣 1 HP
        Root = 2,        // 禁止移动（MoveCommand 直接 Illegal）
        PhaseInvert = 3, // 强制相位翻转（Light↔Dark）
    }
}