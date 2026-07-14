using System;
using System.Collections.Generic;
using Starfall.Core.Map.Cover;

namespace Starfall.Core.Map.Tile
{
    /// <summary>
    /// doc2 MAP-04 §4.1 11 类标准地形的固定值注册表。
    ///
    /// <para/>
    /// **角色**：集中保存每种 <see cref="TerrainType"/> 的"出厂默认"配置，
    /// 业务代码（<see cref="TileDefinitionRegistry"/>、<see cref="MapStateLookupAdapter"/>）
    /// 通过 <see cref="GetStandard"/> 按枚举取值，避免在各处硬编码。
    ///
    /// <para/>
    /// **确定性**：所有 <see cref="TerrainDefinition"/> 实例都是 readonly 字段，
    /// 同一进程内地址与值都稳定；任何调用方多次访问同一 <see cref="TerrainType"/>
    /// 都得到同一实例（结构体按值传递，Equals 也稳定）。
    ///
    /// <para/>
    /// **数值契约**（与 doc2 §3.4 验收矩阵对齐，**禁止修改**）：
    /// <list type="table">
    /// <listheader><term>地形</term><description>移动 / 阻挡 / 掩体 / 相位 / 伤害</description></listheader>
    /// <item><term><see cref="TerrainType.Plain"/></term><description>1 / false / None / 不允许 / 0</description></item>
    /// <item><term><see cref="TerrainType.Rough"/></term><description>2 / false / None / 不允许 / 0</description></item>
    /// <item><term><see cref="TerrainType.Ruins"/></term><description>2 / false / Half / 不允许 / 0</description></item>
    /// <item><term><see cref="TerrainType.Wall"/></term><description>99 / true / Full / 不允许 / 0（移动成本 99 是"不可通过"的哨兵值）</description></item>
    /// <item><term><see cref="TerrainType.BrokenBridge"/></term><description>2 / false / None / 不允许 / 0</description></item>
    /// <item><term><see cref="TerrainType.LightBridge"/></term><description>1 / false / None / 不允许 / 0</description></item>
    /// <item><term><see cref="TerrainType.Void"/></term><description>99 / true / None / 不允许 / 0（阻挡移动但**不**阻挡视线）</description></item>
    /// <item><term><see cref="TerrainType.ShalterAstralTide"/></term><description>2 / false / None / 不允许 / 5</description></item>
    /// <item><term><see cref="TerrainType.DeepAstralTide"/></term><description>3 / false / None / 不允许 / 15</description></item>
    /// <item><term><see cref="TerrainType.GateTile"/></term><description>1 / false / None / 允许 / 0</description></item>
    /// <item><term><see cref="TerrainType.AnchorTile"/></term><description>1 / true / None / 不允许 / 0（初始锁定）</description></item>
    /// </list>
    ///
    /// <para/>
    /// **使用方式**：
    /// <code>
    /// var def = TerrainRegistry.GetStandard(TerrainType.Wall);
    /// Assert.IsTrue(def.BlocksMovement);
    /// Assert.AreEqual(CoverLevel.Full, def.CoverLevel);
    /// </code>
    /// </summary>
    public static class TerrainRegistry
    {
        // ──────────── 标准值（11 项）────────────

        /// <summary><see cref="TerrainType.Plain"/> 标准值：开阔地，移动 1，无掩体。</summary>
        public static readonly TerrainDefinition Plain = new TerrainDefinition(
            type: TerrainType.Plain,
            baseMoveCost: 1,
            blocksMovement: false,
            blocksVision: false,
            blocksProjectile: false,
            coverLevel: CoverLevel.None,
            coverDirections: CoverDirection.All,
            phaseFlipAllowed: false,
            hazardousDamagePerTurn: 0);

        /// <summary><see cref="TerrainType.Rough"/> 标准值：碎石，移动 2，无掩体。</summary>
        public static readonly TerrainDefinition Rough = new TerrainDefinition(
            type: TerrainType.Rough,
            baseMoveCost: 2,
            blocksMovement: false,
            blocksVision: false,
            blocksProjectile: false,
            coverLevel: CoverLevel.None,
            coverDirections: CoverDirection.All,
            phaseFlipAllowed: false,
            hazardousDamagePerTurn: 0);

        /// <summary><see cref="TerrainType.Ruins"/> 标准值：废墟，移动 2，提供 Half 掩体。</summary>
        public static readonly TerrainDefinition Ruins = new TerrainDefinition(
            type: TerrainType.Ruins,
            baseMoveCost: 2,
            blocksMovement: false,
            blocksVision: false,
            blocksProjectile: false,
            coverLevel: CoverLevel.Half,
            coverDirections: CoverDirection.All,
            phaseFlipAllowed: false,
            hazardousDamagePerTurn: 0);

        /// <summary><see cref="TerrainType.Wall"/> 标准值：整墙，移动 99（不可通过），Full 掩体。</summary>
        public static readonly TerrainDefinition Wall = new TerrainDefinition(
            type: TerrainType.Wall,
            baseMoveCost: 99,
            blocksMovement: true,
            blocksVision: true,
            blocksProjectile: true,
            coverLevel: CoverLevel.Full,
            coverDirections: CoverDirection.All,
            phaseFlipAllowed: false,
            hazardousDamagePerTurn: 0);

        /// <summary><see cref="TerrainType.BrokenBridge"/> 标准值：断桥，移动 2，无掩体。</summary>
        public static readonly TerrainDefinition BrokenBridge = new TerrainDefinition(
            type: TerrainType.BrokenBridge,
            baseMoveCost: 2,
            blocksMovement: false,
            blocksVision: false,
            blocksProjectile: false,
            coverLevel: CoverLevel.None,
            coverDirections: CoverDirection.All,
            phaseFlipAllowed: false,
            hazardousDamagePerTurn: 0);

