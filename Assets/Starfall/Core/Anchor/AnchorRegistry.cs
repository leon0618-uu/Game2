using System.Collections.Generic;

namespace Starfall.Core.Anchor
{
    /// <summary>
    /// 全局锚点注册表。ZoneId 升序迭代（确定性）。
    /// </summary>
    public sealed class AnchorRegistry
    {
        private readonly Dictionary<int, AnchorZone> _zones = new Dictionary<int, AnchorZone>();
        public IReadOnlyList<AnchorZone> ZonesInOrder
        {
            get
            {
                var list = new List<AnchorZone>(_zones.Values);
                list.Sort((a, b) => a.ZoneId.CompareTo(b.ZoneId));
                return list;
            }
        }

        public void Register(AnchorZone zone)
        {
            if (zone == null) throw new System.ArgumentNullException(nameof(zone));
            _zones[zone.ZoneId] = zone;
        }

        public AnchorZone Get(int zoneId) => _zones.TryGetValue(zoneId, out var z) ? z : null;
    }
}
