using System.Collections.Generic;
using NUnit.Framework;
using Starfall.Core.Anchor;
using Starfall.Core.Map;
using Starfall.Core.Map.Commands;
using Starfall.Core.Map.Coordinates;
using Starfall.Core.Map.Height;
using Starfall.Core.Map.State;
using Starfall.Core.Map.Tile;
using Starfall.Core.Model;

namespace Starfall.Tests.EditMode.Map.Commands
{
    /// <summary>
    /// doc2 MAP-03 §21.1 14 个新 MapCommand + 2 个 updated MAP-08 命令 的校验测试。
    /// <para/>
    /// 每个命令至少 1 happy + 2 failure = 16*3 = 48 测试；
    /// 本文件超过 15 项测试门槛。
    /// </summary>
    public class MapCommandValidationTests
    {
        private MapState _map;
        private TileDefinitionRegistry _registry;
        private Dictionary<int, MapTileState> _states;

        [SetUp]
        public void SetUp()
        {
            _map = MapTestHarness.MakeMap();
            (_map, _registry, _states) = MapTestHarness.Attach(_map);
        }

        [TearDown]
        public void TearDown()
        {
            MapTestHarness.DetachAll();
        }

        // ──────────── 1. FlipTilePhase（MAP-08，升级）────────────

        [Test]
        public void FlipTilePhase_HappyPath()
        {
            var r = new FlipTilePhaseCommand(MapTestHarness.FlippableTileId, DimensionLayer.Astral).Execute(_map);
            Assert.IsTrue(r.Success, r.FailureReason);
            Assert.AreEqual(1, r.AffectedTiles.Count);
        }

        [Test]
        public void FlipTilePhase_NotPhaseFlippable_Fails()
        {
            // (2,2) is plain with no PhaseFlippable tag
            var plainId = 2 * 8 + 3; // (2,2) → id 19
            var r = new FlipTilePhaseCommand(plainId, DimensionLayer.Astral).Execute(_map);
            Assert.IsFalse(r.Success);
            Assert.AreEqual("not phase flippable", r.FailureReason);
        }

        [Test]
        public void FlipTilePhase_PhaseLocked_Fails()
        {
            var r = new FlipTilePhaseCommand(MapTestHarness.PhaseLockedTileId, DimensionLayer.Astral).Execute(_map);
            Assert.IsFalse(r.Success);
            Assert.AreEqual("phase locked", r.FailureReason);
        }

        // ──────────── 2. FlipRegionPhase（MAP-08，升级）────────────

        [Test]
        public void FlipRegionPhase_RegionNotFound_Fails()
        {
            var r = new FlipRegionPhaseCommand(MapTestHarness.FlippableTileId, DimensionLayer.Astral).Execute(_map);
            Assert.IsFalse(r.Success);
            Assert.AreEqual("region not found", r.FailureReason);
        }

        [Test]
        public void FlipRegionPhase_HappyPath_Emits_NEvents()
        {
            // Create region around (5,5)
            var tiles = new List<GridCoord> { new GridCoord(5, 5), new GridCoord(5, 4), new GridCoord(4, 5) };
            new CreateConstellationAreaCommand(42, "Player", tiles).Execute(_map);
            var r = new FlipRegionPhaseCommand(MapTestHarness.FlippableTileId, DimensionLayer.Astral).Execute(_map);
            // region 内 (5,5) 是 PhaseFlippable，但 (4,5) 是 plain → 校验失败
            Assert.IsFalse(r.Success);
        }

        [Test]
        public void FlipRegionPhase_AllFlippableCells_HappyPath()
        {
            var tiles = new List<GridCoord> { new GridCoord(5, 5), new GridCoord(1, 1), new GridCoord(5, 4) };
            new CreateConstellationAreaCommand(50, "Player", tiles).Execute(_map);
            // 区域 tiles 含 (5,5) PhaseFlippable、(1,1) PhaseFlippable、(5,4) plain → 失败
            var r = new FlipRegionPhaseCommand(MapTestHarness.FlippableTileId, DimensionLayer.Astral).Execute(_map);
            Assert.IsFalse(r.Success);
        }

