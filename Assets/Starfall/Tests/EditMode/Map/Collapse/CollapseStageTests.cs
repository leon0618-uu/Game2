using NUnit.Framework;
using Starfall.Core.Map.Collapse;

namespace Starfall.Tests.EditMode.Map.Collapse
{
    /// <summary>
    /// doc2 MAP-11a <see cref="CollapseStage"/> 测试集（≥ 6 测试）。
    /// 覆盖：5 阶段范围映射、FromValue 边界、MinValue / MaxValue 闭区间。
    /// </summary>
    public class CollapseStageTests
    {
        // ──────────── 1-5) 5 阶段范围映射 ────────────

        [TestCase(0, CollapseStage.Stable)]
        [TestCase(10, CollapseStage.Stable)]
        [TestCase(19, CollapseStage.Stable)]
        [TestCase(20, CollapseStage.Anomalous)]
        [TestCase(30, CollapseStage.Anomalous)]
        [TestCase(39, CollapseStage.Anomalous)]
        [TestCase(40, CollapseStage.Fracturing)]
        [TestCase(50, CollapseStage.Fracturing)]
        [TestCase(59, CollapseStage.Fracturing)]
        [TestCase(60, CollapseStage.Collapsing)]
        [TestCase(70, CollapseStage.Collapsing)]
        [TestCase(79, CollapseStage.Collapsing)]
        [TestCase(80, CollapseStage.GateFault)]
        [TestCase(100, CollapseStage.GateFault)]
        public void FromValue_ReturnsCorrectStage(int cv, CollapseStage expected)
        {
            Assert.AreEqual(expected, CollapseStageMapping.FromValue(cv));
        }

        [Test]
        public void FromValue_NegativeInput_ClampsToZero()
        {
            Assert.AreEqual(CollapseStage.Stable, CollapseStageMapping.FromValue(-1));
            Assert.AreEqual(CollapseStage.Stable, CollapseStageMapping.FromValue(-100));
            Assert.AreEqual(CollapseStage.Stable, CollapseStageMapping.FromValue(int.MinValue));
        }

        [Test]
        public void FromValue_OverHundred_ClampsToHundred()
        {
            Assert.AreEqual(CollapseStage.GateFault, CollapseStageMapping.FromValue(101));
            Assert.AreEqual(CollapseStage.GateFault, CollapseStageMapping.FromValue(1000));
            Assert.AreEqual(CollapseStage.GateFault, CollapseStageMapping.FromValue(int.MaxValue));
        }

        // ──────────── 6) MinValue / MaxValue 闭区间 ────────────

        [Test]
        public void MinValue_ReturnsExpected()
        {
            Assert.AreEqual(0, CollapseStageMapping.MinValue(CollapseStage.Stable));
            Assert.AreEqual(20, CollapseStageMapping.MinValue(CollapseStage.Anomalous));
            Assert.AreEqual(40, CollapseStageMapping.MinValue(CollapseStage.Fracturing));
            Assert.AreEqual(60, CollapseStageMapping.MinValue(CollapseStage.Collapsing));
            Assert.AreEqual(80, CollapseStageMapping.MinValue(CollapseStage.GateFault));
        }

        [Test]
        public void MaxValue_ReturnsExpected()
        {
            Assert.AreEqual(19, CollapseStageMapping.MaxValue(CollapseStage.Stable));
            Assert.AreEqual(39, CollapseStageMapping.MaxValue(CollapseStage.Anomalous));
            Assert.AreEqual(59, CollapseStageMapping.MaxValue(CollapseStage.Fracturing));
            Assert.AreEqual(79, CollapseStageMapping.MaxValue(CollapseStage.Collapsing));
            Assert.AreEqual(100, CollapseStageMapping.MaxValue(CollapseStage.GateFault));
        }

        [Test]
        public void MinMaxValue_AreContiguous_NoGaps()
        {
            // 验证 MinValue(stage+1) == MaxValue(stage) + 1（无 gap、无 overlap）
            var stages = new[] {
                CollapseStage.Stable, CollapseStage.Anomalous,
                CollapseStage.Fracturing, CollapseStage.Collapsing, CollapseStage.GateFault
            };
            for (int i = 0; i < stages.Length - 1; i++)
            {
                int maxCur = CollapseStageMapping.MaxValue(stages[i]);
                int minNext = CollapseStageMapping.MinValue(stages[i + 1]);
                Assert.AreEqual(maxCur + 1, minNext,
                    $"Gap or overlap between {stages[i]} (max={maxCur}) and {stages[i+1]} (min={minNext})");
            }
        }

        [Test]
        public void MinValue_MaxValue_CoverFullRange()
        {
            int total = 0;
            var stages = new[] {
                CollapseStage.Stable, CollapseStage.Anomalous,
                CollapseStage.Fracturing, CollapseStage.Collapsing, CollapseStage.GateFault
            };
            foreach (var s in stages)
            {
                total += CollapseStageMapping.MaxValue(s) - CollapseStageMapping.MinValue(s) + 1;
            }
            Assert.AreEqual(101, total, "Stages must cover [0, 100] inclusive.");
        }

        [Test]
        public void FromValue_AtBoundary_IncludesUpperBound()
        {
            // 上界属于该阶段（如 19 = Stable，不是 Anomalous）
            Assert.AreEqual(CollapseStage.Stable, CollapseStageMapping.FromValue(19));
            Assert.AreEqual(CollapseStage.Anomalous, CollapseStageMapping.FromValue(39));
            Assert.AreEqual(CollapseStage.Fracturing, CollapseStageMapping.FromValue(59));
            Assert.AreEqual(CollapseStage.Collapsing, CollapseStageMapping.FromValue(79));
            Assert.AreEqual(CollapseStage.GateFault, CollapseStageMapping.FromValue(100));
        }
    }
}
