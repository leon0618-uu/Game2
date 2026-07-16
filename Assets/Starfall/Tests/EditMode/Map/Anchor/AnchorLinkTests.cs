using NUnit.Framework;
using Starfall.Core.Anchor;
using Starfall.Core.Map.Anchor;
using Starfall.Core.Map.Coordinates;

namespace Starfall.Tests.EditMode.Map.Anchor
{
    /// <summary>
    /// doc2 MAP-12 <see cref="AnchorLink"/> 测试集。
    /// <para/>
    /// 覆盖：TransitionTo / 状态变更 / hash 跟随状态变化 / 不可变字段（Id 不可改，Polygon 可更新）。
    /// <para/>
    /// **per ADR-0009 §9**：PostStateHash 由 <see cref="AnchorLinkHasher.ComputeStateHash"/>
    /// 自动从 (state, tick) 计算；测试断言 PostStateHash 与 (state, tick) 派生值一致。
    /// </summary>
    public class AnchorLinkTests
    {
        private static ConstellationPolygon MakePolygon(string id = "poly-default")
        {
            return new ConstellationPolygon(
                new ConstellationPolygonId(id),
                new System.Collections.Generic.List<ConstellationVertex>
                {
                    new ConstellationVertex(0, 0, DimensionLayer.Reality),
                    new ConstellationVertex(4, 0, DimensionLayer.Reality),
                    new ConstellationVertex(0, 4, DimensionLayer.Reality),
                });
        }

        private static AnchorLink MakeLink(string id = "link-1",
            AnchorZoneState initialState = AnchorZoneState.Inactive,
            int initialTick = 0)
        {
            return new AnchorLink(new AnchorLinkId(id), MakePolygon("poly-" + id),
                initialState: initialState, initialTick: initialTick);
        }

        // ──────────── 构造 ────────────

        [Test]
        public void Construct_StoresIdAndPolygonAndState()
        {
            var link = MakeLink();
            Assert.AreEqual("link-1", link.Id.Value);
            Assert.AreEqual("poly-link-1", link.Polygon.Id.Value);
            Assert.AreEqual(AnchorZoneState.Inactive, link.CurrentState);
            Assert.AreEqual(AnchorZoneState.Inactive, link.InitialState);
            Assert.AreEqual(0, link.StateTick);
            // PostStateHash 由 ComputeStateHash(Inactive, 0) 计算
            ulong expectedHash = AnchorLinkHasher.ComputeStateHash(link);
            Assert.AreEqual(expectedHash, link.PostStateHash);
        }

        [Test]
        public void Construct_NegativeTick_Throws()
        {
            Assert.Throws<System.ArgumentOutOfRangeException>(() =>
                new AnchorLink(new AnchorLinkId("link-bad"), MakePolygon(), initialTick: -1));
        }

        // ──────────── TransitionTo ────────────

        [Test]
        public void TransitionTo_LegalTransition_UpdatesStateAndTick()
        {
            var link = MakeLink();
            link.TransitionTo(AnchorZoneState.PlayerControlled, 5);
            Assert.AreEqual(AnchorZoneState.PlayerControlled, link.CurrentState);
            Assert.AreEqual(5, link.StateTick);
            // PostStateHash 由 ComputeStateHash(PlayerControlled, 5) 自动计算
            ulong expectedHash = AnchorLinkHasher.ComputeStateHash(link);
            Assert.AreEqual(expectedHash, link.PostStateHash);
        }

        [Test]
        public void TransitionTo_SameState_LegalNoOp()
        {
            var link = MakeLink();
            // 同状态自迁移总是合法
            link.TransitionTo(AnchorZoneState.Inactive, 1);
            Assert.AreEqual(AnchorZoneState.Inactive, link.CurrentState);
            Assert.AreEqual(1, link.StateTick);
        }

        [Test]
        public void TransitionTo_IllegalTransition_Throws()
        {
            // Inactive → Destroyed 不在合法矩阵里
            var link = MakeLink();
            Assert.Throws<InvalidAnchorLinkTransitionException>(() =>
                link.TransitionTo(AnchorZoneState.Destroyed, 1));
        }

        [Test]
        public void TransitionTo_NegativeTick_Throws()
        {
            var link = MakeLink();
            Assert.Throws<System.ArgumentOutOfRangeException>(() =>
                link.TransitionTo(AnchorZoneState.PlayerControlled, -1));
        }

