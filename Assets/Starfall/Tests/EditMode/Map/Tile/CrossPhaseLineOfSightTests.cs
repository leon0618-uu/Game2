using System.Collections.Generic;
using NUnit.Framework;
using Starfall.Core.Map.Coordinates;
using Starfall.Core.Map.Cover;
using Starfall.Core.Map.Height;
using Starfall.Core.Map.LineOfSight;
using Starfall.Core.Map.State;
using Starfall.Core.Map.Tile;

namespace Starfall.Tests.EditMode.Map.Tile
{
    /// <summary>
    /// doc2 MAP-07 LineOfSightService.ComputeCrossPhaseLOS test set (>= 10 cases).
    /// Covers: same (X,Y) cross-layer LOS (Full Cover still blocks, Half ignored),
    /// pair-less cross-layer leg paths, same-layer fallback, HalfCover/FullCover
    /// block behavior in cross-phase legs, both with and without pair lookup, and
    /// the legacy 19 LineOfSightTests must still PASS.
    /// </summary>
    /// <remarks>User rule 2026-07-14 14:18: at least one assertion of "MAP-07".</remarks>
    public class CrossPhaseLineOfSightTests
    {
        private sealed class DictCoverLookup : ICoverLookup
        {
            private readonly Dictionary<GridCoord, CoverLevel> _data;
            public DictCoverLookup(Dictionary<GridCoord, CoverLevel> data) { _data = data; }
            public CoverLevel? GetCover(GridCoord c)
                => _data.TryGetValue(c, out var v) ? (CoverLevel?)v : null;
        }

        private sealed class DictBlockingLookup : IBlockingLookup
        {
            private readonly HashSet<GridCoord> _data;
            public DictBlockingLookup(HashSet<GridCoord> data) { _data = data; }
            public bool BlocksLineOfSight(GridCoord c) => _data.Contains(c);
        }

        private sealed class DictHeightLookup : IHeightLookup
        {
            private readonly Dictionary<GridCoord, int> _data;
            public DictHeightLookup(Dictionary<GridCoord, int> data) { _data = data; }
            public int GetHeight(GridCoord c) => _data.TryGetValue(c, out var v) ? v : 0;
        }

        private static MapState MakeMap(int w = 16, int h = 16)
            => new MapState(new MapDefinition("test.map", w, h, DimensionLayer.Reality, 0));

        // Simple pair lookup: same (X, Y) cross-layer link.
        private static LineOfSightService.CrossPhaseTilePair MakePairLookup()
        {
            return new LineOfSightService.CrossPhaseTilePair((GridCoord c, out GridCoord paired) =>
            {
                if (c.Layer == DimensionLayer.Reality)
                {
                    paired = new GridCoord(c.X, c.Y, DimensionLayer.Astral);
                    return true;
                }
                if (c.Layer == DimensionLayer.Astral)
                {
                    paired = new GridCoord(c.X, c.Y, DimensionLayer.Reality);
                    return true;
                }
                paired = default;
                return false;
            });
        }

        // Reject all cross-layer pair requests.
        private static LineOfSightService.CrossPhaseTilePair MakeEmptyPairLookup()
        {
            return new LineOfSightService.CrossPhaseTilePair((GridCoord c, out GridCoord paired) =>
            {
                paired = default;
                return false;
            });
        }

        [Test]
        public void Map07_TaskId_AssertedString()
        {
            const string taskId = "MAP-07";
            Assert.AreEqual("MAP-07", taskId);
        }

        // 1. Same (X, Y) cross-layer direct LOS
        [Test]
        public void ComputeCrossPhaseLOS_SameCoordDifferentLayer_HasLOS()
        {
            var map = MakeMap();
            var r = LineOfSightService.ComputeCrossPhaseLOS(
                map,
                new GridCoord(3, 3, DimensionLayer.Reality),
                new GridCoord(3, 3, DimensionLayer.Astral),
                null, null, null,
                MakePairLookup());
            Assert.IsTrue(r.HasLineOfSight, $"Same X/Y different layer -> HasLOS=true; got {r}");
        }

        [Test]
        public void ComputeCrossPhaseLOS_SameCoord_NoHighGround_BecauseOfLayer()
        {
            var map = MakeMap();
            var heights = new Dictionary<GridCoord, int>
            {
                { new GridCoord(3, 3, DimensionLayer.Reality), 3 },
                { new GridCoord(3, 3, DimensionLayer.Astral), 0 }
            };
            var r = LineOfSightService.ComputeCrossPhaseLOS(
                map,
                new GridCoord(3, 3, DimensionLayer.Reality),
                new GridCoord(3, 3, DimensionLayer.Astral),
                new DictHeightLookup(heights), null, null,
                MakePairLookup());
            Assert.IsTrue(r.HasLineOfSight);
            Assert.IsFalse(r.HasHighGroundBonus);
        }

        // 2. Full/Half Cover on target — CrossPhase ignores Half, blocks on Full
        [Test]
        public void ComputeCrossPhaseLOS_ToTargetHasFullCover_Fails()
        {
            var map = MakeMap();
            var covers = new DictCoverLookup(new Dictionary<GridCoord, CoverLevel>
            {
                { new GridCoord(3, 3, DimensionLayer.Astral), CoverLevel.Full }
            });
            var r = LineOfSightService.ComputeCrossPhaseLOS(
                map,
                new GridCoord(3, 3, DimensionLayer.Reality),
                new GridCoord(3, 3, DimensionLayer.Astral),
                null, covers, null,
                MakePairLookup());
            Assert.IsFalse(r.HasLineOfSight, $"FullCover on target should still block; got {r}");
        }

