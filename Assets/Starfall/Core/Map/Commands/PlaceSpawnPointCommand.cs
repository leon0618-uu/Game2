using System;
using System.Collections.Generic;
using Starfall.Core.Map;
using Starfall.Core.Map.Coordinates;
using Starfall.Core.Map.Regions;
using Starfall.Core.Map.State;

namespace Starfall.Core.Map.Commands
{
    /// <summary>
    /// doc2 MAP-09 放置出生点命令。
    ///
    /// <para/>
    /// **范围**：将一个新的 <see cref="MapSpawnPoint"/> 加入 <see cref="MapState.SpawnPoints"/> 集合。
    ///
    /// <para/>
    /// **校验**：
    /// <list type="bullet">
    /// <item>SpawnId 不重复。</item>
    /// <item>Coord 在地图范围内（<see cref="MapSize"/> 校验）。</item>
    /// <item>OwnerSide >= -1。</item>
    /// <item>Capacity >= 1。</item>
    /// </list>
    ///
    /// <para/>
    /// **Emit**：单 <see cref="MapEventKind.OnRegionChanged"/> 事件（含 RegionId + Coord）。
    /// </summary>
    public sealed class PlaceSpawnPointCommand : IMapCommand
    {
        public int SpawnId { get; }
        public int RegionId { get; }
        public GridCoord Coord { get; }
        public int OwnerSide { get; }
        public int Capacity { get; }
        public bool Active { get; }

        public PlaceSpawnPointCommand(
            int spawnId,
            int regionId,
            GridCoord coord,
            int ownerSide,
            int capacity = 1,
            bool active = true)
        {
            if (spawnId < 0)
                throw new ArgumentOutOfRangeException(nameof(spawnId), spawnId,
                    "SpawnId must be >= 0.");
            if (regionId < 0)
                throw new ArgumentOutOfRangeException(nameof(regionId), regionId,
                    "RegionId must be >= 0.");
            if (ownerSide < -1)
                throw new ArgumentOutOfRangeException(nameof(ownerSide), ownerSide,
                    "OwnerSide must be >= -1.");
            if (capacity < 1)
                throw new ArgumentOutOfRangeException(nameof(capacity), capacity,
                    "Capacity must be >= 1.");

            SpawnId = spawnId;
            RegionId = regionId;
            Coord = coord;
            OwnerSide = ownerSide;
            Capacity = capacity;
            Active = active;
        }

        public MapCommandResult Execute(MapState mapState)
        {
            if (mapState == null) throw new ArgumentNullException(nameof(mapState));
            int previousVersion = mapState.Version;
            int newVersion = previousVersion + 1;

            // SpawnId 不重复
            for (int i = 0; i < mapState.SpawnPoints.Count; i++)
            {
                if (mapState.SpawnPoints[i].SpawnIdValue.Value == SpawnId)
                    return MapCommandResult.Fail($"duplicate spawn id {SpawnId}");
            }

            // Coord 越界检查
            if (!Coord.IsInBounds(mapState.Definition.Size))
                return MapCommandResult.Fail($"coord {Coord} out of bounds");

            var spawn = new MapSpawnPoint(
                new SpawnId(SpawnId), RegionId, Coord, OwnerSide, Capacity, Active);
            mapState.AddSpawnPoint(spawn);
            _executed = true;

            var events = new List<MapEvent>(1)
            {
                new MapEvent(
                    MapEventKind.OnRegionChanged,
                    regionId: RegionId,
                    coord: Coord,
                    newValue: SpawnId,
                    description: "spawn placed")
            };
            events.Sort();
            return MapCommandResult.Ok(events, newVersion);
        }

        public void Undo(MapState mapState)
        {
            if (mapState == null) throw new ArgumentNullException(nameof(mapState));
            if (!_executed)
                throw new InvalidOperationException(
                    "PlaceSpawnPointCommand.Undo called without prior Execute.");
            mapState.RemoveSpawnPoint(SpawnId);
            _executed = false;
        }

        public int Version => 1;
        public string CommandId => $"place-spawn:{SpawnId}";
        public IReadOnlyList<string> Dependencies => Array.Empty<string>();

        private bool _executed;

        public override string ToString()
            => $"PlaceSpawnPointCommand(SpawnId={SpawnId}, Region={RegionId}, {Coord}, Side={OwnerSide}, Cap={Capacity})";
    }
}