using NUnit.Framework;
using Starfall.Core.Map.Coordinates;
using Starfall.Core.Map.Cover;
using Starfall.Core.Map.Height;
using Starfall.Core.Map.Tile;

namespace Starfall.Tests.EditMode.Map.Tile
{
    /// <summary>
    /// doc2 MAP-04 §4.4 TileDefinition 测试集。
    /// 覆盖：默认构造、自定义覆盖、等价性、tileId 校验、Tags。
    /// </summary>
    public class TileDefinitionTests
    {
        private static TileDefinition MakePlain(int tileId = 1, GridCoord? coord = null)
        {
            return new TileDefinition(
                tileId: tileId,
                coord: coord ?? new GridCoord(0, 0),
                terrainType: TerrainType.Plain,
                terrain: TerrainRegistry.Plain);
        }

        // ──────────── 1. 默认构造使用 TerrainDefinition 默认值 ────────────

        [Test]
        public void DefaultConstructor_UsesTerrainDefinitionDefaults()
        {
            var def = MakePlain(tileId: 1);
            Assert.AreEqual(TerrainType.Plain, def.TerrainType);
            Assert.AreEqual(TerrainRegistry.Plain, def.Terrain);
            Assert.AreEqual(1, def.BaseMoveCost);
            Assert.IsFalse(def.BlocksMovement);
            Assert.IsFalse(def.BlocksVision);
            Assert.IsFalse(def.BlocksProjectile);
            Assert.AreEqual(CoverLevel.None, def.CoverLevel);
            Assert.AreEqual(CoverDirection.All, def.CoverDirections);
            Assert.IsFalse(def.PhasePairTileId.HasValue);
            Assert.AreEqual(TileTags.None, def.Tags);
        }

        // ──────────── 2. 自定义 BaseMoveCost 不破坏其他字段 ────────────

        [Test]
        public void CustomBaseMoveCost_DoesNotAffectOtherFields()
        {
            var def = new TileDefinition(
                tileId: 1,
                coord: new GridCoord(0, 0),
                terrainType: TerrainType.Rough,
                terrain: TerrainRegistry.Rough,
                baseMoveCost: 3);  // override Rough default 2

            Assert.AreEqual(3, def.BaseMoveCost, "Custom BaseMoveCost should override default.");
            Assert.AreEqual(TerrainType.Rough, def.TerrainType);
            Assert.AreEqual(2, def.Terrain.BaseMoveCost, "TerrainDefinition itself is unchanged.");
            // 其他字段从 TerrainDefinition 派生，未被破坏。
            Assert.IsFalse(def.BlocksMovement);
        }

        // ──────────── 3. 等价性 ────────────

        [Test]
        public void Equals_SameFields_AreEqual()
        {
            var a = new TileDefinition(
                tileId: 5,
                coord: new GridCoord(3, 2),
                terrainType: TerrainType.Plain,
                terrain: TerrainRegistry.Plain);
            var b = new TileDefinition(
                tileId: 5,
                coord: new GridCoord(3, 2),
                terrainType: TerrainType.Plain,
                terrain: TerrainRegistry.Plain);
            Assert.AreEqual(a, b);
            Assert.AreEqual(a.GetHashCode(), b.GetHashCode());
        }

        [Test]
        public void Equals_DifferentTileId_AreNotEqual()
        {
            var a = MakePlain(tileId: 1);
            var b = MakePlain(tileId: 2);
            Assert.AreNotEqual(a, b);
        }

        [Test]
        public void Equals_DifferentCoord_AreNotEqual()
        {
            var a = MakePlain(tileId: 1, coord: new GridCoord(0, 0));
            var b = MakePlain(tileId: 1, coord: new GridCoord(0, 1));
            Assert.AreNotEqual(a, b);
        }

        // ──────────── 4. 验证 ────────────

        [Test]
        public void Constructor_TileIdLessThanOne_Throws()
        {
            Assert.Throws<System.ArgumentOutOfRangeException>(() => MakePlain(tileId: 0));
            Assert.Throws<System.ArgumentOutOfRangeException>(() => MakePlain(tileId: -1));
        }