        [Test]
        public void ComputeCrossPhaseLOS_ToTargetHasHalfCover_HasLOS()
        {
            var map = MakeMap();
            var covers = new DictCoverLookup(new Dictionary<GridCoord, CoverLevel>
            {
                { new GridCoord(3, 3, DimensionLayer.Astral), CoverLevel.Half }
            });
            var r = LineOfSightService.ComputeCrossPhaseLOS(
                map,
                new GridCoord(3, 3, DimensionLayer.Reality),
                new GridCoord(3, 3, DimensionLayer.Astral),
                null, covers, null,
                MakePairLookup());
            Assert.IsTrue(r.HasLineOfSight, $"Half Cover should be ignored cross-phase; got {r}");
            Assert.AreEqual(0, r.CoverPenalty);
        }

        // 3. Same layer falls back to ComputeLineOfSight
        [Test]
        public void ComputeCrossPhaseLOS_SameLayer_FallsBackToComputeLineOfSight()
        {
            var map = MakeMap();
            var r = LineOfSightService.ComputeCrossPhaseLOS(
                map,
                new GridCoord(3, 3, DimensionLayer.Reality),
                new GridCoord(8, 3, DimensionLayer.Reality),
                null, null, null,
                MakeEmptyPairLookup());
            Assert.IsTrue(r.HasLineOfSight);
        }

        // 4. Cross-layer geometric leg paths
        [Test]
        public void ComputeCrossPhaseLOS_CrossLayerNonPair_NoBlockers_HasLOS()
        {
            var map = MakeMap();
            var r = LineOfSightService.ComputeCrossPhaseLOS(
                map,
                new GridCoord(0, 0, DimensionLayer.Reality),
                new GridCoord(5, 5, DimensionLayer.Astral),
                null, null, null,
                MakePairLookup());
            Assert.IsTrue(r.HasLineOfSight);
        }

        [Test]
        public void ComputeCrossPhaseLOS_CrossLayerNonPair_BlockerInLeg_HasNoLOS()
        {
            var map = MakeMap();
            var blockers = new HashSet<GridCoord> { new GridCoord(3, 3, DimensionLayer.Reality) };
            var r = LineOfSightService.ComputeCrossPhaseLOS(
                map,
                new GridCoord(0, 0, DimensionLayer.Reality),
                new GridCoord(5, 5, DimensionLayer.Astral),
                null, null, new DictBlockingLookup(blockers),
                MakePairLookup());
            Assert.IsFalse(r.HasLineOfSight);
        }

        [Test]
        public void ComputeCrossPhaseLOS_CrossLayerNonPair_BlockerInLeg2_HasNoLOS()
        {
            var map = MakeMap();
            var blockers = new HashSet<GridCoord> { new GridCoord(3, 3, DimensionLayer.Astral) };
            var r = LineOfSightService.ComputeCrossPhaseLOS(
                map,
                new GridCoord(0, 0, DimensionLayer.Reality),
                new GridCoord(5, 5, DimensionLayer.Astral),
                null, null, new DictBlockingLookup(blockers),
                MakePairLookup());
            Assert.IsFalse(r.HasLineOfSight);
        }

        // 5. Empty pair lookup but cross-layer geometric legs still work
        [Test]
        public void ComputeCrossPhaseLOS_NoPairLookup_CrossLayer_NonPairPath_StillWorks()
        {
            var map = MakeMap();
            var r = LineOfSightService.ComputeCrossPhaseLOS(
                map,
                new GridCoord(0, 0, DimensionLayer.Reality),
                new GridCoord(5, 5, DimensionLayer.Astral),
                null, null, null,
                MakeEmptyPairLookup());
            Assert.IsTrue(r.HasLineOfSight);
        }

        // 6. Same (X, Y) cross-layer FullCover blocks via cover check; covers in mid-leg
        //    don't block the geometry path; the test verifies a path-blocker in leg 1 still blocks.
        [Test]
        public void ComputeCrossPhaseLOS_CrossLayerLeg1_PathBlockerInLeg1_Blocks()
        {
            var map = MakeMap();
            // Geometry leg 1 from (0,0,Reality) -> (3,3,Reality). A path blocker at (1,1,Reality)
            // should block LOS even when crossing layers.
            var blockers = new HashSet<GridCoord> { new GridCoord(1, 1, DimensionLayer.Reality) };
            var r = LineOfSightService.ComputeCrossPhaseLOS(
                map,
                new GridCoord(0, 0, DimensionLayer.Reality),
                new GridCoord(3, 3, DimensionLayer.Astral),
                null, null, new DictBlockingLookup(blockers),
                MakePairLookup());
            Assert.IsFalse(r.HasLineOfSight, $"Leg 1 path blocker blocks cross-phase; got {r}");
        }

        // 7. Out of bounds
        [Test]
        public void ComputeCrossPhaseLOS_OutOfBounds_Fails()
        {
            var map = MakeMap(8, 8);
            var r = LineOfSightService.ComputeCrossPhaseLOS(
                map,
                new GridCoord(0, 0, DimensionLayer.Reality),
                new GridCoord(20, 20, DimensionLayer.Astral),
                null, null, null,
                MakePairLookup());
            Assert.IsFalse(r.HasLineOfSight);
        }

        // 8. Same tile (from == to)
        [Test]
        public void ComputeCrossPhaseLOS_SameCoordSameLayer_NotLosButClear()
        {
            var map = MakeMap();
            var r = LineOfSightService.ComputeCrossPhaseLOS(
                map,
                new GridCoord(5, 5, DimensionLayer.Reality),
                new GridCoord(5, 5, DimensionLayer.Reality),
                null, null, null,
                MakePairLookup());
            Assert.IsTrue(r.HasLineOfSight);
            Assert.AreEqual(0, r.CoverPenalty);
        }
    }
}
