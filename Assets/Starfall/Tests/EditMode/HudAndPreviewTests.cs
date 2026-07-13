using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using Starfall.Core.Anchor;
using Starfall.Core.Combat;
using Starfall.Core.Model;
using Starfall.Core.Status;
using Starfall.Unity.Input;
using Starfall.Unity.Presentation;

namespace Starfall.Tests.EditMode
{
    /// <summary>
    /// Task 18 HUD 与预览：纯逻辑测试（不引用 UnityEngine 渲染管线）。
    /// 覆盖：LegalPreviewHelper / UnitSnapshot / HudSnapshot / BoardSnapshot 派生字段。
    /// </summary>
    public class HudAndPreviewTests
    {
        // ===== Fixtures =====

        private static BoardState MakeBoard(int w, int h, params (int x, int y, TileState st)[] overrides)
        {
            var tiles = new Dictionary<GridPos, TileState>();
            for (int y = 0; y < h; y++)
                for (int x = 0; x < w; x++)
                    tiles[new GridPos(x, y)] = TileState.Normal;
            foreach (var o in overrides) tiles[new GridPos(o.x, o.y)] = o.st;
            return new BoardState(w, h, tiles);
        }

        private static BattleState MakeState(
            int w = 4, int h = 4,
            params UnitState[] units)
        {
            var s = new BattleState(0, Owner.Player, MakeBoard(w, h), null);
            foreach (var u in units) s.AddUnit(u);
            return s;
        }

        // ===== 1. Reachable：合法落点 BFS =====

        [Test]
        public void Reachable_EmptyBoard_ReturnsNeighborsWithinRadius()
        {
            var board = MakeBoard(5, 5);
            var occupied = new List<GridPos>();
            // (2,2) 步长 1 → 4 邻居（不含自身 + 不越界）
            var r = LegalPreviewHelper.Reachable(board, new GridPos(2, 2), occupied, 1);
            Assert.AreEqual(4, r.Count);
            Assert.IsTrue(r.Contains(new GridPos(2, 3))); // 下
            Assert.IsTrue(r.Contains(new GridPos(1, 2))); // 左
            Assert.IsTrue(r.Contains(new GridPos(3, 2))); // 右
            Assert.IsTrue(r.Contains(new GridPos(2, 1))); // 上
        }

        [Test]
        public void Reachable_Blocked_Tiles_AreSkipped()
        {
            var board = MakeBoard(5, 5, (2, 3, TileState.Blocked));
            var occupied = new List<GridPos>();
            var r = LegalPreviewHelper.Reachable(board, new GridPos(2, 2), occupied, 1);
            // (2,3) 被 Blocked，应当不在结果中
            Assert.AreEqual(3, r.Count);
            Assert.IsFalse(r.Contains(new GridPos(2, 3)));
        }

        [Test]
        public void Reachable_OtherUnits_AreObstacles()
        {
            var board = MakeBoard(5, 5);
            var occupied = new List<GridPos> { new GridPos(2, 3) }; // 占住 (2,3)
            var r = LegalPreviewHelper.Reachable(board, new GridPos(2, 2), occupied, 1);
            Assert.IsFalse(r.Contains(new GridPos(2, 3)));
        }

        [Test]
        public void Reachable_Deterministic_NeighborOrder_DownLeftRightUp()
        {
            // AGENTS.md §11：邻居顺序 = 下、左、右、上
            // 4 步到 (2,2) 之后没有阻塞时，结果应包含全部 16 邻居（半径 1..4 = 不同距离）
            // 关键：result 内部应按 (Y, X) 升序，且必须包含至少一个 Down/Left/Right/Up 各方向的格
            var board = MakeBoard(8, 8);
            var r = LegalPreviewHelper.Reachable(board, new GridPos(4, 4), new List<GridPos>(), 4);
            // 半径 4 内、不含自身 = 4+8+12+16-1 = 39
            // 简单测：必须包含 4 个直接邻居
            Assert.IsTrue(r.Contains(new GridPos(4, 5))); // 下
            Assert.IsTrue(r.Contains(new GridPos(3, 4))); // 左
            Assert.IsTrue(r.Contains(new GridPos(5, 4))); // 右
            Assert.IsTrue(r.Contains(new GridPos(4, 3))); // 上
            // 顺序：检查 result 自身是 (Y, X) 升序
            for (int i = 1; i < r.Count; i++)
            {
                Assert.LessOrEqual(r[i - 1].CompareTo(r[i]), 0,
                    $"Reachable list not sorted at index {i}: {r[i - 1]} vs {r[i]}");
            }
        }

