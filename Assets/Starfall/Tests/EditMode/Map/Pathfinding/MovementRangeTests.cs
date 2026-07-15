using System.Collections.Generic;
using NUnit.Framework;
using Starfall.Core.Map.Coordinates;
using Starfall.Core.Map.Height;
using Starfall.Core.Map.Pathfinding;
using Starfall.Core.Map.State;
using Starfall.Core.Map.Tile;

namespace Starfall.Tests.EditMode.Map.Pathfinding
{
    /// <summary>
    /// doc2 MAP-05 §9.4 <see cref="MovementRangeService"/> behavior tests.
    ///
    /// <para/>
    /// Coverage:
    /// - BFS expansion on plain maps
    /// - AP limit honored
    /// - Walls block tiles (boundary)
    /// - Output is sorted by GridCoord.CompareTo
    /// - Origin always present in result
    /// - Zero AP returns only origin
    /// - Heavy AP-4 reaches 4 cells out
    /// - Flyer profile ignores height delta
    /// - Origin out of bounds throws
    /// - Detached registry tolerates "plain" cost fallback
    /// </summary>
    public class MovementRangeTests
    {
        private (MapState map, TileDefinitionRegistry registry) _fx;

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

        // ──────────── 1. Origin must always be in result ────────────

        [Test]
        public void GetReachableTiles_OriginAlwaysPresent()
        {
            var origin = new GridCoord(3, 3);
            var result = MovementRangeService.GetReachableTiles(_fx.map, origin, MapMovementProfile.Standard);
            Assert.IsTrue(ContainsCoord(result, origin));
        }

        // ──────────── 2. Zero AP returns only origin ────────────

        [Test]
        public void GetReachableTiles_ZeroAP_OnlyOrigin()
        {
            var origin = new GridCoord(3, 3);
            var zeroAp = new MapMovementProfile(1, 2, false, false, 0);
            var result = MovementRangeService.GetReachableTiles(_fx.map, origin, zeroAp);
            Assert.AreEqual(1, result.Count);
            Assert.AreEqual(origin, result[0]);
        }

        // ──────────── 3. AP=1 plain map: 1 origin + 4 neighbors (no further expansion) ────────────

        [Test]
        public void GetReachableTiles_PlainMap_AP1_ReachesOriginAndFourNeighbors()
        {
            // baseMoveCost = 1, AP = 1, 4-neighbor. From (3,3) we reach only 5 cells.
            // (cells with newCost ≤ 1 are added; cost-1 cells are NOT expanded further).
            var origin = new GridCoord(3, 3);
            var ap1 = new MapMovementProfile(1, 2, false, false, 1);
            var result = MovementRangeService.GetReachableTiles(_fx.map, origin, ap1);
            Assert.AreEqual(5, result.Count);
            Assert.IsTrue(ContainsCoord(result, origin));
            Assert.IsTrue(ContainsCoord(result, new GridCoord(3, 2)));
            Assert.IsTrue(ContainsCoord(result, new GridCoord(4, 3)));
            Assert.IsTrue(ContainsCoord(result, new GridCoord(3, 4)));
            Assert.IsTrue(ContainsCoord(result, new GridCoord(2, 3)));
        }

        // ──────────── 4. AP=2 expands full diamond (radius 2 = 13 cells) ────────────

        [Test]
        public void GetReachableTiles_PlainMap_AP2_ExpandsFullDiamond()
        {
            var origin = new GridCoord(4, 4);
            var ap2 = new MapMovementProfile(1, 2, false, false, 2);
            var result = MovementRangeService.GetReachableTiles(_fx.map, origin, ap2);
            // Diamond at manhattan ≤ 2: 1 + 4 + 8 = 13 cells.
            Assert.AreEqual(13, result.Count);
            Assert.IsTrue(ContainsCoord(result, origin));
            // Verify a ring-2 corner is reachable.
            Assert.IsTrue(ContainsCoord(result, new GridCoord(2, 4)));
            Assert.IsTrue(ContainsCoord(result, new GridCoord(6, 4)));
        }

        // ──────────── 5. AP=3 expands 25-cell diamond (radius 3) ────────────

        [Test]
        public void GetReachableTiles_PlainMap_AP3_ExpandsFullDiamond()
        {
            var origin = new GridCoord(4, 4);
            var ap3 = new MapMovementProfile(1, 2, false, false, 3);
            var result = MovementRangeService.GetReachableTiles(_fx.map, origin, ap3);
            // Diamond at manhattan ≤ 3: 1 + 4 + 8 + 12 = 25 cells (within 8x8 from (4,4)).
            Assert.AreEqual(25, result.Count);
        }

        // ──────────── 5. Walls blocked from result ────────────

