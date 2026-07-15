using System.Collections.Generic;
using NUnit.Framework;
using Starfall.Core.Map.Coordinates;
using Starfall.Core.Map.Environment;

namespace Starfall.Tests.EditMode.Map.Environment
{
    /// <summary>
    /// doc2 MAP-11b <see cref="MapEnvironmentSchedule"/> 测试集（≥ 6 测试）。
    /// 覆盖：Empty 工厂、FromEvents 顺序（按 phase 0..9 排列）、事件列表稳定。
    /// </summary>
    public class MapEnvironmentScheduleTests
    {
        // ──────────── 1) Empty 工厂 ────────────

        [Test]
        public void Empty_HasZeroEvents_AndZeroScheduleId()
        {
            var s = MapEnvironmentSchedule.Empty(createdTick: 0);
            Assert.IsTrue(s.IsEmpty);
            Assert.AreEqual(0, s.Count);
            Assert.AreEqual(0, s.ScheduleId);
            Assert.AreEqual(0, s.CreatedTick);
        }

        // ──────────── 2) FromEvents 按 phase 排序 ────────────

        [Test]
        public void FromEvents_SortsEventsByPhaseIndex_Ascending()
        {
            // 反向构造：phase 5 → phase 0 → phase 9 → phase 2
            var events = new List<MapEnvironmentEvent>
            {
                MapEnvironmentEvent.FallTrigger(new GridCoord(0, 0)),
                MapEnvironmentEvent.DeferredTrigger(new GridCoord(1, 1), 0),
                MapEnvironmentEvent.WarningEmitted(1, new List<GridCoord>{ new GridCoord(2, 2) }),
                MapEnvironmentEvent.LocalDamage(new GridCoord(3, 3), 5),
            };
            var s = MapEnvironmentSchedule.FromEvents(events, scheduleId: 42, createdTick: 7);
            // 期望顺序：phase 0 (DeferredTrigger), phase 1 (LocalDamage), phase 5 (FallTrigger), phase 9 (WarningEmitted)
            Assert.AreEqual(EnvironmentEventKind.DeferredTrigger, s.Events[0].Kind);
            Assert.AreEqual(EnvironmentEventKind.LocalDamageAmount, s.Events[1].Kind);
            Assert.AreEqual(EnvironmentEventKind.FallTrigger, s.Events[2].Kind);
            Assert.AreEqual(EnvironmentEventKind.WarningEmitted, s.Events[3].Kind);
            Assert.AreEqual(42, s.ScheduleId);
            Assert.AreEqual(7, s.CreatedTick);
        }

        // ──────────── 3) 无效 phase 抛异常 ────────────

        [Test]
        public void FromEvents_InvalidKindKind_Throws()
        {
            var events = new List<MapEnvironmentEvent>
            {
                new MapEnvironmentEvent(EnvironmentEventKind.None, 0, null)
            };
            Assert.Throws<System.ArgumentException>(
                () => MapEnvironmentSchedule.FromEvents(events, 1, 0));
        }

        // ──────────── 4) GetEventsForPhase ────────────

        [Test]
        public void GetEventsForPhase_Phase1_ReturnsOnlyLocalDamageEvents()
        {
            var events = new List<MapEnvironmentEvent>
            {
                MapEnvironmentEvent.DeferredTrigger(new GridCoord(1, 1), 5), // phase 0
                MapEnvironmentEvent.LocalDamage(new GridCoord(2, 2), 5),     // phase 1
                MapEnvironmentEvent.LocalDamage(new GridCoord(3, 3), 7),     // phase 1
                MapEnvironmentEvent.FallTrigger(new GridCoord(4, 4)),         // phase 5
            };
            var s = MapEnvironmentSchedule.FromEvents(events, 1, 0);
            var phase1 = s.GetEventsForPhase(EnvironmentPhaseIndex.ContinuousEffects);
            Assert.AreEqual(2, phase1.Count);
            Assert.AreEqual(EnvironmentEventKind.LocalDamageAmount, phase1[0].Kind);
            Assert.AreEqual(EnvironmentEventKind.LocalDamageAmount, phase1[1].Kind);
        }

        // ──────────── 5) Equals / HashCode ────────────

        [Test]
        public void Equals_SameEventsAndIds_ReturnsTrue()
        {
            var ev = MapEnvironmentEvent.LocalDamage(new GridCoord(1, 1), 5);
            var a = MapEnvironmentSchedule.FromEvents(new List<MapEnvironmentEvent> { ev }, 7, 8);
            var b = MapEnvironmentSchedule.FromEvents(new List<MapEnvironmentEvent> { ev }, 7, 8);
            Assert.AreEqual(a, b);
            Assert.AreEqual(a.GetHashCode(), b.GetHashCode());
        }

        [Test]
        public void Equals_DifferentScheduleIds_ReturnsFalse()
        {
            var ev = MapEnvironmentEvent.LocalDamage(new GridCoord(1, 1), 5);
            var a = MapEnvironmentSchedule.FromEvents(new List<MapEnvironmentEvent> { ev }, 1, 0);
            var b = MapEnvironmentSchedule.FromEvents(new List<MapEnvironmentEvent> { ev }, 2, 0);
            Assert.AreNotEqual(a, b);
        }

        // ──────────── 6) Empty 工厂带 createdTick ────────────

        [Test]
        public void Empty_CreatedTick5_HasCorrectCreatedTick()
        {
            var s = MapEnvironmentSchedule.Empty(createdTick: 5);
            Assert.AreEqual(5, s.CreatedTick);
            Assert.AreEqual(0, s.ScheduleId);
        }

        // ──────────── 7) ToString 包含 ScheduleId + Events 数 ────────────

        [Test]
        public void ToString_ContainsScheduleIdAndEventsCount()
        {
            var s = MapEnvironmentSchedule.Empty(0);
            string str = s.ToString();
            StringAssert.Contains("ScheduleId", str);
            StringAssert.Contains("Events", str);
        }
    }
}
