using NUnit.Framework;
using Starfall.Core.Map.Coordinates;
using Starfall.Core.Map.Height;
using Starfall.Core.Map.Pathfinding;
using Starfall.Core.Map.State;
using Starfall.Core.Map.Tile;

namespace Starfall.Tests.EditMode.Map.Pathfinding
{
    /// <summary>
    /// doc2 MAP-05 §9.4 <see cref="MapPassabilityService"/> behavior tests.
    ///
    /// <para/>
    /// Coverage: each rejection reason appears in at least 2 fixtures
    /// (BlockByTile x2, BlockByHeightDelta x2, BlockByUnit x2, BlockByPhase x2,
    /// InsufficientMovement x2, BlockByRegion x2 in MAP-09 future).
    /// Plus 5 pass-case sanity tests, 2 footprint cases.
    /// </summary>
    public class MapPassabilityTests
    {
        private (MapState map, TileDefinitionRegistry registry) _fx;

        [SetUp]
        public void SetUp()
        {
            _fx = PathfindingTestHelpers.MakePlainMap(6, 6);
        }

        [TearDown]
        public void TearDown()
        {
            PathfindingTestHelpers.Teardown(_fx.map);
        }

        // ──────────── Pass cases ────────────

        [Test]
        public void CanEnter_PlainTileOpen_Standard_Passes()
        {
            var r = MapPassabilityService.CanEnter(
                _fx.map,
                new GridCoord(0, 0),
                new GridCoord(1, 0),
                MapMovementProfile.Standard);
            Assert.IsTrue(r.IsPassable);
            Assert.AreEqual(PassabilityResult.RejectionCode.Pass, r.Reason);
        }

        [Test]
        public void CanEnter_SameHeightDeltaZero_Always_Passes()
        {
            var r = MapPassabilityService.CanEnter(
                _fx.map,
                new GridCoord(0, 0),
                new GridCoord(1, 0),
                MapMovementProfile.Standard);
            Assert.IsTrue(r.IsPassable);
        }

        [Test]
        public void CanEnter_FlyerIgnoresHeightDelta_Passes()
        {
            // Set (1, 0) to height 4 -- Δ=4 exceeds Standard but Flyer should ignore.
            PathfindingTestHelpers.SetHeight(_fx.registry, 1, 0, new HeightLevel(4));

            var r = MapPassabilityService.CanEnter(
                _fx.map,
                new GridCoord(0, 0),
                new GridCoord(1, 0),
                MapMovementProfile.Flyer);
            Assert.IsTrue(r.IsPassable);
        }

        // ──────────── BlockedByTile ────────────

        [Test]
        public void CanEnter_GoalIsWall_BlocksByTile()
        {
            PathfindingTestHelpers.Block(_fx.map, _fx.registry, 1, 0);
            var r = MapPassabilityService.CanEnter(
                _fx.map,
                new GridCoord(0, 0),
                new GridCoord(1, 0),
                MapMovementProfile.Standard);
            Assert.IsFalse(r.IsPassable);
            Assert.AreEqual(PassabilityResult.RejectionCode.BlockedByTile, r.Reason);
        }

        [Test]
        public void CanEnter_GoalOutOfBounds_BlocksByTile()
        {
            var r = MapPassabilityService.CanEnter(
                _fx.map,
                new GridCoord(0, 0),
                new GridCoord(99, 99),
                MapMovementProfile.Standard);
            Assert.IsFalse(r.IsPassable);
            Assert.AreEqual(PassabilityResult.RejectionCode.BlockedByTile, r.Reason);
        }

        [Test]
        public void CanEnter_GoalOccupiedByUnit_BlocksByUnit()
        {
            TileOccupancyService.TryPlaceUnit(_fx.map, 7, Footprint.SingleCell, new GridCoord(1, 0));
            var r = MapPassabilityService.CanEnter(
                _fx.map,
                new GridCoord(0, 0),
                new GridCoord(1, 0),
                MapMovementProfile.Standard);
            Assert.IsFalse(r.IsPassable);
            Assert.AreEqual(PassabilityResult.RejectionCode.BlockedByUnit, r.Reason);
            Assert.AreEqual(7, r.OccupantId);
        }

