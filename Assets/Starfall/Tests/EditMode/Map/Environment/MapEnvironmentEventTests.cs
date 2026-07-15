using System.Collections.Generic;
using NUnit.Framework;
using Starfall.Core.Map.Coordinates;
using Starfall.Core.Map.Environment;

namespace Starfall.Tests.EditMode.Map.Environment
{
    /// <summary>
    /// doc2 MAP-11b <see cref="MapEnvironmentEvent"/> 测试集（≥ 8 测试）。
    /// 覆盖：10 种 Kind 工厂、AffectedCoords 排序稳定、Tags 排序、equals。
    /// </summary>
    public class MapEnvironmentEventTests
    {
        // ──────────── 1) LocalDamageAmount 工厂 ────────────

        [Test]
        public void LocalDamageFactory_BuildsEvent_KindLocalDamageAmount()
        {
            var coord = new GridCoord(2, 3);
            var ev = MapEnvironmentEvent.LocalDamage(coord, 10);
            Assert.AreEqual(EnvironmentEventKind.LocalDamageAmount, ev.Kind);
            Assert.AreEqual(0, ev.TriggerTick);
            Assert.AreEqual(10, ev.Magnitude);
            Assert.AreEqual(1, ev.AffectedCoords.Count);
            Assert.AreEqual(coord, ev.AffectedCoords[0]);
        }

        [Test]
        public void LocalDamageFactory_NegativeAmount_Throws()
        {
            var coord = new GridCoord(0, 0);
            Assert.Throws<System.ArgumentOutOfRangeException>(
                () => MapEnvironmentEvent.LocalDamage(coord, -1));
        }

        // ──────────── 2) AffectedCoords 排序 ────────────

        [Test]
        public void AffectedCoords_AreSortedAscedingByCompareTo()
        {
            // 故意反向传递：b (3,5) 应该排在 a (1,2) 之后（按 CompareTo Y→X→Layer）。
            var c1 = new GridCoord(1, 2);
            var c2 = new GridCoord(3, 1);
            var c3 = new GridCoord(0, 5);
            var unsorted = new List<GridCoord> { c1, c2, c3 };
            var ev = new MapEnvironmentEvent(
                EnvironmentEventKind.LocalDamageAmount,
                0,
                unsorted,
                magnitude: 5);
            // 按 CompareTo 顺序：先 Y（0,1,1,2,2,3,3,5...）
            // c3.Y=5 最大 → 第 1 个？错了。CompareTo 先比 Y：
            // c2.Y=1, c1.Y=2, c3.Y=5 → 排序应为 c2, c1, c3
            Assert.AreEqual(c2, ev.AffectedCoords[0]);
            Assert.AreEqual(c1, ev.AffectedCoords[1]);
            Assert.AreEqual(c3, ev.AffectedCoords[2]);
        }

        // ──────────── 3) Tags 排序（Ordinal）────────────

        [Test]
        public void Tags_AreSortedAscedingOrdinal()
        {
            var tags = new List<string> { "c", "a", "b" };
            var ev = new MapEnvironmentEvent(
                EnvironmentEventKind.RegionActivation,
                0,
                tags: tags);
            Assert.AreEqual("a", ev.Tags[0]);
            Assert.AreEqual("b", ev.Tags[1]);
            Assert.AreEqual("c", ev.Tags[2]);
        }

        // ──────────── 4) Equals / HashCode ────────────

        [Test]
        public void Equals_SameContent_ReturnsTrue()
        {
            var a = MapEnvironmentEvent.LocalDamage(new GridCoord(1, 1), 5, triggerTick: 7);
            var b = MapEnvironmentEvent.LocalDamage(new GridCoord(1, 1), 5, triggerTick: 7);
            Assert.AreEqual(a, b);
            Assert.AreEqual(a.GetHashCode(), b.GetHashCode());
        }

        [Test]
        public void Equals_DifferentMagnitude_ReturnsFalse()
        {
            var a = MapEnvironmentEvent.LocalDamage(new GridCoord(1, 1), 5);
            var b = MapEnvironmentEvent.LocalDamage(new GridCoord(1, 1), 6);
            Assert.AreNotEqual(a, b);
        }

        // ──────────── 5) WarningEmitted 工厂 ────────────

