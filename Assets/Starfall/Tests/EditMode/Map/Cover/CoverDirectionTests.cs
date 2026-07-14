using NUnit.Framework;
using Starfall.Core.Map.Coordinates;
using Starfall.Core.Map.Cover;

namespace Starfall.Tests.EditMode.Map.Cover
{
    /// <summary>
    /// <see cref="CoverDirection"/> 行为测试（≥ 6 项）。
    /// 验证固定顺序与数值（AGENTS.md §11）。
    /// </summary>
    public class CoverDirectionTests
    {
        [Test]
        public void EnumValues_FixedOrder()
        {
            var values = System.Enum.GetValues(typeof(CoverDirection));
            Assert.AreEqual(5, values.Length);
            Assert.AreEqual(CoverDirection.North, values.GetValue(0));
            Assert.AreEqual(CoverDirection.East, values.GetValue(1));
            Assert.AreEqual(CoverDirection.South, values.GetValue(2));
            Assert.AreEqual(CoverDirection.West, values.GetValue(3));
            Assert.AreEqual(CoverDirection.All, values.GetValue(4));
        }

        [Test]
        public void EnumValues_NumericValues_AreZeroToFour()
        {
            Assert.AreEqual(0, (byte)CoverDirection.North);
            Assert.AreEqual(1, (byte)CoverDirection.East);
            Assert.AreEqual(2, (byte)CoverDirection.South);
            Assert.AreEqual(3, (byte)CoverDirection.West);
            Assert.AreEqual(4, (byte)CoverDirection.All);
        }

        [Test]
        public void ComputeAttackDirection_North_North()
        {
            // attacker 在 defender 北 → CoverDirection.North
            var atk = new GridCoord(5, 6, DimensionLayer.Reality);
            var def = new GridCoord(5, 5, DimensionLayer.Reality);
            Assert.AreEqual(CoverDirection.North, CoverQueryService.ComputeAttackDirection(atk, def));
        }

        [Test]
        public void ComputeAttackDirection_South_South()
        {
            var atk = new GridCoord(5, 4, DimensionLayer.Reality);
            var def = new GridCoord(5, 5, DimensionLayer.Reality);
            Assert.AreEqual(CoverDirection.South, CoverQueryService.ComputeAttackDirection(atk, def));
        }

        [Test]
        public void ComputeAttackDirection_East_East()
        {
            var atk = new GridCoord(6, 5, DimensionLayer.Reality);
            var def = new GridCoord(5, 5, DimensionLayer.Reality);
            Assert.AreEqual(CoverDirection.East, CoverQueryService.ComputeAttackDirection(atk, def));
        }

        [Test]
        public void ComputeAttackDirection_West_West()
        {
            var atk = new GridCoord(4, 5, DimensionLayer.Reality);
            var def = new GridCoord(5, 5, DimensionLayer.Reality);
            Assert.AreEqual(CoverDirection.West, CoverQueryService.ComputeAttackDirection(atk, def));
        }

        [Test]
        public void ComputeAttackDirection_SameTile_All()
        {
            var atk = new GridCoord(5, 5, DimensionLayer.Reality);
            var def = new GridCoord(5, 5, DimensionLayer.Reality);
            Assert.AreEqual(CoverDirection.All, CoverQueryService.ComputeAttackDirection(atk, def));
        }

        [Test]
        public void ComputeAttackDirection_DiagonalXDominant_East()
        {
            // |dx| = 2, |dy| = 1 → 主方向 X → East
            var atk = new GridCoord(7, 6, DimensionLayer.Reality);
            var def = new GridCoord(5, 5, DimensionLayer.Reality);
            Assert.AreEqual(CoverDirection.East, CoverQueryService.ComputeAttackDirection(atk, def));
        }

        [Test]
        public void ComputeAttackDirection_DiagonalYDominant_North()
        {
            // |dx| = 1, |dy| = 2 → 主方向 Y → North
            var atk = new GridCoord(6, 7, DimensionLayer.Reality);
            var def = new GridCoord(5, 5, DimensionLayer.Reality);
            Assert.AreEqual(CoverDirection.North, CoverQueryService.ComputeAttackDirection(atk, def));
        }

        [Test]
        public void ComputeAttackDirection_DiagonalEqual_XTiebreak()
        {
            // |dx| = |dy| = 1 → X 优先 → East
            var atk = new GridCoord(6, 6, DimensionLayer.Reality);
            var def = new GridCoord(5, 5, DimensionLayer.Reality);
            Assert.AreEqual(CoverDirection.East, CoverQueryService.ComputeAttackDirection(atk, def));
        }

        [Test]
        public void ComputeAttackDirection_DiagonalEqual_XTiebreak_West()
        {
            // |dx| = |dy| = 1，attacker 在 defender 西 → West
            var atk = new GridCoord(4, 6, DimensionLayer.Reality);
            var def = new GridCoord(5, 5, DimensionLayer.Reality);
            Assert.AreEqual(CoverDirection.West, CoverQueryService.ComputeAttackDirection(atk, def));
        }

        [Test]
        public void ComputeAttackDirection_CrossLayer_Throws()
        {
            var atk = new GridCoord(5, 6, DimensionLayer.Astral);
            var def = new GridCoord(5, 5, DimensionLayer.Reality);
            Assert.Throws<System.ArgumentException>(
                () => CoverQueryService.ComputeAttackDirection(atk, def));
        }
    }
}
