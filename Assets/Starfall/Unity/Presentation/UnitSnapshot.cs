using Starfall.Core.Model;

namespace Starfall.Unity.Presentation
{
    /// <summary>给 Presenter 用的单位快照（ADR-0002 §Decision 1）。</summary>
    public readonly struct UnitSnapshot
    {
        public int UnitId { get; }
        public GridPos Pos { get; }
        public int Hp { get; }
        public Phase Phase { get; }
        public Owner Owner { get; }
        public UnitSnapshot(int unitId, GridPos pos, int hp, Phase phase, Owner owner)
        {
            UnitId = unitId;
            Pos = pos;
            Hp = hp;
            Phase = phase;
            Owner = owner;
        }
    }
}