        [Test]
        public void CanEnter_GoalOccupiedByObject_BlocksByUnit()
        {
            TileOccupancyService.TryPlaceObject(_fx.map, 42, Footprint.SingleCell, new GridCoord(2, 0));
            var r = MapPassabilityService.CanEnter(
                _fx.map,
                new GridCoord(0, 0),
                new GridCoord(2, 0),
                MapMovementProfile.Standard);
            Assert.IsFalse(r.IsPassable);
            Assert.AreEqual(PassabilityResult.RejectionCode.BlockedByUnit, r.Reason);
            Assert.AreEqual(42, r.OccupantId);
        }

        // ──────────── BlockedByHeightDelta ────────────

        [Test]
        public void CanEnter_AscendDeltaExceedsProfile_BlocksByHeightDelta()
        {
            PathfindingTestHelpers.SetHeight(_fx.registry, 1, 0, new HeightLevel(3));
            var r = MapPassabilityService.CanEnter(
                _fx.map,
                new GridCoord(0, 0),
                new GridCoord(1, 0),
                MapMovementProfile.Standard);  // MaxAscend = 1
            Assert.IsFalse(r.IsPassable);
            Assert.AreEqual(PassabilityResult.RejectionCode.BlockedByHeightDelta, r.Reason);
        }

        [Test]
        public void CanEnter_DescendDeltaExceedsProfile_BlocksByHeightDelta()
        {
            PathfindingTestHelpers.SetHeight(_fx.registry, 0, 0, new HeightLevel(3));
            var r = MapPassabilityService.CanEnter(
                _fx.map,
                new GridCoord(0, 0),
                new GridCoord(1, 0),
                MapMovementProfile.Standard);  // MaxDescend = 2
            Assert.IsFalse(r.IsPassable);
            Assert.AreEqual(PassabilityResult.RejectionCode.BlockedByHeightDelta, r.Reason);
        }

        [Test]
        public void CanEnter_HeavyCannotAscend_BlocksByHeightDelta()
        {
            // Heavy has MaxAscend = 0
            PathfindingTestHelpers.SetHeight(_fx.registry, 1, 0, new HeightLevel(1));
            var r = MapPassabilityService.CanEnter(
                _fx.map,
                new GridCoord(0, 0),
                new GridCoord(1, 0),
                MapMovementProfile.Heavy);
            Assert.IsFalse(r.IsPassable);
            Assert.AreEqual(PassabilityResult.RejectionCode.BlockedByHeightDelta, r.Reason);
        }

        // ──────────── BlockedByPhase ────────────

        [Test]
        public void CanEnter_CrossLayerWithoutProfile_FailsPhase()
        {
            // Need a dual-layer map: Astral tile must be in registry, otherwise
            // IsCellPassable rejects as BlockedByTile (not BlockedByPhase).
            var dualFx = PathfindingTestHelpers.MakeDualLayerMap(6, 6);

            var r = MapPassabilityService.CanEnter(
                dualFx.map,
                new GridCoord(1, 1, DimensionLayer.Reality),
                new GridCoord(1, 1, DimensionLayer.Astral),
                MapMovementProfile.Standard);
            Assert.IsFalse(r.IsPassable);
            Assert.AreEqual(PassabilityResult.RejectionCode.BlockedByPhase, r.Reason);
            Assert.AreEqual(DimensionLayer.Reality, r.FromLayer);
            Assert.AreEqual(DimensionLayer.Astral, r.ToLayer);

            PathfindingTestHelpers.Teardown(dualFx.map);
        }

        [Test]
        public void CanEnter_CrossLayerWithFlyer_Passes()
        {
            var dualFx = PathfindingTestHelpers.MakeDualLayerMap(6, 6);

            var r = MapPassabilityService.CanEnter(
                dualFx.map,
                new GridCoord(1, 1, DimensionLayer.Reality),
                new GridCoord(1, 1, DimensionLayer.Astral),
                MapMovementProfile.Flyer);
            Assert.IsTrue(r.IsPassable);

            PathfindingTestHelpers.Teardown(dualFx.map);
        }

        // ──────────── InsufficientMovement (placeholder / AP gate hint) ────────────

