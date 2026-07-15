using System;
using System.Collections.Generic;
using Starfall.Core.Map.Coordinates;

namespace Starfall.Core.Map.Tile
{
    /// <summary>
    /// doc2 MAP-04 §4.6 单一地块的运行时状态（mutable runtime class）。
    ///
    /// <para/>
    /// **与 <see cref="TileDefinition"/> 的关系**：
    /// <list type="bullet">
    /// <item><see cref="TileDefinition"/>：不可变定义（readonly struct）。</item>
    /// <item><see cref="MapTileState"/>：运行时可变状态（class）。</item>
    /// <item>运行时 <see cref="EffectiveMoveCost"/> / <see cref="IsPassable"/> 等
    ///       在 <see cref="TileDefinition"/> 的不可变字段上叠加可变修饰符
    ///       （<see cref="Stability"/> / <see cref="TemporaryMoveCostModifier"/> /
    ///       <see cref="OccupyingUnitId"/>）。</item>
    /// </list>
    ///
    /// <para/>
    /// **字段语义**：
    /// <list type="bullet">
    /// <item><see cref="Stability"/>：稳定性 [0, 100]，初始 = 100；0 = 已坍塌 → <see cref="IsPassable"/> = false。</item>
    /// <item><see cref="IsPassable"/>：运行时是否可通过（结合 <see cref="Definition.BlocksMovement"/> +
    ///       <see cref="Stability"/> + <see cref="OccupyingUnitId"/>）。</item>
    /// <item><see cref="IsVisible"/>：当前帧是否可见（被玩家单位观察到）。</item>
    /// <item><see cref="IsRevealed"/>：一次性揭示（fog of war），一旦 true 永不可逆。</item>
    /// <item><see cref="OccupyingUnitId"/>：占用此 tile 的单位 id（Footprint 锚点单元）；null = 无单位占用。</item>
    /// <item><see cref="OccupyingObjectId"/>：占用此 tile 的对象 id（同 <see cref="Starfall.Core.Map.State.MapObjectInstance"/>）。</item>
    /// <item><see cref="LocalCollapseValue"/>：本地坍塌值 [0, 100]，与全局 <c>GlobalCollapseValue</c> 共同决定坍塌概率。</item>
    /// <item><see cref="TemporaryMoveCostModifier"/>：临时移动成本修正（律令 / Buff，可负）。</item>
    /// <item><see cref="ActiveMapEffects"/>：当前激活的地图效果字符串列表（如 "OnFire" / "Frozen"），由律令系统管理。</item>
    /// </list>
    ///
    /// <para/>
    /// **与 <c>Core.Model.TileState</c> 同名隔离**：
    /// 旧 <see cref="Starfall.Core.Model.TileState"/> 是 doc1 MVP 的 4 类 enum，
    /// 由 <see cref="Starfall.Core.Model.BoardState"/> 使用；本 <see cref="MapTileState"/>
    /// 是 doc2 MAP-04 引入的运行时 class，类型完全独立，避免命名冲突。
    /// </summary>
    public sealed class MapTileState
    {
        // ──────────── 内部集合 ────────────

        private readonly List<string> _activeEffects;

        // ──────────── 不可变字段 ────────────

        /// <summary>对应的 <see cref="TileDefinition"/>（基线不可变）。</summary>
        public TileDefinition Definition { get; }

        /// <summary>对应的 tile id（与 <see cref="Definition.TileId"/> 一致）。</summary>
        public int TileId => Definition.TileId;

        /// <summary>对应的 <see cref="GridCoord"/>（与 <see cref="Definition.Coord"/> 一致）。</summary>
        public GridCoord Coord => Definition.Coord;

        // ──────────── 可变字段 ────────────

        /// <summary>稳定性 [0, 100]，0 = 已坍塌。初始 = 100。</summary>
        public int Stability { get; set; } = 100;

        /// <summary>运行时是否可通过；结合 <see cref="Definition.BlocksMovement"/> + <see cref="Stability"/> + <see cref="OccupyingUnitId"/> 派生。</summary>
        public bool IsPassable { get; set; }

        /// <summary>当前帧是否可见（被玩家单位观察到）。</summary>
        public bool IsVisible { get; set; }

        /// <summary>一次性揭示（fog of war），一旦 true 永不可逆。</summary>
        public bool IsRevealed { get; set; }

        /// <summary>占用此 tile 的单位 id（Footprint 锚点单元）；null = 无单位占用。</summary>
        public int? OccupyingUnitId { get; set; }

        /// <summary>占用此 tile 的对象 id（同 <see cref="Starfall.Core.Map.State.MapObjectInstance"/>）。</summary>
        public int? OccupyingObjectId { get; set; }

        /// <summary>本地坍塌值 [0, 100]，与全局 <c>GlobalCollapseValue</c> 共同决定坍塌概率。</summary>
        public int LocalCollapseValue { get; set; } = 0;

        /// <summary>临时移动成本修正（律令 / Buff，可负）。</summary>
        public int TemporaryMoveCostModifier { get; set; } = 0;

        /// <summary>当前激活的地图效果字符串列表（只读视图；修改需通过 <see cref="AddEffect"/> / <see cref="RemoveEffect"/>）。</summary>
        public IReadOnlyList<string> ActiveMapEffects => _activeEffects;

