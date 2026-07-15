using System;
using System.Collections.Generic;
using Starfall.Core.Anchor;
using Starfall.Core.Model;
using Starfall.Core.Map;
using Starfall.Core.Map.State;

namespace Starfall.Core.Map.Commands
{
    /// <summary>
    /// doc2 MAP-03 §21.1 创建锚点 link（多边形）命令。
    /// <para/>
    /// **范围**：将一个新的 <see cref="AnchorZone"/> 加入 <see cref="MapState.Anchors"/> 集合；
    /// 同时初始化其 runtime 状态为 <see cref="AnchorZoneState.Inactive"/>。
    /// <para/>
    /// **校验**：
    /// <list type="bullet">
    /// <item>vertex 数 >= 3（<see cref="AnchorZone"/> 构造约束）。</item>
    /// <item>Owner ∈ { "Player" / "Enemy" / "Neutral" }（字符串白名单；Phase 2 引入枚举）。</item>
    /// <item><c>ZoneId</c> 与现有 zone 不重复。</item>
    /// <li>vertices 全部 in-bounds（由 <see cref="AnchorZone"/> 构造隐式校验：每个 vertex
    ///       必须是 <see cref="GridPos"/> 非空且非负）。</li>
    /// </list>
    /// <para/>
    /// **Emit**：单 <see cref="MapEventKind.OnAnchorLinkCreated"/> 事件（含 zoneId + 自增 linkId）；
    /// 额外触发 <see cref="MapEventKind.OnRegionChanged"/> 让 map state hash 跟踪。
    /// </summary>
    public sealed class CreateAnchorLinkCommand : IMapCommand
    {
        private static int _nextLinkId;

        public int ZoneId { get; }
        public string Owner { get; }
        public IReadOnlyList<GridPos> Vertices { get; }

        public CreateAnchorLinkCommand(int zoneId, string owner, IReadOnlyList<GridPos> vertices)
        {
            if (zoneId < 0)
                throw new ArgumentOutOfRangeException(nameof(zoneId), zoneId,
                    "ZoneId must be >= 0.");
            if (vertices == null)
                throw new ArgumentNullException(nameof(vertices));
            if (vertices.Count < 3)
                throw new ArgumentException("AnchorZone polygon must have >= 3 vertices.",
                    nameof(vertices));
            if (owner == null)
                throw new ArgumentNullException(nameof(owner));
            ZoneId = zoneId;
            Owner = owner;
            Vertices = vertices;
        }

        public MapCommandResult Execute(MapState mapState)
        {
            if (mapState == null) throw new ArgumentNullException(nameof(mapState));
            int previousVersion = mapState.Version;
            int newVersion = previousVersion + 1;

            // 1) ZoneId 不重复
            for (int i = 0; i < mapState.Anchors.Count; i++)
            {
                if (mapState.Anchors[i].ZoneId == ZoneId)
                    return MapCommandResult.Fail("duplicate zone id");
            }

            // 2) Owner 白名单
            if (Owner != "Player" && Owner != "Enemy" && Owner != "Neutral")
                return MapCommandResult.Fail("owner must be Player|Enemy|Neutral");

            // 3) 顶点合法性
            for (int i = 0; i < Vertices.Count; i++)
            {
                var v = Vertices[i];
                if (v.X < 0 || v.Y < 0)
                    return MapCommandResult.Fail($"vertex {v} out of bounds");
            }

            // 4) 构造并加入 mapState；外部 AnchorRegistry 不维护（与 PhaseFlipStateService 同模式）。
            var zone = new AnchorZone(ZoneId, Owner, Vertices);
            mapState.AddAnchor(zone);
            AnchorStateService.Attach(mapState);
            AnchorStateService.SetState(mapState, ZoneId, AnchorZoneState.Inactive);

            _executed = true;
            _addedZone = zone;
            _addedLinkId = ++_nextLinkId;

            // 2 个事件：OnAnchorLinkCreated + OnRegionChanged（hash 触发）
            var events = new List<MapEvent>(2)
            {
                MapEvent.AnchorLinkCreated(ZoneId, _addedLinkId, $"anchor-link:{ZoneId}"),
                new MapEvent(MapEventKind.OnRegionChanged, regionId: null, anchorId: ZoneId,
                    description: $"anchor-link-created:{ZoneId}")
            };
            events.Sort();
            return MapCommandResult.Ok(events, newVersion);
        }

        public void Undo(MapState mapState)
        {
            if (mapState == null) throw new ArgumentNullException(nameof(mapState));
            if (!_executed)
                throw new InvalidOperationException("CreateAnchorLinkCommand.Undo called without prior Execute.");
            mapState.RemoveAnchor(ZoneId);
            AnchorStateService.SetState(mapState, ZoneId, AnchorZoneState.Inactive);
            _executed = false;
            _addedZone = null;
        }

        public int Version => 1;
        public string CommandId => $"create-anchor-link:{ZoneId}";
        public IReadOnlyList<string> Dependencies => Array.Empty<string>();

        private bool _executed;
        private AnchorZone _addedZone;
        private int _addedLinkId;

        public override string ToString()
            => $"CreateAnchorLinkCommand(ZoneId={ZoneId}, Owner={Owner}, Vertices={Vertices.Count})";
    }
}
