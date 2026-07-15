using System.Collections.Generic;
using NUnit.Framework;
using Starfall.Core.Map.Coordinates;
using Starfall.Core.Map.Height;
using Starfall.Core.Map.Pathfinding;
using Starfall.Core.Map.Tile;

namespace Starfall.Tests.EditMode.Map.Pathfinding
{
    /// <summary>
    /// doc2 MAP-05 §9.4 <see cref="PathfindingService"/> behavior tests.
    ///
    /// <para/>
    /// Coverage:
    /// 1. A* algorithm basics (start==goal, straight line, Manhattan L-shape).
    /// 2. Obstacle detour (Walls / blocking tiles force alternate route).
    /// 3. Determinism (same input -> same path / same cost).
    /// 4. Failure reasons (NoPath / GoalBlocked / StartOccupied / Unreachable).
    /// 5. Cross-layer / CrossDimension toggle.
    /// 6. Height-delta detour (Standard cannot climb Δh=2; must detour).
    /// 7. Cost ties: A* returns same path on re-run (deterministic tie-break).
    /// 8. Straight-path spot-checks (Y axis ordering preserved).
    /// </summary>
    public class PathfindingServiceTests
    {
        private (Starfall.Core.Map.State.MapState map, TileDefinitionRegistry registry) _fx;

        [SetUp]
        public void SetUp()
        {
            _fx = PathfindingTestHelpers.MakePlainMap(8, 8);
        }

        [TearDown]
        public void TearDown()
        {
            PathfindingTestHelpers.Teardown(_fx.map);
        }

        // ──────────── 1. Start == goal: single tile with cost 0 ────────────

        [Test]
        public void FindPath_StartEqualsGoal_ReturnsSingleTile()
        {
            var start = new GridCoord(4, 4);
            var path = PathfindingService.FindPath(_fx.map, start, start, MapMovementProfile.Standard);

            Assert.IsTrue(path.Success);
            Assert.AreEqual(1, path.Tiles.Count);
            Assert.AreEqual(start, path.Tiles[0]);
            Assert.AreEqual(0, path.TotalCost);
        }

        // ──────────── 2. Straight east path ────────────

        [Test]
        public void FindPath_StraightEast_Succeeds()
        {
            var path = PathfindingService.FindPath(
                _fx.map,
                start: new GridCoord(0, 0),
                goal: new GridCoord(5, 0),
                MapMovementProfile.Standard);

            Assert.IsTrue(path.Success);
            Assert.AreEqual(6, path.Tiles.Count);  // 0,0; 1,0; ... 5,0
            Assert.AreEqual(new GridCoord(0, 0), path.Tiles[0]);
            Assert.AreEqual(new GridCoord(5, 0), path.Tiles[5]);
        }

        // ──────────── 3. L-shape detour around wall ────────────

        [Test]
        public void FindPath_Detours_AroundWall()
        {
            // Block (3,0) so path from (0,0) east to (5,0) must go via Y=1.
            PathfindingTestHelpers.Block(_fx.map, _fx.registry, 3, 0);

            var path = PathfindingService.FindPath(
                _fx.map,
                start: new GridCoord(0, 0),
                goal: new GridCoord(5, 0),
                MapMovementProfile.Standard);

            Assert.IsTrue(path.Success);
            // Path must avoid (3, 0)
            Assert.IsFalse(ContainsTile(path, new GridCoord(3, 0)),
                "Path should not step on the wall tile.");
            Assert.AreEqual(new GridCoord(0, 0), path.Tiles[0]);
            Assert.AreEqual(new GridCoord(5, 0), path.Tiles[path.Tiles.Count - 1]);
        }

        // ──────────── 4. NoPath: goal is plain but unreachable (surrounded by walls) ────────────

        [Test]
        public void FindPath_NoPath_ReturnsNoPath()
        {
            // Goal (7, 7) is plain. Walls at (6, 7) [W] and (7, 6) [S] isolate it.
            // N (7, 8) and E (8, 7) are out of bounds for an 8x8 map.
            // So (7,7) has no path from (0,0); A* exhausts without finding -> NoPath.
            PathfindingTestHelpers.Block(_fx.map, _fx.registry, 6, 7);
            PathfindingTestHelpers.Block(_fx.map, _fx.registry, 7, 6);

            var start = new GridCoord(0, 0);
            var goal = new GridCoord(7, 7);

            var path = PathfindingService.FindPath(_fx.map, start, goal, MapMovementProfile.Standard);
            Assert.IsFalse(path.Success);
            Assert.AreEqual(MapPath.PathFailure.NoPath, path.FailureReason);
        }

