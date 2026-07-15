using System.Collections.Generic;
using NUnit.Framework;
using Starfall.Core.Map;
using Starfall.Core.Map.Collapse;
using Starfall.Core.Map.Coordinates;
using Starfall.Core.Map.Environment;
using Starfall.Core.Map.State;

namespace Starfall.Tests.EditMode.Map.Environment
{
    /// <summary>
    /// doc2 MAP-11b <see cref="EnvironmentPhaseResolver"/> 测试集（≥ 15 测试）。
    /// 覆盖：10 步 phase happy + failure + 与 MAP-11a CV 联动 + 顺序保证 + ValidateSchedule。
    /// </summary>
    public class EnvironmentPhaseResolverTests
    {
        private static MapState MakeMap(int initialCV = 0)
        {
            var map = new MapState(new MapDefinition("map.test", 8, 8,
                DimensionLayer.Reality, initialCV));
            // 添加测试用 tiles
            for (int y = 0; y < 8; y++)
            {
                for (int x = 0; x < 8; x++)
                {
                    map.AddTile(new GridCoord(x, y));
                }
            }
            return map;
        }

        // ──────────── 1-10) ExecutePhase happy 10 steps ────────────

        [Test]
        public void ExecutePhase_Phase0_DeferredTriggers_AddsEventsToPending()
        {
            var map = MakeMap();
            var resolver = new EnvironmentPhaseResolver();
            var events = new List<MapEnvironmentEvent>
            {
                MapEnvironmentEvent.DeferredTrigger(new GridCoord(2, 3), 5)
            };
            var schedule = MapEnvironmentSchedule.FromEvents(events, 1, 0);
            resolver.ExecutePhase(map, 0, schedule);
            Assert.AreEqual(1, map.PendingEvents.Count);
        }

        [Test]
        public void ExecutePhase_Phase1_ContinuousEffects_TicksGlobalCV()
        {
            var map = MakeMap(initialCV: 19);
            var resolver = new EnvironmentPhaseResolver();
            resolver.ExecutePhase(map, 1, MapEnvironmentSchedule.Empty(0));
            // 19 + 1 = 20 → Anomalous
            Assert.AreEqual(20, map.GlobalCV.Value);
            Assert.AreEqual(CollapseStage.Anomalous, map.CurrentStage);
        }

        [Test]
        public void ExecutePhase_Phase2_LocalCV_AppliesDamageToTile()
        {
            var map = MakeMap();
            var resolver = new EnvironmentPhaseResolver();
            var coord = new GridCoord(3, 3);
            var events = new List<MapEnvironmentEvent>
            {
                MapEnvironmentEvent.LocalDamage(coord, 30)
            };
            var schedule = MapEnvironmentSchedule.FromEvents(events, 1, 0);
            resolver.ExecutePhase(map, 2, schedule);
            // 30 → Unstable
            var lcv = map.TryGetLocalCV(coord);
            Assert.IsNotNull(lcv);
            Assert.AreEqual(30, lcv.Value.Value);
        }

        [Test]
        public void ExecutePhase_Phase3_GlobalCV_AccumulatesDelta()
        {
            var map = MakeMap(initialCV: 50);
            var resolver = new EnvironmentPhaseResolver();
            var events = new List<MapEnvironmentEvent>
            {
                MapEnvironmentEvent.GlobalCVShift(10),
                MapEnvironmentEvent.GlobalCVShift(-5)
            };
            var schedule = MapEnvironmentSchedule.FromEvents(events, 1, 0);
            resolver.ExecutePhase(map, 3, schedule);
            // 50 + 10 - 5 = 55
            Assert.AreEqual(55, map.GlobalCV.Value);
        }

        [Test]
        public void ExecutePhase_Phase4_TileStability_AppliesStabilityChange()
        {
            var map = MakeMap();
            var resolver = new EnvironmentPhaseResolver();
            var events = new List<MapEnvironmentEvent>
            {
                MapEnvironmentEvent.TileStabilityChange(new GridCoord(4, 4), (int)TileStability.Fractured)
            };
            var schedule = MapEnvironmentSchedule.FromEvents(events, 1, 0);
            resolver.ExecutePhase(map, 4, schedule);
            // Stability=Fractured → LocalCV.Value=60
            var lcv = map.TryGetLocalCV(new GridCoord(4, 4));
            Assert.IsNotNull(lcv);
            Assert.AreEqual(TileStability.Fractured, lcv.Value.Stability);
        }

        [Test]
        public void ExecutePhase_Phase4_TileReconstruct_ResetsToReconstructed()
        {
            var map = MakeMap();
            var resolver = new EnvironmentPhaseResolver();
            // 先放一个 Collapsed LCV
            map.AddLocalCV(new LocalCollapseValue(new GridCoord(5, 5), 100, 0));
            var events = new List<MapEnvironmentEvent>
            {
                MapEnvironmentEvent.TileReconstruct(new GridCoord(5, 5))
            };
            var schedule = MapEnvironmentSchedule.FromEvents(events, 1, 0);
            resolver.ExecutePhase(map, 4, schedule);
            // 重置为 0（Stability = Stable）
            var lcv = map.TryGetLocalCV(new GridCoord(5, 5));
            Assert.IsNotNull(lcv);
            Assert.AreEqual(0, lcv.Value.Value);
        }

