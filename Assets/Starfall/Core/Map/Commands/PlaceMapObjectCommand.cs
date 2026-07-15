using System;
using System.Collections.Generic;
using Starfall.Core.Map;
using Starfall.Core.Map.Coordinates;
using Starfall.Core.Map.State;

namespace Starfall.Core.Map.Commands
{
    /// <summary>
    /// doc2 MAP-03 §21.1 放置地图对象命令（基础版）。
    /// <para/>
    /// **范围**：仅支持单格对象（<paramref name="anchor"/> 为单 <see cref="GridCoord"/>）。
    /// **本命令不做**：12 类对象枚举 / Footprint / MapObjectStateMachine 状态机 ——
    /// 这些属于 doc2 §10.1 的完整 12 类对象工作（MAP-10 后续）。
    /// <para/>
    /// **校验**：
    /// <list type="bullet">
    /// <item><c>ObjectId</c> >= 0 且不与现有对象重复。</item>
    /// <item><c>ObjectType</c> 非空 / 非 null。</item>
    /// <li><paramref name="anchor"/> in-bounds（<see cref="MapState.Definition"/>）。</item>
    /// </list>
    /// <para/>
    /// **Emit**：单 <see cref="MapEventKind.OnMapObjectPlaced"/> 事件（含 ObjectId）。
    /// </summary>
    public sealed class PlaceMapObjectCommand : IMapCommand
    {
        public int ObjectId { get; }
        public string ObjectType { get; }
        public GridCoord Anchor { get; }

        public PlaceMapObjectCommand(int objectId, string objectType, GridCoord anchor)
        {
            if (objectId < 0)
                throw new ArgumentOutOfRangeException(nameof(objectId), objectId,
                    "ObjectId must be >= 0.");
            if (objectType == null)
                throw new ArgumentNullException(nameof(objectType));
            ObjectId = objectId;
            ObjectType = objectType;
            Anchor = anchor;
        }

        public MapCommandResult Execute(MapState mapState)
        {
            if (mapState == null) throw new ArgumentNullException(nameof(mapState));
            int previousVersion = mapState.Version;
            int newVersion = previousVersion + 1;

            // 1) ObjectId 不重复
            for (int i = 0; i < mapState.MapObjects.Count; i++)
            {
                if (mapState.MapObjects[i].ObjectId == ObjectId)
                    return MapCommandResult.Fail("duplicate object id");
            }

            // 2) ObjectType 非空
            if (string.IsNullOrEmpty(ObjectType))
                return MapCommandResult.Fail("object type must be non-empty");

            // 3) Anchor in-bounds
            if (!Anchor.IsInBounds(mapState.Definition.Size))
                return MapCommandResult.Fail($"anchor {Anchor} out of bounds");

            var obj = new MapObjectInstance(ObjectId, ObjectType, Anchor);
            mapState.AddMapObject(obj);
            _executed = true;
            _addedObject = obj;

            var events = new List<MapEvent>(1)
            {
                MapEvent.MapObjectPlaced(ObjectId, $"map-object-placed:{ObjectId}")
            };
            events.Sort();
            return MapCommandResult.Ok(events, newVersion);
        }

        public void Undo(MapState mapState)
        {
            if (mapState == null) throw new ArgumentNullException(nameof(mapState));
            if (!_executed)
                throw new InvalidOperationException("PlaceMapObjectCommand.Undo called without prior Execute.");
            mapState.RemoveMapObject(ObjectId);
            _executed = false;
            _addedObject = null;
        }

        public int Version => 1;
        public string CommandId => $"place-map-object:{ObjectId}";
        public IReadOnlyList<string> Dependencies => Array.Empty<string>();

        private bool _executed;
        private MapObjectInstance _addedObject;

        public override string ToString()
            => $"PlaceMapObjectCommand(ObjectId={ObjectId}, Type={ObjectType}, Anchor={Anchor})";
    }
}
