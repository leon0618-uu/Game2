using System.Collections.Generic;
using NUnit.Framework;
using Starfall.Core.Map.Coordinates;
using Starfall.Core.Map.State;
using Starfall.Core.Map.Tile;

namespace Starfall.Tests.EditMode.Map.Tile
{
    /// <summary>
    /// doc2 MAP-04 §4.8 TileOccupancyService 测试集。
    /// 覆盖：单格 / 2x2 / 3x3 放置、越界、Impassable、占用、移除、跨 Layer、Stability=0 拒绝。
    /// </summary>
    public class TileOccupancyServiceTests
    {
        private MapState _map;
        private TileDefinitionRegistry _registry;
        private MapSize _size;

        [SetUp]
        public void SetUp()
        {
            _size = new MapSize(8, 8);
            var def = new MapDefinition("map.test", _size.Width, _size.Height,
                DimensionLayer.Reality, 0);
            _map = new MapState(def);

            // 注册 8x8x2 全部 Plain tile（除 Walls）。
            _registry = new TileDefinitionRegistry(_size);
            for (int x = 0; x < _size.Width; x++)
            {
                for (int y = 0; y < _size.Height; y++)
                {
                    var terrain = TerrainRegistry.Plain;
                    if (x == 0 && y == 0)
                    {
                        // (0,0) 注册为 Wall。
                        terrain = TerrainRegistry.Wall;
                    }
                    var tileDef = new TileDefinition(
                        tileId: y * _size.Width + x + 1,
                        coord: new GridCoord(x, y),
                        terrainType: terrain.Type,
                        terrain: terrain);
                    _registry.Register(tileDef);
                    _map.AddTile(tileDef.Coord);
                }
            }

            // MAP-04 setUp addition: register Astral layer too (occupancy cross-layer tests require it).
            for (int x = 0; x < _size.Width; x++)
            {
                for (int y = 0; y < _size.Height; y++)
                {
                    var tileDefAstral = new TileDefinition(
                        tileId: 10000 + y * _size.Width + x + 1,
                        coord: new GridCoord(x, y, DimensionLayer.Astral),
                        terrainType: TerrainRegistry.Plain.Type,
                        terrain: TerrainRegistry.Plain);
                    _registry.Register(tileDefAstral);
                    _map.AddTile(tileDefAstral.Coord);
                }
            }
            TileOccupancyService.Clear();
            TileOccupancyService.AttachTileDefinitionRegistry(_map, _registry);
        }

        [TearDown]
        public void TearDown()
        {
            TileOccupancyService.DetachAll(_map);
            TileOccupancyService.Clear();
        }

        // ──────────── 1. 单格放置成功 ────────────

        [Test]
        public void TryPlaceUnit_SingleCell_Success()
        {
            var anchor = new GridCoord(3, 3);
            Assert.IsTrue(TileOccupancyService.TryPlaceUnit(_map, unitId: 1, Footprint.SingleCell, anchor));
            Assert.IsTrue(TileOccupancyService.IsOccupied(_map, anchor));
            Assert.AreEqual(1, TileOccupancyService.GetOccupantUnit(_map, anchor));
        }

        // ──────────── 2. 2x2 放置 4 cell 全部占用 ────────────

        [Test]
        public void TryPlaceUnit_TwoByTwo_Success_AllCellsOccupied()
        {
            var anchor = new GridCoord(3, 3);
            Assert.IsTrue(TileOccupancyService.TryPlaceUnit(_map, unitId: 1, Footprint.TwoByTwo, anchor));

            var expected = new[]
            {
                new GridCoord(3, 3),
                new GridCoord(4, 3),
                new GridCoord(3, 4),
                new GridCoord(4, 4),
            };
            foreach (var c in expected)
            {
                Assert.IsTrue(TileOccupancyService.IsOccupied(_map, c), $"{c} should be occupied.");
                Assert.AreEqual(1, TileOccupancyService.GetOccupantUnit(_map, c));
            }
        }

        // ──────────── 3. 3x3 放置 9 cell 全部占用 ────────────

        [Test]
        public void TryPlaceUnit_ThreeByThree_Success_AllCellsOccupied()
        {
            var anchor = new GridCoord(2, 2);
            Assert.IsTrue(TileOccupancyService.TryPlaceUnit(_map, unitId: 1, Footprint.ThreeByThree, anchor));

            for (int dy = 0; dy < 3; dy++)
                for (int dx = 0; dx < 3; dx++)
                {
                    var c = new GridCoord(anchor.X + dx, anchor.Y + dy);
                    Assert.IsTrue(TileOccupancyService.IsOccupied(_map, c));
                }
        }