        [Test]
        public void ExecutePhase_Phase5_Falling_TriggersTileCollapse()
        {
            var map = MakeMap();
            var resolver = new EnvironmentPhaseResolver();
            var events = new List<MapEnvironmentEvent>
            {
                MapEnvironmentEvent.FallTrigger(new GridCoord(2, 2))
            };
            var schedule = MapEnvironmentSchedule.FromEvents(events, 1, 0);
            resolver.ExecutePhase(map, 5, schedule);
            var lcv = map.TryGetLocalCV(new GridCoord(2, 2));
            Assert.IsNotNull(lcv);
            Assert.AreEqual(80, lcv.Value.Value); // Collapsing = 80
        }

        [Test]
        public void ExecutePhase_Phase6_RegionActivation_SkipsWithoutService()
        {
            var map = MakeMap();
            var resolver = new EnvironmentPhaseResolver();
            // RegionService 未注入应直接返回
            var events = new List<MapEnvironmentEvent>
            {
                MapEnvironmentEvent.RegionActivation("1", 0)
            };
            var schedule = MapEnvironmentSchedule.FromEvents(events, 1, 0);
            // 不抛异常即通过
            resolver.ExecutePhase(map, 6, schedule);
        }

        [Test]
        public void ExecutePhase_Phase7_ReinforcementSpawn_NoOpStub()
        {
            var map = MakeMap();
            var resolver = new EnvironmentPhaseResolver();
            var events = new List<MapEnvironmentEvent>
            {
                MapEnvironmentEvent.ReinforcementSpawn("99", new GridCoord(3, 3))
            };
            var schedule = MapEnvironmentSchedule.FromEvents(events, 1, 0);
            // stub: 仅 emit 占位事件，不实际放置
            Assert.DoesNotThrow(() => resolver.ExecutePhase(map, 7, schedule));
        }

        [Test]
        public void ExecutePhase_Phase9_WarningEmitted_WithLevel_EmitsAnomaly()
        {
            var map = MakeMap(initialCV: 50);
            var resolver = new EnvironmentPhaseResolver();
            var events = new List<MapEnvironmentEvent>
            {
                MapEnvironmentEvent.WarningEmitted(2, new List<GridCoord>{ new GridCoord(1, 1) })
            };
            var schedule = MapEnvironmentSchedule.FromEvents(events, 1, 0);
            var output = resolver.ExecutePhase(map, 9, schedule);
            // 应 emit OnAnomalyDetected
            bool hasAnomaly = false;
            for (int i = 0; i < output.Count; i++)
            {
                if (output[i].Kind == MapEventKind.OnAnomalyDetected)
                {
                    hasAnomaly = true;
                    break;
                }
            }
            Assert.IsTrue(hasAnomaly);
        }

        // ──────────── 11) ValidateSchedule ────────────

        [Test]
        public void ValidateSchedule_Empty_ReturnsZero()
        {
            var resolver = new EnvironmentPhaseResolver();
            Assert.AreEqual(0, resolver.ValidateSchedule(MapEnvironmentSchedule.Empty(0)));
        }

        [Test]
        public void ValidateSchedule_InOrder_ReturnsZero()
        {
            var events = new List<MapEnvironmentEvent>
            {
                MapEnvironmentEvent.DeferredTrigger(new GridCoord(1, 1), 0),
                MapEnvironmentEvent.LocalDamage(new GridCoord(2, 2), 5),
                MapEnvironmentEvent.FallTrigger(new GridCoord(3, 3))
            };
            var s = MapEnvironmentSchedule.FromEvents(events, 1, 0);
            var resolver = new EnvironmentPhaseResolver();
            Assert.AreEqual(0, resolver.ValidateSchedule(s));
        }

        // ──────────── 12) ExecuteAll 顺序保证 ────────────

