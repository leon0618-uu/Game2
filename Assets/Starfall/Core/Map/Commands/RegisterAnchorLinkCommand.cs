using System;
using System.Collections.Generic;
using Starfall.Core.Map;
using Starfall.Core.Map.Anchor;
using Starfall.Core.Map.State;

namespace Starfall.Core.Map.Commands
{
    /// <summary>
    /// doc2 MAP-12 注册新 <see cref="AnchorLink"/> 命令。
    /// <para/>
    /// **范围**：将一个新的 <see cref="AnchorLink"/> 加入 <see cref="MapState.AnchorLinks"/> 集合。
    /// <para/>
    /// **校验**：
    /// <list type="bullet">
    /// <item><see cref="AnchorLinkId"/> 与现有 link 不重复（由 <see cref="MapState.AddAnchorLink"/> 强制）。</item>
    /// <item><paramref name="polygon"/> 必须通过 <see cref="ConstellationValidator"/>（构造期已校验）。</item>
    /// </list>
    /// **Emit**：单 <see cref="MapEventKind.OnAnchorLinkCreated"/> 事件（含 LinkId + Description）。
    /// </summary>
    public sealed class RegisterAnchorLinkCommand : IMapCommand
    {
        public AnchorLink Link { get; }

        public RegisterAnchorLinkCommand(AnchorLink link)
        {
            if (link == null) throw new ArgumentNullException(nameof(link));
            Link = link;
        }

        public MapCommandResult Execute(MapState mapState)
        {
            if (mapState == null) throw new ArgumentNullException(nameof(mapState));
            int previousVersion = mapState.Version;
            int newVersion = previousVersion + 1;

            // 1) Id 不重复
            for (int i = 0; i < mapState.AnchorLinks.Count; i++)
            {
                if (mapState.AnchorLinks[i].Id.Equals(Link.Id))
                    return MapCommandResult.Fail($"duplicate anchor link id {Link.Id.Value}");
            }

            // 2) 写入 mapState（AddAnchorLink 内部也做 Id 重复校验）
            try
            {
                mapState.AddAnchorLink(Link);
            }
            catch (Exception ex)
            {
                return MapCommandResult.Fail($"add anchor link failed: {ex.Message}");
            }

            _executed = true;
            _addedLink = Link;
            // 记录新 PostStateHash（命令完成后由 MapState.PostStateHash 计算）
            _addedPostStateHash = mapState.PostStateHash;
            // 把 PostStateHash 同步回 link，让后续 Hasher 看到
            Link.TransitionTo(Link.CurrentState, Link.StateTick, _addedPostStateHash);

            var events = new List<MapEvent>(1)
            {
                new MapEvent(MapEventKind.OnRegionChanged,
                    regionId: null,
                    anchorId: null,
                    description: $"anchor-link-registered:{Link.Id.Value}")
            };
            events.Sort();
            return MapCommandResult.Ok(events, newVersion);
        }

        public void Undo(MapState mapState)
        {
            if (mapState == null) throw new ArgumentNullException(nameof(mapState));
            if (!_executed)
                throw new InvalidOperationException("RegisterAnchorLinkCommand.Undo called without prior Execute.");
            mapState.RemoveAnchorLink(Link.Id);
            _executed = false;
            _addedLink = null;
        }

        public int Version => 1;
        public string CommandId => $"register-anchor-link:{Link.Id.Value}";
        public IReadOnlyList<string> Dependencies => Array.Empty<string>();

        private bool _executed;
        private AnchorLink _addedLink;
        private ulong _addedPostStateHash;

        public override string ToString()
            => $"RegisterAnchorLinkCommand(LinkId={Link.Id.Value}, PolygonId={Link.Polygon.Id.Value})";
    }
}