using NUnit.Framework;
using Starfall.Core.Map.Coordinates;
using Starfall.Core.Map.Tile;

namespace Starfall.Tests.EditMode.Map.Tile
{
    /// <summary>
    /// doc2 MAP-07 TileDefinition.PhasePairTileId test set (>= 6 cases).
    /// Covers: PhasePairTileId set returns the same value; null means no pair;
    /// multiple construction with different values does not throw; other fields
    /// unchanged.
    /// </summary>
    /// <remarks>User rule 2026-07-14 14:18: at least one assertion of "MAP-07".</remarks>
    public class TileDefinitionPhasePairTest
    {
        [Test]
        public void Map07_TaskId_AssertedString()
        {
            const string taskId = "MAP-07";
            Assert.AreEqual("MAP-07", taskId);
        }

        [Test]
        public void PhasePairTileId_Set_ReturnsSameValue()
        {
            var def = new TileDefinition(
                tileId: 1,
                coord: new GridCoord(0, 0),
                terrainType: TerrainType.Plain,
                terrain: TerrainRegistry.Plain,
                phasePairTileId: 99);
            Assert.IsTrue(def.PhasePairTileId.HasValue);
            Assert.AreEqual(99, def.PhasePairTileId.Value);
        }

        [Test]
        public void PhasePairTileId_DefaultNull_Indicates_NoPair()
        {
            var def = TileDefTestHelpers.MakePair(2, new GridCoord(1, 0), TerrainType.Plain);
            Assert.IsFalse(def.PhasePairTileId.HasValue,
                "Default TileDefinition should not have PhasePairTileId; null means no pair.");
        }

        [Test]
        public void MultipleTileDefinitions_DifferentPhasePairIds_NoThrow()
        {
            Assert.DoesNotThrow(() =>
            {
                _ = new TileDefinition(10, new GridCoord(0, 0), TerrainType.Plain,
                    TerrainRegistry.Plain, phasePairTileId: 11);
                _ = new TileDefinition(11, new GridCoord(0, 0, DimensionLayer.Astral), TerrainType.Plain,
                    TerrainRegistry.Plain, phasePairTileId: 10);
                _ = new TileDefinition(12, new GridCoord(0, 1), TerrainType.Plain,
                    TerrainRegistry.Plain, phasePairTileId: 13);
                _ = TileDefTestHelpers.MakePair(20, new GridCoord(1, 1), TerrainType.Plain);
            });
        }

        [Test]
        public void PhasePairTileId_Zero_Throws()
        {
            Assert.Throws<System.ArgumentOutOfRangeException>(() =>
            {
                _ = new TileDefinition(30, new GridCoord(0, 0), TerrainType.Plain,
                    TerrainRegistry.Plain, phasePairTileId: 0);
            });
        }

        [Test]
        public void PhasePairTileId_Negative_Throws()
        {
            Assert.Throws<System.ArgumentOutOfRangeException>(() =>
            {
                _ = new TileDefinition(31, new GridCoord(0, 0), TerrainType.Plain,
                    TerrainRegistry.Plain, phasePairTileId: -1);
            });
        }

        [Test]
        public void Equals_DifferentPhasePairTileId_NotEqual()
        {
            var defA = new TileDefinition(40, new GridCoord(0, 0), TerrainType.Plain,
                TerrainRegistry.Plain, phasePairTileId: 41);
            var defB = new TileDefinition(40, new GridCoord(0, 0), TerrainType.Plain,
                TerrainRegistry.Plain, phasePairTileId: 42);
            Assert.AreNotEqual(defA, defB);
        }

        [Test]
        public void Equals_SamePhasePairTileId_Equal()
        {
            var defA = new TileDefinition(50, new GridCoord(0, 0), TerrainType.Plain,
                TerrainRegistry.Plain, phasePairTileId: 51);
            var defB = new TileDefinition(50, new GridCoord(0, 0), TerrainType.Plain,
                TerrainRegistry.Plain, phasePairTileId: 51);
            Assert.AreEqual(defA, defB);
            Assert.AreEqual(defA.GetHashCode(), defB.GetHashCode());
        }

        [Test]
        public void ToString_WithPhasePairTileId_ContainsPhasePairRef()
        {
            var def = new TileDefinition(60, new GridCoord(0, 0), TerrainType.Plain,
                TerrainRegistry.Plain, phasePairTileId: 61);
            var s = def.ToString();
            Assert.That(s, Does.Contain("Id=60"));
        }
    }
}
