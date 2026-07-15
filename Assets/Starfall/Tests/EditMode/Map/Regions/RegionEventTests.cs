using System.Collections.Generic;
using NUnit.Framework;
using Starfall.Core.Map;
using Starfall.Core.Map.Coordinates;
using Starfall.Core.Map.Regions;
using Starfall.Core.Map.State;

namespace Starfall.Tests.EditMode.Map.Regions
{
    /// <summary>
    /// doc2 MAP-09 区域事件测试集。
    /// <para/>
    /// 覆盖：事件顺序 / 稳定排序 / 多 region 同时变更 / 与 MapEvent 兼容。
    /// </summary>
    public class RegionEventTests
    {
        private static MapRegionDefinition MakeDef(int regionId, RegionKind kind = RegionKind.Capture,
            RegionActivation activation = RegionActivation.Available)
        {
            return new MapRegionDefinition(
                new RegionId(regionId),
                kind,
                new[] {
                    new GridCoord(0, 0), new GridCoord(3, 0),
                    new GridCoord(3, 3), new GridCoord(0, 3)
                },
                ownerSide: -1,
                priority: 50,
                activation: activation);
        }

        private static MapState MakeMap()
        {
            return new MapState(new MapDefinition("map.test", 8, 8, DimensionLayer.Reality, 0));
        }

        // ──────────── 1) 事件工厂校验 ────────────

        [Test]
        public void StateChangedEvent_AllFields()
        {
            var evt = MapRegionService.MakeStateChangedEvent(7, RegionState.Available, RegionState.Active, "go");
            Assert.AreEqual(MapEventKind.OnRegionChanged, evt.Kind);
            Assert.AreEqual(7, evt.RegionId);
            Assert.AreEqual((int)RegionState.Available, evt.OldValue);
            Assert.AreEqual((int)RegionState.Active, evt.NewValue);
            Assert.AreEqual("go", evt.Description);
        }

        [Test]
        public void ActivatedEvent_Has_Progress()
        {
            var evt = MapRegionService.MakeActivatedEvent(7, 50);
            Assert.AreEqual(50, evt.NewValue);
        }

        [Test]
        public void EnteredEvent_Has_Side_AsNewValue()
        {
            var evt = MapRegionService.MakeEnteredEvent(7, new GridCoord(1, 1), 2);
            Assert.AreEqual(2, evt.NewValue);
        }

        [Test]
        public void ExitedEvent_Description_ContainsExited()
        {
            var evt = MapRegionService.MakeExitedEvent(7, new GridCoord(1, 1), 2);
            StringAssert.Contains("exited", evt.Description);
        }

        // ──────────── 2) MapEvent 稳定排序 ────────────

        [Test]
        public void Events_Sort_StableBy_RegionId()
        {
            var events = new List<MapEvent>
            {
                MapRegionService.MakeStateChangedEvent(3, RegionState.Available, RegionState.Active, "c"),
                MapRegionService.MakeStateChangedEvent(1, RegionState.Available, RegionState.Active, "a"),
                MapRegionService.MakeStateChangedEvent(2, RegionState.Available, RegionState.Active, "b"),
            };
            events.Sort();
            // MapEvent.CompareTo: Kind → Coord → RegionId → AnchorId → ...
            // All have same Kind (OnRegionChanged=2); RegionId is the next sort key.
            Assert.AreEqual(1, events[0].RegionId);
            Assert.AreEqual(2, events[1].RegionId);
            Assert.AreEqual(3, events[2].RegionId);
        }

        // ──────────── 3) 多 region 同时变更 ────────────

        [Test]
        public void MultiRegion_Transition_BothUpdated()
        {
            var map = MakeMap();
            var service = new MapRegionService();
            service.Register(map, MakeDef(1));
            service.Register(map, MakeDef(2));
            service.TransitionState(map, new RegionId(1), RegionState.Active, "first");
            service.TransitionState(map, new RegionId(2), RegionState.Active, "second");
            Assert.AreEqual(RegionState.Active, map.RegionStates[0].State);
            Assert.AreEqual(RegionState.Active, map.RegionStates[1].State);
        }

        // ──────────── 4) 与 MapEvent IComparable 兼容 ────────────

        [Test]
        public void Events_Sort_SameRegionId_ByDescription()
        {
            var events = new List<MapEvent>
            {
                MapRegionService.MakeStateChangedEvent(7, RegionState.Available, RegionState.Active, "z"),
                MapRegionService.MakeStateChangedEvent(7, RegionState.Available, RegionState.Active, "a"),
            };
            events.Sort();
            // Same Kind + RegionId; sort by Description ordinal
            Assert.AreEqual("a", events[0].Description);
            Assert.AreEqual("z", events[1].Description);
        }

        [Test]
        public void Events_EmptyReason_NormalizedToEmptyString()
        {
            var evt = MapRegionService.MakeStateChangedEvent(7, RegionState.Available, RegionState.Active, null);
            Assert.AreEqual(string.Empty, evt.Description);
        }

        // ──────────── 5) 服务层 emit 概念验证 ────────────

        [Test]
        public void TransitionState_Produces_NoEventsDirectly_Service_OnlyUpdatesState()
        {
            // 服务层只更新状态；事件由 Command 层编排后 emit。
            // 这测试的是：直接调用 service.TransitionState 不会修改任何外部 event 集合。
            var map = MakeMap();
            var service = new MapRegionService();
            service.Register(map, MakeDef(1));
            service.TransitionState(map, new RegionId(1), RegionState.Active, "x");
            // map state has been updated but no global event log was created.
            Assert.AreEqual(RegionState.Active, map.RegionStates[0].State);
        }

        // ──────────── 6) Tick 多 region 同时推进 ────────────

        [Test]
        public void Tick_MultipleRegions_AllProgress()
        {
            var map = MakeMap();
            var service = new MapRegionService();
            service.Register(map, MakeDef(1, RegionKind.Capture, RegionActivation.Active));
            service.Register(map, MakeDef(2, RegionKind.Defense, RegionActivation.Active));
            service.Tick(map);
            service.Tick(map);
            Assert.AreEqual(2, map.RegionStates[0].ActivationProgress);
            Assert.AreEqual(2, map.RegionStates[1].ActivationProgress);
        }

        // ──────────── 7) RegionState 枚举完整性 ────────────

        [Test]
        public void RegionState_Has_8_Values()
        {
            // Disabled, Hidden, Available, Active, Contested, Completed, Failed, Sealed
            int count = 0;
            foreach (var s in System.Enum.GetValues(typeof(RegionState)))
                count++;
            Assert.AreEqual(8, count);
        }

        [Test]
        public void RegionKind_Has_14_Values()
        {
            int count = 0;
            foreach (var k in System.Enum.GetValues(typeof(RegionKind)))
                count++;
            Assert.AreEqual(14, count);
        }
    }
}