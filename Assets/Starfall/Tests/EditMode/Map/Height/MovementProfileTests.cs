using NUnit.Framework;
using Starfall.Core.Map.Height;

namespace Starfall.Tests.EditMode.Map.Height
{
    /// <summary>
    /// <see cref="MovementProfile"/> 行为测试。
    /// 覆盖：构造校验 / 内置 Standard + Flyer / 等值 / 哈希。
    /// </summary>
    public class MovementProfileTests
    {
        [Test]
        public void Constructor_NegativeAscend_Throws()
        {
            Assert.Throws<System.ArgumentOutOfRangeException>(
                () => new MovementProfile(false, -1, 0, false));
        }

        [Test]
        public void Constructor_NegativeDescend_Throws()
        {
            Assert.Throws<System.ArgumentOutOfRangeException>(
                () => new MovementProfile(false, 0, -1, false));
        }

        [Test]
        public void Standard_IsStandardInfantry()
        {
            Assert.IsFalse(MovementProfile.Standard.CanFly);
            Assert.AreEqual(1, MovementProfile.Standard.MaxAscend);
            Assert.AreEqual(2, MovementProfile.Standard.MaxDescend);
            Assert.IsFalse(MovementProfile.Standard.CanCrossDimension);
        }

        [Test]
        public void Flyer_CanFlyAndCrossDimension()
        {
            Assert.IsTrue(MovementProfile.Flyer.CanFly);
            Assert.IsTrue(MovementProfile.Flyer.CanCrossDimension);
        }

        [Test]
        public void Equals_SameFields_True()
        {
            var a = new MovementProfile(false, 1, 2, false);
            var b = new MovementProfile(false, 1, 2, false);
            Assert.IsTrue(a == b);
            Assert.IsFalse(a != b);
            Assert.AreEqual(a.GetHashCode(), b.GetHashCode());
        }

        [Test]
        public void Equals_DifferentFlyFlag_False()
        {
            var a = new MovementProfile(false, 0, 0, false);
            var b = new MovementProfile(true, 0, 0, false);
            Assert.IsTrue(a != b);
        }

        [Test]
        public void ToString_IncludesAllFields()
        {
            var s = MovementProfile.Standard.ToString();
            StringAssert.Contains("fly=False", s);
            StringAssert.Contains("up=1", s);
            StringAssert.Contains("down=2", s);
            StringAssert.Contains("crossDim=False", s);
        }
    }
}
