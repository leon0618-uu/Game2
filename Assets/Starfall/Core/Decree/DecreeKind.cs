namespace Starfall.Core.Decree
{
    public enum DecreeKind : byte
    {
        None = 0,
        Hold = 1,      // 守住锚点 +N 回合
        Push = 2,      // 推进单位至锚点
        Retreat = 3,   // 撤退至锚点
        PhaseShift = 4,// 强制相位翻转全部友军
    }
}
