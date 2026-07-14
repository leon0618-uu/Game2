using NUnit.Framework;
using System.Collections.Generic;
using Starfall.Core.Map.Coordinates;
using Starfall.Core.Map.Tile;

namespace Starfall.Tests.EditMode.Map.Tile
{
    /// <summary>
    /// doc2 MAP-04 §4.6 MapTileState 测试集。
    /// 覆盖：构造初始状态、EffectiveMoveCost、IsPassable 派生、
    /// OccupyingUnitId 语义、TemporaryMoveCostModifier、Stability 坍塌、
    /// ActiveMapEffects、IsRevealed 一次性揭示。
    /// </summary>
    public class MapTileStateTests
    {
        private static TileDefinition MakePlain(int tileId = 1, int x = 0, int y = 0)
        {
            return TileDefinitionRegistry.Make(tileId, new GridCoord(x, y), TerrainType.Plain);
        }

        // ──────────── 1. 构造初始状态 ────────────

        [Test]
        public void Construction_InitialStability_IsHundred()
        {
            var def = MakePlain();
            var state = new MapTileState(def);
            Assert.AreEqual(100, state.Stability);
        }

        [Test]
        public void Construction_InitialIsPassable_TrueForPlain()
        {
            var def = MakePlain();
            var state = new MapTileState(def);
            Assert.IsTrue(state.IsPassable, "Plain should be initially passable.");
        }

        [Test]
        public void Construction_InitialIsPassable_FalseForWall()
        {
            var def = new TileDefinition(
                tileId: 1,
                coord: new GridCoord(0, 0),
                terrainType: TerrainType.Wall,
                terrain: TerrainRegistry.Wall);
            var state = new MapTileState(def);
            Assert.IsFalse(state.IsPassable);
        }

        [Test]
        public void Construction_InitialIsRevealed_False()
        {
            var def = MakePlain();
            var state = new MapTileState(def);
            Assert.IsFalse(state.IsRevealed);
            Assert.IsFalse(state.IsVisible);
        }

        [Test]
        public void Construction_InitialOccupyingIds_AreNull()
        {
            var def = MakePlain();
            var state = new MapTileState(def);
            Assert.IsFalse(state.OccupyingUnitId.HasValue);
            Assert.IsFalse(state.OccupyingObjectId.HasValue);
        }

        [Test]
        public void Construction_InitialLocalCollapseValue_IsZero()
        {
            var def = MakePlain();
            var state = new MapTileState(def);
            Assert.AreEqual(0, state.LocalCollapseValue);
        }

        // ──────────── 2. EffectiveMoveCost ────────────

        [Test]
        public void EffectiveMoveCost_Default_EqualsBaseMoveCost()
        {
            var state = new MapTileState(MakePlain());
            Assert.AreEqual(1, state.EffectiveMoveCost, "Plain BaseMoveCost = 1.");
        }

        [Test]
        public void EffectiveMoveCost_WithModifier_ReflectsSum()
        {
            var state = new MapTileState(MakePlain());
            state.TemporaryMoveCostModifier = 2;
            Assert.AreEqual(3, state.EffectiveMoveCost);
        }

        [Test]
        public void EffectiveMoveCost_NegativeModifier_ClampedToOne()
        {
            var state = new MapTileState(MakePlain());
            state.TemporaryMoveCostModifier = -5;
            // clamp 到 1（避免 ≤ 0）。
            Assert.AreEqual(1, state.EffectiveMoveCost);
        }

        // ──────────── 3. OccupyingUnitId 影响 IsPassable 派生 ────────────

        [Test]
        public void OccupyingUnitId_Set_DoesNotAutoSetIsPassable()
        {
            // MapTileState.IsPassable 由构造 + 命令层维护；OccupyingUnitId 是独立字段。
            // 验证 IsPassable 由 IsEffectivelyPassable 派生（不是 IsPassable 字段本身）。
            var state = new MapTileState(MakePlain());
            state.OccupyingUnitId = 7;
            // IsPassable 字段保持 true（构造值），但 IsEffectivelyPassable 应该 false。
            Assert.IsTrue(state.IsPassable);
            Assert.IsFalse(state.IsEffectivelyPassable);
            Assert.IsTrue(state.IsOccupiedByUnit);
        }

