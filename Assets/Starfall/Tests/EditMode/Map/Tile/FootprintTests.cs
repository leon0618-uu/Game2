using System.Collections.Generic;
using NUnit.Framework;
using Starfall.Core.Map.Coordinates;
using Starfall.Core.Map.Tile;

namespace Starfall.Tests.EditMode.Map.Tile
{
    /// <summary>
    /// doc2 MAP-04 §4.3 Footprint 测试集。
    /// 覆盖：3 种形状的格点枚举、顺序、越界、跨 Layer、稳定排序。
    /// </summary>
    public class FootprintTests
    {
        private static MapSize StandardSize => new MapSize(8, 8);

        // ──────────── 1. SingleCell 1 格 ────────────

        [Test]
        public void SingleCell_AtAnchor_ReturnsOnlyAnchor()
        {
            var cells = FootprintExtensions.GetOccupiedCells(
                Footprint.SingleCell,
                new GridCoord(2, 3),
                StandardSize);
            Assert.AreEqual(1, cells.Count);
            Assert.AreEqual(new GridCoord(2, 3), cells[0]);
        }

        // ──────────── 2. TwoByTwo 4 格 + 顺序 ────────────

        [Test]
        public void TwoByTwo_AtAnchor11_ReturnsFourCellsInYThenXOrder()
        {
            var cells = FootprintExtensions.GetOccupiedCells(
                Footprint.TwoByTwo,
                new GridCoord(1, 1),
                StandardSize);
            Assert.AreEqual(4, cells.Count);
            Assert.AreEqual(new GridCoord(1, 1), cells[0]);
            Assert.AreEqual(new GridCoord(2, 1), cells[1]);
            Assert.AreEqual(new GridCoord(1, 2), cells[2]);
            Assert.AreEqual(new GridCoord(2, 2), cells[3]);
        }

        // ──────────── 3. ThreeByThree 9 格 + 顺序 ────────────

        [Test]
        public void ThreeByThree_AtOrigin_ReturnsNineCellsInYThenXOrder()
        {
            var cells = FootprintExtensions.GetOccupiedCells(
                Footprint.ThreeByThree,
                new GridCoord(0, 0),
                StandardSize);
            Assert.AreEqual(9, cells.Count);
            // (0,0) (1,0) (2,0) (0,1) (1,1) (2,1) (0,2) (1,2) (2,2)
            Assert.AreEqual(new GridCoord(0, 0), cells[0]);
            Assert.AreEqual(new GridCoord(1, 0), cells[1]);
            Assert.AreEqual(new GridCoord(2, 0), cells[2]);
            Assert.AreEqual(new GridCoord(0, 1), cells[3]);
            Assert.AreEqual(new GridCoord(1, 1), cells[4]);
            Assert.AreEqual(new GridCoord(2, 1), cells[5]);
            Assert.AreEqual(new GridCoord(0, 2), cells[6]);
            Assert.AreEqual(new GridCoord(1, 2), cells[7]);
            Assert.AreEqual(new GridCoord(2, 2), cells[8]);
        }

        // ──────────── 4. 越界抛 ArgumentOutOfRangeException ────────────

        [Test]
        public void TwoByTwo_OutOfBounds_Throws()
        {
            // anchor = (7, 1) → (7,1) (8,1) (7,2) (8,2) — X=8 越界 8x8 地图。
            Assert.Throws<System.ArgumentOutOfRangeException>(() =>
                FootprintExtensions.GetOccupiedCells(
                    Footprint.TwoByTwo,
                    new GridCoord(7, 1),
                    StandardSize));
        }

        [Test]
        public void ThreeByThree_OutOfBounds_Throws()
        {
            // anchor = (6, 6) → (6,6) (7,6) (8,6) (6,7) (7,7) (8,7) (6,8) (7,8) (8,8)
            Assert.Throws<System.ArgumentOutOfRangeException>(() =>
                FootprintExtensions.GetOccupiedCells(
                    Footprint.ThreeByThree,
                    new GridCoord(6, 6),
                    StandardSize));
        }

        [Test]
        public void NegativeAnchor_OutOfBounds_Throws()
        {
            Assert.Throws<System.ArgumentOutOfRangeException>(() =>
                FootprintExtensions.GetOccupiedCells(
                    Footprint.SingleCell,
                    new GridCoord(-1, 0),
                    StandardSize));
        }

        // ──────────── 5. 跨 Layer 共享 anchor.Layer ────────────

        [Test]
        public void Footprint_PreservesAnchorLayer()
        {
            var cells = FootprintExtensions.GetOccupiedCells(
                Footprint.TwoByTwo,
                new GridCoord(1, 1, DimensionLayer.Astral),
                StandardSize);
            Assert.AreEqual(4, cells.Count);
            foreach (var c in cells)
            {
                Assert.AreEqual(DimensionLayer.Astral, c.Layer,
                    $"Footprint cell {c} should preserve anchor Layer = Astral.");
            }
        }

        // ──────────── 6. 稳定排序（结果按 Y→X 升序）────────────

        [Test]
        public void Footprint_ResultIsSortedByYThenX()
        {
            var cells = FootprintExtensions.GetOccupiedCells(
                Footprint.ThreeByThree,
                new GridCoord(2, 2),
                StandardSize);
            // 验证返回列表已按 (Y, X) 升序排序。
            for (int i = 1; i < cells.Count; i++)
            {
                int cmp = cells[i - 1].CompareTo(cells[i]);
                Assert.Less(cmp, 0, $"Cells[{i - 1}] = {cells[i - 1]} should come before Cells[{i}] = {cells[i]}.");
            }
        }

        // ──────────── 7. CanPlace 预检 ────────────

        [Test]
        public void CanPlace_WithinBounds_ReturnsTrue()
        {
            Assert.IsTrue(FootprintExtensions.CanPlace(Footprint.SingleCell, new GridCoord(0, 0), StandardSize));
            Assert.IsTrue(FootprintExtensions.CanPlace(Footprint.TwoByTwo, new GridCoord(6, 6), StandardSize));
            Assert.IsTrue(FootprintExtensions.CanPlace(Footprint.ThreeByThree, new GridCoord(5, 5), StandardSize));
        }

        [Test]
        public void CanPlace_OutOfBounds_ReturnsFalse()
        {
            Assert.IsFalse(FootprintExtensions.CanPlace(Footprint.TwoByTwo, new GridCoord(7, 0), StandardSize));
            Assert.IsFalse(FootprintExtensions.CanPlace(Footprint.ThreeByThree, new GridCoord(6, 0), StandardSize));
            Assert.IsFalse(FootprintExtensions.CanPlace(Footprint.SingleCell, new GridCoord(-1, 0), StandardSize));
        }

        // ──────────── 8. GetSideLength ────────────

        [Test]
        public void GetSideLength_ForEachFootprint()
        {
            Assert.AreEqual(1, FootprintExtensions.GetSideLength(Footprint.SingleCell));
            Assert.AreEqual(2, FootprintExtensions.GetSideLength(Footprint.TwoByTwo));
            Assert.AreEqual(3, FootprintExtensions.GetSideLength(Footprint.ThreeByThree));
        }

        // ──────────── 9. 数值（byte 值 = 格数）────────────

        [Test]
        public void Footprint_ByteValue_EqualsCellCount()
        {
            Assert.AreEqual(1, (byte)Footprint.SingleCell);
            Assert.AreEqual(4, (byte)Footprint.TwoByTwo);
            Assert.AreEqual(9, (byte)Footprint.ThreeByThree);
        }
    }
}