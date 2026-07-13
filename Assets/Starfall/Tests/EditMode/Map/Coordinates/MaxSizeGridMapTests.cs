using NUnit.Framework;
using Starfall.Core.Map.Coordinates;

namespace Starfall.Tests.EditMode.Map.Coordinates
{
    /// <summary>
    /// 最大尺寸地图边界测试。
    /// 48×64×2 = 6144 格是 doc2 MAP-01 的内存峰值预算，必须能成功构造而不抛错。
    /// </summary>
    public class MaxSizeGridMapTests
    {
        [Test]
        public void MaxSize_48x64_ConstructsWithoutThrow()
        {
            GridMap<int> map = null;
            Assert.DoesNotThrow(() => map = new GridMap<int>(MapSize.Max));
            Assert.IsNotNull(map);
            Assert.AreEqual(MapSize.Max, map.Size);
            Assert.AreEqual(0, map.Count);
        }

        [Test]
        public void MaxSize_TileCount_Is6144()
        {
            // 48 × 64 × 2 = 6144。
            Assert.AreEqual(6144, MapSize.Max.TileCount);
        }

        [Test]
        public void MaxSize_CornerCoords_AreInBounds()
        {
            // (0,0) 和 (47, 63) 是合法的角点坐标。
            var map = new GridMap<int>(MapSize.Max);
            var a = new GridCoord(0, 0, DimensionLayer.Reality);
            var b = new GridCoord(47, 63, DimensionLayer.Astral);
            Assert.IsTrue(a.IsInBounds(MapSize.Max));
            Assert.IsTrue(b.IsInBounds(MapSize.Max));

            map.Set(a, 1);
            map.Set(b, 2);
            Assert.AreEqual(1, map[a]);
            Assert.AreEqual(2, map[b]);
            Assert.AreEqual(2, map.Count);
        }

        [Test]
        public void MaxSize_BoundaryPlusOne_IsOutOfBounds()
        {
            var map = new GridMap<int>(MapSize.Max);
            // X=48, Y=63 → X 越界（Width=48 时 X 取值范围是 0..47）。
            Assert.IsFalse(new GridCoord(48, 63).IsInBounds(MapSize.Max));
            // X=47, Y=64 → Y 越界（Height=64 时 Y 取值范围是 0..63）。
            Assert.IsFalse(new GridCoord(47, 64).IsInBounds(MapSize.Max));

            // GridMap.Set 对越界坐标应抛 ArgumentOutOfRangeException。
            Assert.Throws<System.ArgumentOutOfRangeException>(
                () => map.Set(new GridCoord(48, 63), 0));
            Assert.Throws<System.ArgumentOutOfRangeException>(
                () => map.Set(new GridCoord(47, 64), 0));
        }
    }
}
