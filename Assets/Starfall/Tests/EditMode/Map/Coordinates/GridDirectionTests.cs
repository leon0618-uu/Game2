using NUnit.Framework;
using Starfall.Core.Map.Coordinates;

namespace Starfall.Tests.EditMode.Map.Coordinates
{
    /// <summary>
    /// GridDirection 枚举测试。验证固定顺序与数值（AGENTS.md §11）。
    /// 任何重排或重新赋值都会破坏寻路 / 锚点 / 律令 / 哈希的确定性。
    /// </summary>
    public class GridDirectionTests
    {
        [Test]
        public void EnumValues_FixedOrder()
        {
            // 必须严格保持 North → East → South → West。
            var values = System.Enum.GetValues(typeof(GridDirection));
            Assert.AreEqual(4, values.Length);
            Assert.AreEqual(GridDirection.North, values.GetValue(0));
            Assert.AreEqual(GridDirection.East, values.GetValue(1));
            Assert.AreEqual(GridDirection.South, values.GetValue(2));
            Assert.AreEqual(GridDirection.West, values.GetValue(3));
        }

        [Test]
        public void EnumValues_NumericValues_AreZeroToThree()
        {
            Assert.AreEqual(0, (byte)GridDirection.North);
            Assert.AreEqual(1, (byte)GridDirection.East);
            Assert.AreEqual(2, (byte)GridDirection.South);
            Assert.AreEqual(3, (byte)GridDirection.West);
        }
    }
}
