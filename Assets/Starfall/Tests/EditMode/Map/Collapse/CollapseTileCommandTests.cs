using NUnit.Framework;
using Starfall.Core.Map;
using Starfall.Core.Map.Collapse;
using Starfall.Core.Map.Commands;
using Starfall.Core.Map.Coordinates;
using Starfall.Core.Map.State;

namespace Starfall.Tests.EditMode.Map.Collapse
{
    /// <summary>
    /// doc2 MAP-11a <see cref="CollapseTileCommand"/> 测试集（≥ 6 测试）。
    /// 覆盖：happy / undo / 越界 / 非法目标 / 已坍塌。
    /// </summary>
    public class CollapseTileCommandTests
    {
        private static MapState MakeMap()
        {
            var map = new MapState(new MapDefinition("map.test", 8, 8,
                DimensionLayer.Reality, 0));
            // 添加几个 tile
            for (int y = 0; y < 4; y++)
            {
                for (int x = 0; x < 4; x++)
                {
                    map.AddTile(new GridCoord(x, y));
                }
            }
            return map;
        }

        // ──────────── 1) Happy path ────────────

        [Test]
        public void Execute_CollapseTile_SetsCollapsing()
        {
            var map = MakeMap();
            var coord = new GridCoord(0, 0);
            var cmd = new CollapseTileCommand(coord, TileStability.Collapsing);
            var result = cmd.Execute(map);
            Assert.IsTrue(result.Success);
            var lcv = map.TryGetLocalCV(coord);
            Assert.IsTrue(lcv.HasValue);
            Assert.AreEqual(80, lcv.Value.Value);
            Assert.AreEqual(TileStability.Collapsing, lcv.Value.Stability);
        }

        [Test]
        public void Execute_CollapseTile_ToCollapsed()
        {
            var map = MakeMap();
            var coord = new GridCoord(1, 0);
            var cmd = new CollapseTileCommand(coord, TileStability.Collapsed);
            var result = cmd.Execute(map);
            Assert.IsTrue(result.Success);
            var lcv = map.TryGetLocalCV(coord).Value;
            Assert.AreEqual(100, lcv.Value);
            Assert.AreEqual(TileStability.Collapsed, lcv.Stability);
        }

        [Test]
        public void Execute_Emits_OnTileFractured_Event()
        {
            var map = MakeMap();
            var coord = new GridCoord(0, 0);
            var cmd = new CollapseTileCommand(coord, TileStability.Collapsing);
            var result = cmd.Execute(map);
            Assert.AreEqual(1, result.Events.Count);
            var e = result.Events[0];
            Assert.AreEqual(MapEventKind.OnTileFractured, e.Kind);
            Assert.AreEqual(coord, e.Coord);
        }

        // ──────────── 2) Undo 路径 ────────────

        [Test]
        public void Undo_RestoresPreviousStability()
        {
            var map = MakeMap();
            var coord = new GridCoord(2, 0);
            // 预先累积到 50 (Fractured)
            map.AddLocalCV(LocalCollapseValue.Of(coord, 50));
            var cmd = new CollapseTileCommand(coord, TileStability.Collapsed);
            cmd.Execute(map);
            var after = map.TryGetLocalCV(coord).Value;
            Assert.AreEqual(TileStability.Collapsed, after.Stability);

            cmd.Undo(map);
            var before = map.TryGetLocalCV(coord).Value;
            Assert.AreEqual(TileStability.Fractured, before.Stability);
            Assert.AreEqual(50, before.Value);
        }

        [Test]
        public void Undo_RemovesLCV_IfNoneBefore()
        {
            var map = MakeMap();
            var coord = new GridCoord(0, 0);
            // 之前没 LCV
            var cmd = new CollapseTileCommand(coord, TileStability.Collapsing);
            cmd.Execute(map);
            Assert.IsTrue(map.TryGetLocalCV(coord).HasValue);
            cmd.Undo(map);
            Assert.IsFalse(map.TryGetLocalCV(coord).HasValue);
        }

        [Test]
        public void Undo_WithoutExecute_Throws()
        {
            var map = MakeMap();
            var cmd = new CollapseTileCommand(new GridCoord(0, 0), TileStability.Collapsing);
            Assert.Throws<System.InvalidOperationException>(() => cmd.Undo(map));
        }

        // ──────────── 3) 越界 / 非法 ────────────

        [Test]
        public void Execute_TileNotInMap_Fails()
        {
            var map = MakeMap();
            var coord = new GridCoord(7, 7); // 不在 map 内
            var cmd = new CollapseTileCommand(coord, TileStability.Collapsing);
            var result = cmd.Execute(map);
            Assert.IsFalse(result.Success);
            Assert.AreEqual("tile not in map", result.FailureReason);
        }

        [Test]
        public void Execute_AlreadyCollapsed_Fails()
        {
            var map = MakeMap();
            var coord = new GridCoord(0, 0);
            map.AddLocalCV(LocalCollapseValue.Of(coord, 100));
            var cmd = new CollapseTileCommand(coord, TileStability.Collapsed);
            var result = cmd.Execute(map);
            Assert.IsFalse(result.Success);
            Assert.AreEqual("already collapsed", result.FailureReason);
        }

        [Test]
        public void Constructor_InvalidTarget_Throws()
        {
            var coord = new GridCoord(0, 0);
            Assert.Throws<System.ArgumentOutOfRangeException>(
                () => new CollapseTileCommand(coord, TileStability.Stable));
            Assert.Throws<System.ArgumentOutOfRangeException>(
                () => new CollapseTileCommand(coord, TileStability.Unstable));
            Assert.Throws<System.ArgumentOutOfRangeException>(
                () => new CollapseTileCommand(coord, TileStability.Reconstructed));
        }

        // ──────────── 4) CommandId / Version ────────────

        [Test]
        public void CommandId_Format()
        {
            var coord = new GridCoord(2, 3, DimensionLayer.Astral);
            var cmd = new CollapseTileCommand(coord, TileStability.Collapsing);
            Assert.AreEqual("collapse-tile:2,3,1", cmd.CommandId);
        }

        [Test]
        public void Version_Is1()
        {
            Assert.AreEqual(1, new CollapseTileCommand(new GridCoord(0, 0), TileStability.Collapsing).Version);
        }

        [Test]
        public void Dependencies_IsEmpty()
        {
            var cmd = new CollapseTileCommand(new GridCoord(0, 0), TileStability.Collapsing);
            Assert.AreEqual(0, cmd.Dependencies.Count);
        }

        [Test]
        public void ToString_ContainsCoordAndTarget()
        {
            var cmd = new CollapseTileCommand(new GridCoord(0, 0), TileStability.Collapsing);
            string s = cmd.ToString();
            StringAssert.Contains("Collapsing", s);
        }
    }
}
