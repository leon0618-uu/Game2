using NUnit.Framework;
using Starfall.Core.Map.Collapse;
using Starfall.Core.Map.State;

namespace Starfall.Tests.EditMode.Map.Collapse
{
    /// <summary>
    /// doc2 MAP-11a <see cref="GlobalCollapseValue"/> 测试集（≥ 8 测试）。
    /// 覆盖：构造工厂、Value clamp、Stage 自动计算、序列化往返、MapState 集成、Hash 稳定。
    /// </summary>
    public class GlobalCollapseValueTests
    {
        // ──────────── 1) Zero 工厂 ────────────

        [Test]
        public void Zero_HasValue0_StableStage_Tick0()
        {
            var gcv = GlobalCollapseValue.Zero;
            Assert.AreEqual(0, gcv.Value);
            Assert.AreEqual(CollapseStage.Stable, gcv.Stage);
            Assert.AreEqual(0, gcv.TickAccumulated);
            Assert.AreEqual(19, gcv.Threshold);
        }

        // ──────────── 2) Of 工厂 + clamp ────────────

        [Test]
        public void Of_NegativeValue_ClampsToZero()
        {
            var gcv = GlobalCollapseValue.Of(-50);
            Assert.AreEqual(0, gcv.Value);
            Assert.AreEqual(CollapseStage.Stable, gcv.Stage);
        }

        [Test]
        public void Of_OverHundred_ClampsToHundred()
        {
            var gcv = GlobalCollapseValue.Of(200);
            Assert.AreEqual(100, gcv.Value);
            Assert.AreEqual(CollapseStage.GateFault, gcv.Stage);
        }

        [Test]
        public void Of_AcceptsValidValue()
        {
            var gcv = GlobalCollapseValue.Of(50, tickAccumulated: 7);
            Assert.AreEqual(50, gcv.Value);
            Assert.AreEqual(CollapseStage.Fracturing, gcv.Stage);
            Assert.AreEqual(7, gcv.TickAccumulated);
            Assert.AreEqual(59, gcv.Threshold);
        }

        // ──────────── 3) FromStage 工厂 ────────────

        [Test]
        public void FromStage_ReturnsMaxValueOfStage()
        {
            Assert.AreEqual(19, GlobalCollapseValue.FromStage(CollapseStage.Stable).Value);
            Assert.AreEqual(39, GlobalCollapseValue.FromStage(CollapseStage.Anomalous).Value);
            Assert.AreEqual(59, GlobalCollapseValue.FromStage(CollapseStage.Fracturing).Value);
            Assert.AreEqual(79, GlobalCollapseValue.FromStage(CollapseStage.Collapsing).Value);
            Assert.AreEqual(100, GlobalCollapseValue.FromStage(CollapseStage.GateFault).Value);
        }

        // ──────────── 4) Stage 自动计算 ────────────

        [TestCase(0, CollapseStage.Stable)]
        [TestCase(20, CollapseStage.Anomalous)]
        [TestCase(40, CollapseStage.Fracturing)]
        [TestCase(60, CollapseStage.Collapsing)]
        [TestCase(80, CollapseStage.GateFault)]
        public void Constructor_DerivesCorrectStage(int value, CollapseStage expectedStage)
        {
            var gcv = GlobalCollapseValue.Of(value);
            Assert.AreEqual(expectedStage, gcv.Stage);
        }

        // ──────────── 5) 派生操作（WithDelta / WithValue / WithIncrementedTick）────────────

        [Test]
        public void WithDelta_AppliesAndClamps()
        {
            var a = GlobalCollapseValue.Of(40);
            var b = a.WithDelta(15); // 40 + 15 = 55, Fracturing
            Assert.AreEqual(55, b.Value);
            Assert.AreEqual(CollapseStage.Fracturing, b.Stage);

            var c = a.WithDelta(-100); // 40 - 100 = -60, clamp to 0
            Assert.AreEqual(0, c.Value);
        }

        [Test]
        public void WithValue_ReplacesValue_PreservesTick()
        {
            var a = GlobalCollapseValue.Of(40, tickAccumulated: 3);
            var b = a.WithValue(70);
            Assert.AreEqual(70, b.Value);
            Assert.AreEqual(3, b.TickAccumulated);
        }

        [Test]
        public void WithIncrementedTick_AddsOne()
        {
            var a = GlobalCollapseValue.Of(0);
            var b = a.WithIncrementedTick();
            Assert.AreEqual(1, b.TickAccumulated);
            Assert.AreEqual(a.Value, b.Value);
        }

        // ──────────── 6) MapState 集成（影子字段）────────────

