using System;
using System.Collections.Generic;
using Starfall.Core.Map;
using Starfall.Core.Map.State;

namespace Starfall.Core.Map.Commands
{
    /// <summary>
    /// doc2 MAP-03 §21.1 移除地图对象命令。
    /// <para/>
    /// **范围**：从 <see cref="MapState.MapObjects"/> 集合中按 <see cref="ObjectId"/> 移除
    /// 一个 <see cref="MapObjectInstance"/>；**不维护 12 类对象状态机**（同
    /// <see cref="PlaceMapObjectCommand"/> 的 MVP 限制）。
    /// <para/>
    /// **校验**：<paramref name="objectId"/> 必须在 <see cref="MapState.MapObjects"/>
    /// 中存在；否则 <c>"object not found"</c>。
    /// </summary>
    public sealed class RemoveMapObjectCommand : IMapCommand
    {
        public int ObjectId { get; }

        public RemoveMapObjectCommand(int objectId)
        {
            if (objectId < 0)
                throw new ArgumentOutOfRangeException(nameof(objectId), objectId,
                    "ObjectId must be >= 0.");
            ObjectId = objectId;
        }

        public MapCommandResult Execute(MapState mapState)
        {
            if (mapState == null) throw new ArgumentNullException(nameof(mapState));
            int previousVersion = mapState.Version;
            int newVersion = previousVersion + 1;

            MapObjectInstance removed = null;
            for (int i = 0; i < mapState.MapObjects.Count; i++)
            {
                if (mapState.MapObjects[i].ObjectId == ObjectId)
                {
                    removed = mapState.MapObjects[i];
                    break;
                }
            }
            if (removed == null)
                return MapCommandResult.Fail("object not found");

            mapState.RemoveMapObject(ObjectId);
            _executed = true;
            _removedObject = removed;

            var events = new List<MapEvent>(1)
            {
                MapEvent.MapObjectRemoved(ObjectId, $"map-object-removed:{ObjectId}")
            };
            events.Sort();
            return MapCommandResult.Ok(events, newVersion);
        }

        public void Undo(MapState mapState)
        {
            if (mapState == null) throw new ArgumentNullException(nameof(mapState));
            if (!_executed)
                throw new InvalidOperationException("RemoveMapObjectCommand.Undo called without prior Execute.");
            if (_removedObject != null)
                mapState.AddMapObject(_removedObject);
            _executed = false;
            _removedObject = null;
        }

        public int Version => 1;
        public string CommandId => $"remove-map-object:{ObjectId}";
        public IReadOnlyList<string> Dependencies => Array.Empty<string>();

        private bool _executed;
        private MapObjectInstance _removedObject;

        public override string ToString()
            => $"RemoveMapObjectCommand(ObjectId={ObjectId})";
    }
}
