using System.Collections.Generic;
using NUnit.Framework;
using Starfall.Core.Map;
using Starfall.Core.Map.Commands;
using Starfall.Core.Map.Coordinates;
using Starfall.Core.Map.Environment;
using Starfall.Core.Map.State;

namespace Starfall.Tests.EditMode.Map.Commands
{
    /// <summary>
    /// doc2 MAP-11b <see cref="ClearEnvironmentScheduleCommand"/> 测试集（≥ 4 测试）。
    /// 覆盖：happy path / Undo / version 自增 / CommandId。
    /// </summary>
    public class ClearEnvironmentScheduleCommandTests
    {
        private static MapState MakeMap()
        {
            return new MapState(new MapDefinition("map.test", 8, 8,
                DimensionLayer.Reality, 0));
        }

        // ──────────── 1) Happy path ────────────

        [Test]
        public void Execute_ClearsActiveSchedule_AndPendingEvents()
        {
            var map = MakeMap();
            // 准备 schedule + pending events
            map.SetActiveSchedule(MapEnvironmentSchedule.FromEvents(
                new List<MapEnvironmentEvent>
                {
                    MapEnvironmentEvent.LocalDamage(new GridCoord(1, 1), 5),
                },
                scheduleId: 7,
                createdTick: 3));
            map.AddPendingEvent(MapEnvironmentEvent.DeferredTrigger(new GridCoord(2, 2), 0));
            map.AddPendingEvent(MapEnvironmentEvent.DeferredTrigger(new GridCoord(3, 3), 0));

            var cmd = new ClearEnvironmentScheduleCommand();
            var result = cmd.Execute(map);
            Assert.IsTrue(result.Success);
            Assert.AreEqual(0, map.ActiveSchedule.Count);
            Assert.AreEqual(0, map.PendingEvents.Count);
        }

        // ──────────── 2) Undo 恢复 ────────────

        [Test]
        public void Undo_RestoresActiveSchedule_AndPendingEvents()
        {
            var map = MakeMap();
            map.SetActiveSchedule(MapEnvironmentSchedule.FromEvents(
                new List<MapEnvironmentEvent>
                {
                    MapEnvironmentEvent.LocalDamage(new GridCoord(1, 1), 5),
                },
                scheduleId: 7,
                createdTick: 3));
            map.AddPendingEvent(MapEnvironmentEvent.DeferredTrigger(new GridCoord(2, 2), 0));

            var oldSchedule = map.ActiveSchedule;
            var oldPendingCount = map.PendingEvents.Count;

            var cmd = new ClearEnvironmentScheduleCommand();
            cmd.Execute(map);
            Assert.AreEqual(0, map.ActiveSchedule.Count);
            Assert.AreEqual(0, map.PendingEvents.Count);

            cmd.Undo(map);
            Assert.AreEqual(oldSchedule, map.ActiveSchedule);
            Assert.AreEqual(oldPendingCount, map.PendingEvents.Count);
        }

        // ──────────── 3) Version 自增 ────────────

        [Test]
        public void Execute_IncrementsVersionByOne()
        {
            var map = MakeMap();
            int oldVersion = map.Version;
            var cmd = new ClearEnvironmentScheduleCommand();
            cmd.Execute(map);
            Assert.AreEqual(oldVersion + 1, map.Version);
        }

        // ──────────── 4) CommandId format ────────────

        [Test]
        public void CommandId_Format()
        {
            var cmd = new ClearEnvironmentScheduleCommand();
            Assert.AreEqual("clear-environment-schedule", cmd.CommandId);
            Assert.AreEqual(1, cmd.Version);
        }

        // ──────────── 5) Undo without Execute 抛 ────────────

        [Test]
        public void Undo_WithoutExecute_Throws()
        {
            var map = MakeMap();
            var cmd = new ClearEnvironmentScheduleCommand();
            Assert.Throws<System.InvalidOperationException>(() => cmd.Undo(map));
        }
    }
}
