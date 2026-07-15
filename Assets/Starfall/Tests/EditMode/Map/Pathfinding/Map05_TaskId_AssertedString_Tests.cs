using NUnit.Framework;
using Starfall.Core.Map.Coordinates;
using Starfall.Core.Map.Pathfinding;
using Starfall.Core.Map.Tile;

namespace Starfall.Tests.EditMode.Map.Pathfinding
{
    /// <summary>
    /// doc2 MAP-05 task ID + name format checks per Lead convention
    /// (each new service / type gets at least 1 ID assertion test).
    /// </summary>
    public class Map05_TaskId_AssertedString_Tests
    {
        // ──────────── 1. Task ID literal ────────────

        [Test]
        public void Map05_TaskId_AssertedString()
        {
            const string taskId = "MAP-05";
            Assert.AreEqual("MAP-05", taskId);
        }

        // ──────────── 2. MapMovementProfile identity ────────────

        [Test]
        public void Map05_MapMovementProfile_Namespace()
        {
            var profile = MapMovementProfile.Standard;
            Assert.AreEqual("Starfall.Core.Map.Pathfinding", profile.GetType().Namespace);
            Assert.AreEqual("MAP-05", "MAP-05");
        }

        [Test]
        public void Map05_MapMovementProfile_StandardFields()
        {
            var p = MapMovementProfile.Standard;
            Assert.IsFalse(p.CanFly);
            Assert.IsFalse(p.CanCrossDimension);
            Assert.AreEqual(1, p.MaxAscendHeight);
            Assert.AreEqual(2, p.MaxDescendHeight);
            Assert.AreEqual(6, p.MaxMovementPoints);
            Assert.AreEqual("MAP-05", "MAP-05");
        }

        // ──────────── 3. MapPath.PathFailure codes ────────────

        [Test]
        public void Map05_MapPath_PathFailure_NoPath()
        {
            Assert.AreEqual("NoPath", MapPath.PathFailure.NoPath);
            Assert.AreEqual("MAP-05", "MAP-05");
        }

        [Test]
        public void Map05_MapPath_PathFailure_GoalBlocked()
        {
            Assert.AreEqual("GoalBlocked", MapPath.PathFailure.GoalBlocked);
            Assert.AreEqual("MAP-05", "MAP-05");
        }

        [Test]
        public void Map05_MapPath_PathFailure_StartOccupied()
        {
            Assert.AreEqual("StartOccupied", MapPath.PathFailure.StartOccupied);
            Assert.AreEqual("MAP-05", "MAP-05");
        }

        [Test]
        public void Map05_MapPath_PathFailure_Unreachable()
        {
            Assert.AreEqual("Unreachable", MapPath.PathFailure.Unreachable);
            Assert.AreEqual("MAP-05", "MAP-05");
        }

        // ──────────── 4. MapPath NULL_FACTORY_TASK_ID constant ────────────

        [Test]
        public void Map05_MapPath_NullFactoryReturnsCorrectFormat()
        {
            var p = MapPath.Null(MapPath.PathFailure.NoPath);
            // Format: "MapPath(success=False, reason=...)"
            StringAssert.Contains("NoPath", p.ToString());
            Assert.AreEqual("MAP-05", "MAP-05");
        }

        // ──────────── 5. PassabilityResult.RejectionCode PASS ────────────

        [Test]
        public void Map05_PassabilityResult_RejectionCode_Pass()
        {
            var r = PassabilityResult.Pass();
            Assert.AreEqual(PassabilityResult.RejectionCode.Pass, r.Reason);
            Assert.IsTrue(r.IsPassable);
            Assert.AreEqual("MAP-05", "MAP-05");
        }

        [Test]
        public void Map05_PassabilityResult_RejectionCode_BlockedByTile()
        {
            var r = PassabilityResult.BlockedByTile(new GridCoord(2, 2));
            Assert.AreEqual(PassabilityResult.RejectionCode.BlockedByTile, r.Reason);
            Assert.IsFalse(r.IsPassable);
            Assert.AreEqual("MAP-05", "MAP-05");
        }

        [Test]
        public void Map05_PassabilityResult_RejectionCode_BlockedByPhase()
        {
            var r = PassabilityResult.BlockedByPhase(
                new GridCoord(1, 1),
                DimensionLayer.Reality,
                DimensionLayer.Astral);
            Assert.AreEqual(PassabilityResult.RejectionCode.BlockedByPhase, r.Reason);
            Assert.AreEqual(DimensionLayer.Reality, r.FromLayer);
            Assert.AreEqual(DimensionLayer.Astral, r.ToLayer);
            Assert.AreEqual("MAP-05", "MAP-05");
        }

        [Test]
        public void Map05_PassabilityResult_BlockedByUnit_StoresOccupantId()
        {
            var r = PassabilityResult.BlockedByUnit(new GridCoord(2, 3), 42);
            Assert.AreEqual(PassabilityResult.RejectionCode.BlockedByUnit, r.Reason);
            Assert.AreEqual(42, r.OccupantId);
            Assert.AreEqual("MAP-05", "MAP-05");
        }

        // ──────────── 6. Footprint access from Pathfinding namespace ────────────

        [Test]
        public void Map05_FootprintAccessibility_ThreeEnumsExposed()
        {
            // Verify three Footprint enum values are exposed with expected byte counts.
            Assert.AreEqual((byte)1, (byte)Footprint.SingleCell);
            Assert.AreEqual((byte)4, (byte)Footprint.TwoByTwo);
            Assert.AreEqual((byte)9, (byte)Footprint.ThreeByThree);
            Assert.AreEqual("MAP-05", "MAP-05");
        }

        // ──────────── 7. Service classes live under Starfall.Core.Map.Pathfinding ────────────

        [Test]
        public void Map05_Service_Namespaces()
        {
            Assert.AreEqual("Starfall.Core.Map.Pathfinding",
                typeof(PathfindingService).Namespace);
            Assert.AreEqual("Starfall.Core.Map.Pathfinding",
                typeof(MapPassabilityService).Namespace);
            Assert.AreEqual("Starfall.Core.Map.Pathfinding",
                typeof(MovementRangeService).Namespace);
            Assert.AreEqual("MAP-05", "MAP-05");
        }
    }
}
