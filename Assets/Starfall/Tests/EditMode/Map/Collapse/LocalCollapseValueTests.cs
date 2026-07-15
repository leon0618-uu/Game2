using NUnit.Framework;
using Starfall.Core.Map.Collapse;
using Starfall.Core.Map.Coordinates;
using Starfall.Core.Map.State;

namespace Starfall.Tests.EditMode.Map.Collapse
{
    /// <summary>
    /// doc2 MAP-11a <see cref="LocalCollapseValue"/> 测试集（≥ 6 测试）。
    /// 覆盖：构造工厂、Coord 关联、Stability 派生、序列化、MapState 集成。
    /// </summary>
    public class LocalCollapseValueTests
    {
        // ──────────── 1) Zero 工厂 ────────────

        [Test]
        public void Zero_HasValue0_StableStage_Tick0()
        {
            var lcv = LocalCollapseValue.Zero(new GridCoord(1, 2));
            Assert.AreEqual(0, lcv.Value);
            Assert.AreEqual(TileStability.Stable, lcv.Stability);
            Assert.AreEqual(0, lcv.TickAccumulated);
            Assert.AreEqual(new GridCoord(1, 2), lcv.Coord);
        }

        [Test]
        public void Of_AcceptsValidValue_DerivesStability()
        {
            var lcv = LocalCollapseValue.Of(new GridCoord(0, 0), 75, 3);
            Assert.AreEqual(75, lcv.Value);
            Assert.AreEqual(TileStability.Collapsing, lcv.Stability);
            Assert.AreEqual(3, lcv.TickAccumulated);
        }

        [Test]
        public void Of_ClampsValue_AndDerivesStability()
        {
            // 200 → 100 → Collapsed
            var high = LocalCollapseValue.Of(new GridCoord(0, 0), 200);
            Assert.AreEqual(100, high.Value);
            Assert.AreEqual(TileStability.Collapsed, high.Stability);

            // -10 → 0 → Stable
            var low = LocalCollapseValue.Of(new GridCoord(0, 0), -10);
            Assert.AreEqual(0, low.Value);
            Assert.AreEqual(TileStability.Stable, low.Stability);
        }

        // ──────────── 2) Coord 关联 ────────────

        [Test]
        public void Constructor_PreservesCoord_AllFields()
        {
            var coord = new GridCoord(3, 5, DimensionLayer.Astral);
            var lcv = new LocalCollapseValue(coord, 50, 2);
            Assert.AreEqual(coord, lcv.Coord);
            Assert.AreEqual(DimensionLayer.Astral, lcv.Coord.Layer);
        }

        // ──────────── 3) WithDelta / WithValue / WithIncrementedTick ────────────

        [Test]
        public void WithDelta_ClampsAndPreservesCoord()
        {
            var lcv = LocalCollapseValue.Of(new GridCoord(1, 1), 30, 1);
            var next = lcv.WithDelta(50); // 30 + 50 = 80 → Collapsing
            Assert.AreEqual(new GridCoord(1, 1), next.Coord);
            Assert.AreEqual(80, next.Value);
            Assert.AreEqual(TileStability.Collapsing, next.Stability);
            Assert.AreEqual(1, next.TickAccumulated); // 不变
        }

        [Test]
        public void WithDelta_Negative_Clamps()
        {
            var lcv = LocalCollapseValue.Of(new GridCoord(0, 0), 30, 0);
            var next = lcv.WithDelta(-50);
            Assert.AreEqual(0, next.Value);
            Assert.AreEqual(TileStability.Stable, next.Stability);
        }

        [Test]
        public void WithValue_ReplacesValue_PreservesCoordAndTick()
        {
            var lcv = LocalCollapseValue.Of(new GridCoord(2, 2), 20, 5);
            var next = lcv.WithValue(80);
            Assert.AreEqual(new GridCoord(2, 2), next.Coord);
            Assert.AreEqual(80, next.Value);
            Assert.AreEqual(5, next.TickAccumulated);
        }

        [Test]
        public void WithIncrementedTick_AddsOne()
        {
            var lcv = LocalCollapseValue.Of(new GridCoord(0, 0), 0, 4);
            var next = lcv.WithIncrementedTick();
            Assert.AreEqual(5, next.TickAccumulated);
        }

        // ──────────── 4) MapState 集成 ────────────

        [Test]
        public void MapState_AddLocalCV_GetByTryGet()
        {
            var map = MakeMap();
            var lcv = LocalCollapseValue.Of(new GridCoord(0, 0), 60);
            map.AddLocalCV(lcv);
            var got = map.TryGetLocalCV(new GridCoord(0, 0));
            Assert.IsTrue(got.HasValue);
            Assert.AreEqual(60, got.Value.Value);
        }

