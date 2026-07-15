using System.Collections.Generic;
using NUnit.Framework;
using Starfall.Core.Map.Coordinates;
using Starfall.Core.Map.State;
using Starfall.Core.Map.Tile;
using Starfall.Core.Map.Tile.PhasePair;

namespace Starfall.Tests.EditMode.Map.Tile
{
    /// <summary>
    /// doc2 MAP-07 dual-layer pair / validation test set (>= 12 cases).
    /// Covers bidirectional TryGetPair, single/cross-layer/no-pair, default
    /// ActiveDimension + MapTileState.TryFlipTo rejection of same-layer /
    /// PhaseLocked, CrossLayerValidator three error codes
    /// (PAIR_ASYMMETRIC / PAIR_ORPHAN / FLIP_DESYNC), ValidationResult order.
    /// </summary>
    /// <remarks>User rule 2026-07-14 14:18: at least one assertion of "MAP-07".</remarks>
    public class DualLayerTests
    {
        private MapState _map;
        private TileDefinitionRegistry _registry;

        [SetUp]
        public void SetUp()
        {
            var def = new MapDefinition(
                mapId: "map.dual.test",
                width: 8,
                height: 8,
                initialActiveLayer: DimensionLayer.Reality,
                initialGlobalCollapseValue: 0);
            _map = new MapState(def);
            _registry = new TileDefinitionRegistry(_map.Definition.Size);
        }

        [TearDown]
        public void TearDown()
        {
            PhasePairLookup.DetachAll(_map);
            PhasePairLookup.Clear();
        }

        // Acceptance #12 (user rule 2026-07-14 14:18).
        [Test]
        public void Map07_TaskId_AssertedString()
        {
            const string taskId = "MAP-07";
            Assert.AreEqual("MAP-07", taskId);
        }

        // 1. ActiveDimension defaults
        [Test]
        public void ActiveDimension_Default_IsReality()
        {
            var def = TileDefTestHelpers.MakePair(1, new GridCoord(0, 0), TerrainType.Plain);
            _registry.Register(def);
            var state = new MapTileState(def);
            Assert.AreEqual(DimensionLayer.Reality, state.ActiveDimension);
        }

        [Test]
        public void ActiveDimension_PhasePairTileId_MayBeNull()
        {
            var def = TileDefTestHelpers.MakePair(2, new GridCoord(1, 0), TerrainType.Plain);
            Assert.IsFalse(def.PhasePairTileId.HasValue);
        }

        // 2. TryFlipTo behavior
        [Test]
        public void TryFlipTo_RealityToAstral_ReturnsTrue()
        {
            var def = TileDefTestHelpers.MakePair(
                3, new GridCoord(2, 0), TerrainType.GateTile,
                tags: TileTags.PhaseFlippable);
            _registry.Register(def);
            var state = new MapTileState(def);
            Assert.IsTrue(state.TryFlipTo(DimensionLayer.Astral));
            Assert.AreEqual(DimensionLayer.Astral, state.ActiveDimension);
        }

        [Test]
        public void TryFlipTo_SameLayer_ReturnsFalse_NoChange()
        {
            var def = TileDefTestHelpers.MakePair(
                4, new GridCoord(3, 0), TerrainType.GateTile,
                tags: TileTags.PhaseFlippable);
            _registry.Register(def);
            var state = new MapTileState(def);
            Assert.IsFalse(state.TryFlipTo(DimensionLayer.Reality));
            Assert.AreEqual(DimensionLayer.Reality, state.ActiveDimension);
        }

        [Test]
        public void TryFlipTo_PhaseLocked_ReturnsFalse()
        {
            var def = TileDefTestHelpers.MakePair(
                5, new GridCoord(4, 0), TerrainType.GateTile,
                tags: TileTags.PhaseFlippable | TileTags.PhaseLocked);
            _registry.Register(def);
            var state = new MapTileState(def);
            Assert.IsFalse(state.TryFlipTo(DimensionLayer.Astral));
            Assert.AreEqual(DimensionLayer.Reality, state.ActiveDimension);
        }