        // ──────────── 5. GoalBlocked: goal is a Wall ────────────

        [Test]
        public void FindPath_GoalIsWall_ReturnsGoalBlocked()
        {
            PathfindingTestHelpers.Block(_fx.map, _fx.registry, 3, 3);
            var path = PathfindingService.FindPath(
                _fx.map,
                start: new GridCoord(0, 0),
                goal: new GridCoord(3, 3),
                MapMovementProfile.Standard);

            Assert.IsFalse(path.Success);
            Assert.AreEqual(MapPath.PathFailure.GoalBlocked, path.FailureReason);
        }

        // ──────────── 6. StartOccupied: start is a Wall ────────────

        [Test]
        public void FindPath_StartIsWall_ReturnsStartOccupied()
        {
            PathfindingTestHelpers.Block(_fx.map, _fx.registry, 2, 2);
            var path = PathfindingService.FindPath(
                _fx.map,
                start: new GridCoord(2, 2),
                goal: new GridCoord(5, 5),
                MapMovementProfile.Standard);

            Assert.IsFalse(path.Success);
            Assert.AreEqual(MapPath.PathFailure.StartOccupied, path.FailureReason);
        }

        // ──────────── 7. Start out of bounds: StartOccupied ────────────

        [Test]
        public void FindPath_StartOutOfBounds_ReturnsStartOccupied()
        {
            var path = PathfindingService.FindPath(
                _fx.map,
                start: new GridCoord(99, 99),
                goal: new GridCoord(0, 0),
                MapMovementProfile.Standard);

            Assert.IsFalse(path.Success);
            Assert.AreEqual(MapPath.PathFailure.StartOccupied, path.FailureReason);
        }

        // ──────────── 8. Goal out of bounds: GoalBlocked ────────────

        [Test]
        public void FindPath_GoalOutOfBounds_ReturnsGoalBlocked()
        {
            var path = PathfindingService.FindPath(
                _fx.map,
                start: new GridCoord(0, 0),
                goal: new GridCoord(99, 99),
                MapMovementProfile.Standard);

            Assert.IsFalse(path.Success);
            Assert.AreEqual(MapPath.PathFailure.GoalBlocked, path.FailureReason);
        }

        // ──────────── 9. Determinism: same input -> same output ────────────

        [Test]
        public void FindPath_SameInput_ProducesSamePathAndCost()
        {
            PathfindingTestHelpers.Block(_fx.map, _fx.registry, 3, 1);
            PathfindingTestHelpers.Block(_fx.map, _fx.registry, 3, 3);

            var p1 = PathfindingService.FindPath(
                _fx.map, new GridCoord(0, 0), new GridCoord(5, 5), MapMovementProfile.Standard);
            var p2 = PathfindingService.FindPath(
                _fx.map, new GridCoord(0, 0), new GridCoord(5, 5), MapMovementProfile.Standard);

            Assert.AreEqual(p1.TotalCost, p2.TotalCost);
            Assert.AreEqual(p1.Success, p2.Success);
            Assert.AreEqual(p1.Tiles.Count, p2.Tiles.Count);
            for (int i = 0; i < p1.Tiles.Count; i++)
                Assert.AreEqual(p1.Tiles[i], p2.Tiles[i]);
        }

        // ──────────── 10. Tie-break: identical cost paths remain identical across runs ────────────

        [Test]
        public void FindPath_TieBreak_DeterministicAcrossRuns()
        {
            // Same query multiple times - identical result (no hidden time / hash dependency).
            var p1 = PathfindingService.FindPath(
                _fx.map, new GridCoord(0, 0), new GridCoord(4, 4), MapMovementProfile.Standard);
            var p2 = PathfindingService.FindPath(
                _fx.map, new GridCoord(0, 0), new GridCoord(4, 4), MapMovementProfile.Standard);

            Assert.AreEqual(p1.TotalCost, p2.TotalCost);
            for (int i = 0; i < p1.Tiles.Count; i++)
                Assert.AreEqual(p1.Tiles[i], p2.Tiles[i]);
        }

