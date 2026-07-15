using NUnit.Framework;
using Starfall.Core.Map.Pathfinding;

namespace Starfall.Tests.EditMode.Map.Pathfinding
{
    /// <summary>
    /// doc2 MAP-05 §9.4 <see cref="MapMovementProfile"/> data type tests.
    /// Covers factory defaults, boundary validation, equality / hashing, ToString.
    /// </summary>
    public class MovementProfileTests
    {
        // ──────────── 1. Standard preset values ────────────

        [Test]
        public void Standard_HasExpectedValues()
        {
            var p = MapMovementProfile.Standard;
            Assert.IsFalse(p.CanFly);
            Assert.IsFalse(p.CanCrossDimension);
            Assert.AreEqual(1, p.MaxAscendHeight);
            Assert.AreEqual(2, p.MaxDescendHeight);
            Assert.AreEqual(6, p.MaxMovementPoints);
        }

        // ──────────── 2. Flyer preset values ────────────

        [Test]
        public void Flyer_HasExpectedValues()
        {
            var p = MapMovementProfile.Flyer;
            Assert.IsTrue(p.CanFly);
            Assert.IsTrue(p.CanCrossDimension);
            Assert.AreEqual(0, p.MaxAscendHeight);
            Assert.AreEqual(0, p.MaxDescendHeight);
            Assert.AreEqual(6, p.MaxMovementPoints);
        }

        // ──────────── 3. Heavy preset values ────────────

        [Test]
        public void Heavy_HasExpectedValues()
        {
            var p = MapMovementProfile.Heavy;
            Assert.IsFalse(p.CanFly);
            Assert.IsFalse(p.CanCrossDimension);
            Assert.AreEqual(0, p.MaxAscendHeight);  // cannot climb
            Assert.AreEqual(1, p.MaxDescendHeight); // 1 step down ok
            Assert.AreEqual(4, p.MaxMovementPoints);
        }

        // ──────────── 4. Equality ────────────

        [Test]
        public void Equality_SameValues_AreEqual()
        {
            var p1 = new MapMovementProfile(1, 2, false, false, 6);
            var p2 = new MapMovementProfile(1, 2, false, false, 6);
            Assert.AreEqual(p1, p2);
            Assert.IsTrue(p1 == p2);
            Assert.IsFalse(p1 != p2);
            Assert.AreEqual(p1.GetHashCode(), p2.GetHashCode());
        }

        // ──────────── 5. Inequality on each field ────────────

        [Test]
        public void Inequality_DifferentAscend()
        {
            var p1 = new MapMovementProfile(1, 2, false, false, 6);
            var p2 = new MapMovementProfile(2, 2, false, false, 6);
            Assert.AreNotEqual(p1, p2);
        }

        [Test]
        public void Inequality_DifferentAP()
        {
            var p1 = new MapMovementProfile(1, 2, false, false, 6);
            var p2 = new MapMovementProfile(1, 2, false, false, 8);
            Assert.AreNotEqual(p1, p2);
        }

        [Test]
        public void Inequality_DifferentFly()
        {
            var p1 = new MapMovementProfile(1, 2, false, false, 6);
            var p2 = new MapMovementProfile(1, 2, true, false, 6);
            Assert.AreNotEqual(p1, p2);
        }

        [Test]
        public void Inequality_DifferentCrossDim()
        {
            var p1 = new MapMovementProfile(1, 2, false, false, 6);
            var p2 = new MapMovementProfile(1, 2, false, true, 6);
            Assert.AreNotEqual(p1, p2);
        }

        // ──────────── 6. Validation: negative MaxAscend rejected ────────────

        [Test]
        public void Constructor_NegativeAscend_Throws()
        {
            Assert.Throws<System.ArgumentOutOfRangeException>(
                () => new MapMovementProfile(-1, 2, false, false, 6));
        }

        // ──────────── 7. Validation: negative MaxDescend rejected ────────────

        [Test]
        public void Constructor_NegativeDescend_Throws()
        {
            Assert.Throws<System.ArgumentOutOfRangeException>(
                () => new MapMovementProfile(1, -2, false, false, 6));
        }

        // ──────────── 8. Validation: negative MaxMovementPoints rejected ────────────

        [Test]
        public void Constructor_NegativeAP_Throws()
        {
            Assert.Throws<System.ArgumentOutOfRangeException>(
                () => new MapMovementProfile(1, 2, false, false, -1));
        }

        // ──────────── 9. AP=0 is allowed ("static" unit) ────────────

        [Test]
        public void Constructor_ZeroAP_IsAllowed()
        {
            var p = new MapMovementProfile(1, 2, false, false, 0);
            Assert.AreEqual(0, p.MaxMovementPoints);
        }

        // ──────────── 10. ToString contains key fields ────────────

        [Test]
        public void ToString_ContainsKeyFields()
        {
            var p = MapMovementProfile.Standard;
            var s = p.ToString();
            StringAssert.Contains("ascend=1", s);
            StringAssert.Contains("descend=2", s);
            StringAssert.Contains("ap=6", s);
            StringAssert.Contains("fly=False", s);
        }
    }
}