        // ──────────── 4. TemporaryMoveCostModifier 可负（不破 IsPassable）────────────

        [Test]
        public void TemporaryMoveCostModifier_NegativeValue_IsAccepted()
        {
            var state = new MapTileState(MakePlain());
            state.TemporaryMoveCostModifier = -3;
            Assert.AreEqual(-3, state.TemporaryMoveCostModifier);
            // IsPassable 不变。
            Assert.IsTrue(state.IsPassable);
        }

        // ──────────── 5. Stability 降到 0 → IsEffectivelyPassable = false ────────────

        [Test]
        public void Stability_Zero_MakesTileUnpassable()
        {
            var state = new MapTileState(MakePlain());
            state.Stability = 0;
            Assert.IsTrue(state.HasCollapsed);
            Assert.IsFalse(state.IsEffectivelyPassable);
        }

        [Test]
        public void Stability_Positive_KeepsTilePassable()
        {
            var state = new MapTileState(MakePlain());
            state.Stability = 50;
            Assert.IsFalse(state.HasCollapsed);
            Assert.IsTrue(state.IsEffectivelyPassable);
        }

        // ──────────── 6. ActiveMapEffects ────────────

        [Test]
        public void ActiveMapEffects_AddAndRemove()
        {
            var state = new MapTileState(MakePlain());
            Assert.AreEqual(0, state.ActiveMapEffects.Count);

            state.AddEffect("OnFire");
            state.AddEffect("Frozen");
            Assert.AreEqual(2, state.ActiveMapEffects.Count);
            Assert.IsTrue(ContainsString(state.ActiveMapEffects, "OnFire"));

            Assert.IsTrue(state.RemoveEffect("OnFire"));
            Assert.AreEqual(1, state.ActiveMapEffects.Count);
            Assert.IsFalse(ContainsString(state.ActiveMapEffects, "OnFire"));
        }

        [Test]
        public void ActiveMapEffects_AddDuplicate_IsNoOp()
        {
            var state = new MapTileState(MakePlain());
            state.AddEffect("OnFire");
            state.AddEffect("OnFire");
            Assert.AreEqual(1, state.ActiveMapEffects.Count);
        }

        [Test]
        public void ActiveMapEffects_RemoveNonExistent_ReturnsFalse()
        {
            var state = new MapTileState(MakePlain());
            Assert.IsFalse(state.RemoveEffect("NonExistent"));
            Assert.IsFalse(state.RemoveEffect(null));
        }

        // ──────────── 7. IsRevealed 一次性揭示 ────────────

        [Test]
        public void IsRevealed_SetTrue_StaysTrue()
        {
            var state = new MapTileState(MakePlain());
            state.IsRevealed = true;
            Assert.IsTrue(state.IsRevealed);
        }

        [Test]
        public void IsRevealed_DefaultFalse_RemainsFalse()
        {
            var state = new MapTileState(MakePlain());
            Assert.IsFalse(state.IsRevealed);
        }

        // ──────────── 8. OccupyingObjectId ────────────

        [Test]
        public void OccupyingObjectId_SetAndGet()
        {
            var state = new MapTileState(MakePlain());
            state.OccupyingObjectId = 99;
            Assert.AreEqual(99, state.OccupyingObjectId);
            Assert.IsTrue(state.IsOccupiedByObject);
        }

        // ──────────── 9. Definition / TileId / Coord 派生 ────────────

        [Test]
        public void Definition_TileId_Coord_Exposed()
        {
            var def = MakePlain(tileId: 5, x: 2, y: 3);
            var state = new MapTileState(def);
            Assert.AreEqual(def, state.Definition);
            Assert.AreEqual(5, state.TileId);
            Assert.AreEqual(new GridCoord(2, 3), state.Coord);
        }


        private static bool ContainsString(IReadOnlyList<string> list, string value)
        {
            for (int i = 0; i < list.Count; i++)
                if (string.Equals(list[i], value, System.StringComparison.Ordinal)) return true;
            return false;
        }
    }
}