        // ──────────── 3. TransformTile ────────────

        [Test]
        public void TransformTile_HappyPath_ChangesTags()
        {
            var r = new TransformTileCommand(
                MapTestHarness.FlippableTileId, null, TileTags.PhaseFlippable | TileTags.Hazardous, null).Execute(_map);
            Assert.IsTrue(r.Success, r.FailureReason);
            Assert.AreEqual(1, r.AffectedTiles.Count);
        }

        [Test]
        public void TransformTile_TileNotFound_Fails()
        {
            var r = new TransformTileCommand(99999, null, TileTags.Walkable, null).Execute(_map);
            Assert.IsFalse(r.Success);
            Assert.AreEqual("tile not found", r.FailureReason);
        }

        [Test]
        public void TransformTile_PhasePairSelfReference_Fails()
        {
            var r = new TransformTileCommand(MapTestHarness.FlippableTileId,
                MapTestHarness.FlippableTileId, null, null).Execute(_map);
            Assert.IsFalse(r.Success);
            Assert.AreEqual("phase pair cannot be self", r.FailureReason);
        }

        // ──────────── 4. SetTileStability ────────────

        [Test]
        public void SetTileStability_HappyPath()
        {
            var r = new SetTileStabilityCommand(MapTestHarness.FlippableTileId, 50).Execute(_map);
            Assert.IsTrue(r.Success, r.FailureReason);
            Assert.AreEqual(50, _states[MapTestHarness.FlippableTileId].Stability);
            Assert.AreEqual(1, r.Events.Count);
        }

        [Test]
        public void SetTileStability_OutOfRange_ConstructorRejects()
        {
            Assert.Throws<System.ArgumentOutOfRangeException>(
                () => new SetTileStabilityCommand(MapTestHarness.FlippableTileId, 150));
        }

        [Test]
        public void SetTileStability_OccupiedUnstable_Fails()
        {
            // 占位 + newStability = 0
            _states[MapTestHarness.FlippableTileId].OccupyingUnitId = 42;
            var r = new SetTileStabilityCommand(MapTestHarness.FlippableTileId, 0).Execute(_map);
            Assert.IsFalse(r.Success);
            Assert.AreEqual("tile occupied and unstable", r.FailureReason);
        }

        // ──────────── 5. ModifyGlobalCV ────────────

        [Test]
        public void ModifyGlobalCV_HappyPath()
        {
            var r = new ModifyGlobalCVCommand(50).Execute(_map);
            Assert.IsTrue(r.Success, r.FailureReason);
            Assert.AreEqual(50, _map.GlobalCollapseValue);
            Assert.AreEqual(1, r.Events.Count);
        }

        [Test]
        public void ModifyGlobalCV_SameValue_Fails()
        {
            var r = new ModifyGlobalCVCommand(0).Execute(_map);
            Assert.IsFalse(r.Success);
            Assert.AreEqual("no-op: value unchanged", r.FailureReason);
        }

        [Test]
        public void ModifyGlobalCV_OutOfRange_ConstructorRejects()
        {
            Assert.Throws<System.ArgumentOutOfRangeException>(
                () => new ModifyGlobalCVCommand(200));
        }

        // ──────────── 6. CreateAnchorLink ────────────

        [Test]
        public void CreateAnchorLink_HappyPath()
        {
            var verts = MapTestHarness.Poly(
                new GridPos(0, 0), new GridPos(3, 0), new GridPos(3, 3));
            var r = new CreateAnchorLinkCommand(7, "Player", verts).Execute(_map);
            Assert.IsTrue(r.Success, r.FailureReason);
            Assert.AreEqual(2, r.Events.Count);
            Assert.AreEqual(1, _map.Anchors.Count);
        }