        [Test]
        public void Reachable_ZeroSteps_ReturnsEmpty()
        {
            var board = MakeBoard(3, 3);
            var r = LegalPreviewHelper.Reachable(board, new GridPos(0, 0), new List<GridPos>(), 0);
            Assert.AreEqual(0, r.Count);
        }

        // ===== 2. AdjacentEnemies：攻击目标 =====

        [Test]
        public void AdjacentEnemies_PlayerAttacker_FindsAdjacentEnemy()
        {
            var player = new UnitState(1, new GridPos(1, 1), 10, 10, Phase.Light, Owner.Player);
            var enemy1 = new UnitState(2, new GridPos(1, 2), 10, 10, Phase.Light, Owner.Enemy); // 下
            var enemy2 = new UnitState(3, new GridPos(2, 1), 10, 10, Phase.Light, Owner.Enemy); // 右
            var far    = new UnitState(4, new GridPos(3, 3), 10, 10, Phase.Light, Owner.Enemy); // 远
            var ally   = new UnitState(5, new GridPos(0, 0), 10, 10, Phase.Light, Owner.Player);
            var s = MakeState(5, 5, player, enemy1, enemy2, far, ally);

            var targets = LegalPreviewHelper.AdjacentEnemies(s, 1);
            Assert.AreEqual(2, targets.Count);
            Assert.IsTrue(System.Array.IndexOf(
                new[] { targets[0].UnitId, targets[1].UnitId }, 2) >= 0);
            Assert.IsTrue(System.Array.IndexOf(
                new[] { targets[0].UnitId, targets[1].UnitId }, 3) >= 0);
        }

        [Test]
        public void AdjacentEnemies_PlayerAttacker_NoEnemy_ReturnsEmpty()
        {
            var player = new UnitState(1, new GridPos(0, 0), 10, 10, Phase.Light, Owner.Player);
            var ally = new UnitState(2, new GridPos(0, 1), 10, 10, Phase.Light, Owner.Player);
            var s = MakeState(3, 3, player, ally);
            var targets = LegalPreviewHelper.AdjacentEnemies(s, 1);
            Assert.AreEqual(0, targets.Count);
        }

        [Test]
        public void AdjacentEnemies_InvalidAttacker_ReturnsEmpty()
        {
            var s = MakeState(3, 3);
            var targets = LegalPreviewHelper.AdjacentEnemies(s, 999);
            Assert.AreEqual(0, targets.Count);
        }

        // ===== 3. FallTargets：坠落预览 =====

        [Test]
        public void FallTargets_DarkPhase_UnitOnHazard_IsFlagged()
        {
            var player = new UnitState(1, new GridPos(1, 1), 10, 10, Phase.Dark, Owner.Player);
            var s = MakeState(3, 3, player);
            // 把 (1,1) 标成 Hazard
            s.Board.Tiles.TryGetValue(new GridPos(1, 1), out _); // sanity
            // 通过 tiles dict 替换：用 reflection-free 方式 — 重新构建 board
            var tiles = new Dictionary<GridPos, TileState>();
            for (int y = 0; y < 3; y++) for (int x = 0; x < 3; x++)
                tiles[new GridPos(x, y)] = TileState.Normal;
            tiles[new GridPos(1, 1)] = TileState.Hazard;
            s = new BattleState(0, Owner.Player, new BoardState(3, 3, tiles), null);
            s.AddUnit(new UnitState(1, new GridPos(1, 1), 10, 10, Phase.Dark, Owner.Player));

            var falls = LegalPreviewHelper.FallTargets(s, Phase.Dark);
            Assert.AreEqual(1, falls.Count);
            Assert.AreEqual(1, falls[0].UnitId);
            Assert.AreEqual(1, falls[0].FallDamage);
        }

