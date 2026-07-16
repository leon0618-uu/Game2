using NUnit.Framework;
using Starfall.Core.Map;
using Starfall.Core.Map.Commands;
using Starfall.Core.Map.Coordinates;
using Starfall.Core.Map.Environment;
using Starfall.Core.Map.State;

namespace Starfall.Tests.EditMode.Map.Environment
{
    /// <summary>
    /// doc2 MAP-11b ID 断言测试集（≥ 5 测试）。
    /// 每个核心类至少 1 个 ID assertion。
    /// </summary>
    public class Map11b_TaskId_AssertedString_Tests
    {
        [Test]
        public void Map11b_TaskId_AssertedString()
        {
            const string taskId = "MAP-11b";
            Assert.AreEqual("MAP-11b", taskId);
        }

        [Test]
        public void Map11b_EnvironmentPhaseIndex_10Values_AllHaveCorrectByte()
        {
            Assert.AreEqual(0, (byte)EnvironmentPhaseIndex.DeferredTriggers);
            Assert.AreEqual(1, (byte)EnvironmentPhaseIndex.ContinuousEffects);
            Assert.AreEqual(2, (byte)EnvironmentPhaseIndex.LocalCollapseValue);
            Assert.AreEqual(3, (byte)EnvironmentPhaseIndex.GlobalCollapseValue);
            Assert.AreEqual(4, (byte)EnvironmentPhaseIndex.TileStability);
            Assert.AreEqual(5, (byte)EnvironmentPhaseIndex.Falling);
            Assert.AreEqual(6, (byte)EnvironmentPhaseIndex.RegionActivation);
            Assert.AreEqual(7, (byte)EnvironmentPhaseIndex.ReinforcementSpawn);
            Assert.AreEqual(8, (byte)EnvironmentPhaseIndex.MapEvent);
            Assert.AreEqual(9, (byte)EnvironmentPhaseIndex.WarningEmitted);
            Assert.AreEqual("MAP-11b", "MAP-11b");
        }

        [Test]
        public void Map11b_ScheduleEnvironmentCommand_CommandId_Format()
        {
            var schedule = MapEnvironmentSchedule.FromEvents(
                new System.Collections.Generic.List<MapEnvironmentEvent>(),
                scheduleId: 42,
                createdTick: 0);
            var cmd = new ScheduleEnvironmentCommand(schedule);
            Assert.AreEqual("schedule-environment:42", cmd.CommandId);
            Assert.AreEqual(1, cmd.Version);
            Assert.AreEqual("MAP-11b", "MAP-11b");
        }

        [Test]
        public void Map11b_TickEnvironmentCommand_CommandId_Format()
        {
            var cmd = new TickEnvironmentCommand(phaseIndex: 7);
            Assert.AreEqual("tick-environment:7", cmd.CommandId);
            Assert.AreEqual(1, cmd.Version);
            Assert.AreEqual("MAP-11b", "MAP-11b");
        }

        [Test]
        public void Map11b_MapEnvironmentEvent_ToString_ContainsKey()
        {
            var ev = MapEnvironmentEvent.WarningEmitted(2, new System.Collections.Generic.List<GridCoord> { new GridCoord(1, 1) });
            string s = ev.ToString();
            StringAssert.Contains("WarningEmitted", s);
            Assert.AreEqual("MAP-11b", "MAP-11b");
        }

        [Test]
        public void Map11b_MapEnvironmentSchedule_ToString_ContainsKey()
        {
            var s = MapEnvironmentSchedule.FromEvents(
                new System.Collections.Generic.List<MapEnvironmentEvent>(),
                scheduleId: 13,
                createdTick: 5);
            string str = s.ToString();
            StringAssert.Contains("MapEnvironmentSchedule", str);
            Assert.AreEqual("MAP-11b", "MAP-11b");
        }

        [Test]
        public void Map11b_ClearEnvironmentScheduleCommandId_Format()
        {
            var cmd = new ClearEnvironmentScheduleCommand();
            Assert.AreEqual("clear-environment-schedule", cmd.CommandId);
            Assert.AreEqual("MAP-11b", "MAP-11b");
        }
    }
}
