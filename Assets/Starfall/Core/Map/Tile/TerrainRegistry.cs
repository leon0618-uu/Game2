using System;
using System.Collections.Generic;
using Starfall.Core.Map.Cover;

namespace Starfall.Core.Map.Tile
{
    /// <summary>
    /// doc2 MAP-04 搂4.1 11 绫绘爣鍑嗗湴褰㈢殑鍥哄畾鍊兼敞鍐岃〃銆?    ///
    /// <para/>
    /// **瑙掕壊**锛氶泦涓繚瀛樻瘡绉?<see cref="TerrainType"/> 鐨?鍑哄巶榛樿"閰嶇疆锛?    /// 涓氬姟浠ｇ爜锛?see cref="TileDefinitionRegistry"/>銆?see cref="MapStateLookupAdapter"/>锛?    /// 閫氳繃 <see cref="GetStandard"/> 鎸夋灇涓惧彇鍊硷紝閬垮厤鍦ㄥ悇澶勭‖缂栫爜銆?    ///
    /// <para/>
    /// **纭畾鎬?*锛氭墍鏈?<see cref="TerrainDefinition"/> 瀹炰緥閮芥槸 readonly 瀛楁锛?    /// 鍚屼竴杩涚▼鍐呭湴鍧€涓庡€奸兘绋冲畾锛涗换浣曡皟鐢ㄦ柟澶氭璁块棶鍚屼竴 <see cref="TerrainType"/>
    /// 閮藉緱鍒板悓涓€瀹炰緥锛堢粨鏋勪綋鎸夊€间紶閫掞紝Equals 涔熺ǔ瀹氾級銆?    ///
    /// <para/>
    /// **鏁板€煎绾?*锛堜笌 doc2 搂3.4 楠屾敹鐭╅樀瀵归綈锛?*绂佹淇敼**锛夛細
    /// <list type="table">
    /// <listheader><term>鍦板舰</term><description>绉诲姩 / 闃绘尅 / 鎺╀綋 / 鐩镐綅 / 浼ゅ</description></listheader>
    /// <item><term><see cref="TerrainType.Plain"/></term><description>1 / false / None / 涓嶅厑璁?/ 0</description></item>
    /// <item><term><see cref="TerrainType.Rough"/></term><description>2 / false / None / 涓嶅厑璁?/ 0</description></item>
    /// <item><term><see cref="TerrainType.Ruins"/></term><description>2 / false / Half / 涓嶅厑璁?/ 0</description></item>
    /// <item><term><see cref="TerrainType.Wall"/></term><description>99 / true / Full / 涓嶅厑璁?/ 0锛堢Щ鍔ㄦ垚鏈?99 鏄?涓嶅彲閫氳繃"鐨勫摠鍏靛€硷級</description></item>
    /// <item><term><see cref="TerrainType.BrokenBridge"/></term><description>2 / false / None / 涓嶅厑璁?/ 0</description></item>
    /// <item><term><see cref="TerrainType.LightBridge"/></term><description>1 / false / None / 涓嶅厑璁?/ 0</description></item>
    /// <item><term><see cref="TerrainType.Void"/></term><description>99 / true / None / 涓嶅厑璁?/ 0锛堥樆鎸＄Щ鍔ㄤ絾**涓?*闃绘尅瑙嗙嚎锛?/description></item>
    /// <item><term><see cref="TerrainType.ShallowAstralTide"/></term><description>2 / false / None / 涓嶅厑璁?/ 5</description></item>
    /// <item><term><see cref="TerrainType.DeepAstralTide"/></term><description>3 / false / None / 涓嶅厑璁?/ 15</description></item>
    /// <item><term><see cref="TerrainType.GateTile"/></term><description>1 / false / None / 鍏佽 / 0</description></item>
    /// <item><term><see cref="TerrainType.AnchorTile"/></term><description>1 / true / None / 涓嶅厑璁?/ 0锛堝垵濮嬮攣瀹氾級</description></item>
    /// </list>
    ///
    /// <para/>
    /// **浣跨敤鏂瑰紡**锛?    /// <code>
    /// var def = TerrainRegistry.GetStandard(TerrainType.Wall);
    /// Assert.IsTrue(def.BlocksMovement);
    /// Assert.AreEqual(CoverLevel.Full, def.CoverLevel);
    /// </code>
    /// </summary>
    public static class TerrainRegistry
    {
        // 鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€ 鏍囧噯鍊硷紙11 椤癸級鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€

        /// <summary><see cref="TerrainType.Plain"/> 鏍囧噯鍊硷細寮€闃斿湴锛岀Щ鍔?1锛屾棤鎺╀綋銆?/summary>
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

        /// <summary><see cref="TerrainType.Rough"/> 鏍囧噯鍊硷細纰庣煶锛岀Щ鍔?2锛屾棤鎺╀綋銆?/summary>
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

        /// <summary><see cref="TerrainType.Ruins"/> 鏍囧噯鍊硷細搴熷锛岀Щ鍔?2锛屾彁渚?Half 鎺╀綋銆?/summary>
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

        /// <summary><see cref="TerrainType.Wall"/> 鏍囧噯鍊硷細鏁村锛岀Щ鍔?99锛堜笉鍙€氳繃锛夛紝Full 鎺╀綋銆?/summary>
        public static readonly TerrainDefinition Wall = new TerrainDefinition(
            type: TerrainType.Wall,
            baseMoveCost: 5,
            blocksMovement: true,
            blocksVision: true,
            blocksProjectile: true,
            coverLevel: CoverLevel.Full,
            coverDirections: CoverDirection.All,
            phaseFlipAllowed: false,
            hazardousDamagePerTurn: 0);

