using NUnit.Framework;
using Starfall.Core.Map.Coordinates;
using Starfall.Core.Map.Regions;
using Starfall.Core.Map.State;

namespace Starfall.Tests.EditMode.Map.Regions
{
    /// <summary>
    /// doc2 MAP-09 <see cref="MapSpawnPoint"/> + <see cref="MapSpawnService"/> 测试集。
    /// <para/>
    /// 覆盖：空闲 SpawnPoint 查询 / 容量 / 敌我区分 / 重复 ID 拒绝。
    /// </summary>
    public class SpawnPointTests
    {
        private static MapState MakeMap()
        {
            return new MapState(new MapDefinition("map.test", 8, 8, DimensionLayer.Reality, 0));
        }

        // ──────────── 1) 构造 + 校验 ────────────

        [Test]
        public void Constructor_DefaultsCapacity1AndActive()
        {
            var s = new MapSpawnPoint(new SpawnId(1), 1, new GridCoord(0, 0), 0);
            Assert.AreEqual(1, s.Capacity);
            Assert.IsTrue(s.Active);
            Assert.AreEqual(0, s.OwnerSide);
        }

        [Test]
        public void Constructor_NegativeSpawnId_Throws()
        {
            Assert.Throws<System.ArgumentOutOfRangeException>(() =>
                new MapSpawnPoint(new SpawnId(-1), 1, new GridCoord(0, 0), 0));
        }

        [Test]
        public void Constructor_CapacityZero_Throws()
        {
            Assert.Throws<System.ArgumentOutOfRangeException>(() =>
                new MapSpawnPoint(new SpawnId(1), 1, new GridCoord(0, 0), 0, capacity: 0));
        }

        // ──────────── 2) AddSpawnPoint 重复 ID ────────────

        [Test]
        public void AddSpawnPoint_DuplicateId_Throws()
        {
            var map = MakeMap();
            map.AddSpawnPoint(new MapSpawnPoint(new SpawnId(1), 1, new GridCoord(0, 0), 0));
            Assert.Throws<System.InvalidOperationException>(() =>
                map.AddSpawnPoint(new MapSpawnPoint(new SpawnId(1), 1, new GridCoord(1, 0), 0)));
        }

        [Test]
        public void RemoveSpawnPoint_ById_ReturnsTrue()
        {
            var map = MakeMap();
            map.AddSpawnPoint(new MapSpawnPoint(new SpawnId(1), 1, new GridCoord(0, 0), 0));
            Assert.IsTrue(map.RemoveSpawnPoint(1));
            Assert.AreEqual(0, map.SpawnPoints.Count);
        }

        [Test]
        public void RemoveSpawnPoint_NotFound_ReturnsFalse()
        {
            var map = MakeMap();
            Assert.IsFalse(map.RemoveSpawnPoint(99));
        }

        // ──────────── 3) GetAvailableSpawns ────────────

        [Test]
        public void GetAvailableSpawns_OnlyReturnsActive()
        {
            var map = MakeMap();
            map.AddSpawnPoint(new MapSpawnPoint(new SpawnId(1), 1, new GridCoord(0, 0), 0, active: true));
            map.AddSpawnPoint(new MapSpawnPoint(new SpawnId(2), 1, new GridCoord(1, 0), 0, active: false));
            var spawns = MapSpawnService.GetAvailableSpawns(map, 0);
            Assert.AreEqual(1, spawns.Count);
            Assert.AreEqual(1, spawns[0].SpawnId);
        }

        [Test]
        public void GetAvailableSpawns_FiltersBySide()
        {
            var map = MakeMap();
            map.AddSpawnPoint(new MapSpawnPoint(new SpawnId(1), 1, new GridCoord(0, 0), 0));  // player
            map.AddSpawnPoint(new MapSpawnPoint(new SpawnId(2), 1, new GridCoord(1, 0), 1));  // enemy
            var playerSpawns = MapSpawnService.GetAvailableSpawns(map, 0);
            var enemySpawns = MapSpawnService.GetAvailableSpawns(map, 1);
            Assert.AreEqual(1, playerSpawns.Count);
            Assert.AreEqual(1, enemySpawns.Count);
            Assert.AreEqual(1, playerSpawns[0].SpawnId);
            Assert.AreEqual(2, enemySpawns[0].SpawnId);
        }

        [Test]
        public void GetAvailableSpawns_EmptyMap_ReturnsEmpty()
        {
            var map = MakeMap();
            var spawns = MapSpawnService.GetAvailableSpawns(map, 0);
            Assert.AreEqual(0, spawns.Count);
        }

        // ──────────── 4) HasFreeSpawnAt ────────────

        [Test]
        public void HasFreeSpawnAt_ReturnsTrue_WhenFound()
        {
            var map = MakeMap();
            map.AddSpawnPoint(new MapSpawnPoint(new SpawnId(1), 1, new GridCoord(0, 0), 0));
            Assert.IsTrue(MapSpawnService.HasFreeSpawnAt(map, new GridCoord(0, 0), 0));
        }

        [Test]
        public void HasFreeSpawnAt_ReturnsFalse_WhenNotFound()
        {
            var map = MakeMap();
            map.AddSpawnPoint(new MapSpawnPoint(new SpawnId(1), 1, new GridCoord(0, 0), 0));
            Assert.IsFalse(MapSpawnService.HasFreeSpawnAt(map, new GridCoord(5, 5), 0));
        }

        // ──────────── 5) GetSpawnsInRegion ────────────

        [Test]
        public void GetSpawnsInRegion_FiltersByRegion()
        {
            var map = MakeMap();
            map.AddSpawnPoint(new MapSpawnPoint(new SpawnId(1), 1, new GridCoord(0, 0), 0));
            map.AddSpawnPoint(new MapSpawnPoint(new SpawnId(2), 1, new GridCoord(1, 0), 0));
            map.AddSpawnPoint(new MapSpawnPoint(new SpawnId(3), 2, new GridCoord(5, 5), 0));
            var inRegion1 = MapSpawnService.GetSpawnsInRegion(map, 1);
            Assert.AreEqual(2, inRegion1.Count);
            var inRegion2 = MapSpawnService.GetSpawnsInRegion(map, 2);
            Assert.AreEqual(1, inRegion2.Count);
        }

        // ──────────── 6) Equals / GetHashCode ────────────

        [Test]
        public void Equals_SameContent_AreEqual()
        {
            var a = new MapSpawnPoint(new SpawnId(1), 1, new GridCoord(0, 0), 0);
            var b = new MapSpawnPoint(new SpawnId(1), 1, new GridCoord(0, 0), 0);
            Assert.AreEqual(a, b);
            Assert.AreEqual(a.GetHashCode(), b.GetHashCode());
        }

        [Test]
        public void Equals_DifferentCoord_NotEqual()
        {
            var a = new MapSpawnPoint(new SpawnId(1), 1, new GridCoord(0, 0), 0);
            var b = new MapSpawnPoint(new SpawnId(1), 1, new GridCoord(1, 0), 0);
            Assert.AreNotEqual(a, b);
        }

        // ──────────── 7) ToString ────────────

        [Test]
        public void ToString_Contains_All_Fields()
        {
            var s = new MapSpawnPoint(new SpawnId(7), 1, new GridCoord(3, 3), 0, capacity: 2);
            string str = s.ToString();
            StringAssert.Contains("7", str);
            StringAssert.Contains("(3, 3", str);
            StringAssert.Contains("2", str);
        }
    }
}