        // ──────────── 4. 越界返回 false ────────────

        [Test]
        public void TryPlaceUnit_OutOfBounds_ReturnsFalse()
        {
            Assert.IsFalse(TileOccupancyService.TryPlaceUnit(_map, unitId: 1, Footprint.TwoByTwo, new GridCoord(7, 0)));
            Assert.IsFalse(TileOccupancyService.TryPlaceUnit(_map, unitId: 1, Footprint.SingleCell, new GridCoord(-1, 0)));
            Assert.IsFalse(TileOccupancyService.TryPlaceUnit(_map, unitId: 1, Footprint.ThreeByThree, new GridCoord(6, 6)));
        }

        // ──────────── 5. Impassable 上放置返回 false ────────────

        [Test]
        public void TryPlaceUnit_OnWall_ReturnsFalse()
        {
            // (0,0) 注册为 Wall。
            Assert.IsFalse(TileOccupancyService.TryPlaceUnit(_map, unitId: 1, Footprint.SingleCell, new GridCoord(0, 0)));
        }

        // ──────────── 6. 已有 unit 占用的 cell 上放置返回 false ────────────

        [Test]
        public void TryPlaceUnit_OnOccupiedCell_ReturnsFalse()
        {
            Assert.IsTrue(TileOccupancyService.TryPlaceUnit(_map, unitId: 1, Footprint.SingleCell, new GridCoord(2, 2)));
            // 同一 cell 放另一个 unit 失败。
            Assert.IsFalse(TileOccupancyService.TryPlaceUnit(_map, unitId: 2, Footprint.SingleCell, new GridCoord(2, 2)));
            // 部分重叠的 2x2 也失败。
            Assert.IsFalse(TileOccupancyService.TryPlaceUnit(_map, unitId: 3, Footprint.TwoByTwo, new GridCoord(2, 2)));
        }

        // ──────────── 7. 同一 unit 重复放置返回 false ────────────

        [Test]
        public void TryPlaceUnit_DuplicateUnit_ReturnsFalse()
        {
            Assert.IsTrue(TileOccupancyService.TryPlaceUnit(_map, unitId: 1, Footprint.SingleCell, new GridCoord(2, 2)));
            Assert.IsFalse(TileOccupancyService.TryPlaceUnit(_map, unitId: 1, Footprint.SingleCell, new GridCoord(3, 3)));
        }

        // ──────────── 8. 移除 unit 释放所有 cells ────────────

        [Test]
        public void TryRemoveUnit_ReleasesAllFootprintCells()
        {
            Assert.IsTrue(TileOccupancyService.TryPlaceUnit(_map, unitId: 1, Footprint.TwoByTwo, new GridCoord(3, 3)));
            Assert.IsTrue(TileOccupancyService.TryRemoveUnit(_map, unitId: 1));

            var cells = new[]
            {
                new GridCoord(3, 3), new GridCoord(4, 3),
                new GridCoord(3, 4), new GridCoord(4, 4),
            };
            foreach (var c in cells)
                Assert.IsFalse(TileOccupancyService.IsOccupied(_map, c));

            // 可以重新放置。
            Assert.IsTrue(TileOccupancyService.TryPlaceUnit(_map, unitId: 2, Footprint.SingleCell, new GridCoord(3, 3)));
        }

        [Test]
        public void TryRemoveUnit_NonExistent_ReturnsFalse()
        {
            Assert.IsFalse(TileOccupancyService.TryRemoveUnit(_map, unitId: 999));
        }

        // ──────────── 9. 多次放置不同 unitId 共存 ────────────

        [Test]
        public void TryPlaceUnit_MultipleUnits_Coexist()
        {
            Assert.IsTrue(TileOccupancyService.TryPlaceUnit(_map, unitId: 1, Footprint.SingleCell, new GridCoord(2, 2)));
            Assert.IsTrue(TileOccupancyService.TryPlaceUnit(_map, unitId: 2, Footprint.SingleCell, new GridCoord(3, 3)));
            Assert.IsTrue(TileOccupancyService.TryPlaceUnit(_map, unitId: 3, Footprint.SingleCell, new GridCoord(4, 4)));

            Assert.AreEqual(1, TileOccupancyService.GetOccupantUnit(_map, new GridCoord(2, 2)));
            Assert.AreEqual(2, TileOccupancyService.GetOccupantUnit(_map, new GridCoord(3, 3)));
            Assert.AreEqual(3, TileOccupancyService.GetOccupantUnit(_map, new GridCoord(4, 4)));
        }