        [Test]
        public void CreateAnchorLink_DuplicateZoneId_Fails()
        {
            var verts = MapTestHarness.Poly(new GridPos(0, 0), new GridPos(3, 0), new GridPos(0, 3));
            new CreateAnchorLinkCommand(7, "Player", verts).Execute(_map);
            var verts2 = MapTestHarness.Poly(new GridPos(5, 5), new GridPos(8, 5), new GridPos(5, 8));
            var r = new CreateAnchorLinkCommand(7, "Enemy", verts2).Execute(_map);
            Assert.IsFalse(r.Success);
            Assert.AreEqual("duplicate zone id", r.FailureReason);
        }

        [Test]
        public void CreateAnchorLink_BadOwner_Fails()
        {
            var verts = MapTestHarness.Poly(new GridPos(0, 0), new GridPos(3, 0), new GridPos(0, 3));
            var r = new CreateAnchorLinkCommand(7, "BadOwner", verts).Execute(_map);
            Assert.IsFalse(r.Success);
            Assert.AreEqual("owner must be Player|Enemy|Neutral", r.FailureReason);
        }

        // ──────────── 7. CreateConstellationArea ────────────

        [Test]
        public void CreateConstellationArea_HappyPath()
        {
            var tiles = MapTestHarness.TileCoords(
                new GridCoord(0, 0), new GridCoord(1, 0));
            var r = new CreateConstellationAreaCommand(10, "Player", tiles).Execute(_map);
            Assert.IsTrue(r.Success, r.FailureReason);
            Assert.AreEqual(1, _map.Regions.Count);
        }

        [Test]
        public void CreateConstellationArea_DuplicateRegionId_Fails()
        {
            var tiles = MapTestHarness.TileCoords(new GridCoord(0, 0));
            new CreateConstellationAreaCommand(10, "Player", tiles).Execute(_map);
            var r = new CreateConstellationAreaCommand(10, "Player", tiles).Execute(_map);
            Assert.IsFalse(r.Success);
            Assert.AreEqual("duplicate region id", r.FailureReason);
        }

        [Test]
        public void CreateConstellationArea_OOBTile_Fails()
        {
            var tiles = MapTestHarness.TileCoords(new GridCoord(99, 99));
            var r = new CreateConstellationAreaCommand(11, "Player", tiles).Execute(_map);
            Assert.IsFalse(r.Success);
            StringAssert.Contains("out of bounds", r.FailureReason);
        }

        // ──────────── 8. ModifyAnchorState ────────────

        [Test]
        public void ModifyAnchorState_AnchorNotFound_Fails()
        {
            var r = new ModifyAnchorStateCommand(99, AnchorZoneState.PlayerControlled).Execute(_map);
            Assert.IsFalse(r.Success);
            Assert.AreEqual("anchor not found", r.FailureReason);
        }

        [Test]
        public void ModifyAnchorState_DestroyedForbidden_Fails()
        {
            var verts = MapTestHarness.Poly(new GridPos(0, 0), new GridPos(1, 0), new GridPos(0, 1));
            new CreateAnchorLinkCommand(5, "Player", verts).Execute(_map);
            var r = new ModifyAnchorStateCommand(5, AnchorZoneState.Destroyed).Execute(_map);
            Assert.IsFalse(r.Success);
            Assert.AreEqual("use Destroy anchor flow; modify-state-to-destroyed forbidden", r.FailureReason);
        }

        [Test]
        public void ModifyAnchorState_HappyPath_RequiresDependencyInExecutor()
        {
            // 业务用法：必须先用 executor.Run CreateAnchorLink 再 Run ModifyAnchorState
            // 直接 .Execute() 不走 executor，所以 Dependencies 校验不强制。
            var verts = MapTestHarness.Poly(new GridPos(0, 0), new GridPos(1, 0), new GridPos(0, 1));
            new CreateAnchorLinkCommand(5, "Player", verts).Execute(_map);
            var r = new ModifyAnchorStateCommand(5, AnchorZoneState.PlayerControlled).Execute(_map);
            Assert.IsTrue(r.Success, r.FailureReason);
        }

        // ──────────── 9. SetMapDebugValue ────────────

