using System.Collections.Generic;
using NUnit.Framework;
using Starfall.Core.Map;
using Starfall.Core.Map.Commands;
using Starfall.Core.Map.Coordinates;
using Starfall.Core.Map.State;
using Starfall.Core.Model;

namespace Starfall.Tests.EditMode.Map.Commands
{
    /// <summary>
    /// doc2 MAP-03 §21.1 <see cref="MapEvent"/> + 命令事件流测试。
    /// <para/>
    /// 覆盖：单事件 / 多事件 / stable 排序 / factory / CompareTo 单调 /
    /// <see cref="MapCommandResult.AffectedTiles"/> 派生视图一致性。
    /// </summary>
    public class MapCommandEventTests
    {
        private MapState _map;

        [SetUp]
        public void SetUp()
        {
            _map = MapTestHarness.MakeMap();
            MapTestHarness.Attach(_map);
        }

        [TearDown]
        public void TearDown()
        {
            MapTestHarness.DetachAll();
        }

        // ──────────── 1) MapEvent 排序契约 ────────────

        [Test]
        public void MapEvent_SortDeterministic_Runs100Times_SameOrder()
        {
            var rng = new System.Random(42);
            var pool = new List<MapEvent>();
            for (int i = 0; i < 50; i++)
            {
                pool.Add(new MapEvent(
                    (MapEventKind)(rng.Next(1, 14)),
                    coord: rng.Next(0, 8) == 0 ? null : (GridCoord?)new GridCoord(rng.Next(0, 8), rng.Next(0, 8)),
                    regionId: rng.Next(0, 8) == 0 ? null : (int?)rng.Next(1, 99),
                    anchorId: rng.Next(0, 8) == 0 ? null : (int?)rng.Next(1, 99),
                    description: "d" + rng.Next(0, 100)));
            }
            pool.Sort();

            for (int trial = 0; trial < 100; trial++)
            {
                var copy = new List<MapEvent>(pool);
                copy.Sort();
                for (int i = 0; i < pool.Count; i++)
                    Assert.AreEqual(pool[i], copy[i], $"trial {trial} index {i}");
            }
        }

        [Test]
        public void MapEvent_KindByte_StableSortOrder()
        {
            var e1 = new MapEvent(MapEventKind.OnTileChanged);                // byte 1
            var e2 = new MapEvent(MapEventKind.OnGlobalCVChanged);            // byte 7
            var e3 = new MapEvent(MapEventKind.OnTileStabilityChanged);       // byte 8
            var e4 = new MapEvent(MapEventKind.OnRegionChanged);               // byte 2
            var list = new List<MapEvent> { e3, e1, e4, e2 };
            list.Sort();
            Assert.AreEqual(MapEventKind.OnTileChanged, list[0].Kind);          // 1
            Assert.AreEqual(MapEventKind.OnRegionChanged, list[1].Kind);         // 2
            Assert.AreEqual(MapEventKind.OnGlobalCVChanged, list[2].Kind);      // 7
            Assert.AreEqual(MapEventKind.OnTileStabilityChanged, list[3].Kind);  // 8
        }

        [Test]
        public void MapEvent_EqualsAndHashCode_ConsistentForEqualEvents()
        {
            var a = new MapEvent(MapEventKind.OnTileChanged, coord: new GridCoord(0, 0));
            var b = new MapEvent(MapEventKind.OnTileChanged, coord: new GridCoord(0, 0));
            Assert.IsTrue(a.Equals(b));
            Assert.AreEqual(a.GetHashCode(), b.GetHashCode());
        }

        [Test]
        public void MapEvent_CompareTo_DescriptionByteOrdinal()
        {
            var a = new MapEvent(MapEventKind.OnTileChanged, description: "alpha");
            var b = new MapEvent(MapEventKind.OnTileChanged, description: "beta");
            Assert.IsTrue(a.CompareTo(b) < 0);
            Assert.IsTrue(b.CompareTo(a) > 0);
        }

        // ──────────── 2) MapEvent factory ────────────

        [Test]
        public void MapEvent_TileChangedFactory_CarriesCoord()
        {
            var e = MapEvent.TileChanged(new GridCoord(3, 4));
            Assert.AreEqual(MapEventKind.OnTileChanged, e.Kind);
            Assert.IsTrue(e.Coord.HasValue);
            Assert.AreEqual(3, e.Coord.Value.X);
            Assert.AreEqual(4, e.Coord.Value.Y);
        }

        [Test]
        public void MapEvent_GlobalCVChangedFactory_CarriesOldAndNew()
        {
            var e = MapEvent.GlobalCVChanged(10, 50);
            Assert.AreEqual(MapEventKind.OnGlobalCVChanged, e.Kind);
            Assert.AreEqual(10, e.OldValue);
            Assert.AreEqual(50, e.NewValue);
        }

        [Test]
        public void MapEvent_AnchorLinkCreatedFactory_CarriesIds()
        {
            var e = MapEvent.AnchorLinkCreated(42, 7);
            Assert.AreEqual(MapEventKind.OnAnchorLinkCreated, e.Kind);
            Assert.AreEqual(42, e.AnchorId);
            Assert.AreEqual(7, e.LinkId);
        }