        [Test]
        public void MapState_RemoveLocalCV_Removes()
        {
            var map = MakeMap();
            map.AddLocalCV(LocalCollapseValue.Of(new GridCoord(0, 0), 60));
            Assert.IsTrue(map.RemoveLocalCV(new GridCoord(0, 0)));
            Assert.IsFalse(map.TryGetLocalCV(new GridCoord(0, 0)).HasValue);
        }

        [Test]
        public void MapState_LocalCVs_Empty_ByDefault()
        {
            var map = MakeMap();
            Assert.AreEqual(0, map.LocalCVs.Count);
        }

        [Test]
        public void MapState_Hash_StableWithLocalCVs_Over100Runs()
        {
            var map = MakeMap();
            map.AddLocalCV(LocalCollapseValue.Of(new GridCoord(0, 0), 50));
            map.AddLocalCV(LocalCollapseValue.Of(new GridCoord(3, 3, DimensionLayer.Astral), 80, 2));
            ulong h0 = map.PostStateHash;
            for (int i = 0; i < 100; i++)
            {
                Assert.AreEqual(h0, map.PostStateHash, $"Hash drift at iteration {i}");
            }
        }

        [Test]
        public void MapState_Hash_DiffersBy_LocalCV_Change()
        {
            var a = MakeMap();
            a.AddLocalCV(LocalCollapseValue.Of(new GridCoord(0, 0), 50));
            var b = MakeMap();
            b.AddLocalCV(LocalCollapseValue.Of(new GridCoord(0, 0), 51));
            Assert.AreNotEqual(a.PostStateHash, b.PostStateHash);
        }

        [Test]
        public void MapState_Hash_OrderIndependent_LocalCVs()
        {
            var a = MakeMap();
            a.AddLocalCV(LocalCollapseValue.Of(new GridCoord(0, 0), 50));
            a.AddLocalCV(LocalCollapseValue.Of(new GridCoord(3, 3), 80));
            var b = MakeMap();
            b.AddLocalCV(LocalCollapseValue.Of(new GridCoord(3, 3), 80));
            b.AddLocalCV(LocalCollapseValue.Of(new GridCoord(0, 0), 50));
            Assert.AreEqual(a.PostStateHash, b.PostStateHash);
        }

        // ──────────── 5) 序列化往返 ────────────

        [Test]
        public void Serialize_Deserialize_RoundTrip()
        {
            var coord = new GridCoord(2, 3, DimensionLayer.Astral);
            var original = LocalCollapseValue.Of(coord, 55, 4);
            var bytes = LocalCollapseValueCodec.Serialize(original);
            var restored = LocalCollapseValueCodec.Deserialize(bytes);
            Assert.AreEqual(original.Coord, restored.Coord);
            Assert.AreEqual(original.Value, restored.Value);
            Assert.AreEqual(original.Stability, restored.Stability);
            Assert.AreEqual(original.TickAccumulated, restored.TickAccumulated);
        }

        [Test]
        public void Deserialize_Empty_Throws()
        {
            Assert.Throws<System.ArgumentException>(() => LocalCollapseValueCodec.Deserialize(null));
            Assert.Throws<System.ArgumentException>(() => LocalCollapseValueCodec.Deserialize(new byte[0]));
        }

        // ──────────── 6) 等值 / 不等 ────────────

        [Test]
        public void Equals_SameValues_ReturnsTrue()
        {
            var a = LocalCollapseValue.Of(new GridCoord(1, 1), 50, 2);
            var b = LocalCollapseValue.Of(new GridCoord(1, 1), 50, 2);
            Assert.IsTrue(a.Equals(b));
            Assert.IsTrue(a == b);
            Assert.AreEqual(a.GetHashCode(), b.GetHashCode());
        }

        [Test]
        public void Equals_DifferentCoord_ReturnsFalse()
        {
            var a = LocalCollapseValue.Of(new GridCoord(1, 1), 50, 2);
            var b = LocalCollapseValue.Of(new GridCoord(2, 2), 50, 2);
            Assert.IsFalse(a.Equals(b));
        }

        [Test]
        public void ToString_ContainsAllFields()
        {
            var lcv = LocalCollapseValue.Of(new GridCoord(1, 1), 80, 2);
            string s = lcv.ToString();
            StringAssert.Contains("80", s);
            StringAssert.Contains("Collapsing", s);
            StringAssert.Contains("2", s);
        }

        // ──────────── 工厂 ────────────

        private static MapState MakeMap()
        {
            return new MapState(new MapDefinition("map.test", 8, 8,
                DimensionLayer.Reality, 0));
        }
    }
}
