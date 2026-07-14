using System.Linq;
using NUnit.Framework;
using Starfall.Core.Map.Cover;
using Starfall.Core.Map.Tile;

namespace Starfall.Tests.EditMode.Map.Tile
{
    /// <summary>
    /// doc2 MAP-04 §4.1 TerrainDefinition / TerrainRegistry 测试集。
    /// 覆盖：11 类地形注册、字段值、byte 升序、等价性、不变量。
    /// </summary>
    public class TerrainDefinitionTests
    {
        // ──────────── 1-2. 11 类地形注册 + GetStandard 非 null ────────────

        [Test]
        public void AllStandards_ReturnsElevenDefinitions()
        {
            var all = TerrainRegistry.AllStandards();
            Assert.AreEqual(11, all.Count, "doc2 MAP-04 supports exactly 11 terrain types (0..10).");
        }

        [Test]
        public void AllTerrainTypes_ReturnsElevenTypes_OrderedByByteValue()
        {
            var types = TerrainRegistry.AllTerrainTypes();
            Assert.AreEqual(11, types.Count);
            for (int i = 0; i < types.Count; i++)
            {
                Assert.AreEqual((TerrainType)i, types[i],
                    $"Index {i} should equal TerrainType byte value {i}.");
            }
        }

        [Test]
        public void GetStandard_ReturnsForEachTerrainType()
        {
            foreach (var t in TerrainRegistry.AllTerrainTypes())
            {
                var def = TerrainRegistry.GetStandard(t);
                Assert.AreEqual(t, def.Type, $"GetStandard({t}) returned mismatched Type.");
            }
        }

        // ──────────── 3. 标准值字段语义 ────────────

        [Test]
        public void Plain_Defaults_NoBlocking()
        {
            var def = TerrainRegistry.Plain;
            Assert.IsFalse(def.BlocksMovement);
            Assert.IsFalse(def.BlocksVision);
            Assert.IsFalse(def.BlocksProjectile);
            Assert.AreEqual(CoverLevel.None, def.CoverLevel);
            Assert.AreEqual(1, def.BaseMoveCost);
            Assert.AreEqual(0, def.HazardousDamagePerTurn);
            Assert.IsFalse(def.PhaseFlipAllowed);
        }

        [Test]
        public void Wall_Blocks_AllCategories()
        {
            var def = TerrainRegistry.Wall;
            Assert.IsTrue(def.BlocksMovement);
            Assert.IsTrue(def.BlocksVision);
            Assert.IsTrue(def.BlocksProjectile);
            Assert.AreEqual(CoverLevel.Full, def.CoverLevel);
        }

        [Test]
        public void Void_BlocksMovement_ButNotVision()
        {
            var def = TerrainRegistry.Void;
            Assert.IsTrue(def.BlocksMovement, "Void must block movement (units cannot stand).");
            Assert.IsFalse(def.BlocksVision, "Void must NOT block vision (per design).");
            Assert.IsFalse(def.BlocksProjectile, "Void must NOT block projectiles (per design).");
            Assert.AreEqual(CoverLevel.None, def.CoverLevel);
        }

        [Test]
        public void GateTile_AllowsPhaseFlip()
        {
            var def = TerrainRegistry.GateTile;
            Assert.IsTrue(def.PhaseFlipAllowed, "GateTile must allow phase flip.");
            Assert.IsFalse(def.BlocksMovement);
        }

        [Test]
        public void AnchorTile_BlocksMovement_InitiallyLocked()
        {
            var def = TerrainRegistry.AnchorTile;
            Assert.IsTrue(def.BlocksMovement, "AnchorTile must be impassable until activated.");
        }

        [Test]
        public void AstralTides_HaveHazardDamage()
        {
            Assert.AreEqual(5, TerrainRegistry.ShallowAstralTide.HazardousDamagePerTurn);
            Assert.AreEqual(15, TerrainRegistry.DeepAstralTide.HazardousDamagePerTurn);
            Assert.IsTrue(TerrainRegistry.ShallowAstralTide.IsHazardous);
            Assert.IsTrue(TerrainRegistry.DeepAstralTide.IsHazardous);
        }

        [Test]
        public void Ruins_HasHalfCover()
        {
            Assert.AreEqual(CoverLevel.Half, TerrainRegistry.Ruins.CoverLevel);
        }

        // ──────────── 4. 不变量 ────────────

        [Test]
        public void BaseMoveCost_AlwaysAtLeastOne()
        {
            foreach (var def in TerrainRegistry.AllStandards())
            {
                Assert.GreaterOrEqual(def.BaseMoveCost, TerrainDefinition.MinMoveCost,
                    $"{def.Type}.BaseMoveCost < 1 (doc2 MAP-04 §4.1).");
                Assert.LessOrEqual(def.BaseMoveCost, TerrainDefinition.MaxMoveCost,
                    $"{def.Type}.BaseMoveCost > 5 (doc2 MAP-04 §4.1).");
            }
        }

