using System.Collections.Generic;
using NUnit.Framework;
using Starfall.Core.Map.Coordinates;
using Starfall.Core.Map.Cover;
using Starfall.Core.Map.LineOfSight;

namespace Starfall.Tests.EditMode.Map.Cover
{
    /// <summary>
    /// <see cref="CoverQueryService.QueryCover"/> + <see cref="CoverLevel"/> 行为测试（≥ 8 项）。
    /// 覆盖：无 provider / 单 tile / 跨 tile / 主导方向 / CrossLayer。
    /// </summary>
    public class CoverQueryTests
    {
        // 简单字典适配器：单元测试用
        private sealed class DictCoverLookup : ICoverLookup
        {
            private readonly Dictionary<GridCoord, CoverLevel> _data;
            public DictCoverLookup(Dictionary<GridCoord, CoverLevel> data) { _data = data; }
            public CoverLevel? GetCover(GridCoord c)
                => _data.TryGetValue(c, out var v) ? (CoverLevel?)v : null;
        }

        [Test]
        public void QueryCover_NullProvider_ReturnsNone()
        {
            var atk = new GridCoord(5, 6, DimensionLayer.Reality);
            var def = new GridCoord(5, 5, DimensionLayer.Reality);
            Assert.AreEqual(CoverLevel.None, CoverQueryService.QueryCover(null, atk, def));
        }

        [Test]
        public void QueryCover_SameTile_ReturnsNone()
        {
            var lookup = new DictCoverLookup(new Dictionary<GridCoord, CoverLevel>
            {
                { new GridCoord(5, 5, DimensionLayer.Reality), CoverLevel.Full },
            });
            var atk = new GridCoord(5, 5, DimensionLayer.Reality);
            var def = new GridCoord(5, 5, DimensionLayer.Reality);
            Assert.AreEqual(CoverLevel.None, CoverQueryService.QueryCover(lookup, atk, def));
        }

        [Test]
        public void QueryCover_HalfCover_ReturnsHalf()
        {
            var defCoord = new GridCoord(5, 5, DimensionLayer.Reality);
            var lookup = new DictCoverLookup(new Dictionary<GridCoord, CoverLevel>
            {
                { defCoord, CoverLevel.Half },
            });
            var atk = new GridCoord(5, 6, DimensionLayer.Reality);
            Assert.AreEqual(CoverLevel.Half, CoverQueryService.QueryCover(lookup, atk, defCoord));
        }

        [Test]
        public void QueryCover_FullCover_ReturnsFull()
        {
            var defCoord = new GridCoord(5, 5, DimensionLayer.Reality);
            var lookup = new DictCoverLookup(new Dictionary<GridCoord, CoverLevel>
            {
                { defCoord, CoverLevel.Full },
            });
            var atk = new GridCoord(5, 6, DimensionLayer.Reality);
            Assert.AreEqual(CoverLevel.Full, CoverQueryService.QueryCover(lookup, atk, defCoord));
        }

        [Test]
        public void QueryCover_NoEntryInProvider_ReturnsNone()
        {
            var lookup = new DictCoverLookup(new Dictionary<GridCoord, CoverLevel>());
            var atk = new GridCoord(5, 6, DimensionLayer.Reality);
            var def = new GridCoord(5, 5, DimensionLayer.Reality);
            Assert.AreEqual(CoverLevel.None, CoverQueryService.QueryCover(lookup, atk, def));
        }

        [Test]
        public void QueryCover_CrossLayer_ReturnsNone()
        {
            var lookup = new DictCoverLookup(new Dictionary<GridCoord, CoverLevel>());
            var atk = new GridCoord(5, 6, DimensionLayer.Astral);
            var def = new GridCoord(5, 5, DimensionLayer.Reality);
            Assert.AreEqual(CoverLevel.None, CoverQueryService.QueryCover(lookup, atk, def));
        }

        [Test]
        public void QueryCover_MultipleTiles_EachReturnsOwnValue()
        {
            var lookup = new DictCoverLookup(new Dictionary<GridCoord, CoverLevel>
            {
                { new GridCoord(5, 5, DimensionLayer.Reality), CoverLevel.Half },
                { new GridCoord(6, 5, DimensionLayer.Reality), CoverLevel.Full },
            });
            Assert.AreEqual(CoverLevel.Half, CoverQueryService.QueryCover(
                lookup,
                new GridCoord(5, 6, DimensionLayer.Reality),
                new GridCoord(5, 5, DimensionLayer.Reality)));
            Assert.AreEqual(CoverLevel.Full, CoverQueryService.QueryCover(
                lookup,
                new GridCoord(6, 6, DimensionLayer.Reality),
                new GridCoord(6, 5, DimensionLayer.Reality)));
        }

        [Test]
        public void QueryCover_AstralLayer_IndependentFromReality()
        {
            var lookup = new DictCoverLookup(new Dictionary<GridCoord, CoverLevel>
            {
                { new GridCoord(5, 5, DimensionLayer.Reality), CoverLevel.Full },
                { new GridCoord(5, 5, DimensionLayer.Astral), CoverLevel.Half },
            });
            // Astral attacker → Astral defender → Half
            Assert.AreEqual(CoverLevel.Half, CoverQueryService.QueryCover(
                lookup,
                new GridCoord(5, 6, DimensionLayer.Astral),
                new GridCoord(5, 5, DimensionLayer.Astral)));
            // Reality attacker → Reality defender → Full
            Assert.AreEqual(CoverLevel.Full, CoverQueryService.QueryCover(
                lookup,
                new GridCoord(5, 6, DimensionLayer.Reality),
                new GridCoord(5, 5, DimensionLayer.Reality)));
        }

        [Test]
        public void QueryCoverDiagonal_NullProvider_ReturnsNone()
        {
            var atk = new GridCoord(6, 6, DimensionLayer.Reality);
            var def = new GridCoord(5, 5, DimensionLayer.Reality);
            Assert.AreEqual(CoverLevel.None, CoverQueryService.QueryCoverDiagonal(null, atk, def));
        }

        [Test]
        public void QueryCoverDiagonal_HalfCover_ReturnsHalf()
        {
            var defCoord = new GridCoord(5, 5, DimensionLayer.Reality);
            var lookup = new DictCoverLookup(new Dictionary<GridCoord, CoverLevel>
            {
                { defCoord, CoverLevel.Half },
            });
            var atk = new GridCoord(6, 6, DimensionLayer.Reality);
            Assert.AreEqual(CoverLevel.Half, CoverQueryService.QueryCoverDiagonal(lookup, atk, defCoord));
        }

        [Test]
        public void QueryCoverDiagonal_SameTile_ReturnsNone()
        {
            var lookup = new DictCoverLookup(new Dictionary<GridCoord, CoverLevel>
            {
                { new GridCoord(5, 5, DimensionLayer.Reality), CoverLevel.Full },
            });
            var atk = new GridCoord(5, 5, DimensionLayer.Reality);
            var def = new GridCoord(5, 5, DimensionLayer.Reality);
            Assert.AreEqual(CoverLevel.None, CoverQueryService.QueryCoverDiagonal(lookup, atk, def));
        }
    }
}
