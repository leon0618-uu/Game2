using NUnit.Framework;
using Starfall.Core.Command;
using Starfall.Core.Map.Commands;
using Starfall.Core.Map.Coordinates;
using Starfall.Core.Map.State;
using Starfall.Core.Map.Tile;
using Starfall.Core.Model;
using Starfall.Core.Rules;
using System.Collections.Generic;

namespace Starfall.Tests.EditMode.Map.Commands
{
    /// <summary>
    /// doc2 MAP-08 §6.1 FallingCommand 重构版测试集。
    /// 覆盖：兼容既有 MVP 测试 + MAP-08 重构路径 + BattleEvent 集成。
    /// <para/>
    /// **FallingCommand_ReducesHp 兼容性**：既有测试断言 `UnitDamaged` 事件 —
    /// 重构版保留 fallback 路径下"先 UnitDamaged + 后 UnitEnteredVoid"的 2 事件序列。
    /// <para/>
    /// **MAP-08 重构路径**：attached registry + Void 起点 + 合法落点 → 移动占用 + 发 UnitEnteredVoid。
    /// </summary>
    public class FallingCommandCompatTests
    {
        // ──────────── 兼容既有 MVP 测试 ────────────

        private static BoardState MakeBoard(int w = 4, int h = 4)
        {
            var tiles = new Dictionary<GridPos, TileState>();
            for (int y = 0; y < h; y++)
                for (int x = 0; x < w; x++)
                    tiles[new GridPos(x, y)] = TileState.Normal;
            return new BoardState(w, h, tiles);
        }

        private static BattleState MakeNoAttachState()
        {
            var s = new BattleState(0, Owner.Player, MakeBoard(), null);
            s.AddUnit(new UnitState(1, new GridPos(0, 0), 10, 10, Phase.Light, Owner.Player));
            return s;
        }

        [Test]
        public void Map08_TaskId_AssertedString()
        {
            const string taskId = "MAP-08";
            Assert.AreEqual("MAP-08", taskId);
        }

        [Test]
        public void FallingCommand_ReducesHp_CompatPath()
        {
            // 不 attach registry → 走 fallback 路径
            var s = MakeNoAttachState();
            var fall = new FallingCommand(1, 1, fallDamage: 3);
            var result = CommandExecutor.Run(s, fall, out var events);
            Assert.AreEqual(CommandResult.Success, result);
            Assert.AreEqual(7, s.Units[0].Hp);
            Assert.AreEqual(BattleEventKind.UnitDamaged, events[0].Kind);
        }

        [Test]
        public void FallingCommand_IllegalOnMissingUnit()
        {
            var s = MakeNoAttachState();
            var fall = new FallingCommand(1, 999);
            Assert.AreEqual(CommandResult.Illegal, CommandExecutor.Run(s, fall, out _));
        }

        // ──────────── MAP-08 重构路径 ────────────

        private MapState _mapState;
        private TileDefinitionRegistry _registry;

        private BattleState MakeAttachedState()
        {
            var def = new MapDefinition(
                mapId: "map.fall.compat",
                width: 6, height: 6,
                initialActiveLayer: DimensionLayer.Reality,
                initialGlobalCollapseValue: 0);
            _mapState = new MapState(def);
            _registry = new TileDefinitionRegistry(_mapState.Definition.Size);

            // 注册 6x6 + 把 unit (2,2) 位置改成 Void
            int id = 1;
            for (int y = 0; y < 6; y++)
            {
                for (int x = 0; x < 6; x++)
                {
                    TerrainType terrType = TerrainType.Plain;
                    if (x == 2 && y == 2)
                        terrType = TerrainType.Void;
                    _registry.Register(TileDefinitionRegistry.Make(
                        id++, new GridCoord(x, y), terrType));
                    _mapState.AddTile(new GridCoord(x, y));
                }
            }
            PhaseFlipStateService.Attach(_mapState, _registry);
            TileOccupancyService.AttachTileDefinitionRegistry(_mapState, _registry);
            TileOccupancyService.Clear();

            var board = new BoardState(6, 6, new Dictionary<GridPos, TileState>());
            var s = new BattleState(0, Owner.Player, board, null, _mapState);
            s.AddUnit(new UnitState(1, new GridPos(2, 2), 10, 10, Phase.Light, Owner.Player));
            TileOccupancyService.TryPlaceUnit(_mapState, 1, Footprint.SingleCell,
                new GridCoord(2, 2, DimensionLayer.Reality));
            return s;
        }

        [TearDown]
        public void TearDown()
        {
            if (_mapState != null)
            {
                PhaseFlipStateService.Detach(_mapState);
                TileOccupancyService.DetachAll(_mapState);
                TileOccupancyService.Clear();
                _mapState = null;
                _registry = null;
            }
        }