        /// <summary><see cref="TerrainType.LightBridge"/> 标准值：光桥，移动 1，无掩体。</summary>
        public static readonly TerrainDefinition LightBridge = new TerrainDefinition(
            type: TerrainType.LightBridge,
            baseMoveCost: 1,
            blocksMovement: false,
            blocksVision: false,
            blocksProjectile: false,
            coverLevel: CoverLevel.None,
            coverDirections: CoverDirection.All,
            phaseFlipAllowed: false,
            hazardousDamagePerTurn: 0);

        /// <summary><see cref="TerrainType.Void"/> 标准值：虚空，移动 99（不可通过），**不**阻挡视线。</summary>
        public static readonly TerrainDefinition Void = new TerrainDefinition(
            type: TerrainType.Void,
            baseMoveCost: 99,
            blocksMovement: true,
            blocksVision: false,
            blocksProjectile: false,
            coverLevel: CoverLevel.None,
            coverDirections: CoverDirection.All,
            phaseFlipAllowed: false,
            hazardousDamagePerTurn: 0);

        /// <summary><see cref="TerrainType.ShalterAstralTide"/> 标准值：浅层相位潮，移动 2，每回合 5 伤害。</summary>
        public static readonly TerrainDefinition ShalterAstralTide = new TerrainDefinition(
            type: TerrainType.ShalterAstralTide,
            baseMoveCost: 2,
            blocksMovement: false,
            blocksVision: false,
            blocksProjectile: false,
            coverLevel: CoverLevel.None,
            coverDirections: CoverDirection.All,
            phaseFlipAllowed: false,
            hazardousDamagePerTurn: 5);

        /// <summary><see cref="TerrainType.DeepAstralTide"/> 标准值：深层相位潮，移动 3，每回合 15 伤害。</summary>
        public static readonly TerrainDefinition DeepAstralTide = new TerrainDefinition(
            type: TerrainType.DeepAstralTide,
            baseMoveCost: 3,
            blocksMovement: false,
            blocksVision: false,
            blocksProjectile: false,
            coverLevel: CoverLevel.None,
            coverDirections: CoverDirection.All,
            phaseFlipAllowed: false,
            hazardousDamagePerTurn: 15);

        /// <summary><see cref="TerrainType.GateTile"/> 标准值：相位门，移动 1，允许相位翻转。</summary>
        public static readonly TerrainDefinition GateTile = new TerrainDefinition(
            type: TerrainType.GateTile,
            baseMoveCost: 1,
            blocksMovement: false,
            blocksVision: false,
            blocksProjectile: false,
            coverLevel: CoverLevel.None,
            coverDirections: CoverDirection.All,
            phaseFlipAllowed: true,
            hazardousDamagePerTurn: 0);

        /// <summary><see cref="TerrainType.AnchorTile"/> 标准值：锚点 tile，移动 99（初始锁定），不阻挡视线 / 弹道。</summary>
        public static readonly TerrainDefinition AnchorTile = new TerrainDefinition(
            type: TerrainType.AnchorTile,
            baseMoveCost: 99,
            blocksMovement: true,
            blocksVision: false,
            blocksProjectile: false,
            coverLevel: CoverLevel.None,
            coverDirections: CoverDirection.All,
            phaseFlipAllowed: false,
            hazardousDamagePerTurn: 0);

        // ──────────── 查找 ────────────

        /// <summary>按 <see cref="TerrainType"/> 取标准值；不在 11 类内抛 <see cref="ArgumentOutOfRangeException"/>。</summary>
        public static TerrainDefinition GetStandard(TerrainType type)
        {
            switch (type)
            {
                case TerrainType.Plain: return Plain;
                case TerrainType.Rough: return Rough;
                case TerrainType.Ruins: return Ruins;
                case TerrainType.Wall: return Wall;
                case TerrainType.BrokenBridge: return BrokenBridge;
                case TerrainType.LightBridge: return LightBridge;
                case TerrainType.Void: return Void;
                case TerrainType.ShalterAstralTide: return ShalterAstralTide;
                case TerrainType.DeepAstralTide: return DeepAstralTide;
                case TerrainType.GateTile: return GateTile;
                case TerrainType.AnchorTile: return AnchorTile;
                default:
                    throw new ArgumentOutOfRangeException(nameof(type), type,
                        $"No standard TerrainDefinition for {type} (doc2 MAP-04 supports 11 types: 0..10).");
            }
        }

        /// <summary>全部 11 类标准 <see cref="TerrainDefinition"/>，按 byte 值升序（Plain → AnchorTile）。</summary>
        public static IReadOnlyList<TerrainDefinition> AllStandards()
        {
            // 构造顺序保证 = byte 升序（0..10）。
            return new TerrainDefinition[]
            {
                Plain,
                Rough,
                Ruins,
                Wall,
                BrokenBridge,
                LightBridge,
                Void,
                ShalterAstralTide,
                DeepAstralTide,
                GateTile,
                AnchorTile,
            };
        }

        /// <summary>全部 11 类 <see cref="TerrainType"/> 枚举值，按 byte 值升序（Plain → AnchorTile）。</summary>
        public static IReadOnlyList<TerrainType> AllTerrainTypes()
        {
            return new TerrainType[]
            {
                TerrainType.Plain,
                TerrainType.Rough,
                TerrainType.Ruins,
                TerrainType.Wall,
                TerrainType.BrokenBridge,
                TerrainType.LightBridge,
                TerrainType.Void,
                TerrainType.ShalterAstralTide,
                TerrainType.DeepAstralTide,
                TerrainType.GateTile,
                TerrainType.AnchorTile,
            };
        }
    }
}