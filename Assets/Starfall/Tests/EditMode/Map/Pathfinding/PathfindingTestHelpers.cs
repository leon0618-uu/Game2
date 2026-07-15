using System.Collections.Generic;
using Starfall.Core.Map.Coordinates;
using Starfall.Core.Map.Height;
using Starfall.Core.Map.State;
using Starfall.Core.Map.Tile;

namespace Starfall.Tests.EditMode.Map.Pathfinding
{
    /// <summary>
    /// Pathfinding/MovementRange/MapPassability test fixtures.
    ///
    /// <para/>
    /// All helpers construct a self-contained <see cref="MapState"/> +
    /// <see cref="TileDefinitionRegistry"/> (attached) so each test can
    /// exercise <see cref="TileOccupancyService.IsCellPassable"/> +
    /// height-delta traversal + cost-based A*.
    /// </summary>
    internal static class PathfindingTestHelpers
    {
        /// <summary>Construct an 8x8 (or NxM) plain map with a registry attached.</summary>
        public static (MapState map, TileDefinitionRegistry registry) MakePlainMap(
            int width = 8,
            int height = 8,
            HeightLevel defaultHeight = default,
            int baseMoveCost = 1,
            DimensionLayer layer = DimensionLayer.Reality)
        {
            var size = new MapSize(width, height);
            var def = new MapDefinition("map.path05", size.Width, size.Height, layer, 0);
            var map = new MapState(def);

            var registry = new TileDefinitionRegistry(size);
            int id = 1;
            var plainTerrain = TerrainRegistry.Plain;
            for (int y = 0; y < size.Height; y++)
            {
                for (int x = 0; x < size.Width; x++)
                {
                    var tile = new TileDefinition(
                        tileId: id++,
                        coord: new GridCoord(x, y, layer),
                        terrainType: TerrainType.Plain,
                        terrain: plainTerrain,
                        height: defaultHeight,
                        baseMoveCost: baseMoveCost);
                    registry.Register(tile);
                    map.AddTile(new GridCoord(x, y, layer));
                }
            }

            TileOccupancyService.Clear();
            TileOccupancyService.AttachTileDefinitionRegistry(map, registry);
            return (map, registry);
        }

        /// <summary>
        /// Construct an NxM map with BOTH Reality AND Astral layers registered
        /// in the same <see cref="TileDefinitionRegistry"/>. Used for cross-layer tests
        /// where the Astral target tile must be in the registry so
        /// <see cref="TileOccupancyService.IsCellPassable"/> returns true.
        /// </summary>
        public static (MapState map, TileDefinitionRegistry registry) MakeDualLayerMap(
            int width = 8,
            int height = 8,
            HeightLevel defaultHeight = default,
            int baseMoveCost = 1)
        {
            var size = new MapSize(width, height);
            var def = new MapDefinition("map.path05.dual", size.Width, size.Height, DimensionLayer.Reality, 0);
            var map = new MapState(def);

            var registry = new TileDefinitionRegistry(size);
            int id = 1;
            var plainTerrain = TerrainRegistry.Plain;

            // Register Reality layer first.
            for (int y = 0; y < size.Height; y++)
            {
                for (int x = 0; x < size.Width; x++)
                {
                    var tile = new TileDefinition(
                        tileId: id++,
                        coord: new GridCoord(x, y, DimensionLayer.Reality),
                        terrainType: TerrainType.Plain,
                        terrain: plainTerrain,
                        height: defaultHeight,
                        baseMoveCost: baseMoveCost);
                    registry.Register(tile);
                    map.AddTile(new GridCoord(x, y, DimensionLayer.Reality));
                }
            }
            // Register Astral layer (tile ids offset to remain unique).
            for (int y = 0; y < size.Height; y++)
            {
                for (int x = 0; x < size.Width; x++)
                {
                    var tile = new TileDefinition(
                        tileId: id++,
                        coord: new GridCoord(x, y, DimensionLayer.Astral),
                        terrainType: TerrainType.Plain,
                        terrain: plainTerrain,
                        height: defaultHeight,
                        baseMoveCost: baseMoveCost);
                    registry.Register(tile);
                    map.AddTile(new GridCoord(x, y, DimensionLayer.Astral));
                }
            }

            TileOccupancyService.Clear();
            TileOccupancyService.AttachTileDefinitionRegistry(map, registry);
            return (map, registry);
        }

        /// <summary>Set (X, Y) tile to a blocking type (uses Wall terrain).</summary>
        public static void Block(MapState map, TileDefinitionRegistry registry, int x, int y, DimensionLayer layer = DimensionLayer.Reality)
        {
            var wallTerrain = TerrainRegistry.Wall;
            var prev = registry.TryGetByCoord(new GridCoord(x, y, layer), out var oldDef) ? oldDef : default;
            registry.Remove(new GridCoord(x, y, layer));
            if (prev.TileId != 0)
            {
                registry.RemoveById(prev.TileId);
            }
            int newId = (map.Definition.Size.Width * map.Definition.Size.Height * 100) + (y * 256 + x);
            var tile = new TileDefinition(
                tileId: newId,
                coord: new GridCoord(x, y, layer),
                terrainType: TerrainType.Wall,
                terrain: wallTerrain,
                height: default,
                baseMoveCost: 5);
            registry.Register(tile);
        }

        /// <summary>Set (X, Y) tile to a specific height (still walkable).</summary>
        public static void SetHeight(TileDefinitionRegistry registry, int x, int y, HeightLevel h, DimensionLayer layer = DimensionLayer.Reality)
        {
            var coord = new GridCoord(x, y, layer);
            if (!registry.TryGetByCoord(coord, out var def)) return;
            registry.Update(def.TileId, new TileDefinition(
                tileId: def.TileId,
                coord: coord,
                terrainType: def.TerrainType,
                terrain: def.Terrain,
                height: h,
                baseMoveCost: def.BaseMoveCost));
        }

        /// <summary>Detach all + clear occupancy registry.</summary>
        public static void Teardown(MapState map)
        {
            TileOccupancyService.DetachAll(map);
            TileOccupancyService.Clear();
        }
    }
}