        [Test]
        public void FallTargets_LightPhase_ReturnsEmpty()
        {
            var tiles = new Dictionary<GridPos, TileState>();
            for (int y = 0; y < 3; y++) for (int x = 0; x < 3; x++)
                tiles[new GridPos(x, y)] = TileState.Hazard; // 全是 hazard
            var s = new BattleState(0, Owner.Player, new BoardState(3, 3, tiles), null);
            s.AddUnit(new UnitState(1, new GridPos(1, 1), 10, 10, Phase.Light, Owner.Player));

            var falls = LegalPreviewHelper.FallTargets(s, Phase.Light);
            Assert.AreEqual(0, falls.Count);
        }

        // ===== 4. DamagePreview：伤害数字 =====

        [Test]
        public void DamagePreview_SamePhase_ReturnsBase()
        {
            var atk = new UnitState(1, new GridPos(0, 0), 10, 10, Phase.Light, Owner.Player);
            var tgt = new UnitState(2, new GridPos(0, 1), 10, 10, Phase.Light, Owner.Enemy);
            var s = MakeState(3, 3, atk, tgt);
            // Light vs Light → 1.0x → 3
            Assert.AreEqual(3, LegalPreviewHelper.PreviewDamage(s, 1, 2, 3));
        }

        [Test]
        public void DamagePreview_DiffPhase_Returns1p5x()
        {
            var atk = new UnitState(1, new GridPos(0, 0), 10, 10, Phase.Light, Owner.Player);
            var tgt = new UnitState(2, new GridPos(0, 1), 10, 10, Phase.Dark, Owner.Enemy);
            var s = MakeState(3, 3, atk, tgt);
            // Light vs Dark → 1.5x → 3 * 3 / 2 = 4
            Assert.AreEqual(4, LegalPreviewHelper.PreviewDamage(s, 1, 2, 3));
        }

        [Test]
        public void DamagePreview_NotAdjacent_ReturnsMinus1()
        {
            var atk = new UnitState(1, new GridPos(0, 0), 10, 10, Phase.Light, Owner.Player);
            var tgt = new UnitState(2, new GridPos(2, 2), 10, 10, Phase.Light, Owner.Enemy);
            var s = MakeState(5, 5, atk, tgt);
            Assert.AreEqual(-1, LegalPreviewHelper.PreviewDamage(s, 1, 2, 3));
        }

        [Test]
        public void DamagePreview_WithBurnStatus_Adds1()
        {
            var atk = new UnitState(1, new GridPos(0, 0), 10, 10, Phase.Light, Owner.Player);
            var tgt = new UnitState(2, new GridPos(0, 1), 10, 10, Phase.Light, Owner.Enemy);
            var s = MakeState(3, 3, atk, tgt);
            s.AddStatus(new StatusInstance(1, StatusKind.Burn, 2, sourceUnitId: 1));
            // 3 + 1 = 4
            Assert.AreEqual(4, LegalPreviewHelper.PreviewDamage(s, 1, 2, 3));
        }

        // ===== 5. DeriveStats：HUD 派生值 =====

