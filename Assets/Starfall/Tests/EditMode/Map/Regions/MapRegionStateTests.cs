using NUnit.Framework;
using Starfall.Core.Map.Coordinates;
using Starfall.Core.Map.Regions;

namespace Starfall.Tests.EditMode.Map.Regions
{
    /// <summary>
    /// doc2 MAP-09 <see cref="MapRegionState"/> 测试集。
    /// <para/>
    /// 覆盖：默认值 / 字段访问 / 序列化（FNV-1a 64） / 哈希稳定。
    /// </summary>
    public class MapRegionStateTests
    {
        private static MapRegionDefinition MakeDef(int regionId = 1, RegionKind kind = RegionKind.Capture,
            RegionActivation activation = RegionActivation.Available)
        {
            return new MapRegionDefinition(
                new RegionId(regionId),
                kind,
                new[] {
                    new GridCoord(0, 0), new GridCoord(2, 0),
                    new GridCoord(2, 2), new GridCoord(0, 2)
                },
                ownerSide: -1,
                priority: 50,
                activation: activation);
        }

        // ──────────── 1) 默认值校验 ────────────

        [Test]
        public void Default_StateAlignsWithActivation()
        {
            var rs = new MapRegionState(MakeDef(activation: RegionActivation.Available));
            Assert.AreEqual(RegionState.Available, rs.State);
        }

        [Test]
        public void Default_HiddenActivation_GivesHiddenState()
        {
            var rs = new MapRegionState(MakeDef(activation: RegionActivation.Hidden));
            Assert.AreEqual(RegionState.Hidden, rs.State);
        }

        [Test]
        public void Default_ActiveActivation_GivesActiveState()
        {
            var rs = new MapRegionState(MakeDef(activation: RegionActivation.Active));
            Assert.AreEqual(RegionState.Active, rs.State);
        }

        [Test]
        public void Default_DisabledActivation_GivesDisabledState()
        {
            var rs = new MapRegionState(MakeDef(activation: RegionActivation.Disabled));
            Assert.AreEqual(RegionState.Disabled, rs.State);
        }

        [Test]
        public void Default_InitialFields_AreZero()
        {
            var rs = new MapRegionState(MakeDef());
            Assert.AreEqual(0, rs.OccupantCount);
            Assert.AreEqual(0, rs.ActivationProgress);
            Assert.AreEqual(0, rs.CurrentlyOccupiedCells.Count);
            Assert.AreEqual(-1, rs.CurrentOwnerSide);  // inherited from def.OwnerSide = -1
            Assert.AreEqual(0, rs.TickEntered);
        }

        // ──────────── 2) 字段访问（read-only）────────────

        [Test]
        public void Definition_IsImmutable()
        {
            var def = MakeDef();
            var rs = new MapRegionState(def);
            Assert.AreEqual(def, rs.Definition);
        }

        [Test]
        public void CurrentlyOccupiedCells_EmptyByDefault()
        {
            var rs = new MapRegionState(MakeDef());
            Assert.AreEqual(0, rs.CurrentlyOccupiedCells.Count);
        }

        // ──────────── 3) 序列化（FNV-1a 64）────────────

        [Test]
        public void PostStateHash_Default_IsNonZero()
        {
            var rs = new MapRegionState(MakeDef());
            Assert.AreNotEqual(0UL, rs.PostStateHash);
        }

        [Test]
        public void PostStateHash_SameInput_IsStable()
        {
            var rs1 = new MapRegionState(MakeDef(7, RegionKind.Capture));
            var rs2 = new MapRegionState(MakeDef(7, RegionKind.Capture));
            Assert.AreEqual(rs1.PostStateHash, rs2.PostStateHash);
        }

        [Test]
        public void PostStateHash_DifferentKind_IsDifferent()
        {
            var a = new MapRegionState(MakeDef(1, RegionKind.Capture));
            var b = new MapRegionState(MakeDef(1, RegionKind.Defense));
            Assert.AreNotEqual(a.PostStateHash, b.PostStateHash);
        }

        [Test]
        public void PostStateHash_DifferentRegionId_IsDifferent()
        {
            var a = new MapRegionState(MakeDef(1));
            var b = new MapRegionState(MakeDef(2));
            Assert.AreNotEqual(a.PostStateHash, b.PostStateHash);
        }

        // ──────────── 4) ToString ────────────

        [Test]
        public void ToString_Contains_Kind_And_State()
        {
            var rs = new MapRegionState(MakeDef(7, RegionKind.Capture, RegionActivation.Available));
            string s = rs.ToString();
            StringAssert.Contains("7", s);
            StringAssert.Contains("Capture", s);
        }

        // ──────────── 5) IsInBounds + Coordinates Integration ────────────

        [Test]
        public void Definition_Bounds_Coordinates_AreReadable()
        {
            var def = MakeDef();
            var rs = new MapRegionState(def);
            Assert.AreEqual(4, rs.Definition.Bounds.Count);
            Assert.AreEqual(new GridCoord(0, 0), rs.Definition.Bounds[0]);
        }
    }
}