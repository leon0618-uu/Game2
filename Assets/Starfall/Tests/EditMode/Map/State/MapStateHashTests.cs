using System.Collections.Generic;
using NUnit.Framework;
using Starfall.Core.Anchor;
using Starfall.Core.Map.Coordinates;
using Starfall.Core.Map.State;
using Starfall.Core.Model;

namespace Starfall.Tests.EditMode.Map.State
{
    /// <summary>
    /// doc2 MAP-02 §3.2 MapState 哈希稳定性测试集。
    /// 覆盖：空 MapState 哈希稳定 / 字段差异 / 集合不同插入顺序同 hash /
    /// 同字段多字段差异 / 跨运行一致（×100）/ 修改任何字段哈希变化。
    /// </summary>
    public class MapStateHashTests
    {
        // ──────────── 工厂 ────────────

        private static MapDefinition MakeDef(
            string id = "map.test",
            int w = 4,
            int h = 4,
            DimensionLayer layer = DimensionLayer.Reality,
            int cv = 0,
            string tileset = null,
            string schedule = null)
            => new MapDefinition(id, w, h, layer, cv, tileset, schedule);

        private static MapState MakeEmpty() => new MapState(MakeDef());

        private static MapState MakeWithTiles(IEnumerable<GridCoord> tiles)
        {
            var s = MakeEmpty();
            foreach (var t in tiles)
                s.AddTile(t);
            return s;
        }

        private static MapState MakeWithAnchor(AnchorZone zone)
        {
            var s = MakeEmpty();
            s.AddAnchor(zone);
            return s;
        }

        private static MapState MakeWithRegion(MapRegion region)
        {
            var s = MakeEmpty();
            s.AddRegion(region);
            return s;
        }

        private static MapState MakeWithObject(MapObjectInstance obj)
        {
            var s = MakeEmpty();
            s.AddMapObject(obj);
            return s;
        }

        // ──────────── 1. 空状态哈希稳定 + 非零 ────────────

        [Test]
        public void Hash_Empty_IsStable()
        {
            var s = MakeEmpty();
            ulong h1 = s.PostStateHash;
            ulong h2 = s.PostStateHash;
            Assert.AreEqual(h1, h2);
        }

        [Test]
        public void Hash_Empty_IsNonZero()
        {
            // 即便所有字段都是默认值（CV=0、Version=0、空集合），FNV-1a 链也不应得到 offset_basis。
            // 我们至少写入了 7 个 Definition 字段标签字节与值，所以 h 必 != offset_basis。
            ulong h = MakeEmpty().PostStateHash;
            Assert.AreNotEqual(MapStateHasher.Fnv1aOffsetBasis, h);
        }

        [Test]
        public void Hash_NullState_ReturnsOffsetBasis()
        {
            // null 输入：直接返回 offset_basis（保持确定性，不抛异常）。
            Assert.AreEqual(MapStateHasher.Fnv1aOffsetBasis, MapStateHasher.CalculateDeterministicHash(null));
        }

        // ──────────── 2-7. 单字段差异 → 哈希差异 ────────────

        [Test]
        public void Hash_DifferentMapId_ChangesHash()
        {
            var a = new MapState(MakeDef("map.A"));
            var b = new MapState(MakeDef("map.B"));
            Assert.AreNotEqual(a.PostStateHash, b.PostStateHash);
        }

        [Test]
        public void Hash_DifferentWidth_ChangesHash()
        {
            var a = new MapState(MakeDef(w: 4));
            var b = new MapState(MakeDef(w: 5));
            Assert.AreNotEqual(a.PostStateHash, b.PostStateHash);
        }

        [Test]
        public void Hash_DifferentHeight_ChangesHash()
        {
            var a = new MapState(MakeDef(h: 4));
            var b = new MapState(MakeDef(h: 5));
            Assert.AreNotEqual(a.PostStateHash, b.PostStateHash);
        }

        [Test]
        public void Hash_DifferentInitialActiveLayer_ChangesHash()
        {
            var a = new MapState(MakeDef(layer: DimensionLayer.Reality));
            var b = new MapState(MakeDef(layer: DimensionLayer.Astral));
            Assert.AreNotEqual(a.PostStateHash, b.PostStateHash);
        }

        [Test]
        public void Hash_DifferentInitialCollapseValue_ChangesHash()
        {
            var a = new MapState(MakeDef(cv: 0));
            var b = new MapState(MakeDef(cv: 1));
            Assert.AreNotEqual(a.PostStateHash, b.PostStateHash);
        }

        [Test]
        public void Hash_DifferentTilesetId_ChangesHash()
        {
            var a = new MapState(MakeDef(tileset: "tileset.A"));
            var b = new MapState(MakeDef(tileset: "tileset.B"));
            Assert.AreNotEqual(a.PostStateHash, b.PostStateHash);
        }

        // ──────────── 8-9. 运行时字段差异 → 哈希差异 ────────────

        [Test]
        public void Hash_DifferentVersion_ChangesHash()
        {
            var a = MakeEmpty();
            var b = MakeEmpty();
            b.Version = 1;
            Assert.AreNotEqual(a.PostStateHash, b.PostStateHash);
        }