        // ──────────── 11. Cross-layer: same X/Y different Layer requires CanCrossDimension ────────────

        [Test]
        public void FindPath_CrossLayer_WithoutCrossDimension_ReturnsUnreachable()
        {
            // Start in Reality, goal in Astral at same (X,Y). Standard has CanCrossDimension=false.
            // PhasePass-through is rejected -> Unreachable (per PathfindingService step 4).
            // Need a dual-layer map: Astral tile must be in registry so the layer check
            // fires (otherwise BlockedByTile / GoalBlocked kicks in first).
            var dualFx = PathfindingTestHelpers.MakeDualLayerMap(4, 4);

            var path = PathfindingService.FindPath(
                dualFx.map,
                start: new GridCoord(1, 1, DimensionLayer.Reality),
                goal: new GridCoord(1, 1, DimensionLayer.Astral),
                MapMovementProfile.Standard);

            Assert.IsFalse(path.Success);
            Assert.AreEqual(MapPath.PathFailure.Unreachable, path.FailureReason);

            PathfindingTestHelpers.Teardown(dualFx.map);
        }

        // ──────────── 12. Height delta: Standard cannot climb Δh=2; A* detours via Y=1 ────────────

        [Test]
        public void FindPath_HeightDeltaExceedsProfile_DetoursThroughLowTiles()
        {
            // (1,0) at height 2: Standard MaxAscend=1 cannot step (0,0)->(1,0) directly.
            // A* must detour through (1,1) or (0,1)/(2,1) to reach (2,0).
            PathfindingTestHelpers.SetHeight(_fx.registry, 1, 0, new HeightLevel(2));

            var path = PathfindingService.FindPath(
                _fx.map,
                start: new GridCoord(0, 0),
                goal: new GridCoord(2, 0),
                MapMovementProfile.Standard);

            Assert.IsTrue(path.Success, "A* should detour around the high tile.");
            Assert.IsFalse(ContainsTile(path, new GridCoord(1, 0)),
                "Path must not step on the impassable high tile (1,0).");
            Assert.AreEqual(new GridCoord(0, 0), path.Tiles[0]);
            Assert.AreEqual(new GridCoord(2, 0), path.Tiles[path.Tiles.Count - 1]);
        }

        // ──────────── 13. Goal height exceeds profile: direct step blocked -> GoalBlocked ────────────

        [Test]
        public void FindPath_GoalHeightExceedsProfile_ReturnsGoalBlocked()
        {
            // Goal (2,0) at height 10. Standard MaxAscend=1 -> goal pre-check fails.
            PathfindingTestHelpers.SetHeight(_fx.registry, 2, 0, new HeightLevel(10));

            var path = PathfindingService.FindPath(
                _fx.map,
                start: new GridCoord(0, 0),
                goal: new GridCoord(2, 0),
                MapMovementProfile.Standard);

            Assert.IsFalse(path.Success);
            Assert.AreEqual(MapPath.PathFailure.GoalBlocked, path.FailureReason);
        }

        // ──────────── 14. Flyer ignores height delta ────────────

        [Test]
        public void FindPath_Flyer_IgnoresHeightDelta()
        {
            // (1,0) at height 5. Standard would refuse; Flyer (CanFly=true) passes through.
            PathfindingTestHelpers.SetHeight(_fx.registry, 1, 0, new HeightLevel(5));

            var path = PathfindingService.FindPath(
                _fx.map,
                start: new GridCoord(0, 0),
                goal: new GridCoord(2, 0),
                MapMovementProfile.Flyer);

            Assert.IsTrue(path.Success);
            // Flyer can step through (1,0) directly.
            Assert.IsTrue(ContainsTile(path, new GridCoord(1, 0)),
                "Flyer should be able to step on the high tile.");
        }

        // ──────────── 15. L-shape detour cost match: detour through (0,1)+(1,1)+(2,1)+(2,0) = 4 ────────────

