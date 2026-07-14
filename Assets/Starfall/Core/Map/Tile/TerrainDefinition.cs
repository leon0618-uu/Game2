using System;
using Starfall.Core.Map.Cover;

namespace Starfall.Core.Map.Tile
{
    /// <summary>
    /// doc2 MAP-04 §4.1 单一地形类型的不可变定义（immutable）。
    ///
    /// <para/>
    /// 字段语义（每条字段都从 <see cref="TerrainRegistry"/> 取标准值；
    /// 由 <see cref="TileDefinition"/> 在构造时引用）：
    /// <list type="bullet">
    /// <item><see cref="Type"/>：<see cref="TerrainType"/> 枚举本体。</item>
    /// <item><see cref="BaseMoveCost"/>：基础移动成本；范围 [1, 5]，小于 1 抛 ArgumentOutOfRangeException。</item>
    /// <item><see cref="BlocksMovement"/>：true = 阻挡移动（含飞行以外的所有单位）。</item>
    /// <item><see cref="BlocksVision"/>：true = 完全阻挡视线（与 <see cref="CoverLevel"/> 无关）。</item>
    /// <item><see cref="BlocksProjectile"/>：true = 完全阻挡弹道（Direct / Arc / Beam / Chain / GroundPropagation）。</item>
    /// <item><see cref="CoverLevel"/>：掩体等级（None / Half / Full），与 <see cref="CoverDirection"/> 配对使用。</item>
    /// <item><see cref="CoverDirections"/>：掩体生效的方向集合（All 表示任意方向）。</item>
    /// <item><see cref="PhaseFlipAllowed"/>：true = 允许相位翻转（仅在 <see cref="GateTile"/> 地形上为 true）。</item>
    /// <item><see cref="HazardousDamagePerTurn"/>：每回合自动伤害；0 = 无害（默认）。</item>
    /// </list>
    ///
    /// <para/>
    /// **不变量**（构造时强制）：
    /// <list type="number">
    /// <item><see cref="BaseMoveCost"/> ∈ [1, 5]。</item>
    /// <item><see cref="HazardousDamagePerTurn"/> ∈ [0, 100]。</item>
    /// <item>所有字段不可为 null（struct 天然保证）。</item>
    /// </list>
    ///
    /// <para/>
    /// **与 <see cref="TileDefinition"/> 的关系**：
    /// <c>TerrainDefinition</c> 是"类型级"配置（同一地形的所有 tile 共享），
    /// <c>TileDefinition</c> 是"实例级"配置（每 tile 可覆盖 <c>BaseMoveCost</c> /
    /// <c>Tags</c> 等字段）。运行时由 <see cref="TileDefinition.Terrain"/> 引用
    /// 本类型，再在 <see cref="MapTileState"/> 中叠加 <c>TemporaryMoveCostModifier</c>。
    /// </summary>
    public readonly struct TerrainDefinition : IEquatable<TerrainDefinition>
    {
        /// <summary><see cref="BaseMoveCost"/> 允许的下界（含）。</summary>
        public const int MinMoveCost = 1;

        /// <summary><see cref="BaseMoveCost"/> 允许的上界（含）。</summary>
        public const int MaxMoveCost = 5;

        /// <summary><see cref="HazardousDamagePerTurn"/> 允许的上界（含）。</summary>
        public const int MaxHazardousDamage = 100;

        public readonly TerrainType Type;
        public readonly int BaseMoveCost;
        public readonly bool BlocksMovement;
        public readonly bool BlocksVision;
        public readonly bool BlocksProjectile;
        public readonly CoverLevel CoverLevel;
        public readonly CoverDirection CoverDirections;
        public readonly bool PhaseFlipAllowed;
        public readonly int HazardousDamagePerTurn;

        public TerrainDefinition(
            TerrainType type,
            int baseMoveCost,
            bool blocksMovement,
            bool blocksVision,
            bool blocksProjectile,
            CoverLevel coverLevel,
            CoverDirection coverDirections = CoverDirection.All,
            bool phaseFlipAllowed = false,
            int hazardousDamagePerTurn = 0)
        {
            if (baseMoveCost < MinMoveCost || baseMoveCost > MaxMoveCost)
                throw new ArgumentOutOfRangeException(nameof(baseMoveCost), baseMoveCost,
                    $"BaseMoveCost must be in [{MinMoveCost}, {MaxMoveCost}] (doc2 MAP-04 §4.1).");
            if (hazardousDamagePerTurn < 0 || hazardousDamagePerTurn > MaxHazardousDamage)
                throw new ArgumentOutOfRangeException(nameof(hazardousDamagePerTurn),
                    hazardousDamagePerTurn,
                    $"HazardousDamagePerTurn must be in [0, {MaxHazardousDamage}] (doc2 MAP-04 §4.1).");

            Type = type;
            BaseMoveCost = baseMoveCost;
            BlocksMovement = blocksMovement;
            BlocksVision = blocksVision;
            BlocksProjectile = blocksProjectile;
            CoverLevel = coverLevel;
            CoverDirections = coverDirections;
            PhaseFlipAllowed = phaseFlipAllowed;
            HazardousDamagePerTurn = hazardousDamagePerTurn;
        }

        /// <summary>true = 该地形阻挡移动（不可站立 / 不可通过）。</summary>
        public bool IsImpassable => BlocksMovement;

        /// <summary>true = 该地形对站立单位造成每回合伤害。</summary>
        public bool IsHazardous => HazardousDamagePerTurn > 0;

        // ──────────── 等值 / 哈希 ────────────

        public bool Equals(TerrainDefinition other)
            => Type == other.Type
               && BaseMoveCost == other.BaseMoveCost
               && BlocksMovement == other.BlocksMovement
               && BlocksVision == other.BlocksVision
               && BlocksProjectile == other.BlocksProjectile
               && CoverLevel == other.CoverLevel
               && CoverDirections == other.CoverDirections
               && PhaseFlipAllowed == other.PhaseFlipAllowed
               && HazardousDamagePerTurn == other.HazardousDamagePerTurn;

        public override bool Equals(object obj) => obj is TerrainDefinition other && Equals(other);

        public override int GetHashCode()
        {
            unchecked
            {
                int h = (int)Type;
                h = (h * 397) ^ BaseMoveCost;
                h = (h * 397) ^ (BlocksMovement ? 1 : 0);
                h = (h * 397) ^ (BlocksVision ? 1 : 0);
                h = (h * 397) ^ (BlocksProjectile ? 1 : 0);
                h = (h * 397) ^ (int)CoverLevel;
                h = (h * 397) ^ (int)CoverDirections;
                h = (h * 397) ^ (PhaseFlipAllowed ? 1 : 0);
                h = (h * 397) ^ HazardousDamagePerTurn;
                return h;
            }
        }

        public static bool operator ==(TerrainDefinition a, TerrainDefinition b) => a.Equals(b);

        public static bool operator !=(TerrainDefinition a, TerrainDefinition b) => !a.Equals(b);

        public override string ToString()
            => $"TerrainDef({Type}, move={BaseMoveCost}, blockMv={BlocksMovement}, blockVis={BlocksVision}, cover={CoverLevel}, phaseFlip={PhaseFlipAllowed}, hazard={HazardousDamagePerTurn})";
    }
}