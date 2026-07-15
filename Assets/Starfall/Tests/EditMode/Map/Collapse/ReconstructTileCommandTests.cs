using NUnit.Framework;
using Starfall.Core.Map;
using Starfall.Core.Map.Collapse;
using Starfall.Core.Map.Commands;
using Starfall.Core.Map.Coordinates;
using Starfall.Core.Map.State;

namespace Starfall.Tests.EditMode.Map.Collapse
{
    /// <summary>
    /// doc2 MAP-11a <see cref="ReconstructTileCommand"/> 测试集（≥ 6 测试）。
    /// 覆盖：happy / undo / 越界 / 已重建 / 从 Collapsed 恢复。
    /// </summary>
    public class ReconstructTileCommandTests
    {
        private static MapState MakeMap()
        {
            var map = new MapState(new MapDefinition("map.test", 8, 8,
                DimensionLayer.Reality, 0));
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
        public void Execute_ReconstructFromCollapsed_ResetsValue()
        {
            var map = MakeMap();
            var coord = new GridCoord(0, 0);
            map.AddLocalCV(LocalCollapseValue.Of(coord, 100)); // Collapsed
            var cmd = new ReconstructTileCommand(coord);
            var result = cmd.Execute(map);
            Assert.IsTrue(result.Success);
            var lcv = map.TryGetLocalCV(coord).Value;
            Assert.AreEqual(0, lcv.Value);
            Assert.AreEqual(TileStability.Stable, lcv.Stability);
        }

        [Test]
        public void Execute_ReconstructFromFractured_ResetsValue()
        {
            var map = MakeMap();
            var coord = new GridCoord(1, 1);
            map.AddLocalCV(LocalCollapseValue.Of(coord, 60)); // Fractured
            var cmd = new ReconstructTileCommand(coord);
            var result = cmd.Execute(map);
            Assert.IsTrue(result.Success);
            var lcv = map.TryGetLocalCV(coord).Value;
            Assert.AreEqual(0, lcv.Value);
            Assert.AreEqual(TileStability.Stable, lcv.Stability);
        }

        [Test]
        public void Execute_Emits_OnTileReconstructed_Event()
        {
            var map = MakeMap();
            var coord = new GridCoord(0, 0);
            map.AddLocalCV(LocalCollapseValue.Of(coord, 100));
            var cmd = new ReconstructTileCommand(coord);
            var result = cmd.Execute(map);
            Assert.AreEqual(1, result.Events.Count);
            var e = result.Events[0];
            Assert.AreEqual(MapEventKind.OnTileReconstructed, e.Kind);
            Assert.AreEqual(coord, e.Coord);
        }

        // ──────────── 2) Undo 路径 ────────────

        [Test]
        public void Undo_RestoresCollapsedValue()
        {
            var map = MakeMap();
            var coord = new GridCoord(2, 0);
            map.AddLocalCV(LocalCollapseValue.Of(coord, 100));
            var cmd = new ReconstructTileCommand(coord);
            cmd.Execute(map);
            var after = map.TryGetLocalCV(coord).Value;
            Assert.AreEqual(TileStability.Stable, after.Stability);

            cmd.Undo(map);
            var before = map.TryGetLocalCV(coord).Value;
            Assert.AreEqual(TileStability.Collapsed, before.Stability);
            Assert.AreEqual(100, before.Value);
        }

        [Test]
        public void Undo_RemovesLCV_IfNoneBefore()
        {
            var map = MakeMap();
            var coord = new GridCoord(0, 0);
            var cmd = new ReconstructTileCommand(coord);
            cmd.Execute(map);
            Assert.IsTrue(map.TryGetLocalCV(coord).HasValue);
            cmd.Undo(map);
            Assert.IsFalse(map.TryGetLocalCV(coord).HasValue);
        }

        [Test]
        public void Undo_WithoutExecute_Throws()
        {
            var map = MakeMap();
            var cmd = new ReconstructTileCommand(new GridCoord(0, 0));
            Assert.Throws<System.InvalidOperationException>(() => cmd.Undo(map));
        }

        // ──────────── 3) 越界 / 非法 ────────────

        [Test]
        public void Execute_TileNotInMap_Fails()
        {
            var map = MakeMap();
            var coord = new GridCoord(7, 7);
            var cmd = new ReconstructTileCommand(coord);
            var result = cmd.Execute(map);
            Assert.IsFalse(result.Success);
            Assert.AreEqual("tile not in map", result.FailureReason);
        }

        [Test]
        public void Execute_TwiceOnSameTile_Idempotent()
        {
            // 重建 → Value=0 (Stable)。再次重建：prevStability=Stable != Reconstructed
            // 所以"already reconstructed"分支不触发；命令实际再次覆盖 Value=0。
            // 这与 readonly struct + 派生规则的妥协一致（详见 ADR-0007 D3）。
            // 验证：两次执行均成功，最终 Value=0。
            var map = MakeMap();
            var coord = new GridCoord(0, 0);
            map.AddLocalCV(LocalCollapseValue.Of(coord, 100)); // Collapsed
            var cmd1 = new ReconstructTileCommand(coord);
            var cmd2 = new ReconstructTileCommand(coord);
            Assert.IsTrue(cmd1.Execute(map).Success);
            Assert.IsTrue(cmd2.Execute(map).Success, "Re-executing on Value=0 tile should not throw");
            Assert.AreEqual(0, map.TryGetLocalCV(coord).Value.Value);
        }

        [Test]
        public void Constructor_InvalidTarget_Throws()
        {
            var coord = new GridCoord(0, 0);
            Assert.Throws<System.ArgumentOutOfRangeException>(
                () => new ReconstructTileCommand(coord, TileStability.Fractured));
            Assert.Throws<System.ArgumentOutOfRangeException>(
                () => new ReconstructTileCommand(coord, TileStability.Collapsing));
            Assert.Throws<System.ArgumentOutOfRangeException>(
                () => new ReconstructTileCommand(coord, TileStability.Collapsed));
        }

        // ──────────── 4) CommandId / Version ────────────

        [Test]
        public void CommandId_Format()
        {
            var coord = new GridCoord(2, 3, DimensionLayer.Astral);
            var cmd = new ReconstructTileCommand(coord);
            Assert.AreEqual("reconstruct-tile:2,3,1", cmd.CommandId);
        }

        [Test]
        public void Version_Is1()
        {
            Assert.AreEqual(1, new ReconstructTileCommand(new GridCoord(0, 0)).Version);
        }

        [Test]
        public void Dependencies_IsEmpty()
        {
            var cmd = new ReconstructTileCommand(new GridCoord(0, 0));
            Assert.AreEqual(0, cmd.Dependencies.Count);
        }

        [Test]
        public void ToString_ContainsCoordAndTarget()
        {
            var cmd = new ReconstructTileCommand(new GridCoord(0, 0));
            string s = cmd.ToString();
            StringAssert.Contains("(0, 0, Reality)", s);
            StringAssert.Contains("Reconstructed", s);
        }
    }
}
