using System.Collections.Generic;
using Starfall.Core.Anchor;
using Starfall.Core.Map;
using Starfall.Core.Map.Commands;
using Starfall.Core.Map.Coordinates;
using Starfall.Core.Map.Height;
using Starfall.Core.Map.State;
using Starfall.Core.Map.Tile;
using Starfall.Core.Model;

namespace Starfall.Tests.EditMode.Map.Commands
{
    /// <summary>
    /// doc2 MAP-03 测试基础设施：构造测试用 MapState + TileDefinitionRegistry +
    /// runtime states（一次性 attach 到 <see cref="PhaseFlipStateService"/> /
    /// <see cref="AnchorStateService"/>）+ 几个常用 tile 配置。
    /// <para/>
    /// 在 NUnit <c>[SetUp]</c> 调用 <see cref="Attach"/>，<c>[TearDown]</c> 调用 <see cref="DetachAll"/>
    /// 即可保证测试隔离（与既有 MAP-08 测试同模式）。
    /// </summary>
    internal static class MapTestHarness
    {
        // 标准 tile id 区间：1..64 (8x8) 是 plain；65,66,67 是 3 个特殊 tile
        public const int PlainTileId = 1;
        public const int PhaseLockedTileId = 65;  // (3,3) PhaseLocked + PhaseFlippable
        public const int FlippableTileId = 66;    // (5,5) PhaseFlippable only
        public const int GateTileId = 67;         // (1,1) GateTile + PhaseFlippable
        public const int DestructibleTileId = 5;

        // 8x8 Reality 平面（每个 cell 一个 tile）
        public const int MapWidth = 8;
        public const int MapHeight = 8;

        /// <summary>新建一个空白 8x8 MapState（Doc2 §4.2 默认）。</summary>
        public static MapState MakeMap(DimensionLayer initialActiveLayer = DimensionLayer.Reality,
            int initialCV = 0)
        {
            var def = new MapDefinition(
                mapId: "map.test",
                width: MapWidth,
                height: MapHeight,
                initialActiveLayer: initialActiveLayer,
                initialGlobalCollapseValue: initialCV);
            return new MapState(def);
        }

        /// <summary>注册 8x8 = 64 个 Plain tile + 5 个特殊 tile + 同步 AddTile 至 MapState。</summary>
        public static (MapState map, TileDefinitionRegistry registry, Dictionary<int, MapTileState> states) Attach(
            MapState map,
            bool attachAnchorService = true)
        {
            var registry = new TileDefinitionRegistry(map.Definition.Size);

            // 基础 64 tile：plain，全部 Reality 层。
            var states = new Dictionary<int, MapTileState>(64);
            int id = PlainTileId;
            for (int y = 0; y < MapHeight; y++)
            {
                for (int x = 0; x < MapWidth; x++)
                {
                    var def = TileDefinitionRegistry.Make(id, new GridCoord(x, y), TerrainType.Plain);
                    registry.Register(def);
                    map.AddTile(new GridCoord(x, y));
                    states[id] = new MapTileState(def);
                    id++;
                }
            }
            // id 现在 = 65

            // 特殊 tile：(3, 3) PhaseLocked + PhaseFlippable
            // 先移除刚注册的 plain tile (3,3)，id = 3*8+4 = 28 (1-based row-major)
            registry.Remove(new GridCoord(3, 3));
            map.RemoveTile(new GridCoord(3, 3));
            registry.RemoveById(28);
            states.Remove(28);
            int lockedNewId = id++;
            var lockedDef = TileDefinitionRegistry.Make(
                lockedNewId, new GridCoord(3, 3), TerrainType.GateTile,
                tags: TileTags.PhaseFlippable | TileTags.PhaseLocked);
            registry.Register(lockedDef);
            map.AddTile(new GridCoord(3, 3));
            states[lockedNewId] = new MapTileState(lockedDef);

            // 特殊 tile：(5, 5) PhaseFlippable only
            registry.Remove(new GridCoord(5, 5));
            map.RemoveTile(new GridCoord(5, 5));
            int flippableIdPlain = 5 * 8 + 6; // 46
            registry.RemoveById(flippableIdPlain);
            states.Remove(flippableIdPlain);
            int flippableId = id++;
            var flippableDef = TileDefinitionRegistry.Make(
                flippableId, new GridCoord(5, 5), TerrainType.GateTile,
                tags: TileTags.PhaseFlippable);
            registry.Register(flippableDef);
            map.AddTile(new GridCoord(5, 5));
            states[flippableId] = new MapTileState(flippableDef);

            // 特殊 tile：(1, 1) GateTile with PhaseFlippable (no lock)
            registry.Remove(new GridCoord(1, 1));
            map.RemoveTile(new GridCoord(1, 1));
            int gatePlainId = 8 + 2; // 10
            registry.RemoveById(gatePlainId);
            states.Remove(gatePlainId);
            int gateId = id++;
            var gateDef = TileDefinitionRegistry.Make(
                gateId, new GridCoord(1, 1), TerrainType.GateTile,
                tags: TileTags.PhaseFlippable);
            registry.Register(gateDef);
            map.AddTile(new GridCoord(1, 1));
            states[gateId] = new MapTileState(gateDef);

            // 特殊 tile：(2, 2) Plain with no tags (基础 plain)
            // 已存在 28 个 plain 之一，无需特殊处理

            PhaseFlipStateService.AttachMapState(map, registry);
            PhaseFlipStateService.AttachRuntimeStates(map, states);
            if (attachAnchorService) AnchorStateService.Attach(map);

            return (map, registry, states);
        }

        /// <summary>清空所有 attach 模式的服务状态（[TearDown] 使用）。</summary>
        public static void DetachAll()
        {
            // 没有直接入口清理全部 map，但我们用 TileOccupancyService 同模式：
            // PhaseFlipStateService.Clear() 清全部 ——
            // 在测试里所有 [SetUp] 都是新 map，唯一性由 attach dictionary 自己保证（map 是新对象）
            PhaseFlipStateService.Clear();
            AnchorStateService.DetachAll();
        }

        /// <summary>构造函数 helper：构造 polygon。</summary>
        public static List<GridPos> Poly(params GridPos[] vertices)
            => new List<GridPos>(vertices);

        /// <summary>构造 <see cref="MapRegion"/> 测试用 tiles。</summary>
        public static List<GridCoord> TileCoords(params GridCoord[] tiles)
            => new List<GridCoord>(tiles);
    }
}
