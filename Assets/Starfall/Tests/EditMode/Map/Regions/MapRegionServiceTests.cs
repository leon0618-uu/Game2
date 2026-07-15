using System;
using System.Collections.Generic;
using NUnit.Framework;
using Starfall.Core.Map;
using Starfall.Core.Map.Coordinates;
using Starfall.Core.Map.Regions;
using Starfall.Core.Map.State;

namespace Starfall.Tests.EditMode.Map.Regions
{
    /// <summary>
    /// doc2 MAP-09 <see cref="MapRegionService"/> 测试集。
    /// <para/>
    /// 覆盖：状态机合法性表（每个转换 × 1）/ 非法转换拒绝 / 单位进出事件 / Tick 推进。
    /// </summary>
    public class MapRegionServiceTests
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
            var def = new MapDefinition("map.test", 8, 8, DimensionLayer.Reality, 0);
            return new MapState(def);
        }

        // ──────────── 1) 状态机合法性表 — 合法转换 ────────────

        [TestCase(RegionState.Disabled, RegionState.Hidden)]
        [TestCase(RegionState.Disabled, RegionState.Available)]
        [TestCase(RegionState.Hidden, RegionState.Available)]
        [TestCase(RegionState.Available, RegionState.Active)]
        [TestCase(RegionState.Active, RegionState.Contested)]
        [TestCase(RegionState.Active, RegionState.Completed)]
        [TestCase(RegionState.Active, RegionState.Failed)]
        [TestCase(RegionState.Contested, RegionState.Active)]
        [TestCase(RegionState.Contested, RegionState.Completed)]
        [TestCase(RegionState.Completed, RegionState.Sealed)]
        [TestCase(RegionState.Failed, RegionState.Sealed)]
        public void IsTransitionAllowed_ValidTransitions_ReturnTrue(RegionState from, RegionState to)
        {
            Assert.IsTrue(MapRegionService.IsTransitionAllowed(from, to),
                $"{from} -> {to} should be allowed");
        }

        // ──────────── 2) 非法转换拒绝 ────────────

        [TestCase(RegionState.Sealed, RegionState.Active)]
        [TestCase(RegionState.Sealed, RegionState.Available)]
        [TestCase(RegionState.Sealed, RegionState.Completed)]
        [TestCase(RegionState.Completed, RegionState.Active)]
        [TestCase(RegionState.Failed, RegionState.Active)]
        [TestCase(RegionState.Disabled, RegionState.Active)]
        [TestCase(RegionState.Hidden, RegionState.Active)]
        [TestCase(RegionState.Available, RegionState.Completed)]
        public void IsTransitionAllowed_InvalidTransitions_ReturnFalse(RegionState from, RegionState to)
        {
            Assert.IsFalse(MapRegionService.IsTransitionAllowed(from, to),
                $"{from} -> {to} should NOT be allowed");
        }

        // ──────────── 3) 同状态 = 非法 ────────────

        [TestCase(RegionState.Disabled)]
        [TestCase(RegionState.Hidden)]
        [TestCase(RegionState.Available)]
        [TestCase(RegionState.Active)]
        [TestCase(RegionState.Completed)]
        public void IsTransitionAllowed_SameState_ReturnsFalse(RegionState s)
        {
            Assert.IsFalse(MapRegionService.IsTransitionAllowed(s, s));
        }

        // ──────────── 4) Register / Unregister ────────────

        [Test]
        public void Register_AddsRegion()
        {
            var map = MakeMap();
            var service = new MapRegionService();
            var rs = service.Register(map, MakeDef(1));
            Assert.AreEqual(1, map.RegionStates.Count);
            Assert.AreSame(rs, map.RegionStates[0]);
        }

        [Test]
        public void Register_DuplicateId_Throws()
        {
            var map = MakeMap();
            var service = new MapRegionService();
            service.Register(map, MakeDef(1));
            Assert.Throws<InvalidOperationException>(() =>
                service.Register(map, MakeDef(1)));
        }

        [Test]
        public void Unregister_RemovesRegion()
        {
            var map = MakeMap();
            var service = new MapRegionService();
            service.Register(map, MakeDef(1));
            Assert.IsTrue(service.Unregister(map, new RegionId(1)));
            Assert.AreEqual(0, map.RegionStates.Count);
        }

        [Test]
        public void Unregister_NotFound_ReturnsFalse()
        {
            var map = MakeMap();
            var service = new MapRegionService();
            Assert.IsFalse(service.Unregister(map, new RegionId(99)));
        }

        // ──────────── 5) TransitionState 实际修改状态 ────────────

        [Test]
        public void TransitionState_LegalTransition_UpdatesState()
        {
            var map = MakeMap();
            var service = new MapRegionService();
            service.Register(map, MakeDef(1, RegionKind.Capture, RegionActivation.Available));
            service.TransitionState(map, new RegionId(1), RegionState.Active, "test");
            Assert.AreEqual(RegionState.Active, map.RegionStates[0].State);
        }

        [Test]
        public void TransitionState_IllegalTransition_Throws_MapStateUntouched()
        {
            var map = MakeMap();
            var service = new MapRegionService();
            service.Register(map, MakeDef(1, RegionKind.Capture, RegionActivation.Available));
            var pre = map.RegionStates[0].State;
            // Available -> Completed is illegal
            Assert.Throws<InvalidOperationException>(() =>
                service.TransitionState(map, new RegionId(1), RegionState.Completed, "test"));
            Assert.AreEqual(pre, map.RegionStates[0].State);
        }

        [Test]
        public void TransitionState_ToSealed_ClearsOccupiedCells()
        {
            var map = MakeMap();
            var service = new MapRegionService();
            service.Register(map, MakeDef(1, RegionKind.Capture, RegionActivation.Available));
            service.TransitionState(map, new RegionId(1), RegionState.Active, "act");
            service.NotifyUnitEntered(map, new GridCoord(1, 1), 0);
            Assert.AreEqual(1, map.RegionStates[0].OccupantCount);
            service.TransitionState(map, new RegionId(1), RegionState.Completed, "done");
            service.TransitionState(map, new RegionId(1), RegionState.Sealed, "lock");
            Assert.AreEqual(0, map.RegionStates[0].CurrentlyOccupiedCells.Count);
        }

        [Test]
        public void TransitionState_RegionNotFound_Throws()
        {
            var map = MakeMap();
            var service = new MapRegionService();
            Assert.Throws<InvalidOperationException>(() =>
                service.TransitionState(map, new RegionId(99), RegionState.Active, "test"));
        }

        // ──────────── 6) NotifyUnitEntered / Exited ────────────

        [Test]
        public void NotifyUnitEntered_IncrementsOccupantCount()
        {
            var map = MakeMap();
            var service = new MapRegionService();
            service.Register(map, MakeDef(1));
            service.NotifyUnitEntered(map, new GridCoord(1, 1), 0);
            Assert.AreEqual(1, map.RegionStates[0].OccupantCount);
        }

        [Test]
        public void NotifyUnitEntered_Exited_DecrementsOccupantCount()
        {
            var map = MakeMap();
            var service = new MapRegionService();
            service.Register(map, MakeDef(1));
            service.NotifyUnitEntered(map, new GridCoord(1, 1), 0);
            service.NotifyUnitExited(map, new GridCoord(1, 1), 0);
            Assert.AreEqual(0, map.RegionStates[0].OccupantCount);
        }

        [Test]
        public void NotifyUnitEntered_OutsideRegion_DoesNotIncrement()
        {
            var map = MakeMap();
            var service = new MapRegionService();
            service.Register(map, MakeDef(1));
            service.NotifyUnitEntered(map, new GridCoord(10, 10), 0);
            Assert.AreEqual(0, map.RegionStates[0].OccupantCount);
        }

        // ──────────── 7) Tick 推进 ────────────

        [Test]
        public void Tick_HiddenRegion_TransitionsToAvailable()
        {
            var map = MakeMap();
            var service = new MapRegionService();
            service.Register(map, MakeDef(1, RegionKind.StoryTrigger, RegionActivation.Hidden));
            Assert.AreEqual(RegionState.Hidden, map.RegionStates[0].State);
            service.Tick(map);
            Assert.AreEqual(RegionState.Available, map.RegionStates[0].State);
        }

        [Test]
        public void Tick_ActiveRegion_IncrementsActivationProgress()
        {
            var map = MakeMap();
            var service = new MapRegionService();
            service.Register(map, MakeDef(1, RegionKind.Capture, RegionActivation.Active));
            service.Tick(map);
            Assert.AreEqual(1, map.RegionStates[0].ActivationProgress);
        }

        [Test]
        public void Tick_CaptureAt100_TransitionsToCompleted()
        {
            var map = MakeMap();
            var service = new MapRegionService();
            service.Register(map, MakeDef(1, RegionKind.Capture, RegionActivation.Active));
            // Tick 100 times
            for (int i = 0; i < 100; i++)
                service.Tick(map);
            Assert.AreEqual(RegionState.Completed, map.RegionStates[0].State);
        }

        [Test]
        public void Tick_IncrementsCurrentTick()
        {
            var map = MakeMap();
            var service = new MapRegionService();
            service.Tick(map);
            service.Tick(map);
            service.Tick(map);
            Assert.AreEqual(3, service.CurrentTick);
        }

        // ──────────── 8) 查询 ────────────

        [Test]
        public void GetRegionsContaining_ReturnsMatching()
        {
            var map = MakeMap();
            var service = new MapRegionService();
            service.Register(map, MakeDef(1));
            var matches = service.GetRegionsContaining(map, new GridCoord(1, 1));
            Assert.AreEqual(1, matches.Count);
        }

        [Test]
        public void FindRegion_ById_ReturnsNull_IfMissing()
        {
            var map = MakeMap();
            var service = new MapRegionService();
            Assert.IsNull(service.FindRegion(map, new RegionId(99)));
        }

        [Test]
        public void FindRegion_ById_ReturnsRegion()
        {
            var map = MakeMap();
            var service = new MapRegionService();
            service.Register(map, MakeDef(7));
            var found = service.FindRegion(map, new RegionId(7));
            Assert.IsNotNull(found);
            Assert.AreEqual(7, found.Definition.RegionId);
        }

        // ──────────── 9) 事件工厂 ────────────

        [Test]
        public void MakeStateChangedEvent_HasCorrectKind()
        {
            var evt = MapRegionService.MakeStateChangedEvent(7, RegionState.Available, RegionState.Active, "test");
            Assert.AreEqual(MapEventKind.OnRegionChanged, evt.Kind);
            Assert.AreEqual(7, evt.RegionId);
            Assert.AreEqual((int)RegionState.Available, evt.OldValue);
            Assert.AreEqual((int)RegionState.Active, evt.NewValue);
        }

        [Test]
        public void MakeEnteredEvent_IncludesCoordAndSide()
        {
            var evt = MapRegionService.MakeEnteredEvent(7, new GridCoord(3, 3), 0);
            Assert.AreEqual(MapEventKind.OnRegionChanged, evt.Kind);
            Assert.AreEqual(7, evt.RegionId);
            Assert.AreEqual(new GridCoord(3, 3), evt.Coord);
            Assert.AreEqual(0, evt.NewValue);
        }

        [Test]
        public void MakeExitedEvent_DescriptionIsExited()
        {
            var evt = MapRegionService.MakeExitedEvent(7, new GridCoord(3, 3), 0);
            StringAssert.Contains("exited", evt.Description);
        }
    }
}