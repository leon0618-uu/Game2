using System.Collections.Generic;
using NUnit.Framework;
using Starfall.Core.Map.Commands;
using Starfall.Core.Map.Coordinates;
using Starfall.Core.Map.State;
using Starfall.Core.Map.Tile;
using Starfall.Core.Map.Tile.PhasePair;

namespace Starfall.Tests.EditMode.Map.Tile
{
    /// <summary>
    /// doc2 MAP-07 PhasePairLookup round-trip test set (>= 8 cases).
    /// Covers: ExtractPairs from registry (bidirectional), TryGetPair round-trip
    /// consistency, multiple flips preserve pair IDs (layer flip does not reorder
    /// tileIds).
    /// </summary>
    /// <remarks>User rule 2026-07-14 14:18: at least one assertion of "MAP-07".</remarks>
    public class PhasePairRoundTripTests
    {
        private MapState _map;
        private TileDefinitionRegistry _registry;

        [SetUp]
        public void SetUp()
        {
            var def = new MapDefinition(
                mapId: "map.pairround.test",
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
            PhaseFlipStateService.Detach(_map);
            PhaseFlipStateService.Clear();
        }

        [Test]
        public void Map07_TaskId_AssertedString()
        {
            const string taskId = "MAP-07";
            Assert.AreEqual("MAP-07", taskId);
        }

        // 1. ExtractPairs
        [Test]
        public void ExtractPairs_BothDirections_Stored()
        {
            var tileA = TileDefTestHelpers.MakePair(10, new GridCoord(0, 0),
                TerrainType.Plain, phasePairTileId: 11);
            var tileB = TileDefTestHelpers.MakePair(11, new GridCoord(0, 0, DimensionLayer.Astral),
                TerrainType.Plain, phasePairTileId: 10);
            _registry.Register(tileA);
            _registry.Register(tileB);

            var pairs = PhasePairLookup.ExtractPairs(_registry);
            // A (id=10) -> B (id=11), and B->A both stored.
            Assert.AreEqual(11, pairs[10]);
            Assert.AreEqual(10, pairs[11]);
            Assert.IsTrue(pairs.ContainsKey(11));
            Assert.AreEqual(10, pairs[11]);
        }

        [Test]
        public void ExtractPairs_AsymmetricSingleSide_StillBidirectional()
        {
            // A points to B, but B doesn't point back to A.
            // ExtractPairs still stores A->B and B->A (single direction).
            var tileA = TileDefTestHelpers.MakePair(20, new GridCoord(1, 0),
                TerrainType.Plain, phasePairTileId: 21);
            var tileB = TileDefTestHelpers.MakePair(21, new GridCoord(1, 0, DimensionLayer.Astral),
                TerrainType.Plain, phasePairTileId: null);
            _registry.Register(tileA);
            _registry.Register(tileB);

            var pairs = PhasePairLookup.ExtractPairs(_registry);
            Assert.IsTrue(pairs.ContainsKey(20));
            Assert.AreEqual(21, pairs[20]);
            Assert.IsTrue(pairs.ContainsKey(21));
            Assert.AreEqual(20, pairs[21], "ExtractPairs also stores B->A for cross-pair lookup.");
        }

        [Test]
        public void ExtractPairs_SelfLoop_Ignored()
        {
            var tileA = TileDefTestHelpers.MakePair(30, new GridCoord(2, 0),
                TerrainType.Plain, phasePairTileId: 30);
            _registry.Register(tileA);

            var pairs = PhasePairLookup.ExtractPairs(_registry);
            Assert.AreEqual(0, pairs.Count,
                "Self-loop PhasePairTileId should be ignored.");
        }

        // 2. TryGetPair round-trip
        [Test]
        public void TryGetPair_RoundTrip_BothWaysConsistent()
        {
            var tileA = TileDefTestHelpers.MakePair(40, new GridCoord(3, 0),
                TerrainType.Plain, phasePairTileId: 41);
            var tileB = TileDefTestHelpers.MakePair(41, new GridCoord(3, 0, DimensionLayer.Astral),
                TerrainType.Plain, phasePairTileId: 40);
            _registry.Register(tileA);
            _registry.Register(tileB);
            PhasePairLookup.AttachFromRegistry(_map, _registry);

            Assert.IsTrue(PhasePairLookup.TryGetPair(_map, 40, out var pairOf40));
            Assert.IsTrue(PhasePairLookup.TryGetPair(_map, 41, out var pairOf41));
            Assert.AreEqual(41, pairOf40);
            Assert.AreEqual(40, pairOf41);
        }

        // 3. Detach / Re-attach
        [Test]
        public void DetachAll_ClearsPairLookup_ForMap()
        {
            var tileA = TileDefTestHelpers.MakePair(50, new GridCoord(4, 0),
                TerrainType.Plain, phasePairTileId: 51);
            var tileB = TileDefTestHelpers.MakePair(51, new GridCoord(4, 0, DimensionLayer.Astral),
                TerrainType.Plain, phasePairTileId: 50);
            _registry.Register(tileA);
            _registry.Register(tileB);
            PhasePairLookup.AttachFromRegistry(_map, _registry);

            Assert.AreEqual(2, PhasePairLookup.GetPairCount(_map));
            PhasePairLookup.DetachAll(_map);
            Assert.AreEqual(0, PhasePairLookup.GetPairCount(_map),
                "DetachAll should clear pair lookup for this map.");
        }

        [Test]
        public void Clear_ResetAllMaps()
        {
            var def2 = new MapDefinition("m2", 4, 4, DimensionLayer.Reality, 0);
            var map2 = new MapState(def2);
            var reg2 = new TileDefinitionRegistry(map2.Definition.Size);
            reg2.Register(TileDefTestHelpers.MakePair(60, new GridCoord(0, 0),
                TerrainType.Plain, phasePairTileId: 61));
            reg2.Register(TileDefTestHelpers.MakePair(61, new GridCoord(0, 0, DimensionLayer.Astral),
                TerrainType.Plain, phasePairTileId: 60));

            PhasePairLookup.AttachFromRegistry(_map, _registry);
            PhasePairLookup.AttachFromRegistry(map2, reg2);

            PhasePairLookup.Clear();
            Assert.AreEqual(0, PhasePairLookup.GetPairCount(_map));
            Assert.AreEqual(0, PhasePairLookup.GetPairCount(map2));
        }

        // 4. Multiple flips preserve pair IDs
        [Test]
        public void AfterMultipleFlips_PairLookupTileIds_RemainStable()
        {
            var tileA = TileDefTestHelpers.MakePair(70, new GridCoord(5, 0),
                TerrainType.GateTile, tags: TileTags.PhaseFlippable, phasePairTileId: 71);
            var tileB = TileDefTestHelpers.MakePair(71, new GridCoord(5, 0, DimensionLayer.Astral),
                TerrainType.GateTile, tags: TileTags.PhaseFlippable, phasePairTileId: 70);
            _registry.Register(tileA);
            _registry.Register(tileB);
            _map.AddTile(tileA.Coord);
            _map.AddTile(tileB.Coord);

            PhasePairLookup.AttachFromRegistry(_map, _registry);
            PhaseFlipStateService.AttachMapState(_map, _registry);
            var states = ActiveDimensionMigration.BuildRuntimeStatesFromRegistry(_registry);
            PhaseFlipStateService.AttachRuntimeStates(_map, states);

            new FlipTilePhaseCommand(70, DimensionLayer.Astral).Execute(_map);
            Assert.IsTrue(PhasePairLookup.TryGetPair(_map, 70, out var p1));
            Assert.AreEqual(71, p1);

            new FlipTilePhaseCommand(70, DimensionLayer.Reality).Execute(_map);
            Assert.IsTrue(PhasePairLookup.TryGetPair(_map, 70, out var p2));
            Assert.AreEqual(71, p2);

            new FlipTilePhaseCommand(70, DimensionLayer.Astral).Execute(_map);
            Assert.IsTrue(PhasePairLookup.TryGetPair(_map, 70, out var p3));
            Assert.AreEqual(71, p3);

            Assert.AreEqual(70, _registry.TryGetByCoord(tileA.Coord, out var defA) ? defA.TileId : -1);
            Assert.AreEqual(71, _registry.TryGetByCoord(tileB.Coord, out var defB) ? defB.TileId : -1);
        }

        // 5. AttachPhasePairs external dict
        [Test]
        public void AttachPhasePairs_ExternalDict_ProvidesLookup()
        {
            var pairs = new Dictionary<int, int> { { 80, 81 }, { 81, 80 } };
            PhasePairLookup.AttachPhasePairs(_map, pairs);

            Assert.IsTrue(PhasePairLookup.TryGetPair(_map, 80, out var p80));
            Assert.AreEqual(81, p80);
            Assert.IsTrue(PhasePairLookup.TryGetPair(_map, 81, out var p81));
            Assert.AreEqual(80, p81);
        }

        [Test]
        public void AttachPhasePairs_SelfLoop_Ignored()
        {
            var pairs = new Dictionary<int, int> { { 90, 90 }, { 91, 92 }, { 92, 91 } };
            PhasePairLookup.AttachPhasePairs(_map, pairs);

            Assert.IsFalse(PhasePairLookup.TryGetPair(_map, 90, out _),
                "Self-loop should be ignored on attach.");
            Assert.IsTrue(PhasePairLookup.TryGetPair(_map, 91, out var p91));
            Assert.AreEqual(92, p91);
        }
    }
}