        [Test]
        public void Constructor_BaseMoveCostOutOfRange_Throws()
        {
            Assert.Throws<System.ArgumentOutOfRangeException>(() =>
                new TileDefinition(
                    tileId: 1,
                    coord: new GridCoord(0, 0),
                    terrainType: TerrainType.Plain,
                    terrain: TerrainRegistry.Plain,
                    baseMoveCost: 0));
            Assert.Throws<System.ArgumentOutOfRangeException>(() =>
                new TileDefinition(
                    tileId: 1,
                    coord: new GridCoord(0, 0),
                    terrainType: TerrainType.Plain,
                    terrain: TerrainRegistry.Plain,
                    baseMoveCost: 6));
        }

        [Test]
        public void Constructor_PhasePairTileIdLessThanOne_Throws()
        {
            Assert.Throws<System.ArgumentOutOfRangeException>(() =>
                new TileDefinition(
                    tileId: 1,
                    coord: new GridCoord(0, 0),
                    terrainType: TerrainType.Plain,
                    terrain: TerrainRegistry.Plain,
                    phasePairTileId: 0));
        }

        // ──────────── 5. PhasePairTileId 可为 null ────────────

        [Test]
        public void PhasePairTileId_DefaultsToNull()
        {
            var def = MakePlain();
            Assert.IsFalse(def.PhasePairTileId.HasValue);
        }

        [Test]
        public void PhasePairTileId_CanBeSet_AndAffectsEquality()
        {
            var a = new TileDefinition(
                tileId: 1,
                coord: new GridCoord(0, 0),
                terrainType: TerrainType.Plain,
                terrain: TerrainRegistry.Plain,
                phasePairTileId: 42);
            var b = new TileDefinition(
                tileId: 1,
                coord: new GridCoord(0, 0),
                terrainType: TerrainType.Plain,
                terrain: TerrainRegistry.Plain,
                phasePairTileId: 99);
            Assert.IsTrue(a.PhasePairTileId.HasValue);
            Assert.AreEqual(42, a.PhasePairTileId.Value);
            Assert.AreNotEqual(a, b);
        }

        // ──────────── 6. Tags ────────────

        [Test]
        public void Tags_DefaultToNone()
        {
            var def = MakePlain();
            Assert.AreEqual(TileTags.None, def.Tags);
        }

        [Test]
        public void Tags_CanBeSet()
        {
            var def = new TileDefinition(
                tileId: 1,
                coord: new GridCoord(0, 0),
                terrainType: TerrainType.Plain,
                terrain: TerrainRegistry.Plain,
                tags: TileTags.Walkable | TileTags.Spawnable);
            Assert.IsTrue((def.Tags & TileTags.Walkable) == TileTags.Walkable);
            Assert.IsTrue((def.Tags & TileTags.Spawnable) == TileTags.Spawnable);
        }

        // ──────────── 7. 不变性 ────────────

        [Test]
        public void IsReadonlyStruct_NoPublicSetters()
        {
            // 编译期已保证 readonly field；这里用反射验证不存在 setter。
            var type = typeof(TileDefinition);
            var props = type.GetProperties(System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance);
            foreach (var p in props)
            {
                Assert.IsFalse(p.CanWrite,
                    $"TileDefinition.{p.Name} should not have a public setter (immutable readonly struct).");
            }
        }

        // ──────────── 8. Tags 自定义 + PhaseFlippable ────────────

        [Test]
        public void CustomBlocksMovement_Overrides()
        {
            var def = new TileDefinition(
                tileId: 1,
                coord: new GridCoord(0, 0),
                terrainType: TerrainType.Plain,
                terrain: TerrainRegistry.Plain,
                blocksMovement: true); // override: Plain 不阻挡，但这里手动开启
            Assert.IsTrue(def.BlocksMovement);
        }

        // ──────────── 9. Height 字段 ────────────

        [Test]
        public void Height_DefaultsToGround()
        {
            var def = MakePlain();
            Assert.AreEqual(0, def.Height.Value);
        }

        [Test]
        public void Height_CanBeSetToTowerLevel()
        {
            var def = new TileDefinition(
                tileId: 1,
                coord: new GridCoord(0, 0),
                terrainType: TerrainType.Plain,
                terrain: TerrainRegistry.Plain,
                height: new HeightLevel(4));
            Assert.AreEqual(4, def.Height.Value);
        }
    }
}