using System.Collections.Generic;
using NUnit.Framework;
using Starfall.Core.Anchor;
using Starfall.Core.Decree;
using Starfall.Core.Model;

namespace Starfall.Tests.EditMode
{
    public class AnchorAndDecreeTests
    {
        [Test]
        public void AnchorZone_VerticesSorted()
        {
            var z = new AnchorZone(1, "Player", new[] {
                new GridPos(3, 1), new GridPos(1, 0), new GridPos(2, 2)
            });
            // 期望顺序 (1,0), (3,1), (2,2)
            Assert.AreEqual(new GridPos(1, 0), z.Vertices[0]);
            Assert.AreEqual(new GridPos(3, 1), z.Vertices[1]);
            Assert.AreEqual(new GridPos(2, 2), z.Vertices[2]);
        }

        [Test]
        public void AnchorZone_ContainsInside()
        {
            // 矩形 (0,0)-(2,0)-(2,2)-(0,2)
            var z = new AnchorZone(1, "Player", new[] {
                new GridPos(0, 0), new GridPos(2, 0), new GridPos(2, 2), new GridPos(0, 2)
            });
            Assert.IsTrue(z.Contains(new GridPos(1, 1)));
        }

        [Test]
        public void AnchorZone_RejectsOutside()
        {
            var z = new AnchorZone(1, "Player", new[] {
                new GridPos(0, 0), new GridPos(2, 0), new GridPos(2, 2), new GridPos(0, 2)
            });
            Assert.IsFalse(z.Contains(new GridPos(5, 5)));
        }

        [Test]
        public void AnchorRegistry_RegisterAndGet()
        {
            var r = new AnchorRegistry();
            var z = new AnchorZone(7, "Enemy", new[] {
                new GridPos(0, 0), new GridPos(1, 0), new GridPos(0, 1)
            });
            r.Register(z);
            Assert.AreSame(z, r.Get(7));
            Assert.IsNull(r.Get(999));
        }

        [Test]
        public void DecreeRegistry_IssueAndRevoke()
        {
            var r = new DecreeRegistry();
            var d = new Decree(1, DecreeKind.Hold, 7, 3, Owner.Player);
            r.Issue(d);
            Assert.AreEqual(1, r.DecreesInOrder.Count);
            Assert.IsTrue(r.Revoke(1));
            Assert.AreEqual(0, r.DecreesInOrder.Count);
            Assert.IsFalse(r.Revoke(999));
        }

        [Test]
        public void DecreeRegistry_OrdersByDecreeId()
        {
            var r = new DecreeRegistry();
            r.Issue(new Decree(3, DecreeKind.Hold, 1, 1, Owner.Player));
            r.Issue(new Decree(1, DecreeKind.Push, 2, 1, Owner.Enemy));
            r.Issue(new Decree(2, DecreeKind.Retreat, 3, 1, Owner.Player));
            Assert.AreEqual(1, r.DecreesInOrder[0].DecreeId);
            Assert.AreEqual(2, r.DecreesInOrder[1].DecreeId);
            Assert.AreEqual(3, r.DecreesInOrder[2].DecreeId);
        }

        [Test]
        public void BattleState_HashChangesWithAnchor()
        {
            var s = new BattleState(0, Owner.Player,
                new BoardState(4, 4, new Dictionary<GridPos, TileState>()), null);
            ulong h1 = s.PostStateHash;
            s.Anchors.Register(new AnchorZone(1, "Player", new[] {
                new GridPos(0, 0), new GridPos(2, 0), new GridPos(2, 2), new GridPos(0, 2)
            }));
            ulong h2 = s.PostStateHash;
            Assert.AreNotEqual(h1, h2);
        }

        [Test]
        public void BattleState_HashChangesWithDecree()
        {
            var s = new BattleState(0, Owner.Player,
                new BoardState(4, 4, new Dictionary<GridPos, TileState>()), null);
            ulong h1 = s.PostStateHash;
            s.Decrees.Issue(new Decree(1, DecreeKind.Hold, 1, 3, Owner.Player));
            ulong h2 = s.PostStateHash;
            Assert.AreNotEqual(h1, h2);
        }
    }
}