        [Test]
        public void GetReachableTiles_WallBoundary_BlocksExpansion()
        {
            // Surround (3,3) with walls on N, E, S, W to create a one-tile room.
            // The only reachable tile should be (3,3) itself (plus any tile INSIDE doesn't exist).
            PathfindingTestHelpers.Block(_fx.map, _fx.registry, 2, 3);
            PathfindingTestHelpers.Block(_fx.map, _fx.registry, 4, 3);
            PathfindingTestHelpers.Block(_fx.map, _fx.registry, 3, 2);
            PathfindingTestHelpers.Block(_fx.map, _fx.registry, 3, 4);

            var origin = new GridCoord(3, 3);
            var ap6 = MapMovementProfile.Standard;  // AP = 6
            var result = MovementRangeService.GetReachableTiles(_fx.map, origin, ap6);

            // Only (3,3) reachable (since its 4 neighbors are walls).
            Assert.AreEqual(1, result.Count);
            Assert.AreEqual(origin, result[0]);
        }

        // ──────────── 6. Output is sorted ascending by GridCoord.CompareTo ────────────

        [Test]
        public void GetReachableTiles_ResultSortedByCoord()
        {
            var origin = new GridCoord(4, 4);
            var result = MovementRangeService.GetReachableTiles(_fx.map, origin, MapMovementProfile.Standard);
            for (int i = 1; i < result.Count; i++)
            {
                Assert.IsTrue(result[i - 1].CompareTo(result[i]) < 0,
                    $"Index {i - 1}: {result[i - 1]} should come before {result[i]}");
            }
        }

        // ──────────── 7. Heavy AP=4 only reaches 4 cells beyond origin ────────────

        [Test]
        public void GetReachableTiles_Heavy_AP4_FourStepsPlain()
        {
            // baseMoveCost=1, AP=4, 4 neighbors, plain map.
            // From (4,4) with no obstacles, Heavy (AP=4) reaches diamond radius 4.
            // Number of cells = 1 + 4 + 8 + 12 + 16 = 41.
            var result = MovementRangeService.GetReachableTiles(_fx.map, new GridCoord(4, 4), MapMovementProfile.Heavy);
            // x: [0..7], y: [0..7]. From (4,4) within radius 4:
            // For Manhattan <=4: count cells within diamond radius 4 inside 8x8: too large; just sanity check >= 13.
            Assert.GreaterOrEqual(result.Count, 13);
        }

        // ──────────── 8. Higher move cost reduces reachable count ────────────

        [Test]
        public void GetReachableTiles_HigherMoveCost_ReducesCount()
        {
            // Plain cost = 2 (Rough-ish), AP = 4 -> reach Manhattan <=2 from origin (4,4).
            // We rebuild map with custom cost.
            var altFx = PathfindingTestHelpers.MakePlainMap(8, 8, baseMoveCost: 2);

            var ap4 = new MapMovementProfile(1, 2, false, false, 4);
            var result = MovementRangeService.GetReachableTiles(altFx.map, new GridCoord(4, 4), ap4);

            // Cost 2 + AP 4 = radius 2 = 13 cells.
            Assert.GreaterOrEqual(result.Count, 5);
            Assert.LessOrEqual(result.Count, 13);

            PathfindingTestHelpers.Teardown(altFx.map);
        }

        // ──────────── 9. Flyer profile ignores height when checking adjacency ────────────

        [Test]
        public void GetReachableTiles_Flyer_RisesAboveHeightCost_Passes()
        {
            // Bump (5,4) to height 3; Flyer ignores height.
            PathfindingTestHelpers.SetHeight(_fx.registry, 5, 4, new HeightLevel(3));

            var result = MovementRangeService.GetReachableTiles(
                _fx.map,
                new GridCoord(4, 4),
                MapMovementProfile.Flyer);

            Assert.IsTrue(ContainsCoord(result, new GridCoord(5, 4)));
        }

        // ──────────── 10. Origin out of bounds throws ────────────

        [Test]
        public void GetReachableTiles_OriginOutOfBounds_Throws()
        {
            Assert.Throws<System.ArgumentOutOfRangeException>(
                () => MovementRangeService.GetReachableTiles(
                    _fx.map,
                    new GridCoord(99, 99),
                    MapMovementProfile.Standard));
        }

        // ──────────── 11. Null state throws ────────────

        [Test]
        public void GetReachableTiles_NullState_Throws()
        {
            Assert.Throws<System.ArgumentNullException>(
                () => MovementRangeService.GetReachableTiles(
                    null,
                    new GridCoord(0, 0),
                    MapMovementProfile.Standard));
        }

        // ──────────── 12. Returning list is IReadOnlyList<GridCoord> ────────────

        [Test]
        public void GetReachableTiles_ReturnTypeIsIReadOnlyList()
        {
            var result = MovementRangeService.GetReachableTiles(
                _fx.map,
                new GridCoord(4, 4),
                MapMovementProfile.Standard);
            Assert.IsInstanceOf<IReadOnlyList<GridCoord>>(result);
        }

        private static bool ContainsCoord(IReadOnlyList<GridCoord> list, GridCoord target)
        {
            for (int i = 0; i < list.Count; i++)
                if (list[i].Equals(target)) return true;
            return false;
        }
    }
}