        [Test]
        public void TransitionTo_StateMachineMatrix_DestroyedOnlyToInactiveOrLocked()
        {
            // Damaged → Destroyed 合法
            var link = MakeLink(initialState: AnchorZoneState.Damaged);
            link.TransitionTo(AnchorZoneState.Destroyed, 1);
            Assert.AreEqual(AnchorZoneState.Destroyed, link.CurrentState);

            // Destroyed → PlayerControlled 不合法
            Assert.Throws<InvalidAnchorLinkTransitionException>(() =>
                link.TransitionTo(AnchorZoneState.PlayerControlled, 2));

            // Destroyed → Inactive 合法（重建）
            link.TransitionTo(AnchorZoneState.Inactive, 3);
            Assert.AreEqual(AnchorZoneState.Inactive, link.CurrentState);
        }

        [Test]
        public void TransitionTo_HashUpdatesOnStateChange()
        {
            // PostStateHash 仅依赖 (state, tick)：状态改变 → hash 必变
            var link = MakeLink();
            ulong h0 = link.PostStateHash;
            link.TransitionTo(AnchorZoneState.PlayerControlled, 1);
            Assert.AreNotEqual(h0, link.PostStateHash);
            // 校验 hash 实际值
            Assert.AreEqual(AnchorLinkHasher.ComputeStateHash(link), link.PostStateHash);
        }

        // ──────────── UpdatePolygon ────────────

        [Test]
        public void UpdatePolygon_ReplacesPolygon()
        {
            var link = MakeLink();
            var newPoly = new ConstellationPolygon(
                new ConstellationPolygonId("poly-new"),
                new System.Collections.Generic.List<ConstellationVertex>
                {
                    new ConstellationVertex(2, 2, DimensionLayer.Reality),
                    new ConstellationVertex(6, 2, DimensionLayer.Reality),
                    new ConstellationVertex(2, 6, DimensionLayer.Reality),
                });
            link.UpdatePolygon(newPoly);
            Assert.AreEqual("poly-new", link.Polygon.Id.Value);
            // PostStateHash 不变（仅依赖 state + tick）
            ulong expectedHash = AnchorLinkHasher.ComputeStateHash(link);
            Assert.AreEqual(expectedHash, link.PostStateHash);
        }

        // ──────────── 不可变字段（Id）────────────

        [Test]
        public void Id_IsImmutableString()
        {
            var link = MakeLink("link-immutable");
            // AnchorLinkId 是 readonly struct；构造后无法改 Value。
            Assert.AreEqual("link-immutable", link.Id.Value);
        }

        [Test]
        public void ToString_ContainsCoreFields()
        {
            var link = MakeLink();
            string s = link.ToString();
            StringAssert.Contains("AnchorLink", s);
            StringAssert.Contains("link-1", s);
            StringAssert.Contains("Inactive", s);
        }

        [Test]
        public void AnchorLinkStateMachine_LegalTransitions_ForEachState()
        {
            // 对每个 from state，至少存在 1 个 to state 合法（验证矩阵不空）
            Assert.IsTrue(AnchorLinkStateMachine.IsLegalTransition(AnchorZoneState.Inactive, AnchorZoneState.PlayerControlled));
            Assert.IsTrue(AnchorLinkStateMachine.IsLegalTransition(AnchorZoneState.Destroyed, AnchorZoneState.Inactive));
            Assert.IsTrue(AnchorLinkStateMachine.IsLegalTransition(AnchorZoneState.Locked, AnchorZoneState.Destroyed));
        }

        [Test]
        public void AnchorLinkStateMachine_AllowedTargets_NonEmpty()
        {
            foreach (AnchorZoneState s in System.Enum.GetValues(typeof(AnchorZoneState)))
            {
                var targets = AnchorLinkStateMachine.AllowedTargets(s);
                Assert.IsNotNull(targets);
            }
        }

        [Test]
        public void InvalidAnchorLinkTransitionException_ContainsInfo()
        {
            var ex = new InvalidAnchorLinkTransitionException(
                new AnchorLinkId("link-x"),
                AnchorZoneState.Inactive,
                AnchorZoneState.Destroyed);
            Assert.AreEqual("link-x", ex.LinkId.Value);
            Assert.AreEqual(AnchorZoneState.Inactive, ex.FromState);
            Assert.AreEqual(AnchorZoneState.Destroyed, ex.ToState);
            StringAssert.Contains("link-x", ex.Message);
        }
    }
}