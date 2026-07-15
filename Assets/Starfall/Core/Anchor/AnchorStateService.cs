using System;
using System.Collections.Generic;

namespace Starfall.Core.Anchor
{
    /// <summary>
    /// doc2 MAP-03 锚点状态（runtime mutable）注册表。
    /// <para/>
    /// 与 <see cref="Starfall.Core.Map.Commands.PhaseFlipStateService"/> 同模式：
    /// <list type="bullet">
    /// <item><see cref="AnchorZone"/> 是 class，但其字段只有 <c>ZoneId</c> / <c>Owner</c> / <c>Vertices</c>
    ///       —— 文档 §21.1 要求的 7 状态（Inactive / PlayerControlled / EnemyControlled /
    ///       Overloaded / Damaged / Destroyed / Locked）不存于 <see cref="AnchorZone"/>。</item>
    /// <item>本服务为每个 <c>MapState</c> attach 一份 <c>Dictionary&lt;ZoneId, AnchorZoneState&gt;</c>，
    ///       提供 zone 生命周期的状态查询 / 写入。</li>
    /// <li>runtime state 不进入 <see cref="Starfall.Core.Map.State.MapState.PostStateHash"/> 字段作用域
    ///       （与 PhaseFlipStateService 一致）；状态改动本身由 <see cref="Starfall.Core.Map.Commands.ModifyAnchorStateCommand"/>
    ///       显式发射 <c>OnRegionChanged</c> / <c>OnAnchorLinkCreated</c> 事件供 hash 跟踪。</item>
    /// </list>
    /// </summary>
    public static class AnchorStateService
    {
        private static readonly object _gate = new object();
        private static readonly Dictionary<Starfall.Core.Map.State.MapState, Dictionary<int, AnchorZoneState>> _byMap
            = new Dictionary<Starfall.Core.Map.State.MapState, Dictionary<int, AnchorZoneState>>();

        public static void Attach(Starfall.Core.Map.State.MapState map)
        {
            if (map == null) throw new ArgumentNullException(nameof(map));
            lock (_gate)
            {
                if (!_byMap.ContainsKey(map))
                    _byMap[map] = new Dictionary<int, AnchorZoneState>();
            }
        }

        public static void Detach(Starfall.Core.Map.State.MapState map)
        {
            if (map == null) throw new ArgumentNullException(nameof(map));
            lock (_gate)
            {
                _byMap.Remove(map);
            }
        }

        public static void DetachAll()
        {
            lock (_gate)
            {
                _byMap.Clear();
            }
        }

        /// <summary>
        /// 取得 zone 当前状态。未注册 → null。
        /// </summary>
        public static bool TryGetState(Starfall.Core.Map.State.MapState map, int zoneId, out AnchorZoneState state)
        {
            state = AnchorZoneState.Inactive;
            if (map == null) return false;
            lock (_gate)
            {
                if (_byMap.TryGetValue(map, out var dict)
                    && dict.TryGetValue(zoneId, out state))
                {
                    return true;
                }
            }
            return false;
        }

        /// <summary>
        /// 设置 zone 状态。若 zoneId 在附件中不存在则**自动初始化**为 <see cref="AnchorZoneState.Inactive"/>
        /// 后再写入新值（保证 lazy attach 兼容）。
        /// </summary>
        public static void SetState(Starfall.Core.Map.State.MapState map, int zoneId, AnchorZoneState state)
        {
            if (map == null) throw new ArgumentNullException(nameof(map));
            lock (_gate)
            {
                if (!_byMap.TryGetValue(map, out var dict))
                {
                    dict = new Dictionary<int, AnchorZoneState>();
                    _byMap[map] = dict;
                }
                dict[zoneId] = state;
            }
        }

        /// <summary>
        /// 获取 zone 当前状态；不存在则返回 <see cref="AnchorZoneState.Inactive"/> 默认值（不抛）。
        /// </summary>
        public static AnchorZoneState GetOrDefault(Starfall.Core.Map.State.MapState map, int zoneId)
        {
            return TryGetState(map, zoneId, out var s) ? s : AnchorZoneState.Inactive;
        }
    }

    /// <summary>
    /// doc2 §21.1 锚点状态枚举。7 个明确状态 + 0 默认。
    /// </summary>
    public enum AnchorZoneState : byte
    {
        /// <summary>未激活 / 默认值（zone 存在于 map 但未被任何阵营控制）。</summary>
        Inactive = 0,

        /// <summary>玩家阵营控制。</summary>
        PlayerControlled = 1,

        /// <summary>敌方阵营控制。</summary>
        EnemyControlled = 2,

        /// <summary>中立（双方都不是）。</summary>
        Neutral = 3,

        /// <summary>过载（律令 / CV 过载导致可用性下降）。</summary>
        Overloaded = 4,

        /// <summary>损坏（attacker 受损但未毁灭）。</summary>
        Damaged = 5,

        /// <summary>已摧毁（不可恢复；需要重建）。</summary>
        Destroyed = 6,

        /// <summary>锁定（规则禁止任何状态变化，例如剧情节点）。</summary>
        Locked = 7,
    }
}
