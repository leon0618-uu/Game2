using System.Collections.Generic;
using NUnit.Framework;
using Starfall.Core.Anchor;
using Starfall.Core.Map.Coordinates;
using Starfall.Core.Map.State;
using Starfall.Core.Model;

namespace Starfall.Tests.EditMode.Map.State
{
    /// <summary>
    /// doc2 MAP-02 §3.2 MapState 深拷贝测试集。
    /// 覆盖：克隆前后相等 / 引用不同 / 修改克隆不修改原 / 集合彻底独立 /
    /// 多层切片 / 空 MapState / 包含锚点的 MapState。
    /// </summary>
    public class MapStateCloneTests
    {
        // ──────────── 工厂 ────────────

        private static MapDefinition MakeDef(string id = "map.test", int w = 4, int h = 4)
            => new MapDefinition(id, w, h, DimensionLayer.Reality, 0);

        private static MapState MakeEmpty(string id = "map.test", int w = 4, int h = 4)
            => new MapState(MakeDef(id, w, h));

        private static MapState MakeWithTiles(int tileCount, string id = "map.test")
        {
            var s = MakeEmpty(id, 8, 8);
            for (int i = 0; i < tileCount; i++)
            {
                int x = i % 8;
                int y = i / 8;
                var layer = (i % 2 == 0) ? DimensionLayer.Reality : DimensionLayer.Astral;
                s.AddTile(new GridCoord(x, y, layer));
            }
            return s;
        }

        private static MapState MakeWithAnchor()
        {
            var s = MakeEmpty("map.anchored", 6, 6);
            var zone = new AnchorZone(7, "Player", new[]
            {
                new GridPos(1, 1), new GridPos(4, 1), new GridPos(4, 4), new GridPos(1, 4)
            });
            s.AddAnchor(zone);
            return s;
        }

        private static MapState MakeFullyPopulated()
        {
            var s = MakeEmpty("map.full", 6, 6);
            s.Version = 5;
            s.ActiveLayer = DimensionLayer.Astral;
            s.GlobalCollapseValue = 42;
            s.AddTile(new GridCoord(0, 0, DimensionLayer.Reality));
            s.AddTile(new GridCoord(1, 0, DimensionLayer.Astral));
            s.AddAnchor(new AnchorZone(1, "Player", new[]
            {
                new GridPos(0, 0), new GridPos(2, 0), new GridPos(2, 2), new GridPos(0, 2)
            }));
            s.AddRegion(new MapRegion(11, "PlayerDeployment", "Player", new[]
            {
                new GridCoord(0, 0), new GridCoord(2, 0), new GridCoord(2, 2), new GridCoord(0, 2)
            }));
            s.AddMapObject(new MapObjectInstance(101, "Gate", new GridCoord(3, 3, DimensionLayer.Reality)));
            s.AddMapObject(new MapObjectInstance(102, "Terminal", new GridCoord(4, 4, DimensionLayer.Astral)));
            return s;
        }

        // ──────────── 1-2. 空状态克隆 / 引用不等 ────────────

        [Test]
        public void Clone_Null_ReturnsNull()
        {
            Assert.IsNull(MapStateCloner.DeepClone(null));
        }

        [Test]
        public void Clone_Empty_ReturnsNonNull()
        {
            var src = MakeEmpty();
            var clone = MapStateCloner.DeepClone(src);
            Assert.IsNotNull(clone);
            Assert.AreNotSame(src, clone);
        }

        [Test]
        public void Clone_Empty_FieldsEqual()
        {
            var src = MakeEmpty();
            var clone = MapStateCloner.DeepClone(src);
            Assert.AreEqual(src.Definition, clone.Definition);
            Assert.AreEqual(0, clone.Version);
            Assert.AreEqual(src.ActiveLayer, clone.ActiveLayer);
            Assert.AreEqual(0, clone.GlobalCollapseValue);
            Assert.AreEqual(0, clone.Tiles.Count);
            Assert.AreEqual(0, clone.Anchors.Count);
            Assert.AreEqual(0, clone.Regions.Count);
            Assert.AreEqual(0, clone.MapObjects.Count);
        }