        [Test]
        public void DeriveStats_ApEqualsMaxHp_CvSumsStatuses()
        {
            var u = new UnitState(1, new GridPos(0, 0), 5, 10, Phase.Light, Owner.Player);
            var statuses = new List<StatusInstance>
            {
                new StatusInstance(10, StatusKind.Burn, 2, 1),
                new StatusInstance(11, StatusKind.PhaseInvert, 1, 1),
            };
            var d = LegalPreviewHelper.DeriveStats(u, statuses);
            Assert.AreEqual(Phase.Light, d.Pv);
            Assert.AreEqual(10, d.Ap);
            Assert.AreEqual(3, d.Cv);
            Assert.AreEqual(5, d.Hp);
            Assert.AreEqual(10, d.MaxHp);
        }

        [Test]
        public void DeriveStats_StatusesFromOtherUnits_AreIgnored()
        {
            var u = new UnitState(1, new GridPos(0, 0), 5, 10, Phase.Light, Owner.Player);
            var other = new UnitState(2, new GridPos(1, 0), 5, 10, Phase.Light, Owner.Player);
            var statuses = new List<StatusInstance>
            {
                new StatusInstance(10, StatusKind.Burn, 2, 2), // source = other, 应被忽略
            };
            _ = other; // 引用以免 unused 警告
            var d = LegalPreviewHelper.DeriveStats(u, statuses);
            Assert.AreEqual(0, d.Cv);
        }

        // ===== 6. UnitSnapshot 扩展 =====

        [Test]
        public void UnitSnapshot_4ArgCtor_BackwardCompatible_DefaultsPvApCv()
        {
            var s = new UnitSnapshot(1, new GridPos(0, 0), 10, Phase.Dark, Owner.Player);
            Assert.AreEqual(1, s.UnitId);
            Assert.AreEqual(Phase.Dark, s.Phase);
            Assert.AreEqual(Phase.Dark, s.Pv); // PV 默认 = Phase
            Assert.AreEqual(0, s.Ap);
            Assert.AreEqual(0, s.Cv);
        }

        [Test]
        public void UnitSnapshot_8ArgCtor_StoresAllFields()
        {
            var s = new UnitSnapshot(1, new GridPos(0, 0), 10, maxHp: 10, Phase.Light, Owner.Player,
                pv: Phase.Dark, ap: 5, cv: 3);
            Assert.AreEqual(Phase.Dark, s.Pv);
            Assert.AreEqual(5, s.Ap);
            Assert.AreEqual(3, s.Cv);
        }

        // ===== 7. HudSnapshot 扩展 =====

        [Test]
        public void HudSnapshot_FromStateWithInput_FillsObjectiveAndAnchorCount()
        {
            var player = new UnitState(1, new GridPos(0, 0), 10, 10, Phase.Light, Owner.Player);
            var s = MakeState(4, 4, player);
            s.Anchors.Register(new Core.Anchor.AnchorZone(7, "Player", new[]
            {
                new GridPos(0, 0), new GridPos(1, 0), new GridPos(1, 1)
            }));

            var h = HudSnapshot.FromStateWithInput(
                s, BattleOutcome.Ongoing,
                selectedUnitId: 1,
                inputMode: InputMode.SelectUnit,
                lastInputMessage: "[Select] unit #1 ready");

            Assert.AreEqual(Phase.Light, h.CurrentPhase);
            Assert.AreEqual(3, h.AnchorTileCount);
            StringAssert.Contains("Guard", h.ObjectiveText);
            StringAssert.Contains("3", h.ObjectiveText);
            Assert.AreEqual(InputMode.SelectUnit, h.InputModeHint);
            Assert.AreEqual("[Select] unit #1 ready", h.LastInputMessage);
            Assert.IsTrue(h.ActiveUnit.HasValue);
            Assert.AreEqual(1, h.ActiveUnit.Value.UnitId);
        }

        [Test]
        public void HudSnapshot_FromStateWithInput_NoAnchor_FallbackMessage()
        {
            var player = new UnitState(1, new GridPos(0, 0), 10, 10, Phase.Light, Owner.Player);
            var s = MakeState(3, 3, player);

            var h = HudSnapshot.FromStateWithInput(
                s, BattleOutcome.Ongoing, null, InputMode.None, null);

            Assert.AreEqual(0, h.AnchorTileCount);
            StringAssert.Contains("no player anchor", h.ObjectiveText);
        }

