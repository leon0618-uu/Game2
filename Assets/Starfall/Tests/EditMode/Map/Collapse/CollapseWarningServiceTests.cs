using NUnit.Framework;
using Starfall.Core.Map.Collapse;
using Starfall.Core.Map.Coordinates;
using Starfall.Core.Map.State;

namespace Starfall.Tests.EditMode.Map.Collapse
{
    /// <summary>
    /// doc2 MAP-11a <see cref="CollapseWarningService"/> 测试集（≥ 8 测试）。
    /// 覆盖：4 等级评估、ShouldWarn 阈值、GetHotspots 排序。
    /// </summary>
    public class CollapseWarningServiceTests
    {
        private static MapState MakeMap(int initialCV = 0)
        {
            return new MapState(new MapDefinition("map.test", 8, 8,
                DimensionLayer.Reality, initialCV));
        }

        // ──────────── 1) 阈值常量正确 ────────────

        [Test]
        public void ThresholdConstants_AreCorrect()
        {
            Assert.AreEqual(40, CollapseWarningService.CautionThreshold);
            Assert.AreEqual(60, CollapseWarningService.DangerThreshold);
            Assert.AreEqual(80, CollapseWarningService.CriticalThreshold);
        }

        // ──────────── 2) 4 等级评估（边界）────────────

        [TestCase(0, CollapseWarningLevel.None)]
        [TestCase(20, CollapseWarningLevel.None)]
        [TestCase(39, CollapseWarningLevel.None)]
        [TestCase(40, CollapseWarningLevel.Caution)]
        [TestCase(50, CollapseWarningLevel.Caution)]
        [TestCase(59, CollapseWarningLevel.Caution)]
        [TestCase(60, CollapseWarningLevel.Danger)]
        [TestCase(70, CollapseWarningLevel.Danger)]
        [TestCase(79, CollapseWarningLevel.Danger)]
        [TestCase(80, CollapseWarningLevel.Critical)]
        [TestCase(100, CollapseWarningLevel.Critical)]
        public void EvaluateWarningLevel_ReturnsCorrectLevel(int cv, CollapseWarningLevel expected)
        {
            var gcv = GlobalCollapseValue.Of(cv);
            Assert.AreEqual(expected, new CollapseWarningService().EvaluateWarningLevel(gcv));
        }

        // ──────────── 3) ShouldWarn 阈值检测 ────────────

        [Test]
        public void ShouldWarn_DefaultThreshold_BelowCaution_ReturnsFalse()
        {
            var map = MakeMap(0);
            Assert.IsFalse(new CollapseWarningService().ShouldWarn(map));
        }

        [Test]
        public void ShouldWarn_DefaultThreshold_AtCaution_ReturnsTrue()
        {
            var map = MakeMap(40);
            Assert.IsTrue(new CollapseWarningService().ShouldWarn(map));
        }

        [Test]
        public void ShouldWarn_CustomThreshold_Below()
        {
            var map = MakeMap(50);
            var svc = new CollapseWarningService();
            Assert.IsFalse(svc.ShouldWarn(map, threshold: 60));
            Assert.IsTrue(svc.ShouldWarn(map, threshold: 40));
        }

        [Test]
        public void ShouldWarn_InvalidThreshold_Throws()
        {
            var map = MakeMap(0);
            var svc = new CollapseWarningService();
            Assert.Throws<System.ArgumentOutOfRangeException>(() => svc.ShouldWarn(map, threshold: -1));
            Assert.Throws<System.ArgumentOutOfRangeException>(() => svc.ShouldWarn(map, threshold: 101));
        }

        // ──────────── 4) ShouldWarnOnTransition ────────────

        [Test]
        public void ShouldWarnOnTransition_DetectsCrossing()
        {
            var svc = new CollapseWarningService();
            Assert.IsTrue(svc.ShouldWarnOnTransition(39, 40, threshold: 40),
                "39 → 40 跨过 Caution 阈值");
            Assert.IsTrue(svc.ShouldWarnOnTransition(0, 80, threshold: 80),
                "0 → 80 跨过 Critical 阈值");
            Assert.IsFalse(svc.ShouldWarnOnTransition(50, 60, threshold: 40),
                "已 >= threshold, 不算'跨越'");
            Assert.IsFalse(svc.ShouldWarnOnTransition(40, 39, threshold: 40),
                "下降不算跨越");
        }