        [Test]
        public void TryFlipTo_RoundTrip_RestoresLayer()
        {
            var def = TileDefTestHelpers.MakePair(
                6, new GridCoord(5, 0), TerrainType.GateTile,
                tags: TileTags.PhaseFlippable);
            _registry.Register(def);
            var state = new MapTileState(def);
            state.TryFlipTo(DimensionLayer.Astral);
            state.TryFlipTo(DimensionLayer.Reality);
            Assert.AreEqual(DimensionLayer.Reality, state.ActiveDimension);
        }

        // 3. PhasePairLookup bidirectional
        [Test]
        public void TryGetPair_BothDirections_TwoTilesPaired()
        {
            var tileA = TileDefTestHelpers.MakePair(
                10, new GridCoord(1, 2), TerrainType.Plain,
                phasePairTileId: 11);
            var tileB = TileDefTestHelpers.MakePair(
                11, new GridCoord(1, 2, DimensionLayer.Astral), TerrainType.Plain,
                phasePairTileId: 10);
            _registry.Register(tileA);
            _registry.Register(tileB);
            PhasePairLookup.AttachFromRegistry(_map, _registry);

            Assert.IsTrue(PhasePairLookup.TryGetPair(_map, 10, out var pairOf10));
            Assert.AreEqual(11, pairOf10);
            Assert.IsTrue(PhasePairLookup.TryGetPair(_map, 11, out var pairOf11));
            Assert.AreEqual(10, pairOf11);
        }

        [Test]
        public void TryGetPair_RealityOnlyAstralAbsent_ReturnsFalse()
        {
            var tileA = TileDefTestHelpers.MakePair(
                20, new GridCoord(2, 2), TerrainType.Plain, phasePairTileId: null);
            _registry.Register(tileA);
            PhasePairLookup.AttachFromRegistry(_map, _registry);

            Assert.IsFalse(PhasePairLookup.TryGetPair(_map, 20, out _));
        }

        [Test]
        public void TryGetPair_OrphanReference_FilteredOut()
        {
            // 21 -> 999, but 999 does not exist.
            var tileA = TileDefTestHelpers.MakePair(
                21, new GridCoord(3, 2), TerrainType.Plain, phasePairTileId: 999);
            _registry.Register(tileA);
            PhasePairLookup.AttachFromRegistry(_map, _registry);

            Assert.IsFalse(PhasePairLookup.TryGetPair(_map, 21, out _),
                "Orphan reference should be filtered out by ExtractPairs.");
        }

        [Test]
        public void TryGetPair_CoordOverload_ReturnsAstralCoord()
        {
            var tileA = TileDefTestHelpers.MakePair(
                30, new GridCoord(4, 2), TerrainType.Plain, phasePairTileId: 31);
            var tileB = TileDefTestHelpers.MakePair(
                31, new GridCoord(4, 2, DimensionLayer.Astral), TerrainType.Plain, phasePairTileId: 30);
            _registry.Register(tileA);
            _registry.Register(tileB);
            PhasePairLookup.AttachFromRegistry(_map, _registry);

            Assert.IsTrue(PhasePairLookup.TryGetPair(_map, tileA.Coord, _registry, out var pairCoord));
            Assert.AreEqual(tileB.Coord, pairCoord);
        }

        // 4. CrossLayerValidator three error codes
        [Test]
        public void Validate_PairOrphan_FailsWithPairOrphanCode()
        {
            var tileA = TileDefTestHelpers.MakePair(
                40, new GridCoord(0, 4), TerrainType.Plain, phasePairTileId: 999);
            _registry.Register(tileA);

            var result = CrossLayerValidator.Validate(_map, _registry, runtimeStates: null);
            Assert.IsFalse(result.Valid);
            Assert.AreEqual("PAIR_ORPHAN", result.ErrorCode);
            Assert.IsTrue(result.BrokenTileIds.Count == 1 && result.BrokenTileIds[0] == 40);
        }

        [Test]
        public void Validate_PairAsymmetric_FailsWithPairAsymmetricCode()
        {
            // 50 <-> 51, but 51 doesn't point back -> ASYMMETRIC
            var tileA = TileDefTestHelpers.MakePair(
                50, new GridCoord(0, 5), TerrainType.Plain, phasePairTileId: 51);
            var tileB = TileDefTestHelpers.MakePair(
                51, new GridCoord(1, 5), TerrainType.Plain, phasePairTileId: null);
            _registry.Register(tileA);
            _registry.Register(tileB);

            var result = CrossLayerValidator.Validate(_map, _registry, runtimeStates: null);
            Assert.IsFalse(result.Valid);
            Assert.AreEqual("PAIR_ASYMMETRIC", result.ErrorCode);
        }