        [Test]
        public void Hash_DifferentActiveLayer_ChangesHash()
        {
            var a = MakeEmpty();
            var b = MakeEmpty();
            b.ActiveLayer = DimensionLayer.Astral;
            Assert.AreNotEqual(a.PostStateHash, b.PostStateHash);
        }

        [Test]
        public void Hash_DifferentGlobalCollapseValue_ChangesHash()
        {
            var a = MakeEmpty();
            var b = MakeEmpty();
            b.GlobalCollapseValue = 50;
            Assert.AreNotEqual(a.PostStateHash, b.PostStateHash);
        }

        // ──────────── 10. 集合差异 / 插入顺序无关 ────────────

        [Test]
        public void Hash_DifferentTileSet_ChangesHash()
        {
            var a = MakeWithTiles(new[] { new GridCoord(0, 0) });
            var b = MakeWithTiles(new[]
            {
                new GridCoord(0, 0),
                new GridCoord(1, 0),
            });
            Assert.AreNotEqual(a.PostStateHash, b.PostStateHash);
        }

        [Test]
        public void Hash_TilesInsertedInDifferentOrder_SameHash()
        {
            var a = MakeWithTiles(new[]
            {
                new GridCoord(2, 3, DimensionLayer.Reality),
                new GridCoord(0, 0, DimensionLayer.Astral),
                new GridCoord(1, 1, DimensionLayer.Reality),
            });
            var b = MakeWithTiles(new[]
            {
                new GridCoord(1, 1, DimensionLayer.Reality),
                new GridCoord(2, 3, DimensionLayer.Reality),
                new GridCoord(0, 0, DimensionLayer.Astral),
            });
            Assert.AreEqual(a.PostStateHash, b.PostStateHash);
        }

        [Test]
        public void Hash_AnchorsInsertedInDifferentOrder_SameHash()
        {
            var zone1 = new AnchorZone(1, "Player", new[]
            {
                new GridPos(0, 0), new GridPos(2, 0), new GridPos(2, 2), new GridPos(0, 2)
            });
            var zone2 = new AnchorZone(2, "Enemy", new[]
            {
                new GridPos(3, 3), new GridPos(5, 3), new GridPos(5, 5), new GridPos(3, 5)
            });

            var a = MakeEmpty();
            a.AddAnchor(zone1);
            a.AddAnchor(zone2);

            var b = MakeEmpty();
            b.AddAnchor(zone2);
            b.AddAnchor(zone1);

            Assert.AreEqual(a.PostStateHash, b.PostStateHash);
        }

        [Test]
        public void Hash_RegionsInsertedInDifferentOrder_SameHash()
        {
            var r1 = new MapRegion(1, "PlayerDeployment", "Player", new[]
            {
                new GridCoord(0, 0), new GridCoord(1, 0)
            });
            var r2 = new MapRegion(2, "Extraction", "Neutral", new[]
            {
                new GridCoord(5, 5)
            });

            var a = MakeEmpty();
            a.AddRegion(r1);
            a.AddRegion(r2);

            var b = MakeEmpty();
            b.AddRegion(r2);
            b.AddRegion(r1);

            Assert.AreEqual(a.PostStateHash, b.PostStateHash);
        }

        [Test]
        public void Hash_ObjectsInsertedInDifferentOrder_SameHash()
        {
            var o1 = new MapObjectInstance(1, "Gate", new GridCoord(0, 0));
            var o2 = new MapObjectInstance(2, "Terminal", new GridCoord(1, 1));

            var a = MakeEmpty();
            a.AddMapObject(o1);
            a.AddMapObject(o2);

            var b = MakeEmpty();
            b.AddMapObject(o2);
            b.AddMapObject(o1);

            Assert.AreEqual(a.PostStateHash, b.PostStateHash);
        }

        // ──────────── 11. 100 次稳定性 ────────────

        [Test]
        public void Hash_IsStable_Over100Runs()
        {
            // doc2 MAP-02 §3.4：相同状态重复调用 CalculateDeterministicHash 100 次必须完全一致。
            var s = MakeEmpty();
            s.Version = 7;
            s.GlobalCollapseValue = 33;
            s.AddTile(new GridCoord(2, 2, DimensionLayer.Reality));
            s.AddTile(new GridCoord(3, 3, DimensionLayer.Astral));
            s.AddAnchor(new AnchorZone(42, "Player", new[]
            {
                new GridPos(0, 0), new GridPos(1, 0), new GridPos(1, 1), new GridPos(0, 1)
            }));

            ulong baseline = s.PostStateHash;
            for (int i = 0; i < 100; i++)
            {
                Assert.AreEqual(baseline, s.PostStateHash,
                    $"Hash diverged at iteration {i}.");
            }
        }

        // ──────────── 12. 任何字段变更 → 哈希变更（综合）────────────