        // ──────────── 5) GetHotspots ────────────

        [Test]
        public void GetHotspots_ReturnsTopN_SortedByValueDesc()
        {
            var map = MakeMap();
            map.AddLocalCV(LocalCollapseValue.Of(new GridCoord(0, 0), 30));
            map.AddLocalCV(LocalCollapseValue.Of(new GridCoord(1, 0), 80));
            map.AddLocalCV(LocalCollapseValue.Of(new GridCoord(2, 0), 50));
            map.AddLocalCV(LocalCollapseValue.Of(new GridCoord(3, 0), 90));

            var svc = new CollapseWarningService();
            var top = svc.GetHotspots(map, topN: 2);
            Assert.AreEqual(2, top.Count);
            Assert.AreEqual(90, top[0].Value);
            Assert.AreEqual(80, top[1].Value);
        }

        [Test]
        public void GetHotspots_TopNZero_ReturnsAll()
        {
            var map = MakeMap();
            map.AddLocalCV(LocalCollapseValue.Of(new GridCoord(0, 0), 30));
            map.AddLocalCV(LocalCollapseValue.Of(new GridCoord(1, 0), 80));
            var svc = new CollapseWarningService();
            var top = svc.GetHotspots(map, topN: 0);
            Assert.AreEqual(2, top.Count);
        }

        [Test]
        public void GetHotspots_TopNLarger_ReturnsAll()
        {
            var map = MakeMap();
            map.AddLocalCV(LocalCollapseValue.Of(new GridCoord(0, 0), 30));
            var svc = new CollapseWarningService();
            var top = svc.GetHotspots(map, topN: 100);
            Assert.AreEqual(1, top.Count);
        }

        [Test]
        public void GetHotspots_SameValue_TieBrokenByCoord()
        {
            var map = MakeMap();
            map.AddLocalCV(LocalCollapseValue.Of(new GridCoord(2, 0), 50));
            map.AddLocalCV(LocalCollapseValue.Of(new GridCoord(0, 0), 50));
            map.AddLocalCV(LocalCollapseValue.Of(new GridCoord(1, 0), 50));
            var svc = new CollapseWarningService();
            var top = svc.GetHotspots(map, topN: 0);
            // GridCoord.CompareTo 升序: (0,0) < (1,0) < (2,0)
            Assert.AreEqual(new GridCoord(0, 0), top[0].Coord);
            Assert.AreEqual(new GridCoord(1, 0), top[1].Coord);
            Assert.AreEqual(new GridCoord(2, 0), top[2].Coord);
        }

        [Test]
        public void GetHotspots_EmptyMap_ReturnsEmpty()
        {
            var map = MakeMap();
            var svc = new CollapseWarningService();
            var top = svc.GetHotspots(map, topN: 10);
            Assert.AreEqual(0, top.Count);
        }

        [Test]
        public void GetHotspots_NegativeTopN_Throws()
        {
            var map = MakeMap();
            var svc = new CollapseWarningService();
            Assert.Throws<System.ArgumentOutOfRangeException>(() => svc.GetHotspots(map, topN: -1));
        }

        // ──────────── 6) 与 stage 联动（service 端 → warning 端）────────────

        [Test]
        public void WarningLevel_AlignsWithStage_Boundaries()
        {
            // WarningLevel 与 CollapseStage 边界必须严格对齐
            var svc = new CollapseWarningService();
            Assert.AreEqual(CollapseWarningLevel.None, svc.EvaluateWarningLevel(GlobalCollapseValue.Of(0)));
            Assert.AreEqual(CollapseWarningLevel.Caution, svc.EvaluateWarningLevel(GlobalCollapseValue.Of(40)));
            Assert.AreEqual(CollapseWarningLevel.Caution, svc.EvaluateWarningLevel(GlobalCollapseValue.Of(59)));
            Assert.AreEqual(CollapseWarningLevel.Danger, svc.EvaluateWarningLevel(GlobalCollapseValue.Of(60)));
            Assert.AreEqual(CollapseWarningLevel.Danger, svc.EvaluateWarningLevel(GlobalCollapseValue.Of(79)));
            Assert.AreEqual(CollapseWarningLevel.Critical, svc.EvaluateWarningLevel(GlobalCollapseValue.Of(80)));
        }
    }
}
