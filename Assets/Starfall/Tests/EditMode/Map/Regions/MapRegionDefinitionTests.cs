using System;
using System.Collections.Generic;
using NUnit.Framework;
using Starfall.Core.Map.Coordinates;
using Starfall.Core.Map.Regions;

namespace Starfall.Tests.EditMode.Map.Regions
{
    /// <summary>
    /// doc2 MAP-09 <see cref="MapRegionDefinition"/> 测试集。
    /// <para/>
    /// 覆盖：14 种 <see cref="RegionKind"/> × 边界校验 / 静态工厂方法 / 顶点排序 / OwnerSide 校验。
    /// </summary>
    public class MapRegionDefinitionTests
    {
        private static List<GridCoord> SquareBounds(int x0, int y0, int size = 3)
        {
            // 顺时针 4 顶点闭多边形（未排序 — ctor 会排序）
            var list = new List<GridCoord>
            {
                new GridCoord(x0, y0),
                new GridCoord(x0 + size - 1, y0),
                new GridCoord(x0 + size - 1, y0 + size - 1),
                new GridCoord(x0, y0 + size - 1),
            };
            return list;
        }

        // ──────────── 1) 14 种 RegionKind 都能正确构造 ────────────

        [TestCase(RegionKind.PlayerDeployment)]
        [TestCase(RegionKind.EnemySpawn)]
        [TestCase(RegionKind.Reinforcement)]
        [TestCase(RegionKind.Capture)]
        [TestCase(RegionKind.Defense)]
        [TestCase(RegionKind.Escort)]
        [TestCase(RegionKind.Extraction)]
        [TestCase(RegionKind.Restricted)]
        [TestCase(RegionKind.Interaction)]
        [TestCase(RegionKind.BossPhase)]
        [TestCase(RegionKind.StoryTrigger)]
        [TestCase(RegionKind.Collapse)]
        [TestCase(RegionKind.CameraSequence)]
        [TestCase(RegionKind.EnvironmentalHazard)]
        public void Constructor_AllRegionKind_Succeeds(RegionKind kind)
        {
            var def = new MapRegionDefinition(
                new RegionId(1),
                kind,
                SquareBounds(0, 0));
            Assert.AreEqual(kind, def.Kind);
            Assert.AreEqual(1, def.RegionId);
            Assert.AreEqual(4, def.Bounds.Count);
        }

        // ──────────── 2) Bounds 输入顺序保留：Contains / Polygon 依赖顺序 ────────────

        [Test]
        public void Constructor_Bounds_PreservesInputOrder()
        {
            // Bounds 保持输入顺序——多边形连通性依赖此顺序。
            var raw = new List<GridCoord>
            {
                new GridCoord(0, 0),
                new GridCoord(2, 0),
                new GridCoord(2, 2),
                new GridCoord(0, 2)
            };
            var def = new MapRegionDefinition(new RegionId(1), RegionKind.Capture, raw);
            Assert.AreEqual(new GridCoord(0, 0), def.Bounds[0]);
            Assert.AreEqual(new GridCoord(2, 0), def.Bounds[1]);
            Assert.AreEqual(new GridCoord(2, 2), def.Bounds[2]);
            Assert.AreEqual(new GridCoord(0, 2), def.Bounds[3]);
        }

        // ──────────── 3) Bounds 去重：相邻重复顶点视为同一顶点 ────────────

        [Test]
        public void Constructor_Bounds_DedupAdjacentDuplicates()
        {
            var raw = new List<GridCoord>
            {
                new GridCoord(0, 0),
                new GridCoord(2, 0),
                new GridCoord(2, 0),  // duplicate
                new GridCoord(2, 2),
                new GridCoord(0, 2)
            };
            var def = new MapRegionDefinition(new RegionId(1), RegionKind.Capture, raw);
            Assert.AreEqual(4, def.Bounds.Count);
        }

        // ──────────── 4) Bounds < 3 vertices → 抛异常 ────────────

