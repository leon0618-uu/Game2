using System.Collections.Generic;
using NUnit.Framework;
using Starfall.Core.Map.Coordinates;
using Starfall.Core.Map.Height;

namespace Starfall.Tests.EditMode.Map.Height
{
    /// <summary>
    /// <see cref="HeightTraversalService"/> 行为测试（≥ 12 项）。
    /// 覆盖：Standard / Flyer / 边界 / 异常 / 稳定排序。
    /// doc2 §9.4 + MAP-06 §4.4 测试矩阵 Row 133。
    /// </summary>
    public class HeightTraversalTests
    {
        // ──────────── Standard profile：上升 ────────────

        [Test]
        public void Standard_AscendPlusOne_True()
        {
            Assert.IsTrue(HeightTraversalService.CanTraverse(
                new HeightLevel(0), new HeightLevel(1), MovementProfile.Standard));
        }

        [Test]
        public void Standard_AscendPlusTwo_False()
        {
            // MaxAscend = 1 → +2 失败
            Assert.IsFalse(HeightTraversalService.CanTraverse(
                new HeightLevel(0), new HeightLevel(2), MovementProfile.Standard));
        }

        [Test]
        public void Standard_AscendPlusThree_False()
        {
            Assert.IsFalse(HeightTraversalService.CanTraverse(
                new HeightLevel(0), new HeightLevel(3), MovementProfile.Standard));
        }

        // ──────────── Standard profile：下降 ────────────

        [Test]
        public void Standard_DescendMinusOne_True()
        {
            Assert.IsTrue(HeightTraversalService.CanTraverse(
                new HeightLevel(1), new HeightLevel(0), MovementProfile.Standard));
        }

        [Test]
        public void Standard_DescendMinusTwo_True()
        {
            // MaxDescend = 2 → -2 OK
            Assert.IsTrue(HeightTraversalService.CanTraverse(
                new HeightLevel(2), new HeightLevel(0), MovementProfile.Standard));
        }

        [Test]
        public void Standard_DescendMinusThree_False()
        {
            // MaxDescend = 2 → -3 fail
            Assert.IsFalse(HeightTraversalService.CanTraverse(
                new HeightLevel(3), new HeightLevel(0), MovementProfile.Standard));
        }

        // ──────────── 同高度 ────────────

        [Test]
        public void SameHeight_AlwaysTrue()
        {
            Assert.IsTrue(HeightTraversalService.CanTraverse(
                new HeightLevel(2), new HeightLevel(2), MovementProfile.Standard));
            Assert.IsTrue(HeightTraversalService.CanTraverse(
                new HeightLevel(2), new HeightLevel(2), MovementProfile.Flyer));
        }

        // ──────────── Flyer 短路 ────────────

        [Test]
        public void Flyer_IgnoresLargeAscend()
        {
            Assert.IsTrue(HeightTraversalService.CanTraverse(
                new HeightLevel(0), new HeightLevel(4), MovementProfile.Flyer));
        }

        [Test]
        public void Flyer_IgnoresLargeDescend()
        {
            Assert.IsTrue(HeightTraversalService.CanTraverse(
                new HeightLevel(4), new HeightLevel(0), MovementProfile.Flyer));
        }

        // ──────────── MaxAscend / MaxDescend 边界 ────────────

        [Test]
        public void Standard_MaxAscendHeight_IsOne()
        {
            Assert.AreEqual(1, HeightTraversalService.MaxAscendHeight(MovementProfile.Standard));
        }

        [Test]
        public void Standard_MaxDescendHeight_IsTwo()
        {
            Assert.AreEqual(2, HeightTraversalService.MaxDescendHeight(MovementProfile.Standard));
        }

        [Test]
        public void Flyer_MaxAscendHeight_IsMax()
        {
            Assert.AreEqual(int.MaxValue, HeightTraversalService.MaxAscendHeight(MovementProfile.Flyer));
            Assert.AreEqual(int.MaxValue, HeightTraversalService.MaxDescendHeight(MovementProfile.Flyer));
        }

        // ──────────── 5 参数便捷重载 ────────────

        [Test]
        public void ScalarOverload_MatchesStructOverload()
        {
            Assert.IsTrue(HeightTraversalService.CanTraverse(
                new HeightLevel(0), new HeightLevel(1), false, 1, 2));
            Assert.IsFalse(HeightTraversalService.CanTraverse(
                new HeightLevel(0), new HeightLevel(2), false, 1, 2));
        }

        // ──────────── 排序稳定 ────────────

        [Test]
        public void SortByHeightAscending_OrdersByHeightThenYThenX()
        {
            var input = new List<KeyValuePair<GridCoord, HeightLevel>>
            {
                new KeyValuePair<GridCoord, HeightLevel>(new GridCoord(3, 1), new HeightLevel(2)),
                new KeyValuePair<GridCoord, HeightLevel>(new GridCoord(1, 0), new HeightLevel(0)),
                new KeyValuePair<GridCoord, HeightLevel>(new GridCoord(2, 0), new HeightLevel(1)),
                new KeyValuePair<GridCoord, HeightLevel>(new GridCoord(0, 0), new HeightLevel(0)),
            };
            var sorted = HeightTraversalService.SortByHeightAscending(input);

            Assert.AreEqual(4, sorted.Count);
            Assert.AreEqual(0, sorted[0].Value.Value);
            Assert.AreEqual(new GridCoord(0, 0), sorted[0].Key);
            Assert.AreEqual(0, sorted[1].Value.Value);
            Assert.AreEqual(new GridCoord(1, 0), sorted[1].Key);
            Assert.AreEqual(1, sorted[2].Value.Value);
            Assert.AreEqual(new GridCoord(2, 0), sorted[2].Key);
            Assert.AreEqual(2, sorted[3].Value.Value);
            Assert.AreEqual(new GridCoord(3, 1), sorted[3].Key);
        }

        [Test]
        public void SortByHeightAscending_EmptyInput_ReturnsEmpty()
        {
            var sorted = HeightTraversalService.SortByHeightAscending(
                new List<KeyValuePair<GridCoord, HeightLevel>>());
            Assert.AreEqual(0, sorted.Count);
        }

        [Test]
        public void SortByHeightAscending_NullInput_Throws()
        {
            Assert.Throws<System.ArgumentNullException>(
                () => HeightTraversalService.SortByHeightAscending(null));
        }
    }
}
