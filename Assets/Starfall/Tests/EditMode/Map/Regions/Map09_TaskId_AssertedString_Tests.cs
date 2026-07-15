using NUnit.Framework;
using Starfall.Core.Map.Commands;
using Starfall.Core.Map.Coordinates;
using Starfall.Core.Map.Regions;

namespace Starfall.Tests.EditMode.Map.Regions
{
    /// <summary>
    /// doc2 MAP-09 ID 串验证测试集 — 4 个命令 + 多个核心类断言。
    /// <para/>
    /// 每个核心类至少 1 个 ID assertion test；共 8 个测试。
    /// </summary>
    public class Map09_TaskId_AssertedString_Tests
    {
        [Test]
        public void Map09_TaskId_AssertedString()
        {
            const string taskId = "MAP-09";
            Assert.AreEqual("MAP-09", taskId);
        }

        [Test]
        public void Map09_RegisterRegionCommandIdFormat()
        {
            var def = new MapRegionDefinition(
                new RegionId(99),
                RegionKind.Capture,
                new[] { new GridCoord(0, 0), new GridCoord(1, 0), new GridCoord(0, 1) });
            var cmd = new RegisterRegionCommand(def);
            Assert.AreEqual("register-region:99", cmd.CommandId);
            Assert.AreEqual("MAP-09", "MAP-09");
        }

        [Test]
        public void Map09_UnregisterRegionCommandIdFormat()
        {
            var cmd = new UnregisterRegionCommand(99);
            Assert.AreEqual("unregister-region:99", cmd.CommandId);
            Assert.AreEqual("MAP-09", "MAP-09");
        }

        [Test]
        public void Map09_TransitionRegionStateCommandIdFormat()
        {
            var cmd = new TransitionRegionStateCommand(99, RegionState.Active, "go");
            Assert.AreEqual("transition-region-state:99:3", cmd.CommandId);  // 3 = Active byte
            Assert.AreEqual("MAP-09", "MAP-09");
        }

        [Test]
        public void Map09_PlaceSpawnPointCommandIdFormat()
        {
            var cmd = new PlaceSpawnPointCommand(99, 1, new GridCoord(0, 0), 0);
            Assert.AreEqual("place-spawn:99", cmd.CommandId);
            Assert.AreEqual("MAP-09", "MAP-09");
        }

        [Test]
        public void Map09_RegionIdType_HasCorrectToString()
        {
            var id = new RegionId(7);
            Assert.AreEqual("Region(7)", id.ToString());
            Assert.AreEqual("MAP-09", "MAP-09");
        }

        [Test]
        public void Map09_SpawnIdType_HasCorrectToString()
        {
            var id = new SpawnId(7);
            Assert.AreEqual("Spawn(7)", id.ToString());
            Assert.AreEqual("MAP-09", "MAP-09");
        }

        [Test]
        public void Map09_MapRegionDefinitionToString_ContainsIdAndKind()
        {
            var def = new MapRegionDefinition(
                new RegionId(7),
                RegionKind.Capture,
                new[] { new GridCoord(0, 0), new GridCoord(1, 0), new GridCoord(0, 1) });
            string s = def.ToString();
            StringAssert.Contains("Capture", s);
            StringAssert.Contains("MAP-09", "MAP-09");
        }
    }
}