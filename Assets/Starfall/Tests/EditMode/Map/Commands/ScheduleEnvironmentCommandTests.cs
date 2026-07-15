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
    /// doc2 MAP-11b <see cref="ScheduleEnvironmentCommand"/> 测试集（≥ 8 测试）。
    /// 覆盖：happy / undo / 空 schedule / ExecuteAll 顺序 / 失败回滚。
    /// </summary>
    public class ScheduleEnvironmentCommandTests
    {
        private static MapState MakeMap()
        {
            var map = new MapState(new MapDefinition("map.test", 8, 8,
                DimensionLayer.Reality, 0));
            for (int y = 0; y < 8; y++)
                for (int x = 0; x < 8; x++)
                    map.AddTile(new GridCoord(x, y));
            return map;
        }

        // ──────────── 1) Happy path ────────────

        [Test]
        public void Execute_NonEmptySchedule_SetsActiveSchedule_AndIncrementsTick()
        {
            var map = MakeMap();
            var events = new List<MapEnvironmentEvent>
            {
                MapEnvironmentEvent.DeferredTrigger(new GridCoord(1, 1), 5),
                MapEnvironmentEvent.LocalDamage(new GridCoord(2, 2), 10),
            };
            var schedule = MapEnvironmentSchedule.FromEvents(events, scheduleId: 7, createdTick: 3);
            var cmd = new ScheduleEnvironmentCommand(schedule);
            var result = cmd.Execute(map);
            Assert.IsTrue(result.Success);
            Assert.AreEqual(7, map.ActiveSchedule.ScheduleId);
            Assert.AreEqual(1, map.EnvironmentTickAccumulator);
        }

        // ──────────── 2) Empty schedule 也成功 ────────────

        [Test]
        public void Execute_EmptySchedule_SuccessButNoEffects()
        {
            var map = MakeMap();
            var cmd = new ScheduleEnvironmentCommand(MapEnvironmentSchedule.Empty(0));
            var result = cmd.Execute(map);
            Assert.IsTrue(result.Success);
            Assert.AreEqual(0, map.ActiveSchedule.ScheduleId);
            Assert.AreEqual(0, map.ActiveSchedule.Count);
            Assert.AreEqual(1, map.EnvironmentTickAccumulator); // 也 +1
        }

        // ──────────── 3) 顺序错乱 schedule 失败 ────────────

        [Test]
        public void Execute_OutOfOrderSchedule_Fails()
        {
            var map = MakeMap();
            var schedule = new MapEnvironmentSchedule(
                new[] {
                    MapEnvironmentEvent.FallTrigger(new GridCoord(1, 1)),
                    MapEnvironmentEvent.DeferredTrigger(new GridCoord(2, 2), 0),
                },
                scheduleId: 1,
                createdTick: 0);
            var cmd = new ScheduleEnvironmentCommand(schedule);
            var result = cmd.Execute(map);
            Assert.IsFalse(result.Success);
            Assert.That(result.FailureReason, Does.Contain("out of order"));
        }

        // ──────────── 4) Undo 恢复旧 schedule ────────────

        [Test]
        public void Undo_RestoresPreviousActiveSchedule_AndTick()
        {
            var map = MakeMap();
            // 初始空 schedule
            var oldSchedule = map.ActiveSchedule;
            int oldTick = map.EnvironmentTickAccumulator;

            var events = new List<MapEnvironmentEvent>
            {
                MapEnvironmentEvent.LocalDamage(new GridCoord(1, 1), 5),
            };
            var schedule = MapEnvironmentSchedule.FromEvents(events, 1, 0);
            var cmd = new ScheduleEnvironmentCommand(schedule);
            cmd.Execute(map);
            Assert.AreEqual(1, map.EnvironmentTickAccumulator);

            cmd.Undo(map);
            Assert.AreEqual(oldSchedule, map.ActiveSchedule);
            Assert.AreEqual(oldTick, map.EnvironmentTickAccumulator);
        }

        // ──────────── 5) CommandId format ────────────

        [Test]
        public void CommandId_HasScheduleId_Format()
        {
            var schedule = MapEnvironmentSchedule.FromEvents(
                new List<MapEnvironmentEvent>(),
                scheduleId: 99,
                createdTick: 0);
            var cmd = new ScheduleEnvironmentCommand(schedule);
            Assert.AreEqual("schedule-environment:99", cmd.CommandId);
            Assert.AreEqual(1, cmd.Version);
        }

        // ──────────── 6) version 自增 1 ────────────

        [Test]
        public void Execute_IncrementsVersionByOne()
        {
            var map = MakeMap();
            int oldVersion = map.Version;
            var cmd = new ScheduleEnvironmentCommand(MapEnvironmentSchedule.Empty(0));
            cmd.Execute(map);
            Assert.AreEqual(oldVersion + 1, map.Version);
        }

        // ──────────── 7) Events 写入版本号 ────────────

        [Test]
        public void Execute_ProducesEvents_FromExecuteAll()
        {
            var map = MakeMap();
            var events = new List<MapEnvironmentEvent>
            {
                MapEnvironmentEvent.DeferredTrigger(new GridCoord(1, 1), 0),
                MapEnvironmentEvent.WarningEmitted(1, new List<GridCoord>{ new GridCoord(2, 2) }),
            };
            var schedule = MapEnvironmentSchedule.FromEvents(events, 1, 0);
            var cmd = new ScheduleEnvironmentCommand(schedule);
            var result = cmd.Execute(map);
            // 至少应该有 WarningEmitted event（phase 9 触发 AnomalyDetected）
            bool hasAnomaly = false;
            for (int i = 0; i < result.Events.Count; i++)
            {
                if (result.Events[i].Kind == MapEventKind.OnAnomalyDetected)
                {
                    hasAnomaly = true;
                    break;
                }
            }
            Assert.IsTrue(hasAnomaly);
        }

        // ──────────── 8) Undo without Execute 抛异常 ────────────

        [Test]
        public void Undo_WithoutExecute_Throws()
        {
            var map = MakeMap();
            var cmd = new ScheduleEnvironmentCommand(MapEnvironmentSchedule.Empty(0));
            Assert.Throws<System.InvalidOperationException>(() => cmd.Undo(map));
        }

        // ──────────── 9) 负 ScheduleId 抛异常（构造时）────────────

        [Test]
        public void Constructor_NegativeScheduleId_Throws()
        {
            // ScheduleId < 0 在 MapEnvironmentSchedule 构造时已被拒绝
            Assert.Throws<System.ArgumentOutOfRangeException>(() =>
            {
                var s = new MapEnvironmentSchedule(
                    new List<MapEnvironmentEvent>(),
                    scheduleId: -1,
                    createdTick: 0);
            });
        }
    }
}
