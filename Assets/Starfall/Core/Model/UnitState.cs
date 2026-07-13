namespace Starfall.Core.Model
{
    public sealed class UnitState
    {
        public int UnitId { get; }
        public GridPos Pos { get; set; }
        public int Hp { get; set; }
        public int MaxHp { get; }
        public Phase Phase { get; set; }
        public Owner Owner { get; }

        public UnitState(int unitId, GridPos pos, int hp, int maxHp, Phase phase, Owner owner)
        {
            if (unitId < 0) throw new System.ArgumentException("UnitId must be >= 0", nameof(unitId));
            if (hp < 0) throw new System.ArgumentException("Hp must be >= 0", nameof(hp));
            if (maxHp <= 0) throw new System.ArgumentException("MaxHp must be > 0", nameof(maxHp));
            UnitId = unitId;
            Pos = pos;
            Hp = hp;
            MaxHp = maxHp;
            Phase = phase;
            Owner = owner;
        }
    }
}
