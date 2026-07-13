using System.Collections.Generic;
using Starfall.Core.Model;

namespace Starfall.Unity.Presentation
{
    /// <summary>
    /// 给 Presenter 用的锚点围区快照（ADR-0002 §Decision 1 视图模型扩展）。
    /// Presenter 不接触 Core 的 AnchorZone 类型，只读快照；
    /// 顶点顺序与 AnchorZone 一致（按 GridPos.CompareTo 升序 = 先 Y 后 X，确定性）。
    /// </summary>
    public readonly struct AnchorSnapshot
    {
        public int ZoneId { get; }
        public string Owner { get; }   // "Player" / "Enemy" / "Neutral"
        public IReadOnlyList<GridPos> Vertices { get; }

        public AnchorSnapshot(int zoneId, string owner, IReadOnlyList<GridPos> vertices)
        {
            ZoneId = zoneId;
            Owner = owner ?? "Neutral";
            Vertices = vertices ?? System.Array.Empty<GridPos>();
        }
    }
}