        /// <summary>
        /// doc2 MAP-07 当前激活维度（<see cref="DimensionLayer.Reality"/> 默认；
        /// <see cref="Starfall.Core.Map.Commands.FlipTilePhaseCommand"/> 翻转后变为另一层）。
        /// <para/>
        /// **与 <see cref="TileDefinition.PhasePairTileId"/> 的关系**：当某个 tile
        /// 拥有配对 tile（<c>PhasePairTileId</c> != null），本字段变化时，
        /// 配对 tile 的 <see cref="ActiveDimension"/> 也应同步翻转（由
        /// <c>PhasePairLookup</c> + <see cref="Starfall.Core.Map.Commands.FlipTilePhaseCommand"/>
        /// cascade flip 保证；本类只持有单 tile 状态）。
        /// </summary>
        public DimensionLayer ActiveDimension { get; private set; } = DimensionLayer.Reality;

        // ──────────── 构造 ────────────

        public MapTileState(TileDefinition definition)
        {
            if (definition.TileId < 1)
                throw new ArgumentException("TileDefinition.TileId must be >= 1.", nameof(definition));
            Definition = definition;
            _activeEffects = new List<string>();
            // 初始 IsPassable = !BlocksMovement（默认地形状态）；
            // Stability 与 OccupyingUnitId 由后续命令 / 服务更新。
            IsPassable = !definition.BlocksMovement;
            // MAP-07：初始 ActiveDimension = Reality；上层 PhaseFlipStateService
            // 可在 attach 时遍历旧 dict 把已翻转的 tile 同步设置（向后兼容 MAP-08 stub）。
            ActiveDimension = DimensionLayer.Reality;
        }

        // ──────────── MAP-07 ActiveDimension 操作 ────────────

        /// <summary>
        /// doc2 MAP-07 翻转此 tile 到 <paramref name="target"/> 层。
        /// <para/>
        /// **返回**：
        /// <list type="bullet">
        /// <item><c>true</c>：成功翻转（<see cref="ActiveDimension"/> 已更新为 <paramref name="target"/>）。</item>
        /// <item><c>false</c>：未翻转，可能原因 — (a) 已在 <paramref name="target"/> 层；
        ///       (b) <see cref="Definition"/>.Tags 含 <see cref="TileTags.PhaseLocked"/>。</item>
        /// </list>
        /// <para/>
        /// **不引发异常**：与 doc2 §21 一致，调用方负责根据返回值判定
        /// <c>"already at target layer"</c> / <c>"phase locked"</c> 等失败原因。
        /// <para/>
        /// **不更新配对 tile**：调用方（如
        /// <c>FlipTilePhaseCommand</c>）负责主动通过
        /// <c>PhasePairLookup.TryGetPair</c> 找到配对 tile 并 cascade 调用
        /// 其 <c>TryFlipTo</c>，从而保证双层同步。
        /// </summary>
        public bool TryFlipTo(DimensionLayer target)
        {
            if (ActiveDimension == target) return false;
            if ((Definition.Tags & TileTags.PhaseLocked) != 0) return false;
            ActiveDimension = target;
            return true;
        }

        /// <summary>
        /// doc2 MAP-07 直接设置 <see cref="ActiveDimension"/>（无校验）。
        /// <para/>
        /// 仅供 <see cref="Starfall.Core.Map.Commands.PhaseFlipStateService"/> / 测试 fixture 的
        /// migration path（从旧 _flipped dict 迁移到字段）使用；业务代码应使用
        /// <see cref="TryFlipTo"/>。
        /// </summary>
        public void SetActiveDimensionDirect(DimensionLayer layer)
        {
            ActiveDimension = layer;
        }

        // ──────────── 派生属性 ────────────

        /// <summary>
        /// 有效移动成本 = <see cref="Definition.BaseMoveCost"/> + <see cref="TemporaryMoveCostModifier"/>，
        /// clamp 到下界 1（避免律令"陷阱"导致成本 ≤ 0 引发移动系统崩溃）。
        /// </summary>
        public int EffectiveMoveCost
        {
            get
            {
                int cost = Definition.BaseMoveCost + TemporaryMoveCostModifier;
                return cost < 1 ? 1 : cost;
            }
        }

        /// <summary>true = 当前 tile 已被某个单位占用（<see cref="OccupyingUnitId"/> != null）。</summary>
        public bool IsOccupiedByUnit => OccupyingUnitId.HasValue;

        /// <summary>true = 当前 tile 已被某个对象占用（<see cref="OccupyingObjectId"/> != null）。</summary>
        public bool IsOccupiedByObject => OccupyingObjectId.HasValue;

        /// <summary>true = 稳定性已耗尽（坍塌），视为不可通过。</summary>
        public bool HasCollapsed => Stability <= 0;

        /// <summary>
        /// 运行时是否真正可通过——综合 <see cref="IsPassable"/> + <see cref="HasCollapsed"/>
        /// + <see cref="IsOccupiedByUnit"/>。调用方在移动命令前应调用此属性。
        /// </summary>
        public bool IsEffectivelyPassable
        {
            get
            {
                if (HasCollapsed) return false;
                if (IsOccupiedByUnit) return false;
                return IsPassable;
            }
        }

        // ──────────── 集合操作 ────────────

        /// <summary>添加一个激活的地图效果；已存在则不重复添加。</summary>
        public void AddEffect(string effect)
        {
            if (effect == null) throw new ArgumentNullException(nameof(effect));
            if (_activeEffects.Contains(effect)) return;
            _activeEffects.Add(effect);
        }

        /// <summary>移除一个激活的地图效果；不存在则返回 false。</summary>
        public bool RemoveEffect(string effect)
        {
            if (effect == null) return false;
            return _activeEffects.Remove(effect);
        }

        // ──────────── 字符串 ────────────

        public override string ToString()
            => $"MapTileState(Id={TileId}, Coord={Coord}, Stable={Stability}, Passable={IsPassable}, Unit={OccupyingUnitId}, Obj={OccupyingObjectId}, Cost={EffectiveMoveCost})";
    }
}