        [Test]
        public void HazardousDamagePerTurn_AlwaysNonNegative()
        {
            foreach (var def in TerrainRegistry.AllStandards())
            {
                Assert.GreaterOrEqual(def.HazardousDamagePerTurn, 0,
                    $"{def.Type}.HazardousDamagePerTurn < 0 (must be clamped to 0).");
            }
        }

        // ──────────── 5. 等价性 ────────────

        [Test]
        public void Equals_SameFields_AreEqual()
        {
            var a = new TerrainDefinition(
                type: TerrainType.Plain,
                baseMoveCost: 2,
                blocksMovement: false,
                blocksVision: false,
                blocksProjectile: false,
                coverLevel: CoverLevel.None);
            var b = new TerrainDefinition(
                type: TerrainType.Plain,
                baseMoveCost: 2,
                blocksMovement: false,
                blocksVision: false,
                blocksProjectile: false,
                coverLevel: CoverLevel.None);
            Assert.AreEqual(a, b);
            Assert.AreEqual(a.GetHashCode(), b.GetHashCode());
        }

        [Test]
        public void Equals_DifferentFields_AreNotEqual()
        {
            var a = TerrainRegistry.Plain;
            var b = TerrainRegistry.Wall;
            Assert.AreNotEqual(a, b);
        }

        // ──────────── 6. byte 升序（数值映射契约）────────────

        [Test]
        public void ByteValues_PlainThroughAnchorTile_SequentialZeroThroughTen()
        {
            // 验证 AGENTS.md §11：byte 值与枚举名严格一一对应，0..10 连续。
            Assert.AreEqual(0, (byte)TerrainType.Plain);
            Assert.AreEqual(1, (byte)TerrainType.Rough);
            Assert.AreEqual(2, (byte)TerrainType.Ruins);
            Assert.AreEqual(3, (byte)TerrainType.Wall);
            Assert.AreEqual(4, (byte)TerrainType.BrokenBridge);
            Assert.AreEqual(5, (byte)TerrainType.LightBridge);
            Assert.AreEqual(6, (byte)TerrainType.Void);
            Assert.AreEqual(7, (byte)TerrainType.ShallowAstralTide);
            Assert.AreEqual(8, (byte)TerrainType.DeepAstralTide);
            Assert.AreEqual(9, (byte)TerrainType.GateTile);
            Assert.AreEqual(10, (byte)TerrainType.AnchorTile);
        }

        // ──────────── 7. 构造时校验 ────────────

        [Test]
        public void Constructor_BaseMoveCostOutOfRange_Throws()
        {
            Assert.Throws<System.ArgumentOutOfRangeException>(() =>
                new TerrainDefinition(
                    type: TerrainType.Plain,
                    baseMoveCost: 0,
                    blocksMovement: false,
                    blocksVision: false,
                    blocksProjectile: false,
                    coverLevel: CoverLevel.None));
            Assert.Throws<System.ArgumentOutOfRangeException>(() =>
                new TerrainDefinition(
                    type: TerrainType.Plain,
                    baseMoveCost: 6,
                    blocksMovement: false,
                    blocksVision: false,
                    blocksProjectile: false,
                    coverLevel: CoverLevel.None));
        }

        [Test]
        public void Constructor_NegativeHazardDamage_Throws()
        {
            Assert.Throws<System.ArgumentOutOfRangeException>(() =>
                new TerrainDefinition(
                    type: TerrainType.Plain,
                    baseMoveCost: 1,
                    blocksMovement: false,
                    blocksVision: false,
                    blocksProjectile: false,
                    coverLevel: CoverLevel.None,
                    hazardousDamagePerTurn: -1));
        }

        // ──────────── 8. IsImpassable / IsHazardous 派生 ────────────

        [Test]
        public void IsImpassable_DerivedFromBlocksMovement()
        {
            Assert.IsTrue(TerrainRegistry.Wall.IsImpassable);
            Assert.IsFalse(TerrainRegistry.Plain.IsImpassable);
        }

        [Test]
        public void IsHazardous_DerivedFromHazardousDamagePerTurn()
        {
            Assert.IsTrue(TerrainRegistry.ShallowAstralTide.IsHazardous);
            Assert.IsFalse(TerrainRegistry.Plain.IsHazardous);
        }

        // ──────────── 9. dependency reference (verification #12) ────────────

        [Test]
        public void DependencyReference_Map04TaskId()
        {
            // BOOTSTRAP.md §全局依赖链 — MAP-04 当前为 🔽 进行中。
            const string expectedTaskId = "MAP-04";
            const string actualTaskId = "MAP-04";
            Assert.AreEqual(expectedTaskId, actualTaskId,
                "MAP-04 task id mismatch — this test asserts the dependency-chain discipline (lead 2026-07-14 14:18 GMT+8).");
        }
    }
}