        [Test]
        public void WarningEmitted_BuildsEvent_KindWarningEmitted()
        {
            var coords = new List<GridCoord>
            {
                new GridCoord(1, 2),
                new GridCoord(3, 4)
            };
            var ev = MapEnvironmentEvent.WarningEmitted(levelByte: 3, coords);
            Assert.AreEqual(EnvironmentEventKind.WarningEmitted, ev.Kind);
            Assert.AreEqual(3, ev.Magnitude);
            Assert.AreEqual(2, ev.AffectedCoords.Count);
            Assert.AreEqual(new GridCoord(1, 2), ev.AffectedCoords[0]);
            Assert.AreEqual(new GridCoord(3, 4), ev.AffectedCoords[1]);
        }

        [Test]
        public void WarningEmitted_NullCoords_Throws()
        {
            Assert.Throws<System.ArgumentNullException>(
                () => MapEnvironmentEvent.WarningEmitted(1, null));
        }

        // ──────────── 6) DeferredTrigger 工厂 ────────────

        [Test]
        public void DeferredTrigger_BuildsEvent_KindDeferredTrigger()
        {
            var ev = MapEnvironmentEvent.DeferredTrigger(new GridCoord(2, 2), 5);
            Assert.AreEqual(EnvironmentEventKind.DeferredTrigger, ev.Kind);
            Assert.AreEqual(5, ev.Magnitude);
            Assert.AreEqual(1, ev.AffectedCoords.Count);
        }

        // ──────────── 7) RegionActivation 工厂 ────────────

        [Test]
        public void RegionActivation_BuildsEvent_KindRegionActivation()
        {
            var ev = MapEnvironmentEvent.RegionActivation("42", triggerTick: 7);
            Assert.AreEqual(EnvironmentEventKind.RegionActivation, ev.Kind);
            Assert.AreEqual(7, ev.TriggerTick);
            Assert.AreEqual(1, ev.Tags.Count);
            Assert.AreEqual("42", ev.Tags[0]);
        }

        // ──────────── 8) TileStabilityChange 工厂 ────────────

        [Test]
        public void TileStabilityChange_BuildsEvent_KindTileStabilityChange()
        {
            var ev = MapEnvironmentEvent.TileStabilityChange(new GridCoord(2, 3), 3);
            Assert.AreEqual(EnvironmentEventKind.TileStabilityChange, ev.Kind);
            Assert.AreEqual(3, ev.Magnitude);
        }

        // ──────────── 9) GlobalCVShift 工厂 ────────────

        [Test]
        public void GlobalCVShift_BuildsEvent_KindGlobalCVDelta()
        {
            var ev = MapEnvironmentEvent.GlobalCVShift(-10, triggerTick: 5);
            Assert.AreEqual(EnvironmentEventKind.GlobalCVDelta, ev.Kind);
            Assert.AreEqual(-10, ev.Magnitude);
            Assert.AreEqual(5, ev.TriggerTick);
        }

        // ──────────── 10) ReinforcementSpawn 工厂 ────────────

        [Test]
        public void ReinforcementSpawn_BuildsEvent_KindReinforcementSpawn()
        {
            var ev = MapEnvironmentEvent.ReinforcementSpawn("99", new GridCoord(1, 1), triggerTick: 4);
            Assert.AreEqual(EnvironmentEventKind.ReinforcementSpawn, ev.Kind);
            Assert.AreEqual(4, ev.TriggerTick);
            Assert.AreEqual(1, ev.Tags.Count);
            Assert.AreEqual("99", ev.Tags[0]);
            Assert.AreEqual(1, ev.AffectedCoords.Count);
        }

        // ──────────── 11) EnvironmentEventKind 11 个值（10 + None）────────────

        [Test]
        public void EnvironmentEventKind_11Values_AllHaveCorrectByte()
        {
            Assert.AreEqual(0, (byte)EnvironmentEventKind.None);
            Assert.AreEqual(1, (byte)EnvironmentEventKind.DeferredTrigger);
            Assert.AreEqual(2, (byte)EnvironmentEventKind.LocalDamageAmount);
            Assert.AreEqual(3, (byte)EnvironmentEventKind.GlobalCVDelta);
            Assert.AreEqual(4, (byte)EnvironmentEventKind.TileStabilityChange);
            Assert.AreEqual(5, (byte)EnvironmentEventKind.FallTrigger);
            Assert.AreEqual(6, (byte)EnvironmentEventKind.RegionActivation);
            Assert.AreEqual(7, (byte)EnvironmentEventKind.ReinforcementSpawn);
            Assert.AreEqual(8, (byte)EnvironmentEventKind.MapEventRecord);
            Assert.AreEqual(9, (byte)EnvironmentEventKind.WarningEmitted);
            Assert.AreEqual(10, (byte)EnvironmentEventKind.TileReconstruct);
        }
    }
}