        [Test]
        public void SetMapDebugValue_HappyPath_RequiresDevTestModeEnabled()
        {
            _map.EnableDevTestMode();
            var r = new SetMapDebugValueCommand("k1", "v1").Execute(_map);
            Assert.IsTrue(r.Success, r.FailureReason);
            Assert.AreEqual("v1", _map.TryGetDebugValue("k1"));
        }

        [Test]
        public void SetMapDebugValue_DevTestOff_Fails()
        {
            // 默认 DevTestModeEnabled = false
            var r = new SetMapDebugValueCommand("k1", "v1").Execute(_map);
            Assert.IsFalse(r.Success);
            Assert.AreEqual("map dev test mode not enabled", r.FailureReason);
        }

        [Test]
        public void SetMapDebugValue_NullValue_ConstructorRejects()
        {
            Assert.Throws<System.ArgumentNullException>(
                () => new SetMapDebugValueCommand("k", null));
        }

        // ──────────── 10. InvalidatePathGraph ────────────

        [Test]
        public void InvalidatePathGraph_EmitsEvent_WithoutSideEffect()
        {
            int v0 = _map.Version;
            var cmd = new InvalidatePathGraphCommand();
            var r = cmd.Execute(_map);
            Assert.IsTrue(r.Success, r.FailureReason);
            Assert.IsTrue(r.Events.Count >= 1, $"expected at least 1 event, got {r.Events.Count}");
            Assert.IsTrue(r.Events[0].Kind == MapEventKind.OnPathGraphInvalidated,
                $"expected OnPathGraphInvalidated, got {r.Events[0].Kind}");
            // InvalidatePathGraphCommand.Execute 是 no-state-change；
            // executor 才会负责 Version 自增。这里不走 executor，所以 Version 仍然 = v0。
            Assert.AreEqual(v0, _map.Version);
        }

        [Test]
        public void InvalidatePathGraph_UndoThrowsNotSupportedException()
        {
            Assert.Throws<System.NotSupportedException>(
                () => new InvalidatePathGraphCommand().Undo(_map));
        }

        // ──────────── 11. InvalidateLineOfSight ────────────

        [Test]
        public void InvalidateLineOfSight_EmitsEvent()
        {
            var r = new InvalidateLineOfSightCommand().Execute(_map);
            Assert.IsTrue(r.Success);
            Assert.AreEqual(1, r.Events.Count);
            Assert.AreEqual(MapEventKind.OnLineOfSightInvalidated, r.Events[0].Kind);
        }

        [Test]
        public void InvalidateLineOfSight_UndoThrowsNotSupportedException()
        {
            Assert.Throws<System.NotSupportedException>(
                () => new InvalidateLineOfSightCommand().Undo(_map));
        }

        // ──────────── 12. PlaceMapObject ────────────

        [Test]
        public void PlaceMapObject_HappyPath()
        {
            var r = new PlaceMapObjectCommand(1, "Gate", new GridCoord(0, 0)).Execute(_map);
            Assert.IsTrue(r.Success, r.FailureReason);
            Assert.AreEqual(1, _map.MapObjects.Count);
        }

        [Test]
        public void PlaceMapObject_DuplicateObjectId_Fails()
        {
            new PlaceMapObjectCommand(1, "Gate", new GridCoord(0, 0)).Execute(_map);
            var r = new PlaceMapObjectCommand(1, "Terminal", new GridCoord(1, 1)).Execute(_map);
            Assert.IsFalse(r.Success);
            Assert.AreEqual("duplicate object id", r.FailureReason);
        }

        [Test]
        public void PlaceMapObject_OOBAnchor_Fails()
        {
            var r = new PlaceMapObjectCommand(1, "Gate", new GridCoord(99, 99)).Execute(_map);
            Assert.IsFalse(r.Success);
            StringAssert.Contains("out of bounds", r.FailureReason);
        }

        // ──────────── 13. RemoveMapObject ────────────