        [Test]
        public void MapState_GlobalCollapseValue_Getter_ReturnsGlobalCV_Value()
        {
            var map = MakeMap(0);
            Assert.AreEqual(0, map.GlobalCollapseValue);
            map.GlobalCV = GlobalCollapseValue.Of(50, 3);
            Assert.AreEqual(50, map.GlobalCollapseValue);
        }

        [Test]
        public void MapState_GlobalCollapseValue_Setter_SyncsGlobalCV()
        {
            var map = MakeMap(0);
            map.GlobalCollapseValue = 75;
            Assert.AreEqual(75, map.GlobalCV.Value);
            Assert.AreEqual(CollapseStage.Collapsing, map.GlobalCV.Stage);
        }

        [Test]
        public void MapState_CurrentStage_DerivesFromGlobalCV()
        {
            var map = MakeMap(0);
            Assert.AreEqual(CollapseStage.Stable, map.CurrentStage);
            map.GlobalCV = GlobalCollapseValue.Of(85);
            Assert.AreEqual(CollapseStage.GateFault, map.CurrentStage);
        }

        // ──────────── 7) Hash 稳定（与 MapState 集成）────────────

        [Test]
        public void MapState_Hash_StableWithGlobalCV_Over100Runs()
        {
            var map = MakeMap(50);
            map.GlobalCV = GlobalCollapseValue.Of(50, 5);
            ulong h0 = map.PostStateHash;
            for (int i = 0; i < 100; i++)
            {
                Assert.AreEqual(h0, map.PostStateHash, $"Hash drift at iteration {i}");
            }
        }

        [Test]
        public void MapState_Hash_DiffersBy_GlobalCV_Change()
        {
            var a = MakeMap(0);
            a.GlobalCV = GlobalCollapseValue.Of(50);
            var b = MakeMap(0);
            b.GlobalCV = GlobalCollapseValue.Of(51);
            Assert.AreNotEqual(a.PostStateHash, b.PostStateHash);
        }

        [Test]
        public void MapState_Hash_GlobalCollapseValueShadowField_Syncs()
        {
            // 旧字段赋值应该与 typed 字段产生相同 hash
            var a = MakeMap(0);
            a.GlobalCollapseValue = 60;
            var b = MakeMap(0);
            b.GlobalCV = GlobalCollapseValue.Of(60);
            Assert.AreEqual(a.PostStateHash, b.PostStateHash,
                "Shadow field setter must produce same hash as typed setter");
        }

        // ──────────── 8) 序列化往返 ────────────

        [Test]
        public void Serialize_Deserialize_RoundTrip()
        {
            var original = GlobalCollapseValue.Of(45, 7);
            var bytes = GlobalCollapseValueCodec.Serialize(original);
            var restored = GlobalCollapseValueCodec.Deserialize(bytes);
            Assert.AreEqual(original.Value, restored.Value);
            Assert.AreEqual(original.Stage, restored.Stage);
            Assert.AreEqual(original.Threshold, restored.Threshold);
            Assert.AreEqual(original.TickAccumulated, restored.TickAccumulated);
        }

        [Test]
        public void Deserialize_Empty_Throws()
        {
            Assert.Throws<System.ArgumentException>(() => GlobalCollapseValueCodec.Deserialize(null));
            Assert.Throws<System.ArgumentException>(() => GlobalCollapseValueCodec.Deserialize(new byte[0]));
        }

        // ──────────── 9) 等值 / 不等 ────────────

        [Test]
        public void Equals_SameValues_ReturnsTrue()
        {
            var a = GlobalCollapseValue.Of(50, 5);
            var b = GlobalCollapseValue.Of(50, 5);
            Assert.IsTrue(a.Equals(b));
            Assert.IsTrue(a == b);
            Assert.IsFalse(a != b);
            Assert.AreEqual(a.GetHashCode(), b.GetHashCode());
        }

        [Test]
        public void Equals_DifferentValues_ReturnsFalse()
        {
            var a = GlobalCollapseValue.Of(50, 5);
            var b = GlobalCollapseValue.Of(51, 5);
            Assert.IsFalse(a.Equals(b));
            Assert.IsTrue(a != b);
        }

        [Test]
        public void ToString_ContainsAllFields()
        {
            var gcv = GlobalCollapseValue.Of(60, 3);
            string s = gcv.ToString();
            StringAssert.Contains("60", s);
            StringAssert.Contains("Collapsing", s);
            StringAssert.Contains("3", s);
        }

        // ──────────── 工厂 ────────────

        private static MapState MakeMap(int initialCV)
        {
            return new MapState(new MapDefinition("map.test", 8, 8,
                Starfall.Core.Map.Coordinates.DimensionLayer.Reality, initialCV));
        }
    }
}
