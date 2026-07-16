using System;
using System.Collections.Generic;
using Starfall.Core.Map;
using Starfall.Core.Map.Anchor;
using Starfall.Core.Map.State;

namespace Starfall.Core.Map.Commands
{
    /// <summary>
    /// doc2 MAP-12 替换 <see cref="AnchorLink"/> 的 <see cref="ConstellationPolygon"/> 命令。
    /// <para/>
    /// **范围**：将目标 <see cref="AnchorLink"/> 的 <see cref="AnchorLink.Polygon"/> 替换为
    /// 新的多边形（保留旧多边形用于 Undo）。
    /// <para/>
    /// **校验**：
    /// <list type="bullet">
    /// <item>Link 必须存在。</item>
    /// <item>新多边形必须通过 <see cref="ConstellationValidator"/>（构造期已校验）。</item>
    /// </list>
    /// <para/>
    /// **Emit**：单 <see cref="MapEventKind.OnRegionChanged"/> 事件（含 LinkId + 旧 / 新 polygon id）。
    /// <para/>
    /// **Undo**：恢复旧 polygon + 旧 PostStateHash（Undone 时刷新）。
    /// </summary>
    public sealed class UpdateConstellationPolygonCommand : IMapCommand
    {
        public AnchorLinkId LinkId { get; }
        public ConstellationPolygon NewPolygon { get; }

        public UpdateConstellationPolygonCommand(AnchorLinkId linkId, ConstellationPolygon newPolygon)
        {
            if (newPolygon.Vertices == null || newPolygon.Vertices.Count < 3)
                throw new ArgumentException("New polygon must have >= 3 vertices.", nameof(newPolygon));
            LinkId = linkId;
            NewPolygon = newPolygon;
        }

        public MapCommandResult Execute(MapState mapState)
        {
            if (mapState == null) throw new ArgumentNullException(nameof(mapState));
            int previousVersion = mapState.Version;
            int newVersion = previousVersion + 1;

            if (!mapState.TryGetAnchorLink(LinkId, out var link))
                return MapCommandResult.Fail($"anchor link not found: {LinkId.Value}");

            // 记录旧 polygon 用于 Undo
            _executed = true;
            _link = link;
            _oldPolygon = link.Polygon;
            _oldHash = link.PostStateHash;

            // 替换 polygon（ConstellationPolygon 构造期已校验顶点）
            link.UpdatePolygon(NewPolygon);

            // 刷新 PostStateHash
            ulong newHash = mapState.PostStateHash;
            link.TransitionTo(link.CurrentState, link.StateTick, newHash);

            var events = new List<MapEvent>(1)
            {
                new MapEvent(MapEventKind.OnRegionChanged,
                    regionId: null,
                    anchorId: null,
                    description: $"constellation-polygon-updated:{LinkId.Value}:{_oldPolygon.Id.Value}->{NewPolygon.Id.Value}")
            };
            events.Sort();
            return MapCommandResult.Ok(events, newVersion);
        }

        public void Undo(MapState mapState)
        {
            if (mapState == null) throw new ArgumentNullException(nameof(mapState));
            if (!_executed)
                throw new InvalidOperationException("UpdateConstellationPolygonCommand.Undo called without prior Execute.");
            _link.UpdatePolygon(_oldPolygon);
            // 恢复旧 PostStateHash（自迁移合法）
            _link.TransitionTo(_link.CurrentState, _link.StateTick, _oldHash);
            _executed = false;
            _link = null;
            _oldPolygon = default;
        }

        public int Version => 1;
        public string CommandId => $"update-constellation-polygon:{LinkId.Value}:{NewPolygon.Id.Value}";

        public IReadOnlyList<string> Dependencies
        {
            get
            {
                return new[] { $"register-anchor-link:{LinkId.Value}" };
            }
        }

        private bool _executed;
        private AnchorLink _link;
        private ConstellationPolygon _oldPolygon;
        private ulong _oldHash;

        public override string ToString()
            => $"UpdateConstellationPolygonCommand(LinkId={LinkId.Value}, NewPolygon={NewPolygon.Id.Value})";
    }
}