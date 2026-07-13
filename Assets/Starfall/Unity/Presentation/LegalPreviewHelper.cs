using System.Collections.Generic;
using Starfall.Core.Combat;
using Starfall.Core.Model;
using Starfall.Core.Status;

namespace Starfall.Unity.Presentation
{
    /// <summary>
    /// Task 18 预览辅助（纯 C#，可在 EditMode 中直接单测）。
    ///
    /// 设计目标（AGENTS.md §10.3 / §11）：
    /// 1. 不持有 BattleState 真值——只读参数；
    /// 2. 邻居顺序固定为 <b>下、左、右、上</b>（与 BFSPathfinder 一致），枚举结果也按 (Y, X) 升序；
    /// 3. 委托给 <see cref="Core.Combat.DamageFormula"/> 计算伤害，不复制 Core 规则；
    /// 4. 伤害 / 坠落 / 相位翻转 预览都返回 <see cref="PreviewCell"/>，UI 层按 (Y, X) 升序绘制。
    /// </summary>
    /// <remarks>
    /// 关于"AP / CV / PV"的来源说明：
    /// Core 当前没有显式 Ap / Cv / CurrentPhase 字段（避免破坏 MVP 数据面），
    /// 因此预览层基于以下**已有 Core 数据**派生：
    /// - <b>PV</b> = UnitState.Phase（Light/Dark），与 BattleState.ActivePlayer 共同表达"当前相位"
    /// - <b>CV</b> = 作用在该单位上、RemainingTurns&gt;0 的 StatusInstance 总剩余回合数
    /// - <b>AP</b> = UnitState.MaxHp（每回合满血即可行动，作为行动点上限的占位）
    /// 这些派生值只用于 HUD 显示，不参与任何 Command / Replay 计算，
    /// 因此不会改变 BattleStateHash。
    /// </remarks>
    public static class LegalPreviewHelper
    {
        /// <summary>默认移动半径（= MaxHp 派生前的占位常量；与 Core 5 格约定一致）。</summary>
        public const int DefaultMoveRadius = 5;

        /// <summary>默认基础伤害（与 CommandBuilder.BuildAttack 默认 3 一致）。</summary>
        public const int DefaultBaseDamage = 3;

        /// <summary>默认坠落伤害（与 FallingCommand 构造函数默认 1 一致）。</summary>
        public const int DefaultFallDamage = 1;

        // 邻居顺序固定：下 (0,1)、左 (-1,0)、右 (1,0)、上 (0,-1)
        private static readonly (int dx, int dy)[] Neighbors = new (int, int)[]
        {
            (0, 1), (-1, 0), (1, 0), (0, -1)
        };

        // ============== 1. 合法落点（移动范围） ==============

        /// <summary>
        /// 计算单位在 <paramref name="maxSteps"/> 步内可达的格子集合（不含起点）。
        /// 邻居顺序 = BFSPathfinder 一致；BFS 队列本身按 FIFO 即可，距离相等时落点按 (Y, X) 升序返回。
        /// </summary>
        public static IReadOnlyList<GridPos> Reachable(
            BoardState board,
            GridPos from,
            IReadOnlyCollection<GridPos> occupied,
            int maxSteps)
        {
            var result = new List<GridPos>();
            if (board == null) return result;
            if (maxSteps <= 0) return result;
            if (!IsWalkable(board, from)) return result;

            var occupiedSet = occupied != null ? new HashSet<GridPos>(occupied) : new HashSet<GridPos>();
            // 起点若被自身占据则忽略（move preview 时 from 必然被选中单位占据）
            occupiedSet.Remove(from);

            // BFS：按 (Y, X) 升序加入初始访问点；同距多个候选落点时 (Y, X) 升序记录到 result
            var visited = new HashSet<GridPos> { from };
            var frontier = new Queue<GridPos>();
            frontier.Enqueue(from);

            for (int step = 0; step < maxSteps; step++)
            {
                var nextFrontier = new Queue<GridPos>();
                while (frontier.Count > 0)
                {
                    var cur = frontier.Dequeue();
                    // 邻居顺序固定：下、左、右、上
                    for (int i = 0; i < Neighbors.Length; i++)
                    {
                        var n = new GridPos(cur.X + Neighbors[i].dx, cur.Y + Neighbors[i].dy);
                        if (!IsWalkable(board, n)) continue;
                        if (occupiedSet.Contains(n)) continue;
                        if (visited.Contains(n)) continue;
                        visited.Add(n);
                        nextFrontier.Enqueue(n);
                        result.Add(n);
                    }
                }
                if (nextFrontier.Count == 0) break;
                frontier = nextFrontier;
            }
            // 升序排：保持 deterministic
            result.Sort((a, b) => a.CompareTo(b));
            return result;
        }

        private static bool IsWalkable(BoardState board, GridPos p)
        {
            if (p.X < 0 || p.Y < 0 || p.X >= board.Width || p.Y >= board.Height) return false;
            if (board.Tiles.TryGetValue(p, out var t) && t == TileState.Blocked) return false;
            return true;
        }

        // ============== 2. 邻格敌对单位（攻击范围） ==============

