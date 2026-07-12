using Starfall.Core.Model;

namespace Starfall.Unity.Presentation
{
    public enum PresentationEventKind : byte
    {
        None = 0,
        UnitMoveAnimated = 1,
        UnitDamageFlash = 2,
        UnitPhaseFlip = 3,
        StatusAppliedVisual = 4,
        TurnSwitchedBanner = 5,
        OutcomeOverlayShown = 6,
    }

    public readonly struct PresentationEvent
    {
        public PresentationEventKind Kind { get; }
        public int PrimaryUnitId { get; }
        public GridPos? From { get; }
        public GridPos? To { get; }

        public PresentationEvent(PresentationEventKind kind, int primaryUnitId, GridPos? from, GridPos? to)
        {
            Kind = kind;
            PrimaryUnitId = primaryUnitId;
            From = from;
            To = to;
        }
    }
}