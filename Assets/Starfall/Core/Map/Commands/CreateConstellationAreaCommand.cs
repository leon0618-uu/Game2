using System;
using System.Collections.Generic;
using Starfall.Core.Map;
using Starfall.Core.Map.Coordinates;
using Starfall.Core.Map.State;

namespace Starfall.Core.Map.Commands
{
    /// <summary>
    /// doc2 MAP-03 §21.1 创建星座区域（地图 polygon 区域）命令。
    /// <para/>
    /// **范围**：将一个新的 <see cref="MapRegion"/> 加入 <see cref="MapState.Regions"/> 集合；
    /// 类型 <c>"Constellation"</c>（doc2 §21.1 14 类区域之一，Phase 2 引入枚举；
    /// 本阶段用字符串占位以与 <see cref="MapRegion.RegionType"/> 字段对齐）。
    /// <para/>
    /// **校验**：
    /// <list type="bullet">
    /// <item><c>RegionId</c> 与现有 region 不重复。</item>
    /// <item>Owner ∈ { "Player" / "Enemy" / "Neutral" }。</item>
    /// <li>tiles 不为空（至少 1 个）。</li>
    /// <li>tiles 全部 in-bounds（每个 <see cref="GridCoord"/> 必须满足
    ///       <see cref="GridCoord.IsInBounds"/>）。</li>
    /// </list>
    /// <para/>
    /// **MVP 注**：本命令仅做包装（与 doc2 §21.1 "ConstellationPolygonService MAP-XX" 对齐）；
    /// 完整 polygon 算法（多边形求交 / 并集 / 重叠）在后续 MAP-09 阶段实现。当前命令
    /// 仅做最小数据写入（add region + emit event）。
    /// <para/>
    /// **Emit**：单 <see cref="MapEventKind.OnConstellationPolygonCreated"/> 事件（含 RegionId）。
    /// </summary>
    public sealed class CreateConstellationAreaCommand : IMapCommand
    {
        public int RegionId { get; }
        public string Owner { get; }
        public IReadOnlyList<GridCoord> Tiles { get; }

        public CreateConstellationAreaCommand(int regionId, string owner, IReadOnlyList<GridCoord> tiles)
        {
            if (regionId < 0)
                throw new ArgumentOutOfRangeException(nameof(regionId), regionId,
                    "RegionId must be >= 0.");
            if (owner == null)
                throw new ArgumentNullException(nameof(owner));
            if (tiles == null)
                throw new ArgumentNullException(nameof(tiles));
            if (tiles.Count == 0)
                throw new ArgumentException("Region must have at least 1 tile.",
                    nameof(tiles));
            RegionId = regionId;
            Owner = owner;
            Tiles = tiles;
        }

        public MapCommandResult Execute(MapState mapState)
        {
            if (mapState == null) throw new ArgumentNullException(nameof(mapState));
            int previousVersion = mapState.Version;
            int newVersion = previousVersion + 1;

            // 1) RegionId 不重复
            for (int i = 0; i < mapState.Regions.Count; i++)
            {
                if (mapState.Regions[i].RegionId == RegionId)
                    return MapCommandResult.Fail("duplicate region id");
            }

            // 2) Owner 白名单
            if (Owner != "Player" && Owner != "Enemy" && Owner != "Neutral")
                return MapCommandResult.Fail("owner must be Player|Enemy|Neutral");

            // 3) tiles 越界检查
            MapSize size = mapState.Definition.Size;
            for (int i = 0; i < Tiles.Count; i++)
            {
                if (!Tiles[i].IsInBounds(size))
                    return MapCommandResult.Fail($"tile {Tiles[i]} out of bounds");
            }

            // 4) 构造 + 写入 mapState
            var region = new MapRegion(RegionId, "Constellation", Owner, Tiles);
            mapState.AddRegion(region);
            _executed = true;
            _addedRegion = region;

            var events = new List<MapEvent>(1)
            {
                MapEvent.ConstellationPolygonCreated(RegionId, $"constellation:{RegionId}")
            };
            events.Sort();
            return MapCommandResult.Ok(events, newVersion);
        }

        public void Undo(MapState mapState)
        {
            if (mapState == null) throw new ArgumentNullException(nameof(mapState));
            if (!_executed)
                throw new InvalidOperationException("CreateConstellationAreaCommand.Undo called without prior Execute.");
            mapState.RemoveRegion(RegionId);
            _executed = false;
            _addedRegion = null;
        }

        public int Version => 1;
        public string CommandId => $"create-constellation-area:{RegionId}";
        public IReadOnlyList<string> Dependencies => Array.Empty<string>();

        private bool _executed;
        private MapRegion _addedRegion;

        public override string ToString()
            => $"CreateConstellationAreaCommand(RegionId={RegionId}, Owner={Owner}, Tiles={Tiles.Count})";
    }
}
