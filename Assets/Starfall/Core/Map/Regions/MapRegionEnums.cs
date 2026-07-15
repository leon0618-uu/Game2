using System;
using System.Collections.Generic;
using Starfall.Core.Map.Coordinates;

namespace Starfall.Core.Map.Regions
{
    /// <summary>
    /// doc2 MAP-09 §3.4 区域语义分类枚举（14 种）。
    ///
    /// <para/>
    /// <b>位序固定</b>（AGENTS.md §11）：禁止重排或跳号；任何序列化 / 哈希 / 网络协议
    /// 都依赖此位序。新增类别一律追加到末尾，并通过 ADR 升级。
    ///
    /// <para/>
    /// 数值范围：<c>0..13</c>，对应 14 种区域类型。
    /// 写入 <see cref="MapRegionDefinition.Kind"/> 时 cast 为 int / byte 强一致。
    /// </summary>
    public enum RegionKind : byte
    {
        /// <summary>玩家初始部署区（doc2 §21.3 PlayerDeployment）。</summary>
        PlayerDeployment = 0,

        /// <summary>敌方刷新区（EnemySpawn）。</summary>
        EnemySpawn = 1,

        /// <summary>增援到达区（Reinforcement，N+1 回合单位入场）。</summary>
        Reinforcement = 2,

        /// <summary>占领区（Capture，进度达到 100 → 改写 Owner）。</summary>
        Capture = 3,

        /// <summary>防守区（Defense，驻守一定回合数即可获胜）。</summary>
        Defense = 4,

        /// <summary>护送区（Escort，单位穿过全部 Escort 区域即胜利）。</summary>
        Escort = 5,

        /// <summary>撤离目标区（Extraction，所有玩家单位进入 → 胜利；与 BattleState.ExitTile 并存）。</summary>
        Extraction = 6,

        /// <summary>限制区（Restricted，敌方禁止进入）。</summary>
        Restricted = 7,

        /// <summary>互动区（Interaction，可拾取 / 触发机关）。</summary>
        Interaction = 8,

        /// <summary>Boss 阶段区（BossPhase，进入后切换敌人行为阶段）。</summary>
        BossPhase = 9,

        /// <summary>剧情触发区（StoryTrigger，进入后播放对白 / 切镜头）。</summary>
        StoryTrigger = 10,

        /// <summary>坍塌预警区（Collapse，CV 达到阈值后坍塌）。</summary>
        Collapse = 11,

        /// <summary>镜头序列区（CameraSequence，进入触发预设镜头；MVP 仅占位）。</summary>
        CameraSequence = 12,

        /// <summary>环境危害区（EnvironmentalHazard，每回合对内部单位造成固定伤害）。</summary>
        EnvironmentalHazard = 13,
    }

    /// <summary>
    /// doc2 MAP-09 区域激活阶段（与运行时 <see cref="RegionState"/> 平行但不同）。
    ///
    /// <para/>
    /// <b>区别</b>：
    /// <list type="bullet">
    /// <item><see cref="RegionActivation"/>：定义层面的"是否启用 / 隐藏 / 灰显"，由关卡设计者配置，<see cref="MapRegionDefinition.Activation"/> 持有；关卡开始后通常不变。</item>
    /// <item><see cref="RegionState"/>：运行时状态机的当前态，<see cref="MapRegionState.State"/> 持有；每回合 / 每次事件都会改变。</item>
    /// </list>
    ///
    /// <para/>
    /// 位序固定。
    /// </summary>
    public enum RegionActivation : byte
    {
        /// <summary>完全禁用：未注册 / 被移除（def 层显式）。</summary>
        Disabled = 0,

        /// <summary>隐藏：地图上看不见，UI / fog of war 处理后才会显形。</summary>
        Hidden = 1,

        /// <summary>可用：地图可见但未激活（玩家不能与之互动）。</summary>
        Available = 2,

        /// <summary>激活：进入条件满足，玩家可与之互动。</summary>
        Active = 3,

        /// <summary>封存：永久禁用（剧情结束 / 任务达成后）。</summary>
        Sealed = 4,
    }

    /// <summary>
    /// doc2 MAP-09 区域运行时状态机状态（8 种）。
    ///
    /// <para/>
    /// 状态机合法性（<see cref="MapRegionService.TransitionState"/>）：
    /// <list type="bullet">
    /// <item>Disabled → Hidden / Available</item>
    /// <item>Hidden → Available</item>
    /// <item>Available → Active / Sealed</item>
    /// <item>Active → Contested / Completed / Failed / Sealed</item>
    /// <item>Contested → Active / Completed / Failed</item>
    /// <item>Completed → Sealed</item>
    /// <item>Failed → Sealed</item>
    /// <item>Sealed →（终态）</item>
    /// </list>
    ///
    /// 位序固定。
    /// </summary>
    public enum RegionState : byte
    {
        /// <summary>未启用（def 同步；运行时一般不应保留）。</summary>
        Disabled = 0,

        /// <summary>隐藏（地图上不可见 / 不参与查询）。</summary>
        Hidden = 1,

        /// <summary>可用（可见但未激活）。</summary>
        Available = 2,

        /// <summary>激活（玩家可互动）。</summary>
        Active = 3,

