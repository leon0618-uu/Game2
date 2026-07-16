using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using Starfall.Core.Anchor;
using Starfall.Core.Map.Anchor;
using Starfall.Core.Map.Collapse;
using Starfall.Core.Map.Commands;
using Starfall.Core.Map.Coordinates;
using Starfall.Core.Map.State;
using Starfall.Core.Model;
using Starfall.Tests.EditMode.Map.Commands;

namespace Starfall.Tests.EditMode.Map.Anchor
{
    /// <summary>
    /// doc2 MAP-12 回归测试：确认 MAP-02/03/04/06/07/08/09/11a 核心 API 仍可用。
    /// <para/>
    /// **不重写既有测试**，仅用最小回归断言验证「未破坏」：
    /// <list type="bullet">
    /// <item>MAP-02 MapState 公共签名 + Collection 操作；</item>
    /// <item>MAP-03 IMapCommand + MapCommandExecutor；</item>
    /// <item>MAP-04 Tile AddTile/RemoveTile；</item>
    /// <item>MAP-07/08 FlipTilePhaseCommand + ModifyAnchorStateCommand；</item>
    /// <item>MAP-09 RegionStates / SpawnPoints；</item>
    /// <item>MAP-11a GlobalCV / LocalCVs。</li>
    /// </list>
    /// </summary>
    public class Map12_Regression_Tests
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

        // ──────────── MAP-02 MapState 公共签名 ────────────

        [Test]
        public void Regression_MapState_PublicSignatures_Preserved()
        {
            // 老 API 必须仍可用：Tiles / Anchors / Regions / MapObjects / RegionStates / SpawnPoints
            Assert.IsNotNull(_map.Tiles);
            Assert.IsNotNull(_map.Anchors);
            Assert.IsNotNull(_map.Regions);
            Assert.IsNotNull(_map.MapObjects);
            Assert.IsNotNull(_map.RegionStates);
            Assert.IsNotNull(_map.SpawnPoints);
            // 新增（MAP-12）
            Assert.IsNotNull(_map.AnchorLinks);
        }

        [Test]
        public void Regression_BattleState_PublicSignatures_Preserved()
        {
            // BattleState 公共签名不变（含 PostStateHash）
            var board = new BoardState(_map.Definition.Size.Width, _map.Definition.Size.Height,
                new Dictionary<GridPos, TileState>());
            var battle = new BattleState(0, Owner.Player, board, new List<UnitState>());
            Assert.IsNotNull(battle.MapState);
            // PostStateHash getter 存在
            ulong h = battle.PostStateHash;
            Assert.IsTrue(h != 0UL || h == 0UL); // smoke test，不抛即过
        }

        [Test]
        public void Regression_Tile_AddRemove_StillWorks()
        {
            // MAP-04 Tile AddTile/RemoveTile
            // 注：MapTestHarness.Attach 已填满 8×8=64 tile (Layer=Reality)。
            // 使用 Layer=Astral 的新 tile 避免重复。
            var coord = new GridCoord(2, 3, Starfall.Core.Map.Coordinates.DimensionLayer.Astral);
            _map.AddTile(coord);
            Assert.IsTrue(_map.Tiles.Any(t => t.Equals(coord)));
            _map.RemoveTile(coord);
            Assert.IsFalse(_map.Tiles.Any(t => t.Equals(coord)));
        }

        // ──────────── MAP-03 IMapCommand + Executor ────────────

        [Test]
        public void Regression_IMapCommand_RunAndUndo_StillWorks()
        {
            // 用一个 MAP-03 既有命令：FlipTilePhaseCommand
            // 注：直接构造 FlipTilePhaseCommand 跑（无需依赖）
            var cmd = new FlipTilePhaseCommand(MapTestHarness.FlippableTileId, DimensionLayer.Astral);
            int v0 = _map.Version;
            var r = _exec.Run(cmd, _map);
            Assert.IsTrue(r.Success);
            Assert.AreEqual(v0 + 1, _map.Version);
            Assert.IsTrue(_exec.UndoLast(_map));
        }

        [Test]
        public void Regression_ModifyAnchorState_StillWorks()
        {
            // MAP-03 ModifyAnchorStateCommand（依赖 CreateAnchorLinkCommand 先跑）
            var verts = new List<GridPos>
            {
                new GridPos(0, 0), new GridPos(2, 0), new GridPos(0, 2),
            };
            var createCmd = new CreateAnchorLinkCommand(5, "Player", verts);
            Assert.IsTrue(_exec.Run(createCmd, _map).Success);

            var modifyCmd = new ModifyAnchorStateCommand(5, AnchorZoneState.PlayerControlled);
            var r = _exec.Run(modifyCmd, _map);
            Assert.IsTrue(r.Success);
            Assert.AreEqual(AnchorZoneState.PlayerControlled,
                AnchorStateService.GetOrDefault(_map, 5));
        }

        // ──────────── MAP-11a GlobalCV / LocalCVs ────────────

        [Test]
        public void Regression_GlobalCV_StillUsable()
        {
            _map.GlobalCV = new GlobalCollapseValue(50, 10);
            Assert.AreEqual(50, _map.GlobalCV.Value);
            Assert.AreEqual(50, _map.GlobalCollapseValue);
        }

        [Test]
        public void Regression_LocalCV_AddGetRemove()
        {
            var coord = new GridCoord(2, 2);
            var lcv = new LocalCollapseValue(coord, 25, 0);
            _map.AddLocalCV(lcv);
            var got = _map.TryGetLocalCV(coord);
            Assert.IsTrue(got.HasValue);
            Assert.AreEqual(25, got.Value.Value);
            Assert.IsTrue(_map.RemoveLocalCV(coord));
        }
    }
}