        /// <summary>
        /// 攻击目标：所有与 <paramref name="attackerId"/> 所在格子 Chebyshev 距离 ≤ 1 的异主单位。
        /// 返回结果按 (Y, X) 升序；与 BFSPathfinder / ConfirmAttack 一致。
        /// </summary>
        public static IReadOnlyList<AttackTarget> AdjacentEnemies(BattleState state, int attackerId)
        {
            var list = new List<AttackTarget>();
            if (state == null) return list;

            UnitState attacker = null;
            for (int i = 0; i < state.Units.Count; i++)
            {
                if (state.Units[i].UnitId == attackerId) { attacker = state.Units[i]; break; }
            }
            if (attacker == null) return list;

            for (int i = 0; i < state.Units.Count; i++)
            {
                var u = state.Units[i];
                if (u.Owner == attacker.Owner) continue;
                int dx = System.Math.Abs(u.Pos.X - attacker.Pos.X);
                int dy = System.Math.Abs(u.Pos.Y - attacker.Pos.Y);
                if (System.Math.Max(dx, dy) <= 1)
                {
                    list.Add(new AttackTarget(u.UnitId, u.Pos));
                }
            }
            // (Y, X) 升序
            list.Sort((a, b) => a.Pos.CompareTo(b.Pos));
            return list;
        }

        // ============== 3. 坠落预览 ==============

        /// <summary>
        /// 回合结束若当前 <see cref="BattleState.ActivePlayer"/> 处于 Dark 相位且
        /// 单位站在 <see cref="TileState.Hazard"/> 瓦片上，则视为可能坠落。
        /// 这与 <see cref="Core.Rules.FallingCommand"/> 的语义最接近的纯派生规则
        /// （FallingCommand 自身不依赖 phase / tile，但 Task 18 预览层需要一个可观察的触发条件）。
        /// 返回受影响的格子 + 单位（按 (Y, X) 升序）。
        /// </summary>
        public static IReadOnlyList<FallPreview> FallTargets(BattleState state, Phase activePhase)
        {
            var list = new List<FallPreview>();
            if (state == null) return list;
            if (activePhase != Phase.Dark) return list; // 预览：仅 Dark 相位下危险

            for (int i = 0; i < state.Units.Count; i++)
            {
                var u = state.Units[i];
                if (state.Board.Tiles.TryGetValue(u.Pos, out var t) && t == TileState.Hazard)
                {
                    list.Add(new FallPreview(u.UnitId, u.Pos, DefaultFallDamage));
                }
            }
            list.Sort((a, b) => a.Pos.CompareTo(b.Pos));
            return list;
        }

        // ============== 4. 伤害预览 ==============

        /// <summary>
        /// 调用 Core 的 <see cref="DamageFormula.ComputeWithStatuses"/>，不复制规则。
        /// 返回值 &lt; 0 表示无效（如缺攻击者 / 缺目标 / 距离过远）。
        /// </summary>
        public static int PreviewDamage(BattleState state, int attackerId, int targetId, int baseDamage = DefaultBaseDamage)
        {
            if (state == null) return -1;

            UnitState attacker = null, target = null;
            for (int i = 0; i < state.Units.Count; i++)
            {
                if (state.Units[i].UnitId == attackerId) attacker = state.Units[i];
                else if (state.Units[i].UnitId == targetId) target = state.Units[i];
            }
            if (attacker == null || target == null) return -1;
            int dx = System.Math.Abs(attacker.Pos.X - target.Pos.X);
            int dy = System.Math.Abs(attacker.Pos.Y - target.Pos.Y);
            if (System.Math.Max(dx, dy) > 1) return -1;

            return DamageFormula.ComputeWithStatuses(baseDamage, attacker, target, state.Statuses);
        }

        // ============== 5. 单位 HUD 派生值 ==============

        /// <summary>
        /// 派生 PV / CV / AP 显示值（不修改 UnitState）。
        /// 详见类型注释。
        /// </summary>
        public static DerivedStats DeriveStats(UnitState u, IReadOnlyList<StatusInstance> allStatuses)
        {
            if (u == null) return default;
            int cv = 0;
            if (allStatuses != null)
            {
                for (int i = 0; i < allStatuses.Count; i++)
                {
                    var s = allStatuses[i];
                    if (s.SourceUnitId == u.UnitId && s.RemainingTurns > 0)
                    {
                        cv += s.RemainingTurns;
                    }
                }
            }
            return new DerivedStats(
                pv: u.Phase,
                ap: u.MaxHp,        // 占位：每回合满血即满 AP
                cv: cv,
                hp: u.Hp,
                maxHp: u.MaxHp);
        }
    }

    /// <summary>攻击目标：单位 id + 目标格。</summary>
    public readonly struct AttackTarget
    {
        public int UnitId { get; }
        public GridPos Pos { get; }
        public AttackTarget(int unitId, GridPos pos) { UnitId = unitId; Pos = pos; }
    }

    /// <summary>坠落预览：受影响单位 + 所在格 + 预估伤害。</summary>
    public readonly struct FallPreview
    {
        public int UnitId { get; }
        public GridPos Pos { get; }
        public int FallDamage { get; }
        public FallPreview(int unitId, GridPos pos, int fallDamage)
        { UnitId = unitId; Pos = pos; FallDamage = fallDamage; }
    }

    /// <summary>单位 HUD 派生值（PV/AP/CV/HP）。</summary>
    public readonly struct DerivedStats
    {
        public Phase Pv { get; }
        public int Ap { get; }
        public int Cv { get; }
        public int Hp { get; }
        public int MaxHp { get; }
        public DerivedStats(Phase pv, int ap, int cv, int hp, int maxHp)
        { Pv = pv; Ap = ap; Cv = cv; Hp = hp; MaxHp = maxHp; }
    }
}