        /// <summary>争夺中（Capture / Defense 双方单位同时在区域内）。</summary>
        Contested = 4,

        /// <summary>完成（Capture 成功 / Defense 守住 / Escort 抵达）。</summary>
        Completed = 5,

        /// <summary>失败（Capture 失败 / Defense 沦陷 / Escort 中断）。</summary>
        Failed = 6,

        /// <summary>封存（终态，不再变化）。</summary>
        Sealed = 7,
    }

    /// <summary>
    /// doc2 MAP-09 区域事件触发器（用于 <see cref="MapRegionDefinition.Triggers"/>）。
    ///
    /// <para/>
    /// 把"哪种事件触发什么行为"封装成数据，由 <see cref="MapRegionService.Tick"/> /
    /// <see cref="MapRegionService.NotifyUnitEntered"/> 在事件到达时按 trigger 类型分发。
    /// </summary>
    public enum RegionTriggerKind : byte
    {
        /// <summary>单位进入区域时触发。</summary>
        OnEnter = 0,

        /// <summary>单位离开区域时触发。</summary>
        OnExit = 1,

        /// <summary>激活进度达到阈值时触发（参数：阈值）。</summary>
        OnProgressThreshold = 2,

        /// <summary>状态变为 Active 时触发。</summary>
        OnActivated = 3,

        /// <summary>状态变为 Completed 时触发。</summary>
        OnCompleted = 4,

        /// <summary>状态变为 Failed 时触发。</summary>
        OnFailed = 5,

        /// <summary>每 Tick 推进时触发。</summary>
        OnTick = 6,
    }

    /// <summary>
    /// doc2 MAP-09 区域事件触发器实例（绑定到 <see cref="MapRegionDefinition"/>）。
    /// </summary>
    public readonly struct RegionTrigger : IEquatable<RegionTrigger>
    {
        /// <summary>触发器种类。</summary>
        public readonly RegionTriggerKind Kind;

        /// <summary>关联的 tag / 标签（用于上层事件路由；空 = 无 tag）。</summary>
        public readonly string Tag;

        /// <summary>进度阈值（仅 OnProgressThreshold 使用；其它触发器忽略）。</summary>
        public readonly int Threshold;

        public RegionTrigger(RegionTriggerKind kind, string tag = null, int threshold = 0)
        {
            Kind = kind;
            Tag = tag ?? string.Empty;
            if (threshold < 0)
                throw new ArgumentOutOfRangeException(nameof(threshold), threshold,
                    "RegionTrigger.Threshold must be >= 0.");
            Threshold = threshold;
        }

        public bool Equals(RegionTrigger other)
            => Kind == other.Kind
               && string.Equals(Tag, other.Tag, StringComparison.Ordinal)
               && Threshold == other.Threshold;

        public override bool Equals(object obj) => obj is RegionTrigger other && Equals(other);

        public override int GetHashCode()
        {
            unchecked
            {
                int h = (int)Kind * 397;
                h ^= (Tag?.GetHashCode() ?? 0);
                h = (h * 397) ^ Threshold;
                return h;
            }
        }

        public override string ToString()
            => $"RegionTrigger(Kind={Kind}, Tag={Tag ?? "-"}, Threshold={Threshold})";
    }

    /// <summary>
    /// doc2 MAP-09 强类型 RegionId。
    ///
    /// <para/>
    /// 简单值类型，避免与 AnchorId / ObjectId 混用导致参数顺序错位。
    /// </summary>
    public readonly struct RegionId : IEquatable<RegionId>, IComparable<RegionId>
    {
        public readonly int Value;

        public RegionId(int value)
        {
            if (value < 0)
                throw new ArgumentOutOfRangeException(nameof(value), value,
                    "RegionId must be >= 0.");
            Value = value;
        }

        public bool Equals(RegionId other) => Value == other.Value;
        public override bool Equals(object obj) => obj is RegionId other && Equals(other);
        public override int GetHashCode() => Value;
        public int CompareTo(RegionId other) => Value.CompareTo(other.Value);
        public override string ToString() => $"Region({Value})";

        public static bool operator ==(RegionId a, RegionId b) => a.Equals(b);
        public static bool operator !=(RegionId a, RegionId b) => !a.Equals(b);
    }

    /// <summary>
    /// doc2 MAP-09 强类型 SpawnId（出生点 ID）。
    /// </summary>
    public readonly struct SpawnId : IEquatable<SpawnId>, IComparable<SpawnId>
    {
        public readonly int Value;

        public SpawnId(int value)
        {
            if (value < 0)
                throw new ArgumentOutOfRangeException(nameof(value), value,
                    "SpawnId must be >= 0.");
            Value = value;
        }

        public bool Equals(SpawnId other) => Value == other.Value;
        public override bool Equals(object obj) => obj is SpawnId other && Equals(other);
        public override int GetHashCode() => Value;
        public int CompareTo(SpawnId other) => Value.CompareTo(other.Value);
        public override string ToString() => $"Spawn({Value})";

        public static bool operator ==(SpawnId a, SpawnId b) => a.Equals(b);
        public static bool operator !=(SpawnId a, SpawnId b) => !a.Equals(b);
    }
}