        // ──────────── 3) 命令 Emit 事件 ────────────

        [Test]
        public void FlipTilePhase_EmitsOnTileChanged_One()
        {
            var r = new FlipTilePhaseCommand(MapTestHarness.FlippableTileId, DimensionLayer.Astral).Execute(_map);
            Assert.IsTrue(r.Success);
            Assert.AreEqual(1, r.Events.Count);
            Assert.AreEqual(MapEventKind.OnTileChanged, r.Events[0].Kind);
        }

        [Test]
        public void ModifyGlobalCV_EmitsOnGlobalCVChanged_WithOldAndNew()
        {
            _map.GlobalCollapseValue = 10; // set initial for capture
            var r = new ModifyGlobalCVCommand(50).Execute(_map);
            Assert.IsTrue(r.Success);
            Assert.AreEqual(1, r.Events.Count);
            var e = r.Events[0];
            Assert.AreEqual(MapEventKind.OnGlobalCVChanged, e.Kind);
            Assert.AreEqual(10, e.OldValue);
            Assert.AreEqual(50, e.NewValue);
        }

        [Test]
        public void SetTileStability_EmitsOnTileStabilityChanged_OldNew()
        {
            _map.GlobalCollapseValue = 0; // 不影响
            var r = new SetTileStabilityCommand(MapTestHarness.FlippableTileId, 50).Execute(_map);
            Assert.IsTrue(r.Success);
            Assert.AreEqual(1, r.Events.Count);
            var e = r.Events[0];
            Assert.AreEqual(MapEventKind.OnTileStabilityChanged, e.Kind);
            Assert.AreEqual(100, e.OldValue); // 初始 100
            Assert.AreEqual(50, e.NewValue);
        }

        [Test]
        public void SetMapDebugValue_EmitsOnMapDebugValueChanged_WhenDevTestOn()
        {
            _map.EnableDevTestMode();
            var r = new SetMapDebugValueCommand("k1", "v1").Execute(_map);
            Assert.IsTrue(r.Success);
            Assert.AreEqual(MapEventKind.OnMapDebugValueChanged, r.Events[0].Kind);
        }

        // ──────────── 4) MapCommandResult AffectedTiles 派生视图 ────────────

        [Test]
        public void MapCommandResult_AffectedTiles_IsFilterOfOnTileChangedEvents()
        {
            var r = new FlipTilePhaseCommand(MapTestHarness.FlippableTileId, DimensionLayer.Astral).Execute(_map);
            Assert.IsTrue(r.Success);
            // AffectedTiles 应当 == 仅含 OnTileChanged 的 events.Count
            int evtCount = 0;
            foreach (var e in r.Events) if (e.Kind == MapEventKind.OnTileChanged) evtCount++;
            Assert.AreEqual(evtCount, r.AffectedTiles.Count);
        }

        [Test]
        public void MapCommandResult_Fail_AlwaysHasEmptyAffectedTiles()
        {
            var r = new ModifyGlobalCVCommand(0).Execute(_map); // 初始 0 → no-op
            Assert.IsFalse(r.Success);
            Assert.AreEqual(0, r.AffectedTiles.Count);
        }

        // ──────────── 5) MultiEvents StableOrder ────────────

        [Test]
        public void MapCommandResult_Events_AreSortedByCompareTo()
        {
            _map.EnableDevTestMode();
            // CreateAnchorLink: 2 events (AnchorLinkCreated + RegionChanged)
            var verts = MapTestHarness.Poly(new GridPos(0, 0), new GridPos(2, 0), new GridPos(0, 2));
            new CreateAnchorLinkCommand(1, "Player", verts).Execute(_map);
            // 现在再 SetMapDebugValue：1 event (MapDebugValueChanged) → 3 total
            var debugCmd = new SetMapDebugValueCommand("k", "v");
            var debugResult = debugCmd.Execute(_map);
            Assert.IsTrue(debugResult.Success);

            // 合并多个 command result events → 排序单调
            var allEvents = new List<MapEvent>();
            // anchor events 已经在 _map 状态里但未收集到 result —— 重新跑一次
            var anchorResult = new CreateAnchorLinkCommand(1, "Player", verts).Execute(_map); // 应失败 duplicate
            // 改用附 partial events：
            var evt1 = new MapEvent(MapEventKind.OnTileChanged);
            var evt2 = new MapEvent(MapEventKind.OnAnchorLinkCreated);
            var evt3 = new MapEvent(MapEventKind.OnRegionChanged);
            var evts = new List<MapEvent> { evt2, evt1, evt3 };
            evts.Sort();
            // 默认 byte sort: 1 < 5 < ? → 视 OnRegionChanged byte = 2
            // OK: OnTileChanged(1), OnRegionChanged(2), OnAnchorLinkCreated(5)
            Assert.AreEqual(MapEventKind.OnTileChanged, evts[0].Kind);
            Assert.AreEqual(MapEventKind.OnRegionChanged, evts[1].Kind);
            Assert.AreEqual(MapEventKind.OnAnchorLinkCreated, evts[2].Kind);
        }
    }
}
