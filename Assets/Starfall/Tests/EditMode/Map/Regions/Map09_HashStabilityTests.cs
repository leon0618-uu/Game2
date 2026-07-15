using NUnit.Framework;
using Starfall.Core.Map.Coordinates;
using Starfall.Core.Map.Regions;
using Starfall.Core.Map.State;

namespace Starfall.Tests.EditMode.Map.Regions
{
    /// <summary>
    /// doc2 MAP-09 100-run 哈希稳定性测试集（与 ADR-0003 §4 一致）。
    /// <para/>
    /// 覆盖：MapRegionState.PostStateHash 100-run 稳定 / MapStateHasher 跨 100-run 稳定
    ///（含 RegionStates + SpawnPoints 集合）。
    /// </summary>
    public class Map09_HashStabilityTests
    {
        private static MapRegionDefinition MakeDef(int regionId, RegionKind kind = RegionKind.Capture)
        {
            return new MapRegionDefinition(
                new RegionId(regionId),
                kind,
                new[] {
                    new GridCoord(0, 0), new GridCoord(3, 0),
                    new GridCoord(3, 3), new GridCoord(0, 3)
                },
                ownerSide: -1,
                priority: 50,
                activation: RegionActivation.Available);
        }

        // ──────────── 1) MapRegionState.PostStateHash 100-run 稳定 ────────────

        [Test]
        public void MapRegionState_Hash_IsStable_Over100Runs()
        {
            var rs = new MapRegionState(MakeDef(7, RegionKind.Capture));
            ulong h0 = rs.PostStateHash;
            for (int i = 0; i < 100; i++)
            {
                Assert.AreEqual(h0, rs.PostStateHash,
                    $"Hash drift at iteration {i}");
            }
        }

        // ──────────── 2) MapStateHasher 100-run 稳定（含 region / spawn）────────────

        [Test]
        public void MapState_Hash_With_RegionStates_And_SpawnPoints_IsStable_Over100Runs()
        {
            var def = new MapDefinition("map.test", 8, 8, DimensionLayer.Reality, 0);
            var map = new MapState(def);
            map.AddRegionState(new MapRegionState(MakeDef(1, RegionKind.Capture)));
            map.AddRegionState(new MapRegionState(MakeDef(2, RegionKind.Defense)));
            map.AddSpawnPoint(new MapSpawnPoint(new SpawnId(1), 1, new GridCoord(1, 1), 0));
            map.AddSpawnPoint(new MapSpawnPoint(new SpawnId(2), 2, new GridCoord(5, 5), 1));

            ulong h0 = map.PostStateHash;
            for (int i = 0; i < 100; i++)
            {
                Assert.AreEqual(h0, map.PostStateHash,
                    $"Hash drift at iteration {i}");
            }
        }

        // ──────────── 3) 不同 region 集合产生不同哈希（sanity）────────────

        [Test]
        public void MapState_Hash_DiffersBy_RegionAddition()
        {
            var def = new MapDefinition("map.test", 8, 8, DimensionLayer.Reality, 0);
            var mapA = new MapState(def);
            var mapB = new MapState(def);
            mapB.AddRegionState(new MapRegionState(MakeDef(1, RegionKind.Capture)));
            Assert.AreNotEqual(mapA.PostStateHash, mapB.PostStateHash);
        }

        [Test]
        public void MapState_Hash_DiffersBy_SpawnAddition()
        {
            var def = new MapDefinition("map.test", 8, 8, DimensionLayer.Reality, 0);
            var mapA = new MapState(def);
            var mapB = new MapState(def);
            mapB.AddSpawnPoint(new MapSpawnPoint(new SpawnId(1), 0, new GridCoord(0, 0), 0));
            Assert.AreNotEqual(mapA.PostStateHash, mapB.PostStateHash);
        }

        // ──────────── 4) 不同 region definition 输入顺序产生相同哈希（hash 内部排序）────────────

        [Test]
        public void MapState_Hash_Ignores_BoundsInputOrder()
        {
            var def1 = new MapRegionDefinition(
                new RegionId(1), RegionKind.Capture,
                new[] {
                    new GridCoord(0, 0), new GridCoord(3, 0),
                    new GridCoord(3, 3), new GridCoord(0, 3)
                });
            var def2 = new MapRegionDefinition(
                new RegionId(1), RegionKind.Capture,
                new[] {
                    new GridCoord(3, 3), new GridCoord(0, 3),
                    new GridCoord(3, 0), new GridCoord(0, 0)
                });

            var rs1 = new MapRegionState(def1);
            var rs2 = new MapRegionState(def2);
            Assert.AreEqual(rs1.PostStateHash, rs2.PostStateHash);
        }
    }
}