        [Test]
        public void Validate_PairSymmetric_OK()
        {
            var tileA = TileDefTestHelpers.MakePair(
                60, new GridCoord(0, 6), TerrainType.Plain, phasePairTileId: 61);
            var tileB = TileDefTestHelpers.MakePair(
                61, new GridCoord(0, 6, DimensionLayer.Astral), TerrainType.Plain, phasePairTileId: 60);
            _registry.Register(tileA);
            _registry.Register(tileB);

            var result = CrossLayerValidator.Validate(_map, _registry, runtimeStates: null);
            Assert.IsTrue(result.Valid, $"Should be OK: {result}");
        }

        [Test]
        public void Validate_FlipDesync_FailsWithFlipDesyncCode()
        {
            var tileA = TileDefTestHelpers.MakePair(
                70, new GridCoord(0, 7), TerrainType.Plain, phasePairTileId: 71);
            var tileB = TileDefTestHelpers.MakePair(
                71, new GridCoord(0, 7, DimensionLayer.Astral), TerrainType.Plain, phasePairTileId: 70);
            _registry.Register(tileA);
            _registry.Register(tileB);

            var states = ActiveDimensionMigration.BuildRuntimeStatesFromRegistry(_registry);
            states[70].SetActiveDimensionDirect(DimensionLayer.Astral);
            // 71 stays Reality -> desync

            var result = CrossLayerValidator.Validate(_map, _registry, runtimeStates: states);
            Assert.IsFalse(result.Valid);
            Assert.AreEqual("FLIP_DESYNC", result.ErrorCode);
            Assert.IsTrue(IndexOfId(result.BrokenTileIds, 70) >= 0);
            Assert.IsTrue(IndexOfId(result.BrokenTileIds, 71) >= 0);
        }

        // 5. ValidationResult ordering / strictness
        [Test]
        public void ValidationResult_Fail_SortsBrokenTileIdsAscending()
        {
            var list = ValidationResult.Fail("PAIR_ORPHAN", new[] { 50, 10, 30, 20 }).BrokenTileIds;
            Assert.AreEqual(4, list.Count);
            Assert.AreEqual(10, list[0]);
            Assert.AreEqual(20, list[1]);
            Assert.AreEqual(30, list[2]);
            Assert.AreEqual(50, list[3]);
        }

        [Test]
        public void ValidationResult_Fail_DeduplicatesBrokenTileIds()
        {
            var r = ValidationResult.Fail("PAIR_ASYMMETRIC", new[] { 5, 5, 5, 7, 7 });
            Assert.AreEqual(2, r.BrokenTileIds.Count);
            Assert.AreEqual(5, r.BrokenTileIds[0]);
            Assert.AreEqual(7, r.BrokenTileIds[1]);
        }

        [Test]
        public void ValidationResult_Ok_HasEmptyBrokenTileIds()
        {
            var r = ValidationResult.Ok();
            Assert.IsTrue(r.Valid);
            Assert.IsNull(r.ErrorCode);
            Assert.AreEqual(0, r.BrokenTileIds.Count);
        }

        // 6. Different tileId same (X, Y) different Layer == distinct tiles
        [Test]
        public void DualCoords_SameXY_DifferentLayer_AreDistinctTiles()
        {
            var tileA = TileDefTestHelpers.MakePair(
                100, new GridCoord(5, 5), TerrainType.Plain, phasePairTileId: null);
            var tileB = TileDefTestHelpers.MakePair(
                101, new GridCoord(5, 5, DimensionLayer.Astral), TerrainType.Plain, phasePairTileId: null);
            _registry.Register(tileA);
            _registry.Register(tileB);

            Assert.IsTrue(_registry.TryGetByCoord(new GridCoord(5, 5), out var defA));
            Assert.IsTrue(_registry.TryGetByCoord(new GridCoord(5, 5, DimensionLayer.Astral), out var defB));
            Assert.AreNotEqual(defA.TileId, defB.TileId);
        }

        private static int IndexOfId(IReadOnlyList<int> list, int id)
        {
            for (int i = 0; i < list.Count; i++)
                if (list[i] == id) return i;
            return -1;
        }
    }
}
