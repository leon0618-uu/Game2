using Starfall.Core.Model;

namespace Starfall.Unity.Presentation
{
    /// <summary>给 Presenter 用的单位快照（ADR-0002 §Decision 1）。</summary>
    /// <remarks>
    /// Task 18 扩展：
    /// - <see cref="Pv"/> / <see cref="Ap"/> / <see cref="Cv"/> 由 <see cref="LegalPreviewHelper.DeriveStats"/>
    ///   从 UnitState.Phase / MaxHp / StatusInstance.RemainingTurns 派生，<b>不复制 Core 规则</b>；
    /// - 这些字段只用于 HUD 显示，不参与 BattleStateHash（不进入 PostStateHash 链）；
    /// - 保持 4 参构造兼容（Task 16 旧调用点）。
    /// </remarks>
    public readonly struct UnitSnapshot
    {
        public int UnitId { get; }
        public GridPos Pos { get; }
        public int Hp { get; }
        public int MaxHp { get; }
        public Phase Phase { get; }
        public Owner Owner { get; }
        /// <summary>PV（Phase Value）：当前相位。Task 18 新增。</summary>
        public Phase Pv { get; }
        /// <summary>AP（Action Points）：行动点占位。Task 18 新增。</summary>
        public int Ap { get; }
        /// <summary>CV（Concentration/Cooldown）：身上 status 剩余回合总数。Task 18 新增。</summary>
        public int Cv { get; }

        public UnitSnapshot(int unitId, GridPos pos, int hp, Phase phase, Owner owner)
        {
            UnitId = unitId;
            Pos = pos;
            Hp = hp;
            MaxHp = hp; // 默认：4 参构造无 maxHp 信息，用 hp 占位
            Phase = phase;
            Owner = owner;
            Pv = phase; // 默认：PV == Phase
            Ap = 0;
            Cv = 0;
        }

        public UnitSnapshot(int unitId, GridPos pos, int hp, int maxHp, Phase phase, Owner owner, Phase pv, int ap, int cv)
        {
            UnitId = unitId;
            Pos = pos;
            Hp = hp;
            MaxHp = maxHp;
            Phase = phase;
            Owner = owner;
            Pv = pv;
            Ap = ap;
            Cv = cv;
        }
    }
}