        // ──────────── 3-4. 多 Tile / 单 Tile 集合独立 ────────────

        [Test]
        public void Clone_MultiTile_PreservesAndIsolatesTilesList()
        {
            var src = MakeWithTiles(6);
            var clone = MapStateCloner.DeepClone(src);
            Assert.AreNotSame(src.Tiles, clone.Tiles);
            Assert.AreEqual(src.Tiles.Count, clone.Tiles.Count);
            for (int i = 0; i < src.Tiles.Count; i++)
                Assert.AreEqual(src.Tiles[i], clone.Tiles[i]);
        }

        [Test]
        public void Clone_SingleTile_PreservesTile()
        {
            var src = MakeEmpty();
            src.AddTile(new GridCoord(2, 3, DimensionLayer.Reality));
            var clone = MapStateCloner.DeepClone(src);
            Assert.AreEqual(1, clone.Tiles.Count);
            Assert.AreEqual(new GridCoord(2, 3, DimensionLayer.Reality), clone.Tiles[0]);
        }

        [Test]
        public void Clone_AddsTileToClone_DoesNotAffectSource()
        {
            var src = MakeEmpty("map.tile.iso", 4, 4);
            var clone = MapStateCloner.DeepClone(src);
            clone.AddTile(new GridCoord(0, 0, DimensionLayer.Reality));
            Assert.AreEqual(0, src.Tiles.Count);
            Assert.AreEqual(1, clone.Tiles.Count);
        }

        // ──────────── 5. Anchor 集合独立 + 顶点独立 ────────────

        [Test]
        public void Clone_Anchor_PreservesAndIsolatesAnchorZone()
        {
            var src = MakeWithAnchor();
            var clone = MapStateCloner.DeepClone(src);

            Assert.AreEqual(1, clone.Anchors.Count);
            Assert.AreNotSame(src.Anchors[0], clone.Anchors[0]);
            Assert.AreEqual(src.Anchors[0].ZoneId, clone.Anchors[0].ZoneId);
            Assert.AreEqual(src.Anchors[0].Owner, clone.Anchors[0].Owner);
            Assert.AreNotSame(src.Anchors[0].Vertices, clone.Anchors[0].Vertices);
            Assert.AreEqual(src.Anchors[0].Vertices.Count, clone.Anchors[0].Vertices.Count);
            for (int i = 0; i < src.Anchors[0].Vertices.Count; i++)
                Assert.AreEqual(src.Anchors[0].Vertices[i], clone.Anchors[0].Vertices[i]);
        }

        // ──────────── 6. Region / MapObject 集合独立 ────────────

        [Test]
        public void Clone_RegionAndMapObject_PreservesAndIsolatesCollections()
        {
            var src = MakeFullyPopulated();
            var clone = MapStateCloner.DeepClone(src);

            Assert.AreNotSame(src.Regions, clone.Regions);
            Assert.AreNotSame(src.MapObjects, clone.MapObjects);
            Assert.AreEqual(src.Regions.Count, clone.Regions.Count);
            Assert.AreEqual(src.MapObjects.Count, clone.MapObjects.Count);

            for (int i = 0; i < src.Regions.Count; i++)
            {
                Assert.AreNotSame(src.Regions[i], clone.Regions[i]);
                Assert.AreEqual(src.Regions[i].RegionId, clone.Regions[i].RegionId);
                Assert.AreEqual(src.Regions[i].RegionType, clone.Regions[i].RegionType);
                Assert.AreEqual(src.Regions[i].Owner, clone.Regions[i].Owner);
                Assert.AreNotSame(src.Regions[i].TileCoords, clone.Regions[i].TileCoords);
                Assert.AreEqual(src.Regions[i].TileCoords.Count, clone.Regions[i].TileCoords.Count);
            }

            for (int i = 0; i < src.MapObjects.Count; i++)
            {
                Assert.AreNotSame(src.MapObjects[i], clone.MapObjects[i]);
                Assert.AreEqual(src.MapObjects[i].ObjectId, clone.MapObjects[i].ObjectId);
                Assert.AreEqual(src.MapObjects[i].ObjectType, clone.MapObjects[i].ObjectType);
                Assert.AreEqual(src.MapObjects[i].Anchor, clone.MapObjects[i].Anchor);
            }
        }

