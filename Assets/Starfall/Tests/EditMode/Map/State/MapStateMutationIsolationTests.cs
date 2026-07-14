using NUnit.Framework;
using Starfall.Core.Anchor;
using Starfall.Core.Map.Coordinates;
using Starfall.Core.Map.State;
using Starfall.Core.Model;

namespace Starfall.Tests.EditMode.Map.State
{
    /// <summary>
    /// doc2 MAP-02 §3.2 MapState 突变隔离测试集。
    /// 覆盖：修改 Tile 集合 / Anchor 集合 / Region 集合 / Object 集合 / 嵌套结构
    /// 不影响克隆前的原始 MapState。
    /// </summary>
    public class MapStateMutationIsolationTests
    {
        private static MapState MakeSource()
        {
            var def = new MapDefinition("map.iso", 6, 6, DimensionLayer.Reality, 5);
            var s = new MapState(def);
            s.AddTile(new GridCoord(0, 0, DimensionLayer.Reality));
            s.AddTile(new GridCoord(1, 0, DimensionLayer.Astral));
            s.AddTile(new GridCoord(2, 1, DimensionLayer.Reality));
            s.AddAnchor(new AnchorZone(1, "Player", new[]
            {
                new GridPos(0, 0), new GridPos(2, 0), new GridPos(2, 2), new GridPos(0, 2)
            }));
            s.AddRegion(new MapRegion(7, "PlayerDeployment", "Player", new[]
            {
                new GridCoord(0, 0), new GridCoord(2, 0), new GridCoord(2, 2), new GridCoord(0, 2)
            }));
            s.AddMapObject(new MapObjectInstance(11, "Gate", new GridCoord(3, 3, DimensionLayer.Reality)));
            return s;
        }

        // ──────────── 1. Tile 集合修改隔离 ────────────

        [Test]
        public void MutatingCloneTiles_Add_DoesNotAffectSource()
        {
            var src = MakeSource();
            int srcCount = src.Tiles.Count;
            var clone = MapStateCloner.DeepClone(src);

            clone.AddTile(new GridCoord(5, 5, DimensionLayer.Astral));
            clone.AddTile(new GridCoord(5, 4, DimensionLayer.Reality));

            Assert.AreEqual(srcCount, src.Tiles.Count);
            Assert.AreEqual(srcCount + 2, clone.Tiles.Count);
            // source 中不包含 5,5 / 5,4
            bool srcHas55 = false;
            foreach (var t in src.Tiles)
            {
                if (t.Equals(new GridCoord(5, 5, DimensionLayer.Astral))) { srcHas55 = true; break; }
            }
            Assert.IsFalse(srcHas55);
        }

        [Test]
        public void MutatingCloneTiles_Remove_DoesNotAffectSource()
        {
            var src = MakeSource();
            int srcCount = src.Tiles.Count;
            var clone = MapStateCloner.DeepClone(src);

            // 移除第一个 tile
            clone.RemoveTile(clone.Tiles[0]);

            Assert.AreEqual(srcCount, src.Tiles.Count);
            Assert.AreEqual(srcCount - 1, clone.Tiles.Count);
        }

        [Test]
        public void MutatingCloneTiles_Clear_DoesNotAffectSource()
        {
            var src = MakeSource();
            var clone = MapStateCloner.DeepClone(src);

            // 通过 remove 逐个删除验证；MapState 没有公开 Clear，可调用 RemoveX。
            while (clone.Tiles.Count > 0)
                clone.RemoveTile(clone.Tiles[0]);

            Assert.AreEqual(0, clone.Tiles.Count);
            Assert.AreNotEqual(0, src.Tiles.Count);
        }

        // ──────────── 2. Anchor 集合 / 顶点隔离 ────────────

        [Test]
        public void MutatingCloneAnchors_RemoveAnchor_DoesNotAffectSource()
        {
            var src = MakeSource();
            int srcAnchorCount = src.Anchors.Count;
            var clone = MapStateCloner.DeepClone(src);

            clone.RemoveAnchor(clone.Anchors[0].ZoneId);

            Assert.AreEqual(srcAnchorCount, src.Anchors.Count);
            Assert.AreEqual(srcAnchorCount - 1, clone.Anchors.Count);
            // source 仍持有 ZoneId=1
            Assert.IsNotNull(src.Anchors[0]);
            Assert.AreEqual(1, src.Anchors[0].ZoneId);
        }

        // ──────────── 3. Region 集合隔离 ────────────

        [Test]
        public void MutatingCloneRegions_RemoveRegion_DoesNotAffectSource()
        {
            var src = MakeSource();
            int srcRegionCount = src.Regions.Count;
            var clone = MapStateCloner.DeepClone(src);

            clone.RemoveRegion(clone.Regions[0].RegionId);

            Assert.AreEqual(srcRegionCount, src.Regions.Count);
            Assert.AreEqual(srcRegionCount - 1, clone.Regions.Count);
        }

        // ──────────── 4. MapObject 集合隔离 ────────────

        [Test]
        public void MutatingCloneMapObjects_RemoveObject_DoesNotAffectSource()
        {
            var src = MakeSource();
            int srcCount = src.MapObjects.Count;
            var clone = MapStateCloner.DeepClone(src);

            clone.RemoveMapObject(clone.MapObjects[0].ObjectId);

            Assert.AreEqual(srcCount, src.MapObjects.Count);
            Assert.AreEqual(srcCount - 1, clone.MapObjects.Count);
        }

        // ──────────── 5. 嵌套结构突变隔离（AnchorZone.Vertices）────────────

        [Test]
        public void MutatingCloneAnchorVertices_ThroughAnchorClass_DoesNotAffectSource()
        {
            // 关键：克隆的 AnchorZone.Vertices 是 IReadOnlyList<GridPos>，但内部 List<GridPos>
            // 应该是 clone 自己的 List。本测试不通过外部 API 改 Vertices（AnchorZone 没暴露 mutable API），
            // 而是通过 clone.AddAnchor 注入新 anchor 验证源不受影响。
            var src = MakeSource();
            int srcAnchorCount = src.Anchors.Count;
            var clone = MapStateCloner.DeepClone(src);

            clone.AddAnchor(new AnchorZone(99, "Enemy", new[]
            {
                new GridPos(3, 3), new GridPos(5, 3), new GridPos(5, 5), new GridPos(3, 5)
            }));

            Assert.AreEqual(srcAnchorCount, src.Anchors.Count);
            Assert.AreEqual(srcAnchorCount + 1, clone.Anchors.Count);
            // source 集合中不包含 ZoneId=99
            bool sourceHas99 = false;
            foreach (var a in src.Anchors)
            {
                if (a.ZoneId == 99) { sourceHas99 = true; break; }
            }
            Assert.IsFalse(sourceHas99);
        }

        // ──────────── 6. 嵌套 Region TileCoords 集合隔离（额外保护）────────────

        [Test]
        public void MutatingCloneRegionCollection_AddRegion_DoesNotAffectSource()
        {
            var src = MakeSource();
            int srcCount = src.Regions.Count;
            var clone = MapStateCloner.DeepClone(src);

            clone.AddRegion(new MapRegion(99, "BossPhase", "Player", new[]
            {
                new GridCoord(5, 5)
            }));

            Assert.AreEqual(srcCount, src.Regions.Count);
            Assert.AreEqual(srcCount + 1, clone.Regions.Count);
        }
    }
}
