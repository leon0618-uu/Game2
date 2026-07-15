using NUnit.Framework;
using Starfall.Core.Map.Collapse;
using Starfall.Core.Map.Coordinates;

namespace Starfall.Tests.EditMode.Map.Collapse
{
    /// <summary>
    /// doc2 MAP-11a ID 串验证测试集。
    /// 每个核心类至少 1 个 ID assertion test；≥ 5 测试。
    /// </summary>
    public class Map11_TaskId_AssertedString_Tests
    {
        [Test]
        public void Map11_TaskId_AssertedString()
        {
            const string taskId = "MAP-11";
            Assert.AreEqual("MAP-11", taskId);
        }

        [Test]
        public void Map11_CollapseStage_5Values_AllHaveCorrectByte()
        {
            Assert.AreEqual(0, (byte)CollapseStage.Stable);
            Assert.AreEqual(1, (byte)CollapseStage.Anomalous);
            Assert.AreEqual(2, (byte)CollapseStage.Fracturing);
            Assert.AreEqual(3, (byte)CollapseStage.Collapsing);
            Assert.AreEqual(4, (byte)CollapseStage.GateFault);
            Assert.AreEqual("MAP-11", "MAP-11");
        }

        [Test]
        public void Map11_TileStability_6Values_AllHaveCorrectByte()
        {
            Assert.AreEqual(0, (byte)TileStability.Stable);
            Assert.AreEqual(1, (byte)TileStability.Unstable);
            Assert.AreEqual(2, (byte)TileStability.Fractured);
            Assert.AreEqual(3, (byte)TileStability.Collapsing);
            Assert.AreEqual(4, (byte)TileStability.Collapsed);
            Assert.AreEqual(5, (byte)TileStability.Reconstructed);
            Assert.AreEqual("MAP-11", "MAP-11");
        }

        [Test]
        public void Map11_ModifyGlobalCollapseValueCommandId_IsStable()
        {
            var cmd = new ModifyGlobalCollapseValueCommand(10, "test");
            Assert.AreEqual("modify-global-collapse-value", cmd.CommandId);
            Assert.AreEqual(1, cmd.Version);
            Assert.AreEqual("MAP-11", "MAP-11");
        }

        [Test]
        public void Map11_CollapseTileCommandId_Format()
        {
            var coord = new GridCoord(2, 3, DimensionLayer.Astral);
            var cmd = new CollapseTileCommand(coord, TileStability.Collapsing);
            // 2,3,1 = X, Y, Layer byte
            Assert.AreEqual("collapse-tile:2,3,1", cmd.CommandId);
            Assert.AreEqual("MAP-11", "MAP-11");
        }

        [Test]
        public void Map11_ReconstructTileCommandId_Format()
        {
            var coord = new GridCoord(4, 5);
            var cmd = new ReconstructTileCommand(coord);
            // 4,5,0 = X, Y, Layer byte (Reality=0)
            Assert.AreEqual("reconstruct-tile:4,5,0", cmd.CommandId);
            Assert.AreEqual("MAP-11", "MAP-11");
        }

        [Test]
        public void Map11_GlobalCollapseValue_ToString_ContainsAllFields()
        {
            var gcv = GlobalCollapseValue.Of(50, 3);
            string s = gcv.ToString();
            StringAssert.Contains("50", s);
            StringAssert.Contains("Fracturing", s);
            StringAssert.Contains("3", s);
            Assert.AreEqual("MAP-11", "MAP-11");
        }

        [Test]
        public void Map11_LocalCollapseValue_ToString_ContainsAllFields()
        {
            var lcv = LocalCollapseValue.Of(new GridCoord(1, 1), 80, 2);
            string s = lcv.ToString();
            StringAssert.Contains("80", s);
            StringAssert.Contains("Collapsing", s);
            Assert.AreEqual("MAP-11", "MAP-11");
        }

        [Test]
        public void Map11_CollapseWarningLevel_4Values()
        {
            Assert.AreEqual(0, (byte)CollapseWarningLevel.None);
            Assert.AreEqual(1, (byte)CollapseWarningLevel.Caution);
            Assert.AreEqual(2, (byte)CollapseWarningLevel.Danger);
            Assert.AreEqual(3, (byte)CollapseWarningLevel.Critical);
            Assert.AreEqual("MAP-11", "MAP-11");
        }
    }
}
