using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using Starfall.Core.Anchor;
using Starfall.Core.Map.Anchor;
using Starfall.Core.Map.Commands;
using Starfall.Core.Map.Coordinates;
using Starfall.Core.Map.State;
using Starfall.Tests.EditMode.Map.Commands;

namespace Starfall.Tests.EditMode.Map.Anchor
{
    /// <summary>
    /// doc2 MAP-12 MapState 含 AnchorLink 时 hash 稳定 / 100 次一致 / 修改后 hash 变化。
    /// </summary>
    public class Map12_HashStability_Tests
    {
        private MapState _map;
        private MapCommandExecutor _exec;

        [SetUp]
        public void SetUp()
        {
            _map = MapTestHarness.MakeMap();
            MapTestHarness.Attach(_map);
            _exec = new MapCommandExecutor();
        }

        [TearDown]
        public void TearDown()
        {
            MapTestHarness.DetachAll();
        }

        private static AnchorLink MakeLink(string linkId, string polyId)
        {
            return new AnchorLink(
                new AnchorLinkId(linkId),
                new ConstellationPolygon(
                    new ConstellationPolygonId(polyId),
                    new List<ConstellationVertex>
                    {
                        new ConstellationVertex(0, 0, DimensionLayer.Reality),
                        new ConstellationVertex(4, 0, DimensionLayer.Reality),
                        new ConstellationVertex(0, 4, DimensionLayer.Reality),
                    }));
        }

        [Test]
        public void Hash_EmptyAnchorLinks_StableAcross100Calls()
        {
            ulong h0 = _map.PostStateHash;
            for (int i = 0; i < 100; i++)
            {
                Assert.AreEqual(h0, _map.PostStateHash);
            }
        }

        [Test]
        public void Hash_WithAnchorLinks_StableAcross100Calls()
        {
            _exec.Run(new RegisterAnchorLinkCommand(MakeLink("L1", "poly-1")), _map);
            _exec.Run(new RegisterAnchorLinkCommand(MakeLink("L2", "poly-2")), _map);

            ulong h0 = _map.PostStateHash;
            for (int i = 0; i < 100; i++)
            {
                Assert.AreEqual(h0, _map.PostStateHash);
            }
        }

        [Test]
        public void Hash_RegisterAnchorLink_Changes()
        {
            ulong h0 = _map.PostStateHash;
            _exec.Run(new RegisterAnchorLinkCommand(MakeLink("L1", "poly-1")), _map);
            ulong h1 = _map.PostStateHash;
            Assert.AreNotEqual(h0, h1);
        }

        [Test]
        public void Hash_TransitionAnchorLinkState_Changes()
        {
            _exec.Run(new RegisterAnchorLinkCommand(MakeLink("L1", "poly-1")), _map);
            ulong h0 = _map.PostStateHash;
            _exec.Run(new TransitionAnchorLinkStateCommand(
                new AnchorLinkId("L1"), AnchorZoneState.PlayerControlled, 1), _map);
            ulong h1 = _map.PostStateHash;
            Assert.AreNotEqual(h0, h1);
        }

        [Test]
        public void Hash_UpdatePolygon_Changes()
        {
            _exec.Run(new RegisterAnchorLinkCommand(MakeLink("L1", "poly-1")), _map);
            ulong h0 = _map.PostStateHash;

            var newPoly = new ConstellationPolygon(
                new ConstellationPolygonId("poly-1-new"),
                new List<ConstellationVertex>
                {
                    new ConstellationVertex(2, 2, DimensionLayer.Reality),
                    new ConstellationVertex(6, 2, DimensionLayer.Reality),
                    new ConstellationVertex(2, 6, DimensionLayer.Reality),
                });
            _exec.Run(new UpdateConstellationPolygonCommand(new AnchorLinkId("L1"), newPoly), _map);
            ulong h1 = _map.PostStateHash;
            Assert.AreNotEqual(h0, h1);
        }

        [Test]
        public void Hash_UnregisterAnchorLink_ChangesBack()
        {
            _exec.Run(new RegisterAnchorLinkCommand(MakeLink("L1", "poly-1")), _map);
            ulong h0 = _map.PostStateHash;
            _exec.Run(new UnregisterAnchorLinkCommand(new AnchorLinkId("L1")), _map);
            ulong h1 = _map.PostStateHash;
            Assert.AreNotEqual(h0, h1);
            // 撤销回 h0 不可保证（因为 version 变了）；只验证 hash 确实不同
        }

        [Test]
        public void Hash_InsertionOrderIndependent()
        {
            // 两条 link 按不同顺序注册 → MapState Hash 相同（因为按 LinkId 升序混合）
            var mapA = MapTestHarness.MakeMap();
            MapTestHarness.Attach(mapA);
            var execA = new MapCommandExecutor();
            execA.Run(new RegisterAnchorLinkCommand(MakeLink("L1", "poly-1")), mapA);
            execA.Run(new RegisterAnchorLinkCommand(MakeLink("L2", "poly-2")), mapA);

            var mapB = MapTestHarness.MakeMap();
            MapTestHarness.Attach(mapB);
            var execB = new MapCommandExecutor();
            execB.Run(new RegisterAnchorLinkCommand(MakeLink("L2", "poly-2")), mapB);
            execB.Run(new RegisterAnchorLinkCommand(MakeLink("L1", "poly-1")), mapB);

            Assert.AreEqual(mapA.PostStateHash, mapB.PostStateHash);

            // cleanup
            MapTestHarness.DetachAll();
        }
    }
}