        [Test]
        public void Constructor_Bounds_LessThan3Vertices_Throws()
        {
            Assert.Throws<ArgumentException>(() =>
                new MapRegionDefinition(
                    new RegionId(1),
                    RegionKind.Capture,
                    new List<GridCoord> { new GridCoord(0, 0), new GridCoord(1, 1) }));
        }

        [Test]
        public void Constructor_Bounds_Null_Throws()
        {
            Assert.Throws<ArgumentNullException>(() =>
                new MapRegionDefinition(
                    new RegionId(1),
                    RegionKind.Capture,
                    null));
        }

        // ──────────── 5) OwnerSide 校验 ────────────

        [Test]
        public void Constructor_OwnerSide_NegativeBelow_Throws()
        {
            Assert.Throws<ArgumentOutOfRangeException>(() =>
                new MapRegionDefinition(
                    new RegionId(1),
                    RegionKind.Capture,
                    SquareBounds(0, 0),
                    ownerSide: -2));
        }

        [Test]
        public void Constructor_OwnerSide_Accepts_NegativeOne_Zero_And_Positive()
        {
            new MapRegionDefinition(new RegionId(1), RegionKind.Capture, SquareBounds(0, 0), ownerSide: -1);
            new MapRegionDefinition(new RegionId(2), RegionKind.Capture, SquareBounds(0, 0), ownerSide: 0);
            new MapRegionDefinition(new RegionId(3), RegionKind.Capture, SquareBounds(0, 0), ownerSide: 1);
            new MapRegionDefinition(new RegionId(4), RegionKind.Capture, SquareBounds(0, 0), ownerSide: 99);
        }

        // ──────────── 6) Priority 范围校验 ────────────

        [Test]
        public void Constructor_Priority_OutOfRange_Throws()
        {
            Assert.Throws<ArgumentOutOfRangeException>(() =>
                new MapRegionDefinition(
                    new RegionId(1),
                    RegionKind.Capture,
                    SquareBounds(0, 0),
                    priority: -1));
            Assert.Throws<ArgumentOutOfRangeException>(() =>
                new MapRegionDefinition(
                    new RegionId(2),
                    RegionKind.Capture,
                    SquareBounds(0, 0),
                    priority: 101));
        }

        // ──────────── 7) Triggers 排序 + 数量 ────────────

        [Test]
        public void Constructor_Triggers_AreSorted()
        {
            var trigs = new List<RegionTrigger>
            {
                new RegionTrigger(RegionTriggerKind.OnExit, "z"),
                new RegionTrigger(RegionTriggerKind.OnEnter, "a"),
                new RegionTrigger(RegionTriggerKind.OnActivated, "m"),
            };
            var def = new MapRegionDefinition(
                new RegionId(1),
                RegionKind.Capture,
                SquareBounds(0, 0),
                triggers: trigs);
            // 排序键: Kind byte → Threshold → Tag ordinal
            // OnEnter(0) < OnExit(1) < OnActivated(3)
            Assert.AreEqual(RegionTriggerKind.OnEnter, def.Triggers[0].Kind);
            Assert.AreEqual(RegionTriggerKind.OnExit, def.Triggers[1].Kind);
            Assert.AreEqual(RegionTriggerKind.OnActivated, def.Triggers[2].Kind);
        }

        [Test]
        public void Constructor_Triggers_Null_DefaultsToEmpty()
        {
            var def = new MapRegionDefinition(
                new RegionId(1),
                RegionKind.Capture,
                SquareBounds(0, 0),
                triggers: null);
            Assert.AreEqual(0, def.Triggers.Count);
        }

        // ──────────── 8) 静态工厂：默认值校验 ────────────

        [Test]
        public void PlayerSpawn_Factory_Sets_Kind_And_Owner()
        {
            var def = MapRegionDefinition.PlayerSpawn(1, SquareBounds(0, 0));
            Assert.AreEqual(RegionKind.PlayerDeployment, def.Kind);
            Assert.AreEqual(0, def.OwnerSide);
            Assert.AreEqual(RegionActivation.Available, def.Activation);
        }

