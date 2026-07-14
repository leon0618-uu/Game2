using Starfall.Core.Model;

namespace Starfall.Core.Command
{
    public enum BattleEventKind : byte
    {
        None = 0,
        UnitMoved = 1,
        TurnEnded = 2,
        StatusApplied = 3,
        StatusRemoved = 4,
        UnitDamaged = 5,
        UnitPhaseInverted = 6,
        DecreeApplied = 7,
        DecreeExpired = 8,
        UnitFell = 9,           // 新增
        UnitCrushed = 10,        // 新增
        ObjectiveAdvanced = 11, // Task 19: Guard → Retreat 推进
        RetreatComplete = 12,   // Task 19: 撤离完成（所有 Player 到达 ExitTile 邻接）
        UnitEnteredVoid = 13,   // doc2 MAP-08: 单位进入 Void tile（FallResolutionService 落地成功路径）
        UnitPhaseCompressed = 14, // doc2 MAP-08: 单位被相位挤压弹回（PhaseCompressionResolutionService 触发）
    }

    /// <summary>
    /// 已成功执行的命令产生的副作用事件（Presenter 订阅此事件，不是 ICommand）。
    /// </summary>
    public readonly struct BattleEvent
    {
        public BattleEventKind Kind { get; }
        public int PrimaryUnitId { get; }
        public GridPos? From { get; }
        public GridPos? To { get; }

        public BattleEvent(BattleEventKind kind, int primaryUnitId, GridPos? from, GridPos? to)
        {
            Kind = kind;
            PrimaryUnitId = primaryUnitId;
            From = from;
            To = to;
        }
    }
}