        [Test]
        public void FindPath_Detour_CostEqualsShortestDetour()
        {
            PathfindingTestHelpers.Block(_fx.map, _fx.registry, 1, 0);  // block direct east

            var path = PathfindingService.FindPath(
                _fx.map,
                start: new GridCoord(0, 0),
                goal: new GridCoord(2, 0),
                MapMovementProfile.Standard);

            Assert.IsTrue(path.Success);
            // Direct east blocked; shortest detour is 4 cells (up, right, right, down).
            Assert.AreEqual(5, path.Tiles.Count);  // start + 4 steps = 5 tiles
            Assert.AreEqual(4, path.TotalCost);
        }

        // ──────────── SPOT-CHECK: straight-line path consistency (Y-axis ordering) ────────────

        [Test]
        public void SpotCheck_StraightEast_YOrderingPreserved()
        {
            var path = PathfindingService.FindPath(
                _fx.map,
                new GridCoord(0, 0),
                new GridCoord(5, 0),
                MapMovementProfile.Standard);

            Assert.IsTrue(path.Success);
            Assert.AreEqual(6, path.Tiles.Count);
            // Y constant (0), X strictly increasing 0..5.
            for (int i = 0; i < path.Tiles.Count; i++)
            {
                Assert.AreEqual(0, path.Tiles[i].Y);
                Assert.AreEqual(i, path.Tiles[i].X);
            }
        }

        [Test]
        public void SpotCheck_VerticalNorth_YStrictlyIncreasing()
        {
            // From (0,0) to (0,5): Y ascending (North direction).
            var path = PathfindingService.FindPath(
                _fx.map,
                new GridCoord(0, 0),
                new GridCoord(0, 5),
                MapMovementProfile.Standard);

            Assert.IsTrue(path.Success);
            Assert.AreEqual(6, path.Tiles.Count);
            for (int i = 0; i < path.Tiles.Count; i++)
            {
                Assert.AreEqual(i, path.Tiles[i].Y);
                Assert.AreEqual(0, path.Tiles[i].X);
            }
        }

        [Test]
        public void SpotCheck_StartEqualsGoal_SingleTile()
        {
            var start = new GridCoord(3, 3);
            var path = PathfindingService.FindPath(
                _fx.map,
                new GridCoord(start.X, start.Y),
                new GridCoord(start.X, start.Y),
                MapMovementProfile.Standard);

            Assert.IsTrue(path.Success);
            Assert.AreEqual(1, path.Tiles.Count);
            Assert.AreEqual(3, path.Tiles[0].X);
            Assert.AreEqual(3, path.Tiles[0].Y);
        }

        [Test]
        public void SpotCheck_BlockedTileAvoided_DoesNotEnterWall()
        {
            PathfindingTestHelpers.Block(_fx.map, _fx.registry, 2, 0);
            var path = PathfindingService.FindPath(
                _fx.map,
                new GridCoord(0, 0),
                new GridCoord(4, 0),
                MapMovementProfile.Standard);

            Assert.IsTrue(path.Success);
            Assert.IsFalse(ContainsTile(path, new GridCoord(2, 0)),
                "A* must detour around the wall tile.");
        }

        [Test]
        public void SpotCheck_DistanceMatches_NodeCountIsManhattanPlusOne()
        {
            // Straight east: 4 cells = 5 nodes (start + 4 neighbors).
            var path = PathfindingService.FindPath(
                _fx.map,
                new GridCoord(0, 0),
                new GridCoord(4, 0),
                MapMovementProfile.Standard);

            Assert.IsTrue(path.Success);
            Assert.AreEqual(5, path.Tiles.Count);
            Assert.AreEqual(4, path.TotalCost);
        }

        // ──────────── 18. Null state throws ────────────

        [Test]
        public void FindPath_NullState_Throws()
        {
            Assert.Throws<System.ArgumentNullException>(
                () => PathfindingService.FindPath(
                    null,
                    new GridCoord(0, 0),
                    new GridCoord(1, 0),
                    MapMovementProfile.Standard));
        }

        // ──────────── helper ────────────

        private static bool ContainsTile(MapPath path, GridCoord target)
        {
            for (int i = 0; i < path.Tiles.Count; i++)
                if (path.Tiles[i] == target) return true;
            return false;
        }
    }
}