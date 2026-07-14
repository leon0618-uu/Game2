using NUnit.Framework;
using Starfall.Core.Map.Coordinates;
using Starfall.Core.Map.Cover;
using Starfall.Core.Map.Tile;
using TileStateLegacy = Starfall.Core.Model.TileState;

namespace Starfall.Tests.EditMode.Map.Tile
{
    /// <summary>
    /// doc2 MAP-04 §4.7 LegacyTileStateAdapter 测试集。
    /// 覆盖：4 类旧 enum → 新 TileDefinition 的桥接映射。
    /// </summary>
    public class LegacyTileStateAdapterTests
    {
        private static GridCoord AnyCoord(int x = 0, int y = 0)
            => new GridCoord(x, y);

        // ──────────── 1. Normal → Plain + Walkable ────────────

        [Test]
        public void Normal_MapsToPlain_WithWalkableTag()
        {
            var def = LegacyTileStateAdapter.ToTileDefinition(
                TileStateLegacy.Normal, tileId: 1, coord: AnyCoord(2, 3));
            Assert.AreEqual(TerrainType.Plain, def.TerrainType);
            Assert.IsTrue((def.Tags & TileTags.Walkable) == TileTags.Walkable);
            Assert.IsFalse(def.BlocksMovement);
        }

        // ──────────── 2. Blocked → Wall + 三个阻挡标签 + Full Cover ────────────

        [Test]
        public void Blocked_MapsToWall_WithImpassableAndFullCover()
        {
            var def = LegacyTileStateAdapter.ToTileDefinition(
                TileStateLegacy.Blocked, tileId: 1, coord: AnyCoord());
            Assert.AreEqual(TerrainType.Wall, def.TerrainType);
            Assert.IsTrue(def.BlocksMovement, "Blocked must map to a movement-blocking terrain.");
            Assert.AreEqual(CoverLevel.Full, def.CoverLevel);
            Assert.IsTrue((def.Tags & TileTags.Impassable) == TileTags.Impassable);
            Assert.IsTrue((def.Tags & TileTags.VisionBlocker) == TileTags.VisionBlocker);
            Assert.IsTrue((def.Tags & TileTags.ProjectileBlocker) == TileTags.ProjectileBlocker);
        }

        // ──────────── 3. Hazard → Plain + Hazardous tag ────────────

        [Test]
        public void Hazard_MapsToPlain_WithHazardousTag()
        {
            var def = LegacyTileStateAdapter.ToTileDefinition(
                TileStateLegacy.Hazard, tileId: 1, coord: AnyCoord());
            Assert.AreEqual(TerrainType.Plain, def.TerrainType);
            Assert.IsTrue((def.Tags & TileTags.Hazardous) == TileTags.Hazardous);
            // 仍可通行（hazardous 不阻挡移动）。
            Assert.IsFalse(def.BlocksMovement);
        }

        // ──────────── 4. Objective → Plain + GuardObjective tag ────────────

        [Test]
        public void Objective_MapsToPlain_WithGuardObjectiveTag()
        {
            var def = LegacyTileStateAdapter.ToTileDefinition(
                TileStateLegacy.Objective, tileId: 1, coord: AnyCoord());
            Assert.AreEqual(TerrainType.Plain, def.TerrainType);
            Assert.IsTrue((def.Tags & TileTags.GuardObjective) == TileTags.GuardObjective);
            Assert.IsFalse(def.BlocksMovement, "Objective tile remains walkable.");
        }

        // ──────────── 5. 边界：tileId 验证 ────────────

        [Test]
        public void ZeroTileId_Throws()
        {
            Assert.Throws<System.ArgumentOutOfRangeException>(() =>
                LegacyTileStateAdapter.ToTileDefinition(
                    TileStateLegacy.Normal, tileId: 0, coord: AnyCoord()));
        }

        [Test]
        public void NegativeTileId_Throws()
        {
            Assert.Throws<System.ArgumentOutOfRangeException>(() =>
                LegacyTileStateAdapter.ToTileDefinition(
                    TileStateLegacy.Normal, tileId: -1, coord: AnyCoord()));
        }

        [Test]
        public void TileIdOne_IsAccepted()
        {
            var def = LegacyTileStateAdapter.ToTileDefinition(
                TileStateLegacy.Normal, tileId: 1, coord: AnyCoord());
            Assert.AreEqual(1, def.TileId);
        }

        // ──────────── 6. Coord 透传 ────────────

        [Test]
        public void Coord_PassesThroughUnchanged()
        {
            var coord = new GridCoord(7, 5, DimensionLayer.Astral);
            var def = LegacyTileStateAdapter.ToTileDefinition(
                TileStateLegacy.Normal, tileId: 1, coord: coord);
            Assert.AreEqual(coord, def.Coord);
        }

        // ──────────── 7. 确定性：相同输入 → 相同输出 ────────────

        [Test]
        public void Deterministic_SameInput_SameOutput()
        {
            var a = LegacyTileStateAdapter.ToTileDefinition(
                TileStateLegacy.Blocked, tileId: 5, coord: new GridCoord(3, 4));
            var b = LegacyTileStateAdapter.ToTileDefinition(
                TileStateLegacy.Blocked, tileId: 5, coord: new GridCoord(3, 4));
            Assert.AreEqual(a, b);
            Assert.AreEqual(a.GetHashCode(), b.GetHashCode());
        }
    }
}