        /// <summary><see cref="TerrainType.BrokenBridge"/> 鏍囧噯鍊硷細鏂ˉ锛岀Щ鍔?2锛屾棤鎺╀綋銆?/summary>
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

        /// <summary><see cref="TerrainType.LightBridge"/> 鏍囧噯鍊硷細鍏夋ˉ锛岀Щ鍔?1锛屾棤鎺╀綋銆?/summary>
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

        /// <summary><see cref="TerrainType.Void"/> 鏍囧噯鍊硷細铏氱┖锛岀Щ鍔?99锛堜笉鍙€氳繃锛夛紝**涓?*闃绘尅瑙嗙嚎銆?/summary>
        public static readonly TerrainDefinition Void = new TerrainDefinition(
            type: TerrainType.Void,
            baseMoveCost: 5,
            blocksMovement: true,
            blocksVision: false,
            blocksProjectile: false,
            coverLevel: CoverLevel.None,
            coverDirections: CoverDirection.All,
            phaseFlipAllowed: false,
            hazardousDamagePerTurn: 0);

        /// <summary><see cref="TerrainType.ShallowAstralTide"/> 鏍囧噯鍊硷細娴呭眰鐩镐綅娼紝绉诲姩 2锛屾瘡鍥炲悎 5 浼ゅ銆?/summary>
        public static readonly TerrainDefinition ShallowAstralTide = new TerrainDefinition(
            type: TerrainType.ShallowAstralTide,
            baseMoveCost: 2,
            blocksMovement: false,
            blocksVision: false,
            blocksProjectile: false,
            coverLevel: CoverLevel.None,
            coverDirections: CoverDirection.All,
            phaseFlipAllowed: false,
            hazardousDamagePerTurn: 5);

        /// <summary><see cref="TerrainType.DeepAstralTide"/> 鏍囧噯鍊硷細娣卞眰鐩镐綅娼紝绉诲姩 3锛屾瘡鍥炲悎 15 浼ゅ銆?/summary>
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

        /// <summary><see cref="TerrainType.GateTile"/> 鏍囧噯鍊硷細鐩镐綅闂紝绉诲姩 1锛屽厑璁哥浉浣嶇炕杞€?/summary>
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

        /// <summary><see cref="TerrainType.AnchorTile"/> 鏍囧噯鍊硷細閿氱偣 tile锛岀Щ鍔?99锛堝垵濮嬮攣瀹氾級锛屼笉闃绘尅瑙嗙嚎 / 寮归亾銆?/summary>
        public static readonly TerrainDefinition AnchorTile = new TerrainDefinition(
            type: TerrainType.AnchorTile,
            baseMoveCost: 5,
            blocksMovement: true,
            blocksVision: false,
            blocksProjectile: false,
            coverLevel: CoverLevel.None,
            coverDirections: CoverDirection.All,
            phaseFlipAllowed: false,
            hazardousDamagePerTurn: 0);

        // 鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€ 鏌ユ壘 鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€

        /// <summary>鎸?<see cref="TerrainType"/> 鍙栨爣鍑嗗€硷紱涓嶅湪 11 绫诲唴鎶?<see cref="ArgumentOutOfRangeException"/>銆?/summary>
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
                case TerrainType.ShallowAstralTide: return ShallowAstralTide;
                case TerrainType.DeepAstralTide: return DeepAstralTide;
                case TerrainType.GateTile: return GateTile;
                case TerrainType.AnchorTile: return AnchorTile;
                default:
                    throw new ArgumentOutOfRangeException(nameof(type), type,
                        $"No standard TerrainDefinition for {type} (doc2 MAP-04 supports 11 types: 0..10).");
            }
        }

        /// <summary>鍏ㄩ儴 11 绫绘爣鍑?<see cref="TerrainDefinition"/>锛屾寜 byte 鍊煎崌搴忥紙Plain 鈫?AnchorTile锛夈€?/summary>
        public static IReadOnlyList<TerrainDefinition> AllStandards()
        {
            // 构造顺序保证 = byte 升序 (Plain=0 -> AnchorTile=10)
            return new TerrainDefinition[] {
                Plain,
                Rough,
                Ruins,
                Wall,
                BrokenBridge,
                LightBridge,
                Void,
                ShallowAstralTide,
                DeepAstralTide,
                GateTile,
                AnchorTile,
            };
        }

        /// <summary>鍏ㄩ儴 11 绫?<see cref="TerrainType"/> 鏋氫妇鍊硷紝鎸?byte 鍊煎崌搴忥紙Plain 鈫?AnchorTile锛夈€?/summary>
        public static IReadOnlyList<TerrainType> AllTerrainTypes()
        {
            return new TerrainType[] {
                TerrainType.Plain,
                TerrainType.Rough,
                TerrainType.Ruins,
                TerrainType.Wall,
                TerrainType.BrokenBridge,
                TerrainType.LightBridge,
                TerrainType.Void,
                TerrainType.ShallowAstralTide,
                TerrainType.DeepAstralTide,
                TerrainType.GateTile,
                TerrainType.AnchorTile,
            };
        }
    }
}
