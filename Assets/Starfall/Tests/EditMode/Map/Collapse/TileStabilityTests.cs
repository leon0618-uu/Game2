using NUnit.Framework;
using Starfall.Core.Map.Collapse;

namespace Starfall.Tests.EditMode.Map.Collapse
{
    /// <summary>
    /// doc2 MAP-11a <see cref="TileStability"/> 测试集（≥ 6 测试）。
    /// 覆盖：6 值枚举、IsPassable 规则、IsDestroyed 规则、派生规则。
    /// </summary>
    public class TileStabilityTests
    {
        // ──────────── 1) 6 值存在 + 默认值（=0 = Stable）────────────

        [Test]
        public void All_Values_AreDefined()
        {
            var values = System.Enum.GetValues(typeof(TileStability));
            Assert.AreEqual(6, values.Length, "TileStability must have exactly 6 values");
        }

        [Test]
        public void Default_Value_Is_Stable()
        {
            Assert.AreEqual(0, (int)TileStability.Stable, "Stable should be 0 (default)");
            TileStability def = default;
            Assert.AreEqual(TileStability.Stable, def);
        }

        [Test]
        public void Values_AreDistinct()
        {
            var seen = new System.Collections.Generic.HashSet<int>();
            foreach (TileStability s in System.Enum.GetValues(typeof(TileStability)))
            {
                Assert.IsTrue(seen.Add((int)s), $"Duplicate byte value: {(int)s} for {s}");
            }
        }

        // ──────────── 2-3) IsPassable 规则 ────────────

        [Test]
        public void IsPassable_ReturnsTrue_For_PassableValues()
        {
            Assert.IsTrue(TileStability.Stable.IsPassable());
            Assert.IsTrue(TileStability.Unstable.IsPassable());
            Assert.IsTrue(TileStability.Reconstructed.IsPassable());
        }

        [Test]
        public void IsPassable_ReturnsFalse_For_NonPassableValues()
        {
            Assert.IsFalse(TileStability.Fractured.IsPassable());
            Assert.IsFalse(TileStability.Collapsing.IsPassable());
            Assert.IsFalse(TileStability.Collapsed.IsPassable());
        }

        // ──────────── 4) IsDestroyed 规则 ────────────

        [Test]
        public void IsDestroyed_Only_True_For_Collapsed()
        {
            Assert.IsTrue(TileStability.Collapsed.IsDestroyed());
            Assert.IsFalse(TileStability.Stable.IsDestroyed());
            Assert.IsFalse(TileStability.Unstable.IsDestroyed());
            Assert.IsFalse(TileStability.Fractured.IsDestroyed());
            Assert.IsFalse(TileStability.Collapsing.IsDestroyed());
            Assert.IsFalse(TileStability.Reconstructed.IsDestroyed());
        }

        // ──────────── 5) LocalCollapseValue 派生规则 ────────────

        [TestCase(0, TileStability.Stable)]
        [TestCase(1, TileStability.Unstable)]
        [TestCase(30, TileStability.Unstable)]
        [TestCase(49, TileStability.Unstable)]
        [TestCase(50, TileStability.Fractured)]
        [TestCase(60, TileStability.Fractured)]
        [TestCase(69, TileStability.Fractured)]
        [TestCase(70, TileStability.Collapsing)]
        [TestCase(89, TileStability.Collapsing)]
        [TestCase(90, TileStability.Collapsed)]
        [TestCase(100, TileStability.Collapsed)]
        public void LocalCollapseValue_DeriveStability_AtValue(int value, TileStability expected)
        {
            Assert.AreEqual(expected, LocalCollapseValue.DeriveStability(value));
        }

        [Test]
        public void LocalCollapseValue_DeriveStability_Negative_ClampsToStable()
        {
            Assert.AreEqual(TileStability.Stable, LocalCollapseValue.DeriveStability(-1));
        }

        [Test]
        public void LocalCollapseValue_DeriveStability_OverHundred_ClampsToCollapsed()
        {
            Assert.AreEqual(TileStability.Collapsed, LocalCollapseValue.DeriveStability(101));
            Assert.AreEqual(TileStability.Collapsed, LocalCollapseValue.DeriveStability(int.MaxValue));
        }
    }
}