        // ──────────── 10. 跨 Layer 不可放 ────────────

        [Test]
        public void TryPlaceUnit_CrossLayer_TreatedAsDistinctTile()
        {
            // Reality 上放置。
            Assert.IsTrue(TileOccupancyService.TryPlaceUnit(_map, unitId: 1, Footprint.SingleCell, new GridCoord(2, 2, DimensionLayer.Reality)));
            // Astral 同 (X, Y) 不视为占用，可以放置另一个 unit。
            Assert.IsTrue(TileOccupancyService.TryPlaceUnit(_map, unitId: 2, Footprint.SingleCell, new GridCoord(2, 2, DimensionLayer.Astral)));
        }

        // ──────────── 11. Stability=0 tile 上放返回 false ────────────

        [Test]
        public void TryPlaceUnit_OnCollapsedTile_ReturnsFalse()
        {
            // 挂载 runtime states，把 (3,3) 设为 Stability=0。
            var states = new Dictionary<GridCoord, MapTileState>();
            var def3 = _registry.All().FirstOrDefaultByCoord(new GridCoord(3, 3));
            states[new GridCoord(3, 3)] = new MapTileState(def3) { Stability = 0 };
            TileOccupancyService.AttachRuntimeStates(_map, states);

            Assert.IsFalse(TileOccupancyService.TryPlaceUnit(_map, unitId: 1, Footprint.SingleCell, new GridCoord(3, 3)));
        }

        [Test]
        public void TryPlaceUnit_OnStableTile_Succeeds()
        {
            var states = new Dictionary<GridCoord, MapTileState>();
            var def3 = _registry.All().FirstOrDefaultByCoord(new GridCoord(3, 3));
            states[new GridCoord(3, 3)] = new MapTileState(def3) { Stability = 50 };
            TileOccupancyService.AttachRuntimeStates(_map, states);

            Assert.IsTrue(TileOccupancyService.TryPlaceUnit(_map, unitId: 1, Footprint.SingleCell, new GridCoord(3, 3)));
        }

        // ──────────── 12. 对象放置 ────────────

        [Test]
        public void TryPlaceObject_SingleCell_Success()
        {
            Assert.IsTrue(TileOccupancyService.TryPlaceObject(_map, objectId: 100, Footprint.SingleCell, new GridCoord(2, 2)));
            Assert.AreEqual(100, TileOccupancyService.GetOccupantObject(_map, new GridCoord(2, 2)));
        }

        [Test]
        public void TryPlaceObject_OnUnitOccupiedCell_ReturnsFalse()
        {
            Assert.IsTrue(TileOccupancyService.TryPlaceUnit(_map, unitId: 1, Footprint.SingleCell, new GridCoord(2, 2)));
            Assert.IsFalse(TileOccupancyService.TryPlaceObject(_map, objectId: 100, Footprint.SingleCell, new GridCoord(2, 2)));
        }

        // ──────────── 13. GetUnitCells / GetObjectCells ────────────

        [Test]
        public void GetUnitCells_ReturnsFootprintCells()
        {
            Assert.IsTrue(TileOccupancyService.TryPlaceUnit(_map, unitId: 1, Footprint.TwoByTwo, new GridCoord(3, 3)));
            var cells = TileOccupancyService.GetUnitCells(1);
            Assert.IsNotNull(cells);
            Assert.AreEqual(4, cells.Count);
        }

        [Test]
        public void GetUnitCells_NonExistent_ReturnsNull()
        {
            Assert.IsNull(TileOccupancyService.GetUnitCells(999));
        }

        // ──────────── 14. 稳定性（确定排序）────────────

        [Test]
        public void FootprintCells_AreReturnedInStableYThenXOrder()
        {
            Assert.IsTrue(TileOccupancyService.TryPlaceUnit(_map, unitId: 1, Footprint.TwoByTwo, new GridCoord(3, 3)));
            var cells = TileOccupancyService.GetUnitCells(1);
            for (int i = 1; i < cells.Count; i++)
            {
                Assert.Less(cells[i - 1].CompareTo(cells[i]), 0);
            }
        }
    }

    internal static class TileOccupancyTestExtensions
    {
        public static TileDefinition FirstOrDefaultByCoord(this IReadOnlyList<TileDefinition> all, GridCoord coord)
        {
            foreach (var d in all)
                if (d.Coord == coord) return d;
            return default;
        }
    }
}