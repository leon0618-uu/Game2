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
    /// doc2 MAP-11b <see cref="InjectEnvironmentEventCommand"/> 测试集（≥ 6 测试）。
    /// 覆盖：happy / undo / 重复 event 拒绝 / null event 拒绝。
    /// </summary>
    public class InjectEnvironmentEventCommandTests
    {
        private static MapState MakeMap()
        {
            return new MapState(new MapDefinition("map.test", 8, 8,
                DimensionLayer.Reality, 0));
        }

        // ──────────── 1) Happy path：注入事件 ────────────

        [Test]
        public void Execute_AddsEventToActiveSchedule()
        {
            var map = MakeMap();
            // 先注入一个 schedule
            map.SetActiveSchedule(MapEnvironmentSchedule.FromEvents(
                new List<MapEnvironmentEvent>
                {
                    MapEnvironmentEvent.DeferredTrigger(new GridCoord(1, 1), 0),
                },
                scheduleId: 1,
                createdTick: 0));

            var ev = MapEnvironmentEvent.LocalDamage(new GridCoord(2, 2), 5);
            var cmd = new InjectEnvironmentEventCommand(ev);
            var result = cmd.Execute(map);
            Assert.IsTrue(result.Success);
            Assert.AreEqual(2, map.ActiveSchedule.Count);
        }

        // ──────────── 2) Undo 移除事件 ────────────

        [Test]
        public void Undo_RemovesAddedEvent()
        {
            var map = MakeMap();
            map.SetActiveSchedule(MapEnvironmentSchedule.FromEvents(
                new List<MapEnvironmentEvent>
                {
                    MapEnvironmentEvent.DeferredTrigger(new GridCoord(1, 1), 0),
                },
                scheduleId: 1,
                createdTick: 0));
            int oldCount = map.ActiveSchedule.Count;

            var ev = MapEnvironmentEvent.LocalDamage(new GridCoord(2, 2), 5);
            var cmd = new InjectEnvironmentEventCommand(ev);
            cmd.Execute(map);
            Assert.AreEqual(oldCount + 1, map.ActiveSchedule.Count);

            cmd.Undo(map);
            Assert.AreEqual(oldCount, map.ActiveSchedule.Count);
        }

        // ──────────── 3) 重复 event 拒绝 ────────────

        [Test]
        public void Execute_DuplicateEvent_Fails()
        {
            var map = MakeMap();
            var ev = MapEnvironmentEvent.LocalDamage(new GridCoord(2, 2), 5);
            map.SetActiveSchedule(MapEnvironmentSchedule.FromEvents(
                new List<MapEnvironmentEvent> { ev },
                scheduleId: 1,
                createdTick: 0));

            var cmd = new InjectEnvironmentEventCommand(ev);
            var result = cmd.Execute(map);
            Assert.IsFalse(result.Success);
            Assert.That(result.FailureReason, Does.Contain("duplicate"));
        }

        // ──────────── 4) Null event 拒绝（构造时）────────────

        [Test]
        public void Constructor_NullEvent_Throws()
        {
            Assert.Throws<System.ArgumentNullException>(
                () => new InjectEnvironmentEventCommand(null));
        }

        // ──────────── 5) Undo without Execute 抛 ────────────

        [Test]
        public void Undo_WithoutExecute_Throws()
        {
            var map = MakeMap();
            var cmd = new InjectEnvironmentEventCommand(
                MapEnvironmentEvent.LocalDamage(new GridCoord(1, 1), 5));
            Assert.Throws<System.InvalidOperationException>(() => cmd.Undo(map));
        }

        // ──────────── 6) Inject to empty schedule ────────────

        [Test]
        public void Execute_ToEmptySchedule_AddsFirstEvent()
        {
            var map = MakeMap();
            map.SetActiveSchedule(MapEnvironmentSchedule.Empty(0));
            Assert.AreEqual(0, map.ActiveSchedule.Count);

            var ev = MapEnvironmentEvent.LocalDamage(new GridCoord(1, 1), 5);
            var cmd = new InjectEnvironmentEventCommand(ev);
            var result = cmd.Execute(map);
            Assert.IsTrue(result.Success);
            Assert.AreEqual(1, map.ActiveSchedule.Count);
        }

        // ──────────── 7) CommandId format ────────────

        [Test]
        public void CommandId_Format()
        {
            var ev = MapEnvironmentEvent.LocalDamage(new GridCoord(2, 2), 5, triggerTick: 7);
            var cmd = new InjectEnvironmentEventCommand(ev);
            Assert.AreEqual($"inject-environment-event:2:7", cmd.CommandId);
        }

        // ──────────── 8) Increments Version by 1 ────────────

        [Test]
        public void Execute_IncrementsVersionByOne()
        {
            var map = MakeMap();
            int oldVersion = map.Version;
            var cmd = new InjectEnvironmentEventCommand(
                MapEnvironmentEvent.LocalDamage(new GridCoord(1, 1), 5));
            cmd.Execute(map);
            Assert.AreEqual(oldVersion + 1, map.Version);
        }
    }
}
