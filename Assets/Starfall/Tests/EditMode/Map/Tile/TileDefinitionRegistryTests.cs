using System.Linq;
using NUnit.Framework;
using Starfall.Core.Map.Coordinates;
using Starfall.Core.Map.Tile;

namespace Starfall.Tests.EditMode.Map.Tile
{
    /// <summary>
    /// doc2 MAP-04 §4.5 TileDefinitionRegistry 测试集。
    /// 覆盖：注册、按 coord 查询、重复登记抛异常、越界抛异常、稳定遍历。
    /// </summary>
    public class TileDefinitionRegistryTests
    {
        private static MapSize Size8x8 => new MapSize(8, 8);

        private static TileDefinition Make(int tileId, int x, int y)
        {
            return TileDefinitionRegistry.Make(tileId, new GridCoord(x, y), TerrainType.Plain);
        }

        // ──────────── 1. 注册 + 按 Coord 查询 ────────────

        [Test]
        public void Register_AndGetByCoord_Works()
        {
            var registry = new TileDefinitionRegistry(Size8x8);
            var def = Make(1, 3, 2);
            registry.Register(def);

            Assert.IsTrue(registry.TryGetByCoord(new GridCoord(3, 2), out var retrieved));
            Assert.AreEqual(def, retrieved);
            Assert.AreEqual(1, registry.Count);
        }

        [Test]
        public void TryGetByCoord_Unregistered_ReturnsFalse()
        {
            var registry = new TileDefinitionRegistry(Size8x8);
            Assert.IsFalse(registry.TryGetByCoord(new GridCoord(0, 0), out var _));
        }

        [Test]
        public void TryGetById_Works()
        {
            var registry = new TileDefinitionRegistry(Size8x8);
            var def = Make(42, 5, 5);
            registry.Register(def);

            Assert.IsTrue(registry.TryGetById(42, out var retrieved));
            Assert.AreEqual(def, retrieved);
        }

        // ──────────── 2. 重复 Coord 登记抛异常 ────────────

        [Test]
        public void Register_DuplicateCoord_Throws()
        {
            var registry = new TileDefinitionRegistry(Size8x8);
            registry.Register(Make(1, 3, 2));
            Assert.Throws<System.ArgumentException>(() => registry.Register(Make(2, 3, 2)));
        }

        [Test]
        public void Register_DuplicateTileId_Throws()
        {
            var registry = new TileDefinitionRegistry(Size8x8);
            registry.Register(Make(1, 3, 2));
            Assert.Throws<System.ArgumentException>(() => registry.Register(Make(1, 5, 5)));
        }

        // ──────────── 3. 越界抛异常 ────────────

        [Test]
        public void Register_OutOfBounds_Throws()
        {
            var registry = new TileDefinitionRegistry(Size8x8);
            Assert.Throws<System.ArgumentOutOfRangeException>(() => registry.Register(Make(1, 8, 0)));
            Assert.Throws<System.ArgumentOutOfRangeException>(() => registry.Register(Make(1, 0, 8)));
            Assert.Throws<System.ArgumentOutOfRangeException>(() => registry.Register(Make(1, -1, 0)));
        }

        // ──────────── 4. 确定性遍历（按 Y→X→Layer 排序）────────────

        [Test]
        public void All_ReturnsSortedByYThenX()
        {
            var registry = new TileDefinitionRegistry(Size8x8);
            // 故意乱序插入。
            registry.Register(Make(1, 2, 3));
            registry.Register(Make(2, 0, 0));
            registry.Register(Make(3, 1, 1));
            registry.Register(Make(4, 0, 1));

            var all = registry.All();
            Assert.AreEqual(4, all.Count);
            // 期望顺序：(0,0), (0,1), (1,1), (2,3) — 按 Y→X 升序。
            Assert.AreEqual(new GridCoord(0, 0), all[0].Coord);
            Assert.AreEqual(new GridCoord(0, 1), all[1].Coord);
            Assert.AreEqual(new GridCoord(1, 1), all[2].Coord);
            Assert.AreEqual(new GridCoord(2, 3), all[3].Coord);
        }

        [Test]
        public void AllCoords_YieldsInSortedOrder()
        {
            var registry = new TileDefinitionRegistry(Size8x8);
            registry.Register(Make(1, 5, 0));
            registry.Register(Make(2, 0, 0));
            registry.Register(Make(3, 2, 2));

            var coords = registry.AllCoords().ToList();
            Assert.AreEqual(3, coords.Count);
            Assert.AreEqual(new GridCoord(0, 0), coords[0]);
            Assert.AreEqual(new GridCoord(5, 0), coords[1]);
            Assert.AreEqual(new GridCoord(2, 2), coords[2]);
        }

        // ──────────── 5. 移除 + 计数 ────────────

        [Test]
        public void Remove_ByCoord_DecreasesCount()
        {
            var registry = new TileDefinitionRegistry(Size8x8);
            registry.Register(Make(1, 0, 0));
            registry.Register(Make(2, 1, 0));
            Assert.AreEqual(2, registry.Count);

            Assert.IsTrue(registry.Remove(new GridCoord(0, 0)));
            Assert.AreEqual(1, registry.Count);
            Assert.IsFalse(registry.TryGetByCoord(new GridCoord(0, 0), out var _));
            Assert.IsFalse(registry.TryGetById(1, out var _));
        }

        [Test]
        public void RemoveById_DecreasesCount()
        {
            var registry = new TileDefinitionRegistry(Size8x8);
            registry.Register(Make(1, 0, 0));
            Assert.IsTrue(registry.RemoveById(1));
            Assert.AreEqual(0, registry.Count);
        }

        [Test]
        public void Remove_NonExistent_ReturnsFalse()
        {
            var registry = new TileDefinitionRegistry(Size8x8);
            Assert.IsFalse(registry.Remove(new GridCoord(0, 0)));
            Assert.IsFalse(registry.RemoveById(999));
        }

        [Test]
        public void Clear_RemovesAll()
        {
            var registry = new TileDefinitionRegistry(Size8x8);
            registry.Register(Make(1, 0, 0));
            registry.Register(Make(2, 1, 0));
            registry.Clear();
            Assert.AreEqual(0, registry.Count);
            Assert.IsFalse(registry.TryGetByCoord(new GridCoord(0, 0), out var _));
        }

        // ──────────── 6. Create 工厂 ────────────

        [Test]
        public void Create_FromEnumerable_RegistersAll()
        {
            var registry = TileDefinitionRegistry.Create(Size8x8, new[]
            {
                Make(1, 0, 0),
                Make(2, 1, 0),
                Make(3, 2, 0),
            });
            Assert.AreEqual(3, registry.Count);
        }

        [Test]
        public void Make_DefaultToTerrainDefinitionValues()
        {
            var def = TileDefinitionRegistry.Make(
                tileId: 1,
                coord: new GridCoord(0, 0),
                terrainType: TerrainType.Wall);
            Assert.AreEqual(TerrainType.Wall, def.TerrainType);
            Assert.IsTrue(def.BlocksMovement);
            Assert.AreEqual(TerrainRegistry.Wall, def.Terrain);
        }

        // ──────────── 7. Size 属性 ────────────

        [Test]
        public void Size_ReturnsConstructorArgument()
        {
            var registry = new TileDefinitionRegistry(Size8x8);
            Assert.AreEqual(Size8x8, registry.Size);
        }
    }
}