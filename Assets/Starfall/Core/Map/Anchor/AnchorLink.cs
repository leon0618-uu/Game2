using System;
using System.Collections.Generic;
using Starfall.Core.Anchor;

namespace Starfall.Core.Map.Anchor
{
    /// <summary>
    /// doc2 MAP-12 <c>AnchorLink</c> 运行时可变状态（class，非 readonly struct）。
    /// <para/>
    /// **为什么是 class**（与 <see cref="ConstellationPolygon"/> 不同）：
    /// <list type="bullet">
    /// <item>状态需要随时间变更（<see cref="TransitionTo"/> 修改 <see cref="CurrentState"/> / <see cref="StateTick"/>）；</item>
    /// <item>map 持有 <c>IReadOnlyList&lt;AnchorLink&gt;</c> 引用，命令（<c>IMapCommand</c>）
    ///       直接对实例调 <see cref="TransitionTo"/>；Undo 时 <see cref="AnchorLinkCloner"/>
    ///       重建实例。</item>
    /// </list>
    /// <para/>
    /// **状态机（<see cref="AnchorLinkStateMachine"/>）**：
    /// <list type="bullet">
    /// <item>复用 <see cref="Starfall.Core.Anchor.AnchorZoneState"/> 7 状态枚举
    ///       （<c>Inactive / PlayerControlled / EnemyControlled / Neutral / Overloaded /
    ///       Damaged / Destroyed / Locked</c>）。</item>
    /// <item>合法迁移矩阵由 <see cref="AnchorLinkStateMachine.IsLegalTransition"/> 定义；
    ///       非法迁移抛 <see cref="InvalidAnchorLinkTransitionException"/>。</item>
    /// </list>
    /// <para/>
    /// **构造约束**：
    /// <list type="bullet">
    /// <item><paramref name="id"/> 不可为空；</item>
    /// <item><paramref name="polygon"/> 必须通过 <see cref="ConstellationValidator"/>（构造期已校验）；</item>
    /// <item><paramref name="initialTick"/> 必须 &gt;= 0。</item>
    /// </list>
    /// </summary>
    public sealed class AnchorLink
    {
        /// <summary>运行时唯一字符串 ID。</summary>
        public AnchorLinkId Id { get; }

        /// <summary>当前多边形（Constellation 域）。可通过 <see cref="UpdatePolygon"/> 替换。</summary>
        public ConstellationPolygon Polygon { get; private set; }

        /// <summary>当前 7 状态（来自 <see cref="Starfall.Core.Anchor.AnchorZoneState"/>）。</summary>
        public AnchorZoneState CurrentState { get; private set; }

        /// <summary>上次状态变更时的 tick（确定性时间戳，命令在 Execute 时填入）。</summary>
        public int StateTick { get; private set; }

        /// <summary>上次状态变更后的 PostStateHash（确定性缓存，命令在 Execute 时填入）。</summary>
        public ulong PostStateHash { get; private set; }

        /// <summary>构造期初始状态（默认 <see cref="AnchorZoneState.Inactive"/>）。</summary>
        public AnchorZoneState InitialState { get; }

        public AnchorLink(
            AnchorLinkId id,
            ConstellationPolygon polygon,
            AnchorZoneState initialState = AnchorZoneState.Inactive,
            int initialTick = 0,
            ulong initialPostStateHash = 0UL)
        {
            if (initialTick < 0)
                throw new ArgumentOutOfRangeException(nameof(initialTick), initialTick,
                    "initialTick must be >= 0.");
            Id = id;
            Polygon = polygon;
            InitialState = initialState;
            CurrentState = initialState;
            StateTick = initialTick;
            PostStateHash = initialPostStateHash;
        }

        /// <summary>
        /// 状态机迁移（仅允许 <see cref="AnchorLinkStateMachine"/> 中声明的合法迁移）。
        /// 非法迁移抛 <see cref="InvalidAnchorLinkTransitionException"/>。
        /// </summary>
        /// <param name="newState">目标状态。</param>
        /// <param name="tick">新 tick（必须 &gt;= 0）。</param>
        /// <param name="newPostStateHash">新 PostStateHash（命令在 Execute 后写入）。</param>
        public void TransitionTo(AnchorZoneState newState, int tick, ulong newPostStateHash = 0UL)
        {
            if (tick < 0)
                throw new ArgumentOutOfRangeException(nameof(tick), tick, "tick must be >= 0.");
            if (!AnchorLinkStateMachine.IsLegalTransition(CurrentState, newState))
            {
                throw new InvalidAnchorLinkTransitionException(Id, CurrentState, newState);
            }
            CurrentState = newState;
            StateTick = tick;
            PostStateHash = newPostStateHash;
        }

        /// <summary>
        /// 替换多边形（不影响状态 / tick）。仅当新多边形通过
        /// <see cref="ConstellationValidator"/> 时允许。
        /// </summary>
        public void UpdatePolygon(ConstellationPolygon newPolygon)
        {
            // ConstellationPolygon 构造期已校验；这里只需重新赋值。
            Polygon = newPolygon;
        }

        public override string ToString()
            => $"AnchorLink(Id={Id}, State={CurrentState}, Polygon={Polygon}, Tick={StateTick})";
    }

    /// <summary>
    /// doc2 MAP-12 AnchorLink 非法状态迁移异常。
    /// </summary>
    public sealed class InvalidAnchorLinkTransitionException : InvalidOperationException
    {
        public AnchorLinkId LinkId { get; }
        public AnchorZoneState FromState { get; }
        public AnchorZoneState ToState { get; }

        public InvalidAnchorLinkTransitionException(AnchorLinkId linkId, AnchorZoneState from, AnchorZoneState to)
            : base($"Illegal AnchorLink state transition: LinkId={linkId}, {from} -> {to}")
        {
            LinkId = linkId;
            FromState = from;
            ToState = to;
        }
    }