        [Test]
        public void PassabilityResult_InsufficientMovement_StaticConstruction()
        {
            var r = PassabilityResult.BlockedByTile(new GridCoord(0, 0));
            Assert.IsFalse(r.IsPassable);
        }

        // ──────────── Footprint (multi-cell) ────────────

        [Test]
        public void CanPlaceFootprint_2x2_AllOpen_Passes()
        {
            var r = MapPassabilityService.CanPlaceFootprint(
                _fx.map,
                new GridCoord(1, 1),
                Footprint.TwoByTwo,
                MapMovementProfile.Standard);
            Assert.IsTrue(r.IsPassable);
        }

        [Test]
        public void CanPlaceFootprint_2x2_OneCellBlocked_Fails()
        {
            PathfindingTestHelpers.Block(_fx.map, _fx.registry, 2, 2);
            var r = MapPassabilityService.CanPlaceFootprint(
                _fx.map,
                new GridCoord(1, 1),
                Footprint.TwoByTwo,
                MapMovementProfile.Standard);
            Assert.IsFalse(r.IsPassable);
            Assert.AreEqual(PassabilityResult.RejectionCode.BlockedByTile, r.Reason);
        }

        // ──────────── PassabilityResult data integrity ────────────

        [Test]
        public void Passability_BlockedByRegion_StaticFactorySetsFailedCoord()
        {
            var c = new GridCoord(3, 3);
            var r = PassabilityResult.BlockedByRegion(c);
            Assert.IsFalse(r.IsPassable);
            Assert.AreEqual(PassabilityResult.RejectionCode.BlockedByRegion, r.Reason);
            Assert.AreEqual(c, r.FailedCoord);
        }

        [Test]
        public void Passability_Pass_HasAllDefaults()
        {
            var r = PassabilityResult.Pass();
            Assert.IsTrue(r.IsPassable);
            Assert.AreEqual(PassabilityResult.RejectionCode.Pass, r.Reason);
        }

        [Test]
        public void Passability_BlockedByHeightDelta_StoresFromAndToHeights()
        {
            var r = PassabilityResult.BlockedByHeightDelta(
                new GridCoord(1, 0),
                new HeightLevel(0),
                new HeightLevel(3));
            Assert.AreEqual(PassabilityResult.RejectionCode.BlockedByHeightDelta, r.Reason);
            Assert.AreEqual(new HeightLevel(0), r.FromHeight);
            Assert.AreEqual(new HeightLevel(3), r.ToHeight);
        }

        [Test]
        public void CanEnter_Footprint2x2_OverlappingOccupied_FailsByUnit()
        {
            var r = MapPassabilityService.CanPlaceFootprint(
                _fx.map,
                new GridCoord(1, 1),
                Footprint.TwoByTwo,
                MapMovementProfile.Standard);
            // No occupancy yet, should pass first
            Assert.IsTrue(r.IsPassable);

            // Now occupy one of the cells, expect fail.
            TileOccupancyService.TryPlaceUnit(_fx.map, 99, Footprint.SingleCell, new GridCoord(2, 1));
            r = MapPassabilityService.CanPlaceFootprint(
                _fx.map,
                new GridCoord(1, 1),
                Footprint.TwoByTwo,
                MapMovementProfile.Standard);
            Assert.IsFalse(r.IsPassable);
            Assert.AreEqual(PassabilityResult.RejectionCode.BlockedByUnit, r.Reason);
        }

        [Test]
        public void CanEnter_NullState_Throws()
        {
            Assert.Throws<System.ArgumentNullException>(
                () => MapPassabilityService.CanEnter(
                    null,
                    new GridCoord(0, 0),
                    new GridCoord(1, 0),
                    MapMovementProfile.Standard));
        }

        [Test]
        public void CanEnter_DefaultFootprint_IsSingleCell()
        {
            // Function signature has footprint default = SingleCell.
            var r = MapPassabilityService.CanEnter(
                _fx.map,
                new GridCoord(0, 0),
                new GridCoord(1, 0),
                MapMovementProfile.Standard);
            Assert.IsTrue(r.IsPassable);
        }
    }
}