        [Test]
        public void HudSnapshot_Legacy3ArgCtor_BackwardCompatible()
        {
            var s = MakeState(3, 3);
            var h = new HudSnapshot(5, Owner.Enemy, "Ongoing");
            Assert.AreEqual(5, h.TurnNumber);
            Assert.AreEqual(Owner.Enemy, h.ActivePlayer);
            Assert.AreEqual(Phase.Light, h.CurrentPhase); // default
            Assert.IsNull(h.ActiveUnit);
        }

        // ===== 8. BoardSnapshot.FromStateWithPreview 集成 =====

        [Test]
        public void BoardSnapshot_FromStateWithPreview_MoveTarget_ProducesLegalMoves()
        {
            var player = new UnitState(1, new GridPos(2, 2), 10, 10, Phase.Light, Owner.Player);
            var s = MakeState(5, 5, player);

            var snap = BoardSnapshot.FromStateWithPreview(
                s, selectedUnitId: 1, inputMode: InputMode.MoveTarget, cursor: new GridPos(2, 2));

            Assert.AreEqual(InputMode.MoveTarget, snap.InputModeHint);
            Assert.AreEqual(1, snap.SelectedUnitIdForPreview);
            Assert.IsTrue(snap.LegalMoves.Count > 0);
            // 半径 5 + 空棋盘 → 24 格可达
            Assert.AreEqual(24, snap.LegalMoves.Count);
            // 顺序：必须 (Y, X) 升序
            for (int i = 1; i < snap.LegalMoves.Count; i++)
                Assert.LessOrEqual(snap.LegalMoves[i - 1].CompareTo(snap.LegalMoves[i]), 0);
        }

        [Test]
        public void BoardSnapshot_FromStateWithPreview_AttackTarget_ProducesAttackTargets()
        {
            var player = new UnitState(1, new GridPos(2, 2), 10, 10, Phase.Light, Owner.Player);
            var enemy1 = new UnitState(2, new GridPos(2, 3), 10, 10, Phase.Light, Owner.Enemy); // 邻 (下)
            var enemy2 = new UnitState(3, new GridPos(4, 4), 10, 10, Phase.Light, Owner.Enemy); // 远
            var s = MakeState(5, 5, player, enemy1, enemy2);

            var snap = BoardSnapshot.FromStateWithPreview(
                s, selectedUnitId: 1, inputMode: InputMode.AttackTarget, cursor: new GridPos(2, 2));

            Assert.AreEqual(InputMode.AttackTarget, snap.InputModeHint);
            Assert.AreEqual(1, snap.AttackTargets.Count);
            Assert.AreEqual(2, snap.AttackTargets[0].UnitId); // enemy1 是唯一邻格敌人
        }

        [Test]
        public void BoardSnapshot_FromStateWithPreview_SelectUnitMode_EmptyPreviews()
        {
            var player = new UnitState(1, new GridPos(0, 0), 10, 10, Phase.Light, Owner.Player);
            var s = MakeState(3, 3, player);

            var snap = BoardSnapshot.FromStateWithPreview(
                s, selectedUnitId: 1, inputMode: InputMode.SelectUnit, cursor: null);

            Assert.AreEqual(0, snap.LegalMoves.Count);
            Assert.AreEqual(0, snap.AttackTargets.Count);
        }

        [Test]
        public void BoardSnapshot_FromStateWithPreview_DarkPhase_FallTargets_Filled()
        {
            var tiles = new Dictionary<GridPos, TileState>();
            for (int y = 0; y < 3; y++) for (int x = 0; x < 3; x++)
                tiles[new GridPos(x, y)] = TileState.Hazard;
            var s = new BattleState(0, Owner.Player, new BoardState(3, 3, tiles), null);
            s.AddUnit(new UnitState(1, new GridPos(1, 1), 10, 10, Phase.Dark, Owner.Player));

            var snap = BoardSnapshot.FromStateWithPreview(
                s, null, InputMode.SelectUnit, null);

            // Phase = Dark → FallTargets 至少 1 个
            Assert.IsTrue(snap.FallPreviews.Count >= 1);
            Assert.AreEqual(1, snap.FallPreviews[0].UnitId);
        }

