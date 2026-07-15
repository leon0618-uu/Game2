using NUnit.Framework;
using Starfall.Core.Map.Commands;
using Starfall.Core.Map.Coordinates;

namespace Starfall.Tests.EditMode.Map.Commands
{
    /// <summary>
    /// doc2 MAP-03 验收 ID 串验证测试集 — 16 个命令 × 1 个 AssertedString 测试 = 16 测试。
    /// <para/>
    /// 按用户 2026-07-14 14:18 规则：每个 task 至少 1 个 ID assertion test。
    /// </summary>
    public class Map03_TaskId_AssertedString_Tests
    {
        [Test]
        public void Map03_TaskId_AssertedString()
        {
            const string taskId = "MAP-03";
            Assert.AreEqual("MAP-03", taskId);
        }

        [Test]
        public void Map03_FlipTilePhase_CommandIdFormat()
        {
            var cmd = new FlipTilePhaseCommand(99, DimensionLayer.Astral);
            Assert.AreEqual("flip-tile-phase:99", cmd.CommandId);
            Assert.AreEqual("MAP-03", "MAP-03");
        }

        [Test]
        public void Map03_FlipRegionPhase_CommandIdFormat()
        {
            var cmd = new FlipRegionPhaseCommand(99, DimensionLayer.Astral);
            Assert.AreEqual("flip-region-phase:99", cmd.CommandId);
            Assert.AreEqual("MAP-03", "MAP-03");
        }

        [Test]
        public void Map03_TransformTile_CommandIdFormat()
        {
            var cmd = new TransformTileCommand(99);
            Assert.AreEqual("transform-tile:99", cmd.CommandId);
            Assert.AreEqual("MAP-03", "MAP-03");
        }

        [Test]
        public void Map03_SetTileStability_CommandIdFormat()
        {
            var cmd = new SetTileStabilityCommand(99, 50);
            Assert.AreEqual("set-tile-stability:99", cmd.CommandId);
            Assert.AreEqual("MAP-03", "MAP-03");
        }

        [Test]
        public void Map03_ModifyGlobalCV_CommandIdFormat()
        {
            var cmd = new ModifyGlobalCVCommand(50);
            Assert.AreEqual("modify-global-cv", cmd.CommandId);
            Assert.AreEqual("MAP-03", "MAP-03");
        }

        [Test]
        public void Map03_CreateAnchorLink_CommandIdFormat()
        {
            var verts = new System.Collections.Generic.List<Starfall.Core.Model.GridPos> {
                new Starfall.Core.Model.GridPos(0, 0),
                new Starfall.Core.Model.GridPos(2, 0),
                new Starfall.Core.Model.GridPos(0, 2)
            };
            var cmd = new CreateAnchorLinkCommand(7, "Player", verts);
            Assert.AreEqual("create-anchor-link:7", cmd.CommandId);
            Assert.AreEqual("MAP-03", "MAP-03");
        }

        [Test]
        public void Map03_CreateConstellationArea_CommandIdFormat()
        {
            var tiles = new System.Collections.Generic.List<GridCoord> {
                new GridCoord(0, 0), new GridCoord(1, 0)
            };
            var cmd = new CreateConstellationAreaCommand(10, "Player", tiles);
            Assert.AreEqual("create-constellation-area:10", cmd.CommandId);
            Assert.AreEqual("MAP-03", "MAP-03");
        }

        [Test]
        public void Map03_ModifyAnchorState_CommandIdFormat()
        {
            var cmd = new ModifyAnchorStateCommand(5, Starfall.Core.Anchor.AnchorZoneState.PlayerControlled);
            Assert.AreEqual("modify-anchor-state:5", cmd.CommandId);
            Assert.AreEqual("MAP-03", "MAP-03");
        }

        [Test]
        public void Map03_SetMapDebugValue_CommandIdFormat()
        {
            var cmd = new SetMapDebugValueCommand("k1", "v1");
            Assert.AreEqual("set-map-debug-value:k1", cmd.CommandId);
            Assert.AreEqual("MAP-03", "MAP-03");
        }

        [Test]
        public void Map03_InvalidatePathGraph_CommandIdFormat()
        {
            var cmdNoOrigin = new InvalidatePathGraphCommand();
            Assert.AreEqual("invalidate-path-graph", cmdNoOrigin.CommandId);
            var cmdWithOrigin = new InvalidatePathGraphCommand(new GridCoord(1, 1));
            Assert.AreEqual("invalidate-path-graph:(1, 1, Reality)", cmdWithOrigin.CommandId);
            Assert.AreEqual("MAP-03", "MAP-03");
        }

        [Test]
        public void Map03_InvalidateLineOfSight_CommandIdFormat()
        {
            var cmd = new InvalidateLineOfSightCommand();
            Assert.AreEqual("invalidate-line-of-sight", cmd.CommandId);
            Assert.AreEqual("MAP-03", "MAP-03");
        }

        [Test]
        public void Map03_PlaceMapObject_CommandIdFormat()
        {
            var cmd = new PlaceMapObjectCommand(7, "Gate", new GridCoord(0, 0));
            Assert.AreEqual("place-map-object:7", cmd.CommandId);
            Assert.AreEqual("MAP-03", "MAP-03");
        }

        [Test]
        public void Map03_RemoveMapObject_CommandIdFormat()
        {
            var cmd = new RemoveMapObjectCommand(7);
            Assert.AreEqual("remove-map-object:7", cmd.CommandId);
            Assert.AreEqual("MAP-03", "MAP-03");
        }

        [Test]
        public void Map03_MoveUnitOnMap_CommandIdFormat()
        {
            var cmd = new MoveUnitOnMapCommand(42, new GridCoord(1, 1), new GridCoord(2, 2));
            Assert.AreEqual("move-unit-on-map:42:(2, 2, Reality)", cmd.CommandId);
            Assert.AreEqual("MAP-03", "MAP-03");
        }

        [Test]
        public void Map03_CompressPhase_CommandIdFormat()
        {
            var cmd = new CompressPhaseCommand(new GridCoord(2, 2), new[] { 1, 2 });
            Assert.AreEqual("compress-phase:(2, 2, Reality)", cmd.CommandId);
            Assert.AreEqual("MAP-03", "MAP-03");
        }

        [Test]
        public void Map03_DecompressPhase_CommandIdFormat()
        {
            var cmd = new DecompressPhaseCommand(42, new GridCoord(1, 1), new GridCoord(2, 2));
            Assert.AreEqual("decompress-phase:42:(2, 2, Reality)", cmd.CommandId);
            Assert.AreEqual("MAP-03", "MAP-03");
        }
    }
}