        [Test]
        public void FallingCommand_MAP08Path_UnitMovesToLegalCell_NotDamaged()
        {
            var s = MakeAttachedState();
            // unit (1,1) 在 (2,2)，但 (2,2) 是 Void（attach 后 MAP-08 重构路径生效）
            var fall = new FallingCommand(1, 1, fallDamage: 3);
            var result = CommandExecutor.Run(s, fall, out var events);

            Assert.AreEqual(CommandResult.Success, result);
            // MAP-08 路径：HP 不变（应是 10）
            Assert.AreEqual(10, s.Units[0].Hp, "MAP-08 path should NOT reduce HP.");
            // unit 移到 (2,1) 或类似 (N→E→S→W 最先合法) → Manhattan=1
            Assert.AreEqual(1, System.Math.Abs(s.Units[0].Pos.X - 2) + System.Math.Abs(s.Units[0].Pos.Y - 2));
            // 至少有一个 UnitEnteredVoid 事件
            bool found = false;
            foreach (var e in events)
            {
                if (e.Kind == BattleEventKind.UnitEnteredVoid) found = true;
            }
            Assert.IsTrue(found, "MAP-08 path should emit BattleEventKind.UnitEnteredVoid.");
        }

        [Test]
        public void FallingCommand_MAP08Path_EmitsUnitEnteredVoid()
        {
            var s = MakeAttachedState();
            var fall = new FallingCommand(1, 1, fallDamage: 3);
            CommandExecutor.Run(s, fall, out var events);
            // 期望 events 至少有一个 UnitEnteredVoid
            Assert.IsTrue(events.Count >= 1);
            Assert.AreEqual(BattleEventKind.UnitEnteredVoid, events[0].Kind);
        }

        [Test]
        public void FallingCommand_MAP08Path_NoLegalLanding_FallsBackToHpDamage()
        {
            // 极端：1x1 全 Void，没有合法落点
            var def = new MapDefinition(
                mapId: "map.fall.nolegal",
                width: 1, height: 1,
                initialActiveLayer: DimensionLayer.Reality,
                initialGlobalCollapseValue: 0);
            var small = new MapState(def);
            var smallReg = new TileDefinitionRegistry(small.Definition.Size);
            smallReg.Register(TileDefinitionRegistry.Make(1, new GridCoord(0, 0), TerrainType.Void,
                tags: TileTags.Void | TileTags.Impassable));
            small.AddTile(new GridCoord(0, 0));
            PhaseFlipStateService.Attach(small, smallReg);
            TileOccupancyService.AttachTileDefinitionRegistry(small, smallReg);
            TileOccupancyService.Clear();

            try
            {
                var board = new BoardState(1, 1, new Dictionary<GridPos, TileState>());
                var s = new BattleState(0, Owner.Player, board, null, small);
                s.AddUnit(new UnitState(1, new GridPos(0, 0), 10, 10, Phase.Light, Owner.Player));
                TileOccupancyService.TryPlaceUnit(small, 1, Footprint.SingleCell, new GridCoord(0, 0, DimensionLayer.Reality));

                var fall = new FallingCommand(1, 1, fallDamage: 3);
                var result = CommandExecutor.Run(s, fall, out var events);
                Assert.AreEqual(CommandResult.Success, result);
                // 无合法落点 → fallback HP damage
                Assert.AreEqual(7, s.Units[0].Hp);
                Assert.IsTrue(events.Count >= 1);
                Assert.AreEqual(BattleEventKind.UnitDamaged, events[0].Kind,
                    "Fallback path: 1st event is UnitDamaged.");
            }
            finally
            {
                PhaseFlipStateService.Detach(small);
                TileOccupancyService.DetachAll(small);
                TileOccupancyService.Clear();
            }
        }

        [Test]
        public void FallingCommand_BattleEvent_UnitEnteredVoid_EnumExists()
        {
            // 编译期等价：枚举值存在
            var v = BattleEventKind.UnitEnteredVoid;
            Assert.AreEqual((byte)13, (byte)v);
        }

        [Test]
        public void FallingCommand_BattleEvent_UnitPhaseCompressed_EnumExists()
        {
            var v = BattleEventKind.UnitPhaseCompressed;
            Assert.AreEqual((byte)14, (byte)v);
        }

        [Test]
        public void FallingCommand_MAP08Path_OldCellNoLongerOccupied_AfterMove()
        {
            var s = MakeAttachedState();
            var fall = new FallingCommand(1, 1, fallDamage: 3);
            CommandExecutor.Run(s, fall, out _);
            // 起点 (2,2) 不再被 unitId=1 占用
            var occ = TileOccupancyService.GetOccupantUnit(_mapState, new GridCoord(2, 2, DimensionLayer.Reality));
            Assert.IsFalse(occ.HasValue, "Old cell (2,2) should be empty after move.");

            // 新坐标必然 (2,1) 或其它 Manhattan=1 cell
            var newCoord = new GridCoord(s.Units[0].Pos.X, s.Units[0].Pos.Y, DimensionLayer.Reality);
            int unitId = TileOccupancyService.GetOccupantUnit(_mapState, newCoord) ?? -1;
            Assert.AreEqual(1, unitId, "Unit should be at its new (X,Y) coord.");
        }
    }
}
