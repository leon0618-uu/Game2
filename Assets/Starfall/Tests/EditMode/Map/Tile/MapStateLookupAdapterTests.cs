using NUnit.Framework;
using Starfall.Core.Map.Coordinates;
using Starfall.Core.Map.Cover;
using Starfall.Core.Map.Height;
using Starfall.Core.Map.State;
using Starfall.Core.Map.Tile;

namespace Starfall.Tests.EditMode.Map.Tile
{
    /// <summary>
    /// doc2 MAP-04 §4.9 MapStateLookupAdapter 测试集。
    /// 覆盖：构造时填表、GetHeight / GetCover / BlocksLineOfSight 行为、未注册返回 null/0/false。
    /// </summary>
    public class MapStateLookupAdapterTests
    {
        private MapState _map;
        private TileDefinitionRegistry _registry;
        private MapStateLookupAdapter _adapter;
        private MapSize _size;

        [SetUp]
        public void SetUp()
        {
            _size = new MapSize(4, 4);
            var def = new MapDefinition("map.adapter", _size.Width, _size.Height,
                DimensionLayer.Reality, 0);
            _map = new MapState(def);

            _registry = new TileDefinitionRegistry(_size);

            // (0,0) Wall + Full cover
            _registry.Register(new TileDefinition(
                tileId: 1,
                coord: new GridCoord(0, 0),
                terrainType: TerrainType.Wall,
                terrain: TerrainRegistry.Wall,
                coverLevel: CoverLevel.Full));

            // (1,0) Plain + height 2
            _registry.Register(new TileDefinition(
                tileId: 2,
                coord: new GridCoord(1, 0),
                terrainType: TerrainType.Plain,
                terrain: TerrainRegistry.Plain,
                height: new HeightLevel(2)));

            // (2,0) Plain + height 0
            _registry.Register(new TileDefinition(
                tileId: 3,
                coord: new GridCoord(2, 0),
                terrainType: TerrainType.Plain,
                terrain: TerrainRegistry.Plain));

            // (0,1) Plain + custom blocks vision
            _registry.Register(new TileDefinition(
                tileId: 4,
                coord: new GridCoord(0, 1),
                terrainType: TerrainType.Plain,
                terrain: TerrainRegistry.Plain,
                blocksVision: true));

            _adapter = new MapStateLookupAdapter(_map, _registry);
        }

        // ──────────── 1. 构造时填表 ────────────

        [Test]
        public void Construction_PopulatesLookupTables()
        {
            Assert.AreEqual(4, _adapter.HeightEntryCount);
            Assert.AreEqual(4, _adapter.CoverEntryCount);
            Assert.AreEqual(2, _adapter.BlockingEntryCount); // Wall + custom BlocksVision
        }

        // ──────────── 2. GetHeight ────────────

        [Test]
        public void GetHeight_OnRegisteredTile_ReturnsHeight()
        {
            Assert.AreEqual(0, _adapter.GetHeight(new GridCoord(0, 0))); // Wall default height
            Assert.AreEqual(2, _adapter.GetHeight(new GridCoord(1, 0)));
            Assert.AreEqual(0, _adapter.GetHeight(new GridCoord(2, 0)));
        }

        [Test]
        public void GetHeight_OnUnregisteredTile_ReturnsZero()
        {
            Assert.AreEqual(0, _adapter.GetHeight(new GridCoord(3, 3)));
        }

        // ──────────── 3. GetCover ────────────

        [Test]
        public void GetCover_OnRegisteredTile_ReturnsCoverLevel()
        {
            Assert.AreEqual(CoverLevel.Full, _adapter.GetCover(new GridCoord(0, 0)));
            Assert.AreEqual(CoverLevel.None, _adapter.GetCover(new GridCoord(1, 0)));
            Assert.AreEqual(CoverLevel.None, _adapter.GetCover(new GridCoord(2, 0)));
        }

        [Test]
        public void GetCover_OnUnregisteredTile_ReturnsNull()
        {
            Assert.IsNull(_adapter.GetCover(new GridCoord(3, 3)));
        }

        // ──────────── 4. BlocksLineOfSight ────────────

        [Test]
        public void BlocksLineOfSight_OnWall_ReturnsTrue()
        {
            Assert.IsTrue(_adapter.BlocksLineOfSight(new GridCoord(0, 0)));
        }

        [Test]
        public void BlocksLineOfSight_OnPlain_ReturnsFalse()
        {
            Assert.IsFalse(_adapter.BlocksLineOfSight(new GridCoord(2, 0)));
        }

        [Test]
        public void BlocksLineOfSight_OnCustomBlocksVision_ReturnsTrue()
        {
            Assert.IsTrue(_adapter.BlocksLineOfSight(new GridCoord(0, 1)));
        }

        [Test]
        public void BlocksLineOfSight_OnUnregisteredTile_ReturnsFalse()
        {
            Assert.IsFalse(_adapter.BlocksLineOfSight(new GridCoord(3, 3)));
        }

        // ──────────── 5. Implements 3 interfaces ────────────

        [Test]
        public void ImplementsIHeightLookup_ICoverLookup_IBlockingLookup()
        {
            Assert.IsInstanceOf<Starfall.Core.Map.LineOfSight.IHeightLookup>(_adapter);
            Assert.IsInstanceOf<Starfall.Core.Map.LineOfSight.ICoverLookup>(_adapter);
            Assert.IsInstanceOf<Starfall.Core.Map.LineOfSight.IBlockingLookup>(_adapter);
        }

        // ──────────── 6. 空 registry ────────────

        [Test]
        public void EmptyRegistry_AllQueries_ReturnDefaults()
        {
            var emptyRegistry = new TileDefinitionRegistry(_size);
            var emptyAdapter = new MapStateLookupAdapter(_map, emptyRegistry);

            Assert.AreEqual(0, emptyAdapter.HeightEntryCount);
            Assert.AreEqual(0, emptyAdapter.CoverEntryCount);
            Assert.AreEqual(0, emptyAdapter.BlockingEntryCount);
            Assert.AreEqual(0, emptyAdapter.GetHeight(new GridCoord(0, 0)));
            Assert.IsNull(emptyAdapter.GetCover(new GridCoord(0, 0)));
            Assert.IsFalse(emptyAdapter.BlocksLineOfSight(new GridCoord(0, 0)));
        }

        // ──────────── 7. 单参数构造抛 InvalidOperationException（防止误用）────────────

        [Test]
        public void Constructor_WithoutRegistry_Throws()
        {
            Assert.Throws<System.InvalidOperationException>(() => new MapStateLookupAdapter(_map));
        }

        [Test]
        public void Constructor_NullMap_Throws()
        {
            Assert.Throws<System.ArgumentNullException>(() =>
                new MapStateLookupAdapter(null, _registry));
        }

        [Test]
        public void Constructor_NullRegistry_Throws()
        {
            Assert.Throws<System.ArgumentNullException>(() =>
                new MapStateLookupAdapter(_map, null));
        }
    }
}