    /// <summary>
    /// doc2 MAP-12 AnchorLink 7 状态状态机（合法迁移矩阵）。
    /// <para/>
    /// **矩阵设计原则**（业务语义优先 / 与 MAP-03 <c>ModifyAnchorStateCommand</c> 兼容）：
    /// <list type="bullet">
    /// <item><c>Inactive</c> 是默认 / 中心节点，可以被任意非 <c>Destroyed</c> 状态进入；</item>
    /// <item><c>Destroyed</c> 只能迁回 <c>Inactive</c>（"重建"语义），不可直接转其他状态；</item>
    /// <item><c>Locked</c> 是故事锁，仅可被 <c>Destroyed</c> 释放；</item>
    /// <item>同状态自迁移（X → X）视为合法（no-op，命令层另有"未变化"返回）。</item>
    /// </list>
    /// </summary>
    public static class AnchorLinkStateMachine
    {
        // 状态合法迁移表。键 = 当前状态，值 = 允许的目标状态集合。
        // 自迁移（X→X）始终合法，故表中省略；方法层统一处理。
        private static readonly Dictionary<AnchorZoneState, HashSet<AnchorZoneState>> _allowed = BuildMatrix();

        public static bool IsLegalTransition(AnchorZoneState from, AnchorZoneState to)
        {
            if (from == to) return true; // 同状态自迁移：no-op，视为合法
            return _allowed.TryGetValue(from, out var set) && set.Contains(to);
        }

        public static IReadOnlyCollection<AnchorZoneState> AllowedTargets(AnchorZoneState from)
        {
            if (_allowed.TryGetValue(from, out var set)) return set;
            return Array.Empty<AnchorZoneState>();
        }

        private static Dictionary<AnchorZoneState, HashSet<AnchorZoneState>> BuildMatrix()
        {
            // ── 邻接表 ──
            // Inactive  → Player/Enemy/Neutral/Overloaded/Damaged/Locked
            // PlayerControlled → Inactive/Enemy/Neutral/Overloaded/Damaged/Locked
            // EnemyControlled  → Inactive/Player/Neutral/Overloaded/Damaged/Locked
            // Neutral    → Inactive/Player/Enemy/Overloaded/Damaged/Locked
            // Overloaded → Inactive/Player/Enemy/Neutral/Damaged/Locked
            // Damaged    → Inactive/Player/Enemy/Neutral/Overloaded/Destroyed/Locked
            // Destroyed  → Inactive/Locked （仅可重建或被剧情永久锁定）
            // Locked     → Inactive/Player/Enemy/Neutral/Overloaded/Damaged/Destroyed
            //
            // 注：Locked → Destroyed 表示"剧情节点强制销毁"（如清场），
            // 比 Damaged → Destroyed 优先级高，与 doc2 §11.1 一致。
            return new Dictionary<AnchorZoneState, HashSet<AnchorZoneState>>
            {
                [AnchorZoneState.Inactive] = new HashSet<AnchorZoneState>
                {
                    AnchorZoneState.PlayerControlled, AnchorZoneState.EnemyControlled,
                    AnchorZoneState.Neutral, AnchorZoneState.Overloaded,
                    AnchorZoneState.Damaged, AnchorZoneState.Locked,
                },
                [AnchorZoneState.PlayerControlled] = new HashSet<AnchorZoneState>
                {
                    AnchorZoneState.Inactive, AnchorZoneState.EnemyControlled,
                    AnchorZoneState.Neutral, AnchorZoneState.Overloaded,
                    AnchorZoneState.Damaged, AnchorZoneState.Locked,
                },
                [AnchorZoneState.EnemyControlled] = new HashSet<AnchorZoneState>
                {
                    AnchorZoneState.Inactive, AnchorZoneState.PlayerControlled,
                    AnchorZoneState.Neutral, AnchorZoneState.Overloaded,
                    AnchorZoneState.Damaged, AnchorZoneState.Locked,
                },
                [AnchorZoneState.Neutral] = new HashSet<AnchorZoneState>
                {
                    AnchorZoneState.Inactive, AnchorZoneState.PlayerControlled,
                    AnchorZoneState.EnemyControlled, AnchorZoneState.Overloaded,
                    AnchorZoneState.Damaged, AnchorZoneState.Locked,
                },
                [AnchorZoneState.Overloaded] = new HashSet<AnchorZoneState>
                {
                    AnchorZoneState.Inactive, AnchorZoneState.PlayerControlled,
                    AnchorZoneState.EnemyControlled, AnchorZoneState.Neutral,
                    AnchorZoneState.Damaged, AnchorZoneState.Locked,
                },
                [AnchorZoneState.Damaged] = new HashSet<AnchorZoneState>
                {
                    AnchorZoneState.Inactive, AnchorZoneState.PlayerControlled,
                    AnchorZoneState.EnemyControlled, AnchorZoneState.Neutral,
                    AnchorZoneState.Overloaded, AnchorZoneState.Destroyed,
                    AnchorZoneState.Locked,
                },
                [AnchorZoneState.Destroyed] = new HashSet<AnchorZoneState>
                {
                    AnchorZoneState.Inactive, AnchorZoneState.Locked,
                },
                [AnchorZoneState.Locked] = new HashSet<AnchorZoneState>
                {
                    AnchorZoneState.Inactive, AnchorZoneState.PlayerControlled,
                    AnchorZoneState.EnemyControlled, AnchorZoneState.Neutral,
                    AnchorZoneState.Overloaded, AnchorZoneState.Damaged,
                    AnchorZoneState.Destroyed,
                },
            };
        }
    }
}