        [Test]
        public void ExecuteAll_ExecutesPhasesInOrder_0To9()
        {
            var map = MakeMap();
            var resolver = new EnvironmentPhaseResolver();
            // 在每个 phase 放置一个 LocalDamage
            var events = new List<MapEnvironmentEvent>
            {
                MapEnvironmentEvent.DeferredTrigger(new GridCoord(1, 1), 1),   // phase 0
                MapEnvironmentEvent.LocalDamage(new GridCoord(2, 2), 10),       // phase 1
                MapEnvironmentEvent.LocalDamage(new GridCoord(3, 3), 10),       // phase 2
                MapEnvironmentEvent.GlobalCVShift(5),                          // phase 3
                MapEnvironmentEvent.TileStabilityChange(new GridCoord(4, 4), (int)TileStability.Fractured), // phase 4
                MapEnvironmentEvent.FallTrigger(new GridCoord(5, 5)),          // phase 5
                MapEnvironmentEvent.RegionActivation("0", 0),                  // phase 6
                MapEnvironmentEvent.ReinforcementSpawn("1", new GridCoord(6, 6)), // phase 7
                MapEnvironmentEvent.MapEvent("test", 0),                       // phase 8
                MapEnvironmentEvent.WarningEmitted(0, new List<GridCoord>{ new GridCoord(7, 7) }), // phase 9
            };
            var s = MapEnvironmentSchedule.FromEvents(events, 1, 0);
            resolver.ExecuteAll(map, s);

            // 验证 LocalCV（在 phase 1 和 phase 2 应用过）= 20
            var lcv = map.TryGetLocalCV(new GridCoord(2, 2));
            Assert.IsNotNull(lcv);
            // Note: phase 1 + phase 2 both apply; here coord (2,2) only in phase 1, so 10
            Assert.AreEqual(10, lcv.Value.Value);

            // 验证 GlobalCV（在 phase 1 的 tick + phase 3 delta）
            // 0 (initial) + 1 (phase 1 tick) + 5 (phase 3 delta) = 6
            Assert.AreEqual(6, map.GlobalCV.Value);

            // 验证 EnvironmentTickAccumulator 已 +1 (但 ExecuteAll 不修改它；由 ScheduleEnvironmentCommand)
            // 这里只测 resolver 接口：assert TotalScheduleExecutions
            Assert.AreEqual(1, resolver.TotalScheduleExecutions);
            Assert.AreEqual(10, resolver.TotalPhaseExecutions);
        }

        // ──────────── 13) ExecutePhase out of range 抛异常 ────────────

        [Test]
        public void ExecutePhase_PhaseIndexOutOfRange_Throws()
        {
            var map = MakeMap();
            var resolver = new EnvironmentPhaseResolver();
            Assert.Throws<System.ArgumentOutOfRangeException>(
                () => resolver.ExecutePhase(map, 10, MapEnvironmentSchedule.Empty(0)));
        }

        [Test]
        public void ExecutePhase_NegativePhaseIndex_Throws()
        {
            var map = MakeMap();
            var resolver = new EnvironmentPhaseResolver();
            Assert.Throws<System.ArgumentOutOfRangeException>(
                () => resolver.ExecutePhase(map, -1, MapEnvironmentSchedule.Empty(0)));
        }

        // ──────────── 14) ValidateSchedule invalid phase (manually constructed) ────────────

        [Test]
        public void ValidateSchedule_ManuallyConstructedOutOfOrder_ReturnsNonZero()
        {
            // 手动构造违反 phase 顺序的 schedule
            var schedule = new MapEnvironmentSchedule(
                new[] {
                    MapEnvironmentEvent.FallTrigger(new GridCoord(1, 1)), // phase 5
                    MapEnvironmentEvent.DeferredTrigger(new GridCoord(2, 2), 0), // phase 0
                },
                scheduleId: 1,
                createdTick: 0);
            var resolver = new EnvironmentPhaseResolver();
            int code = resolver.ValidateSchedule(schedule);
            Assert.AreNotEqual(0, code);
        }

        // ──────────── 15) ExecutePhase 累计 TotalPhaseExecutions ────────────

        [Test]
        public void ExecutePhase_IncrementsTotalPhaseExecutionsCounter()
        {
            var map = MakeMap();
            var resolver = new EnvironmentPhaseResolver();
            int initial = resolver.TotalPhaseExecutions;
            resolver.ExecutePhase(map, 0, MapEnvironmentSchedule.Empty(0));
            Assert.AreEqual(initial + 1, resolver.TotalPhaseExecutions);
        }

        // ──────────── 16) Phase 1 + Phase 2 同 kind 同时出现 各自独立处理 ────────────

        [Test]
        public void Phase2_LocalDamage_PreservedAfterPhase1()
        {
            // 注：phase 1 持续效果 + phase 2 局部 CV 都是 LocalDamageAmount Kind；
            // schedule 内每个 event 按其 position 决定 phase；phase 1 + phase 2 各处理一次 event。
            var map = MakeMap();
            var resolver = new EnvironmentPhaseResolver();
            var coord = new GridCoord(2, 3);
            var events = new List<MapEnvironmentEvent>
            {
                MapEnvironmentEvent.LocalDamage(coord, 5),   // phase 1
                MapEnvironmentEvent.LocalDamage(coord, 7),   // phase 2
            };
            var s = MapEnvironmentSchedule.FromEvents(events, 1, 0);
            resolver.ExecuteAll(map, s);
            var lcv = map.TryGetLocalCV(coord);
            Assert.IsNotNull(lcv);
            // 5 (phase 1) + 7 (phase 2) = 12
            Assert.AreEqual(12, lcv.Value.Value);
        }
    }
}