        // ──────────── 7. 标量字段（Version / ActiveLayer / GlobalCollapseValue） ────────────

        [Test]
        public void Clone_ScalarFields_PropagateToClone()
        {
            var src = MakeFullyPopulated();
            var clone = MapStateCloner.DeepClone(src);
            Assert.AreEqual(5, clone.Version);
            Assert.AreEqual(DimensionLayer.Astral, clone.ActiveLayer);
            Assert.AreEqual(42, clone.GlobalCollapseValue);
        }

        [Test]
        public void Clone_MutateScalarOnClone_DoesNotAffectSource()
        {
            var src = MakeEmpty();
            src.Version = 1;
            src.GlobalCollapseValue = 10;
            var clone = MapStateCloner.DeepClone(src);
            clone.Version = 999;
            clone.GlobalCollapseValue = 999;
            Assert.AreEqual(1, src.Version);
            Assert.AreEqual(10, src.GlobalCollapseValue);
        }

        // ──────────── 8. Definition 是 struct → 值复制（无需独立引用）────────────

        [Test]
        public void Clone_Definition_PreservedByValue()
        {
            var src = MakeEmpty("map.def", 5, 6);
            var clone = MapStateCloner.DeepClone(src);
            Assert.AreEqual(src.Definition, clone.Definition);
            // Definition 是 readonly struct，比较通过 Equals；
            // 这里再单独验证字段以证明"拷贝彻底"。
            Assert.AreEqual("map.def", clone.Definition.MapId);
            Assert.AreEqual(5, clone.Definition.Width);
            Assert.AreEqual(6, clone.Definition.Height);
            Assert.AreEqual(DimensionLayer.Reality, clone.Definition.InitialActiveLayer);
            Assert.AreEqual(0, clone.Definition.InitialGlobalCollapseValue);
        }

        // ──────────── 9. Hash 一致性（克隆前后 PostStateHash 相同）────────────

        [Test]
        public void Clone_FullyPopulated_HashEqualToSource()
        {
            var src = MakeFullyPopulated();
            var clone = MapStateCloner.DeepClone(src);
            Assert.AreEqual(src.PostStateHash, clone.PostStateHash);
        }

        [Test]
        public void Clone_DoubleClone_SameHash()
        {
            var src = MakeFullyPopulated();
            var c1 = MapStateCloner.DeepClone(src);
            var c2 = MapStateCloner.DeepClone(c1);
            Assert.AreEqual(src.PostStateHash, c2.PostStateHash);
            Assert.AreNotSame(c1, c2);
        }

        // ──────────── 10. BattleStateCloner.Clone 集成（端到端）────────────

        [Test]
        public void BattleStateCloner_DeepCopiesMapState()
        {
            var def = MakeDef("map.battle", 4, 4);
            var map = new MapState(def);
            map.AddTile(new GridCoord(1, 1, DimensionLayer.Reality));
            map.AddAnchor(new AnchorZone(1, "Player", new[]
            {
                new GridPos(0, 0), new GridPos(2, 0), new GridPos(2, 2), new GridPos(0, 2)
            }));

            var board = new BoardState(4, 4, new Dictionary<GridPos, TileState>());
            var battle = new BattleState(0, Owner.Player, board, null, map);

            var clone = BattleStateCloner.Clone(battle);
            Assert.IsNotNull(clone);
            Assert.IsNotNull(clone.MapState);
            Assert.AreNotSame(battle.MapState, clone.MapState);
            Assert.AreNotSame(battle.MapState.Tiles, clone.MapState.Tiles);
            Assert.AreNotSame(battle.MapState.Anchors[0], clone.MapState.Anchors[0]);
            Assert.AreEqual(battle.MapState.PostStateHash, clone.MapState.PostStateHash);

            // 隔离：mutate clone 不影响 source
            clone.MapState.AddTile(new GridCoord(3, 3, DimensionLayer.Astral));
            Assert.AreEqual(1, battle.MapState.Tiles.Count);
            Assert.AreEqual(2, clone.MapState.Tiles.Count);
        }
    }
}