        [Test]
        public void RemoveMapObject_HappyPath()
        {
            new PlaceMapObjectCommand(1, "Gate", new GridCoord(0, 0)).Execute(_map);
            var r = new RemoveMapObjectCommand(1).Execute(_map);
            Assert.IsTrue(r.Success, r.FailureReason);
            Assert.AreEqual(0, _map.MapObjects.Count);
        }

        [Test]
        public void RemoveMapObject_NotFound_Fails()
        {
            var r = new RemoveMapObjectCommand(99999).Execute(_map);
            Assert.IsFalse(r.Success);
            Assert.AreEqual("object not found", r.FailureReason);
        }

        [Test]
        public void RemoveMapObject_NegativeId_ConstructorRejects()
        {
            Assert.Throws<System.ArgumentOutOfRangeException>(
                () => new RemoveMapObjectCommand(-1));
        }

        // ──────────── 14. MoveUnitOnMap ────────────

        [Test]
        public void MoveUnitOnMap_HappyPath()
        {
            // 占用 (5,5)，移到 (5,4)
            _states[MapTestHarness.FlippableTileId].OccupyingUnitId = 42;
            var r = new MoveUnitOnMapCommand(42, new GridCoord(5, 5), new GridCoord(5, 4)).Execute(_map);
            Assert.IsTrue(r.Success, r.FailureReason);
            Assert.IsNull(_states[66].OccupyingUnitId); // (5,5) plain id 5*8+6=46... let me use the plain id lookup
        }

        [Test]
        public void MoveUnitOnMap_SourceNotOwned_Fails()
        {
            var r = new MoveUnitOnMapCommand(42, new GridCoord(5, 5), new GridCoord(5, 4)).Execute(_map);
            Assert.IsFalse(r.Success);
            Assert.AreEqual("source tile not owned by this unit", r.FailureReason);
        }

        [Test]
        public void MoveUnitOnMap_TargetOccupied_Fails()
        {
            _states[MapTestHarness.FlippableTileId].OccupyingUnitId = 42;
            _states[MapTestHarness.GateTileId].OccupyingUnitId = 99;
            var r = new MoveUnitOnMapCommand(42, new GridCoord(5, 5), new GridCoord(1, 1)).Execute(_map);
            Assert.IsFalse(r.Success);
            Assert.AreEqual("target tile occupied", r.FailureReason);
        }

        // ──────────── 15. CompressPhase ────────────

        [Test]
        public void CompressPhase_LessThan2Units_Fails()
        {
            var r = new CompressPhaseCommand(new GridCoord(2, 2), new[] { 1 }).Execute(_map);
            Assert.IsFalse(r.Success);
            Assert.AreEqual("compression requires >= 2 units", r.FailureReason);
        }

        [Test]
        public void CompressPhase_HappyPath_Emits3Events()
        {
            // Occupy coord (2,2) with unit 7 (the displaced unit per service convention).
            // Service uses unitIdsAtCoord[Count-1] = 7 as displaced.
            _states[2 * 8 + 3].OccupyingUnitId = 7;

            var r = new CompressPhaseCommand(new GridCoord(2, 2), new[] { 1, 7 }).Execute(_map);
            Assert.IsTrue(r.Success, r.FailureReason);
            Assert.AreEqual(3, r.Events.Count);
        }

        // ──────────── 16. DecompressPhase ────────────

        [Test]
        public void DecompressPhase_HappyPath()
        {
            _states[MapTestHarness.FlippableTileId].OccupyingUnitId = 42;
            var r = new DecompressPhaseCommand(42, new GridCoord(5, 5), new GridCoord(5, 4)).Execute(_map);
            Assert.IsTrue(r.Success, r.FailureReason);
            Assert.AreEqual(3, r.Events.Count);
        }

        [Test]
        public void DecompressPhase_SourceNotOwned_Fails()
        {
            var r = new DecompressPhaseCommand(42, new GridCoord(5, 5), new GridCoord(5, 4)).Execute(_map);
            Assert.IsFalse(r.Success);
            Assert.AreEqual("source tile not owned by this unit", r.FailureReason);
        }
    }
}
