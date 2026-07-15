using System;
using System.Collections.Generic;
using Starfall.Core.Map.Coordinates;

namespace Starfall.Core.Map.Regions
{
    /// <summary>
    /// doc2 MAP-09 区域定义（immutable readonly struct）。
    ///
    /// <para/>
    /// 字段语义：
    /// <list type="bullet">
    /// <item><see cref="RegionIdValue"/>：强类型 ID（>=0），用于确定性排序与哈希。</item>
    /// <item><see cref="Kind"/>：14 类区域枚举之一（<see cref="RegionKind"/>）。</item>
    /// <item><see cref="Bounds"/>：闭多边形顶点（按 <see cref="GridCoord.CompareTo"/> 升序，
    ///       <c>Y → X → Layer</c>）。顶点仅声明几何，不存顶点级属性（layer / height 等随 tile 字段走）。</item>
    /// <item><see cref="OwnerSide"/>：归属方，<c>-1</c> = 中立，<c>0</c> = 玩家，<c>1+</c> = 敌方 / NPC。</item>
    /// <item><see cref="Priority"/>：0-100，tie-break 排序键。</item>
    /// <item><see cref="Activation"/>：def 层激活阶段（与运行时 <see cref="RegionState"/> 区分）。</item>
    /// <item><see cref="Triggers"/>：触发器列表（只读，按字典序排序）。</item>
    /// </list>
    ///
    /// <para/>
    /// 序列化约定：字段顺序固定，UTF-8 + LE 字节写入由 <see cref="MapRegionStateHasher"/> 实现。
    ///
    /// <para/>
    /// **静态工厂**：<see cref="PlayerSpawn"/> / <see cref="EnemySpawn"/> / <see cref="Capture"/>
    /// 等为常用语义提供默认值，减少调用方样板代码。
    /// </summary>
    public readonly struct MapRegionDefinition : IEquatable<MapRegionDefinition>
    {
        // ──────────── 字段 ────────────

        public readonly RegionId RegionIdValue;
        public readonly RegionKind Kind;
        public readonly IReadOnlyList<GridCoord> Bounds;
        public readonly int OwnerSide;
        public readonly int Priority;
        public readonly RegionActivation Activation;
        public readonly IReadOnlyList<RegionTrigger> Triggers;

        public MapRegionDefinition(
            RegionId id,
            RegionKind kind,
            IEnumerable<GridCoord> bounds,
            int ownerSide = -1,
            int priority = 0,
            RegionActivation activation = RegionActivation.Available,
            IEnumerable<RegionTrigger> triggers = null)
        {
            if (bounds == null)
                throw new ArgumentNullException(nameof(bounds));

            // Bounds 校验：至少 3 个顶点（多边形）。
            var boundsList = new List<GridCoord>(bounds);
            if (boundsList.Count < 3)
                throw new ArgumentException(
                    "MapRegionDefinition.Bounds must have >= 3 vertices for a closed polygon.",
                    nameof(bounds));

            // Bounds 去重（不排序——顶点顺序决定多边形边，排序会破坏多边形连通性）。
            var deduped = new List<GridCoord>(boundsList.Count);
            for (int i = 0; i < boundsList.Count; i++)
            {
                if (i == 0 || !boundsList[i].Equals(boundsList[i - 1]))
                    deduped.Add(boundsList[i]);
            }
            if (deduped.Count < 3)
                throw new ArgumentException(
                    "MapRegionDefinition.Bounds has < 3 unique vertices after dedup.",
                    nameof(bounds));

            // OwnerSide 范围：-1 / 0 / 1+
            if (ownerSide < -1)
                throw new ArgumentOutOfRangeException(nameof(ownerSide), ownerSide,
                    "OwnerSide must be >= -1 (-1 = Neutral, 0 = Player, 1+ = Enemy/NPC).");

            if (priority < 0 || priority > 100)
                throw new ArgumentOutOfRangeException(nameof(priority), priority,
                    "Priority must be in [0, 100].");

            // Triggers 规范化：空 → 空表，非空 → 排序（Kind byte → Threshold → Tag ordinal）。
            List<RegionTrigger> trigList;
            if (triggers == null)
            {
                trigList = new List<RegionTrigger>(0);
            }
            else
            {
                trigList = new List<RegionTrigger>(triggers);
                trigList.Sort((a, b) =>
                {
                    int c = ((byte)a.Kind).CompareTo((byte)b.Kind);
                    if (c != 0) return c;
                    c = a.Threshold.CompareTo(b.Threshold);
                    if (c != 0) return c;
                    return string.CompareOrdinal(a.Tag ?? string.Empty, b.Tag ?? string.Empty);
                });
            }

            RegionIdValue = id;
            Kind = kind;
            Bounds = deduped;
            OwnerSide = ownerSide;
            Priority = priority;
            Activation = activation;
            Triggers = trigList;
        }

        // ──────────── 派生便捷属性 ────────────

        /// <summary>暴露 int 类型 RegionId，方便旧 API 调用。</summary>
        public int RegionId => RegionIdValue.Value;

        /// <summary>Bounds 是否包含指定 tile。</summary>
        public bool Contains(GridCoord coord)
        {
            // ray casting (horizontal ray) — 同 AnchorZone.Contains，但允许跨层。
            var verts = Bounds;
            int n = verts.Count;
            bool inside = false;
            for (int i = 0, j = n - 1; i < n; j = i++)
            {
                var vi = verts[i];
                var vj = verts[j];
                if (((vi.Y > coord.Y) != (vj.Y > coord.Y)) &&
                    (coord.X < (vj.X - vi.X) * (coord.Y - vi.Y) / (double)(vj.Y - vi.Y) + vi.X))
                {
                    inside = !inside;
                }
            }
            return inside;
        }

        // ──────────── 等值 / 哈希 ────────────

        public bool Equals(MapRegionDefinition other)
        {
            if (RegionIdValue != other.RegionIdValue) return false;
            if (Kind != other.Kind) return false;
            if (OwnerSide != other.OwnerSide) return false;
            if (Priority != other.Priority) return false;
            if (Activation != other.Activation) return false;
            if (Bounds.Count != other.Bounds.Count) return false;
            for (int i = 0; i < Bounds.Count; i++)
                if (!Bounds[i].Equals(other.Bounds[i])) return false;
            if (Triggers.Count != other.Triggers.Count) return false;
            for (int i = 0; i < Triggers.Count; i++)
                if (!Triggers[i].Equals(other.Triggers[i])) return false;
            return true;
        }

        public override bool Equals(object obj) => obj is MapRegionDefinition other && Equals(other);

        public override int GetHashCode()
        {
            unchecked
            {
                int h = RegionIdValue.Value * 397;
                h = (h * 397) ^ (int)Kind;
                h = (h * 397) ^ OwnerSide;
                h = (h * 397) ^ Priority;
                h = (h * 397) ^ (int)Activation;
                // Bounds 只用第一个顶点参与 hash（Bounds 已排序，可重复构造）。
                h = (h * 397) ^ (Bounds.Count > 0 ? Bounds[0].GetHashCode() : 0);
                return h;
            }
        }

        public static bool operator ==(MapRegionDefinition a, MapRegionDefinition b) => a.Equals(b);
        public static bool operator !=(MapRegionDefinition a, MapRegionDefinition b) => !a.Equals(b);

        public override string ToString()
            => $"MapRegionDef(Id={RegionIdValue}, Kind={Kind}, Owner={OwnerSide}, Prio={Priority}, Act={Activation}, Bounds={Bounds.Count}, Triggers={Triggers.Count})";

        // ──────────── 静态工厂 ────────────

        /// <summary>玩家初始部署区工厂（<see cref="RegionKind.PlayerDeployment"/>）。</summary>
        public static MapRegionDefinition PlayerSpawn(int regionId, IEnumerable<GridCoord> bounds, int priority = 50)
            => new MapRegionDefinition(
                new RegionId(regionId),
                RegionKind.PlayerDeployment,
                bounds,
                ownerSide: 0,
                priority: priority,
                activation: RegionActivation.Available);

        /// <summary>敌方刷新区工厂（<see cref="RegionKind.EnemySpawn"/>）。</summary>
        public static MapRegionDefinition EnemySpawn(int regionId, IEnumerable<GridCoord> bounds, int priority = 30)
            => new MapRegionDefinition(
                new RegionId(regionId),
                RegionKind.EnemySpawn,
                bounds,
                ownerSide: 1,
                priority: priority,
                activation: RegionActivation.Available);

        /// <summary>占领区工厂（<see cref="RegionKind.Capture"/>）。</summary>
        public static MapRegionDefinition Capture(int regionId, IEnumerable<GridCoord> bounds, int ownerSide = -1, int priority = 50)
            => new MapRegionDefinition(
                new RegionId(regionId),
                RegionKind.Capture,
                bounds,
                ownerSide: ownerSide,
                priority: priority,
                activation: RegionActivation.Available);

        /// <summary>防守区工厂（<see cref="RegionKind.Defense"/>）。</summary>
        public static MapRegionDefinition Defense(int regionId, IEnumerable<GridCoord> bounds, int priority = 50)
            => new MapRegionDefinition(
                new RegionId(regionId),
                RegionKind.Defense,
                bounds,
                ownerSide: 0,
                priority: priority,
                activation: RegionActivation.Available);

        /// <summary>护送区工厂（<see cref="RegionKind.Escort"/>）。</summary>
        public static MapRegionDefinition Escort(int regionId, IEnumerable<GridCoord> bounds, int priority = 50)
            => new MapRegionDefinition(
                new RegionId(regionId),
                RegionKind.Escort,
                bounds,
                ownerSide: 0,
                priority: priority,
                activation: RegionActivation.Available);

        /// <summary>撤离目标区工厂（<see cref="RegionKind.Extraction"/>）。</summary>
        public static MapRegionDefinition Extraction(int regionId, IEnumerable<GridCoord> bounds, int priority = 50)
            => new MapRegionDefinition(
                new RegionId(regionId),
                RegionKind.Extraction,
                bounds,
                ownerSide: 0,
                priority: priority,
                activation: RegionActivation.Available);

        /// <summary>增援区工厂（<see cref="RegionKind.Reinforcement"/>）。</summary>
        public static MapRegionDefinition Reinforcement(int regionId, IEnumerable<GridCoord> bounds, int ownerSide = 0, int priority = 40)
            => new MapRegionDefinition(
                new RegionId(regionId),
                RegionKind.Reinforcement,
                bounds,
                ownerSide: ownerSide,
                priority: priority,
                activation: RegionActivation.Hidden);

        /// <summary>限制区工厂（<see cref="RegionKind.Restricted"/>，默认 0 = 玩家保护）。</summary>
        public static MapRegionDefinition Restricted(int regionId, IEnumerable<GridCoord> bounds, int priority = 50)
            => new MapRegionDefinition(
                new RegionId(regionId),
                RegionKind.Restricted,
                bounds,
                ownerSide: -1,
                priority: priority,
                activation: RegionActivation.Available);

        /// <summary>互动区工厂（<see cref="RegionKind.Interaction"/>）。</summary>
        public static MapRegionDefinition Interaction(int regionId, IEnumerable<GridCoord> bounds, int priority = 50)
            => new MapRegionDefinition(
                new RegionId(regionId),
                RegionKind.Interaction,
                bounds,
                ownerSide: -1,
                priority: priority,
                activation: RegionActivation.Available);

        /// <summary>Boss 阶段区工厂（<see cref="RegionKind.BossPhase"/>）。</summary>
        public static MapRegionDefinition BossPhase(int regionId, IEnumerable<GridCoord> bounds, int priority = 60)
            => new MapRegionDefinition(
                new RegionId(regionId),
                RegionKind.BossPhase,
                bounds,
                ownerSide: -1,
                priority: priority,
                activation: RegionActivation.Hidden);

        /// <summary>剧情触发区工厂（<see cref="RegionKind.StoryTrigger"/>）。</summary>
        public static MapRegionDefinition StoryTrigger(int regionId, IEnumerable<GridCoord> bounds, int priority = 50)
            => new MapRegionDefinition(
                new RegionId(regionId),
                RegionKind.StoryTrigger,
                bounds,
                ownerSide: -1,
                priority: priority,
                activation: RegionActivation.Available);

        /// <summary>坍塌预警区工厂（<see cref="RegionKind.Collapse"/>）。</summary>
        public static MapRegionDefinition Collapse(int regionId, IEnumerable<GridCoord> bounds, int priority = 50)
            => new MapRegionDefinition(
                new RegionId(regionId),
                RegionKind.Collapse,
                bounds,
                ownerSide: -1,
                priority: priority,
                activation: RegionActivation.Hidden);

        /// <summary>环境危害区工厂（<see cref="RegionKind.EnvironmentalHazard"/>）。</summary>
        public static MapRegionDefinition EnvironmentalHazard(int regionId, IEnumerable<GridCoord> bounds, int priority = 50)
            => new MapRegionDefinition(
                new RegionId(regionId),
                RegionKind.EnvironmentalHazard,
                bounds,
                ownerSide: -1,
                priority: priority,
                activation: RegionActivation.Available);

        /// <summary>镜头序列区工厂（<see cref="RegionKind.CameraSequence"/>）。</summary>
        public static MapRegionDefinition CameraSequence(int regionId, IEnumerable<GridCoord> bounds, int priority = 50)
            => new MapRegionDefinition(
                new RegionId(regionId),
                RegionKind.CameraSequence,
                bounds,
                ownerSide: -1,
                priority: priority,
                activation: RegionActivation.Available);
    }
}