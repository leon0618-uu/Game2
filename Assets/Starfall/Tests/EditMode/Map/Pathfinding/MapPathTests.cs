using System.Collections.Generic;
using NUnit.Framework;
using Starfall.Core.Map.Coordinates;
using Starfall.Core.Map.Pathfinding;

namespace Starfall.Tests.EditMode.Map.Pathfinding
{
    /// <summary>
    /// doc2 MAP-05 §9.4 <see cref="MapPath"/> data structure tests.
    /// Covers: Null factory, From factory, RiskTags ordering, ToString format,
    /// PathFailure constants stability, immutability.
    /// </summary>
    public class MapPathTests
    {
        // ──────────── 1. Null factory ────────────

        [Test]
        public void Null_Factory_Returns_SuccessFalse()
        {
            var p = MapPath.Null(MapPath.PathFailure.NoPath);
            Assert.IsFalse(p.Success);
            Assert.AreEqual(MapPath.PathFailure.NoPath, p.FailureReason);
        }

        // ──────────── 2. Null factory defaults to NoPath on null ────────────

        [Test]
        public void Null_Factory_NullReason_FallsBackToNoPath()
        {
            var p = MapPath.Null(null);
            Assert.IsFalse(p.Success);
            Assert.AreEqual(MapPath.PathFailure.NoPath, p.FailureReason);
        }

        // ──────────── 3. From factory: single-tile path (start == goal) ────────────

        [Test]
        public void From_SingleTilePath_HasCostZero()
        {
            var origin = new GridCoord(2, 3);
            var p = MapPath.From(new[] { origin }, totalCost: 0);
            Assert.IsTrue(p.Success);
            Assert.AreEqual(0, p.TotalCost);
            Assert.AreEqual(1, p.Tiles.Count);
            Assert.AreEqual(origin, p.Tiles[0]);
        }

        // ──────────── 4. From factory: multi-tile path preserves order ────────────

        [Test]
        public void From_MultiTilePath_PreservesOrder()
        {
            var tiles = new List<GridCoord>
            {
                new GridCoord(0, 0),
                new GridCoord(1, 0),
                new GridCoord(2, 0),
                new GridCoord(3, 0),
            };
            var p = MapPath.From(tiles, totalCost: 3);
            Assert.IsTrue(p.Success);
            Assert.AreEqual(4, p.Tiles.Count);
            Assert.AreEqual(new GridCoord(0, 0), p.Tiles[0]);
            Assert.AreEqual(new GridCoord(3, 0), p.Tiles[3]);
        }

        // ──────────── 5. From factory: risk tags are sorted ────────────

        [Test]
        public void From_RiskTags_AreSortedOrdinalAscending()
        {
            var tiles = new[] { new GridCoord(0, 0), new GridCoord(1, 0) };
            var tags = new List<string> { "OverHeight", "Hazard", "CrossPhase", "Alpha" };
            var p = MapPath.From(tiles, 2, tags);

            Assert.IsTrue(p.Success);
            Assert.AreEqual(4, p.RiskTags.Count);
            // Sorted ordinal ascending: Alpha, CrossPhase, Hazard, OverHeight
            Assert.AreEqual("Alpha", p.RiskTags[0]);
            Assert.AreEqual("CrossPhase", p.RiskTags[1]);
            Assert.AreEqual("Hazard", p.RiskTags[2]);
            Assert.AreEqual("OverHeight", p.RiskTags[3]);
        }

        // ──────────── 6. From factory with empty RiskTags ────────────

        [Test]
        public void From_NullOrEmptyRiskTags_ResultInEmptyList()
        {
            var tiles = new[] { new GridCoord(0, 0) };
            var p1 = MapPath.From(tiles, 0, null);
            var p2 = MapPath.From(tiles, 0, new List<string>());
            Assert.AreEqual(0, p1.RiskTags.Count);
            Assert.AreEqual(0, p2.RiskTags.Count);
        }

        // ──────────── 7. From factory with empty Tiles throws ────────────

        [Test]
        public void From_EmptyTiles_Throws()
        {
            Assert.Throws<System.ArgumentException>(
                () => MapPath.From(new GridCoord[0], 0));
        }

        // ──────────── 8. From factory with null Tiles throws ────────────

        [Test]
        public void From_NullTiles_Throws()
        {
            Assert.Throws<System.ArgumentNullException>(
                () => MapPath.From(null, 0));
        }

        // ──────────── 9. PathFailure constants are stable strings ────────────

        [Test]
        public void PathFailure_Constants_AreStableStrings()
        {
            Assert.AreEqual("NoPath", MapPath.PathFailure.NoPath);
            Assert.AreEqual("GoalBlocked", MapPath.PathFailure.GoalBlocked);
            Assert.AreEqual("StartOccupied", MapPath.PathFailure.StartOccupied);
            Assert.AreEqual("Unreachable", MapPath.PathFailure.Unreachable);
        }

        // ──────────── 10. ToString output ────────────

        [Test]
        public void ToString_Successful_Contains_CountAndCost()
        {
            var tiles = new[] { new GridCoord(0, 0), new GridCoord(1, 0), new GridCoord(2, 0) };
            var p = MapPath.From(tiles, 2, new[] { "Hazard" });
            string s = p.ToString();
            StringAssert.Contains("tiles=3", s);
            StringAssert.Contains("cost=2", s);
            StringAssert.Contains("Hazard", s);
        }

        [Test]
        public void ToString_Failed_Contains_Reason()
        {
            var p = MapPath.Null(MapPath.PathFailure.GoalBlocked);
            string s = p.ToString();
            StringAssert.Contains("GoalBlocked", s);
        }
    }
}