        [Test]
        public void BoardSnapshot_FromState_UnitsCarryDerivedStats()
        {
            var player = new UnitState(1, new GridPos(0, 0), 7, 10, Phase.Light, Owner.Player);
            var s = MakeState(3, 3, player);
            s.AddStatus(new StatusInstance(1, StatusKind.Burn, 3, 1));

            var snap = BoardSnapshot.FromState(s);
            Assert.AreEqual(1, snap.Units.Count);
            Assert.AreEqual(Phase.Light, snap.Units[0].Pv);
            Assert.AreEqual(10, snap.Units[0].Ap);
            Assert.AreEqual(3, snap.Units[0].Cv);
            Assert.AreEqual(7, snap.Units[0].Hp);
        }

        // ===== 9. AGENTS.md §11 确定性：BFS 邻居顺序 = 下、左、右、上 =====

        [Test]
        public void Reachable_NeighborOrder_DownLeftRightUp_FirstStep()
        {
            // 起点 (X=2,Y=2)，半径 1：4 个邻居：
            //   下：(X=2,Y=3)
            //   左：(X=1,Y=2)
            //   右：(X=3,Y=2)
            //   上：(X=2,Y=1)
            // (Y,X) 升序：(X=2,Y=1) Y=1 → 第一
            //             (X=1,Y=2) Y=2,X=1 → 第二
            //             (X=3,Y=2) Y=2,X=3 → 第三
            //             (X=2,Y=3) Y=3 → 第四
            var board = MakeBoard(5, 5);
            var r = LegalPreviewHelper.Reachable(board, new GridPos(2, 2), new List<GridPos>(), 1);
            Assert.AreEqual(4, r.Count);
            Assert.AreEqual(new GridPos(2, 1), r[0]); // Y=1 (上)
            Assert.AreEqual(new GridPos(1, 2), r[1]); // Y=2, X=1 (左)
            Assert.AreEqual(new GridPos(3, 2), r[2]); // Y=2, X=3 (右)
            Assert.AreEqual(new GridPos(2, 3), r[3]); // Y=3 (下)
        }

        [Test]
        public void AdjacentEnemies_Order_ByYX()
        {
            // 攻击者 (X=2,Y=2) 周围三个敌人：
            //   e2: (X=2,Y=1) 上方 → Y=1
            //   e3: (X=1,Y=2) 左侧 → Y=2, X=1
            //   e1: (X=3,Y=2) 右侧 → Y=2, X=3
            // (Y,X) 升序：e2, e3, e1
            var atk = new UnitState(1, new GridPos(2, 2), 10, 10, Phase.Light, Owner.Player);
            var e1 = new UnitState(2, new GridPos(3, 2), 10, 10, Phase.Light, Owner.Enemy);
            var e2 = new UnitState(3, new GridPos(2, 1), 10, 10, Phase.Light, Owner.Enemy);
            var e3 = new UnitState(4, new GridPos(1, 2), 10, 10, Phase.Light, Owner.Enemy);
            var s = MakeState(5, 5, atk, e1, e2, e3);

            var targets = LegalPreviewHelper.AdjacentEnemies(s, 1);
            Assert.AreEqual(3, targets.Count);
            Assert.AreEqual(new GridPos(2, 1), targets[0].Pos); // Y=1
            Assert.AreEqual(new GridPos(1, 2), targets[1].Pos); // Y=2, X=1
            Assert.AreEqual(new GridPos(3, 2), targets[2].Pos); // Y=2, X=3
        }
    }
}
