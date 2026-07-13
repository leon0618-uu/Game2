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