        [Test]
        public void EnemySpawn_Factory_Sets_Kind_And_Owner()
        {
            var def = MapRegionDefinition.EnemySpawn(1, SquareBounds(0, 0));
            Assert.AreEqual(RegionKind.EnemySpawn, def.Kind);
            Assert.AreEqual(1, def.OwnerSide);
        }

        [Test]
        public void Capture_Factory_Sets_Kind()
        {
            var def = MapRegionDefinition.Capture(1, SquareBounds(0, 0), ownerSide: -1);
            Assert.AreEqual(RegionKind.Capture, def.Kind);
            Assert.AreEqual(-1, def.OwnerSide);
        }

        [Test]
        public void Reinforcement_Factory_HiddenByDefault()
        {
            var def = MapRegionDefinition.Reinforcement(1, SquareBounds(0, 0));
            Assert.AreEqual(RegionKind.Reinforcement, def.Kind);
            Assert.AreEqual(RegionActivation.Hidden, def.Activation);
        }

        // ──────────── 9) Contains 多边形包含检测 ────────────

        [Test]
        public void Contains_PointInside_ReturnsTrue()
        {
            // 3x3 square at (0,0). 用严格内部点(1,1)。
            var def = new MapRegionDefinition(
                new RegionId(1),
                RegionKind.Capture,
                SquareBounds(0, 0, 3));
            Assert.IsTrue(def.Contains(new GridCoord(1, 1, DimensionLayer.Reality)),
                $"Contains(1,1) should be true. Bounds: [{def.Bounds[0]}, {def.Bounds[1]}, {def.Bounds[2]}, {def.Bounds[3]}]");
        }

        [Test]
        public void Contains_PointOutside_ReturnsFalse()
        {
            var def = new MapRegionDefinition(
                new RegionId(1),
                RegionKind.Capture,
                SquareBounds(0, 0, 3));
            Assert.IsFalse(def.Contains(new GridCoord(5, 5)));
            Assert.IsFalse(def.Contains(new GridCoord(-1, 0)));
        }

        [Test]
        public void Contains_CrossLayer_PolygonMatchesAnyLayer()
        {
            // Bounds only contains Reality tiles. Polygon still includes Astral at same coord
            // (ray casting doesn't consider Layer).
            var def = new MapRegionDefinition(
                new RegionId(1),
                RegionKind.Capture,
                SquareBounds(0, 0, 3));
            Assert.IsTrue(def.Contains(new GridCoord(1, 1, DimensionLayer.Astral)));
        }

        // ──────────── 10) Equals / GetHashCode ────────────

        [Test]
        public void Equals_SameContent_AreEqual()
        {
            var a = new MapRegionDefinition(new RegionId(7), RegionKind.Capture, SquareBounds(0, 0));
            var b = new MapRegionDefinition(new RegionId(7), RegionKind.Capture, SquareBounds(0, 0));
            Assert.AreEqual(a, b);
            Assert.AreEqual(a.GetHashCode(), b.GetHashCode());
        }

        [Test]
        public void Equals_DifferentRegionId_NotEqual()
        {
            var a = new MapRegionDefinition(new RegionId(7), RegionKind.Capture, SquareBounds(0, 0));
            var b = new MapRegionDefinition(new RegionId(8), RegionKind.Capture, SquareBounds(0, 0));
            Assert.AreNotEqual(a, b);
        }

        // ──────────── 11) ToString ────────────

        [Test]
        public void ToString_ContainsIdAndKind()
        {
            var def = new MapRegionDefinition(new RegionId(7), RegionKind.Capture, SquareBounds(0, 0));
            string s = def.ToString();
            StringAssert.Contains("7", s);
            StringAssert.Contains("Capture", s);
        }
    }
}