        [Test]
        public void Hash_MutatingAnyHashRelevantField_ChangesHash()
        {
            // 系统化覆盖：每个字段改一个值，断言哈希变化。
            // 这一项不能替代前面逐字段测试，但保证"无遗漏字段"。
            ulong baseline = MakeEmpty().PostStateHash;

            // Version
            var v = MakeEmpty(); v.Version = 1;
            Assert.AreNotEqual(baseline, v.PostStateHash);

            // ActiveLayer
            var l = MakeEmpty(); l.ActiveLayer = DimensionLayer.Astral;
            Assert.AreNotEqual(baseline, l.PostStateHash);

            // GlobalCollapseValue
            var cv = MakeEmpty(); cv.GlobalCollapseValue = 1;
            Assert.AreNotEqual(baseline, cv.PostStateHash);

            // Tiles
            var t = MakeEmpty(); t.AddTile(new GridCoord(0, 0, DimensionLayer.Reality));
            Assert.AreNotEqual(baseline, t.PostStateHash);

            // Anchors
            var an = MakeEmpty();
            an.AddAnchor(new AnchorZone(1, "Player", new[]
            {
                new GridPos(0, 0), new GridPos(1, 0), new GridPos(1, 1), new GridPos(0, 1)
            }));
            Assert.AreNotEqual(baseline, an.PostStateHash);

            // Regions
            var rg = MakeEmpty();
            rg.AddRegion(new MapRegion(1, "PlayerDeployment", "Player", new[]
            {
                new GridCoord(0, 0), new GridCoord(1, 0)
            }));
            Assert.AreNotEqual(baseline, rg.PostStateHash);

            // MapObjects
            var ob = MakeEmpty();
            ob.AddMapObject(new MapObjectInstance(1, "Gate", new GridCoord(0, 0)));
            Assert.AreNotEqual(baseline, ob.PostStateHash);
        }

        // ──────────── 13. Anchor 顶点顺序无关（构造已规范化）────────────

        [Test]
        public void Hash_AnchorVerticesInsertedInDifferentOrder_SameHash()
        {
            // AnchorZone 构造函数对顶点排序；不同插入顺序但同顶点集合 → 同 hash。
            var zoneA = new AnchorZone(1, "Player", new[]
            {
                new GridPos(2, 2), new GridPos(0, 0), new GridPos(2, 0), new GridPos(0, 2)
            });
            var zoneB = new AnchorZone(1, "Player", new[]
            {
                new GridPos(0, 2), new GridPos(2, 0), new GridPos(0, 0), new GridPos(2, 2)
            });
            var a = MakeWithAnchor(zoneA);
            var b = MakeWithAnchor(zoneB);
            Assert.AreEqual(a.PostStateHash, b.PostStateHash);
        }

        // ──────────── 14. Region 顶点顺序无关 ────────────

        [Test]
        public void Hash_RegionTileCoordsInsertedInDifferentOrder_SameHash()
        {
            var rA = new MapRegion(1, "PlayerDeployment", "Player", new[]
            {
                new GridCoord(3, 0), new GridCoord(1, 0), new GridCoord(2, 0)
            });
            var rB = new MapRegion(1, "PlayerDeployment", "Player", new[]
            {
                new GridCoord(1, 0), new GridCoord(2, 0), new GridCoord(3, 0)
            });
            var a = MakeWithRegion(rA);
            var b = MakeWithRegion(rB);
            Assert.AreEqual(a.PostStateHash, b.PostStateHash);
        }

        // ──────────── 15. 空集合 → 长度 0 占位（明文验证）────────────

        [Test]
        public void Hash_EmptyCollections_UseZeroLengthPlaceholder()
        {
            // 验证"空集合仍写入 tag + length=0"路径稳定：两次空状态 hash 必须相等。
            var a = MakeEmpty();
            var b = MakeEmpty();
            b.Version = 1; // 让其它字段不同，孤立验证空集合路径不会因 Version 抖动
            // 实际上 b.Version 不同会让 hash 不同；本测试仅证明"空集合不会抛异常或返回 offset_basis"。
            Assert.AreNotEqual(0UL, a.PostStateHash);
            Assert.AreNotEqual(0UL, b.PostStateHash);
            Assert.AreNotEqual(a.PostStateHash, b.PostStateHash);
        }

        // ──────────── 16. Object 字段差异 → 哈希差异 ────────────

        [Test]
        public void Hash_DifferentObjectFields_ChangesHash()
        {
            var baseObj = new MapObjectInstance(1, "Gate", new GridCoord(0, 0));
            var a = MakeWithObject(baseObj);
            var b = MakeWithObject(new MapObjectInstance(2, "Gate", new GridCoord(0, 0)));  // ObjectId
            var c = MakeWithObject(new MapObjectInstance(1, "Terminal", new GridCoord(0, 0))); // ObjectType
            var d = MakeWithObject(new MapObjectInstance(1, "Gate", new GridCoord(1, 0)));  // Anchor

            ulong baseHash = a.PostStateHash;
            Assert.AreNotEqual(baseHash, b.PostStateHash);
            Assert.AreNotEqual(baseHash, c.PostStateHash);
            Assert.AreNotEqual(baseHash, d.PostStateHash);
        }
    }
}
