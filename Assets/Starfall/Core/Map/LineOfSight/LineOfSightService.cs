using System;
using System.Collections.Generic;
using Starfall.Core.Map.Coordinates;
using Starfall.Core.Map.Cover;
using Starfall.Core.Map.Height;
using Starfall.Core.Map.State;

namespace Starfall.Core.Map.LineOfSight
{
    /// <summary>
    /// doc2 MAP-06 §4.6 视线服务（战斗视线，不得依赖 <c>Physics.Raycast</c>）。
    ///
    /// <para/>
    /// **算法**：整数 Supercover（grid traversal, no gaps），从 (x0, y0) 到 (x1, y1)
    /// 枚举所有经过的格子（含起点 + 终点）。任何一格标记为阻挡（<see cref="IBlockingLookup"/>）
    /// 即判定视线失败，记录该格为第一个 <see cref="Result.BlockingTiles"/> 元素。
    ///
    /// <para/>
    /// **关键不变量**（AGENTS.md §10.1 + §11）：
    /// <list type="bullet">
    /// <item>**纯函数**：无副作用、无随机源、不依赖时间 / 线程调度。</item>
    /// <item>**同 Layer 默认**：attacker.Layer == defender.Layer 时走该 Layer 视线；</item>
    /// <item>**跨 Layer** 默认阻挡（除非 <see cref="ProjectileType.CrossPhase"/>）。</item>
    /// <item>**稳定排序**：返回的阻挡集合按 (Y, X) 升序（调用方约定）。</item>
    /// <item>**同 (X, Y) 路径唯一**：Supercover 保证同起终点 → 同路径序列。</item>
    /// </list>
    ///
    /// <para/>
    /// **高地优势（High Ground）**：
    /// <list type="bullet">
    /// <item>同 Layer + attacker.Height - defender.Height ≥ 1 → <see cref="Result.HasHighGroundBonus"/> = true。</item>
    /// <item>CrossPhase 不算 high ground（跨层不算）。</item>
    /// <item>High Ground 时 Half Cover 被忽略（不计入 <see cref="Result.CoverPenalty"/>）。</item>
    /// </list>
    ///
    /// <para/>
    /// **6 种弹道规则**（<see cref="ComputeProjectileLOS"/>）：
    /// <list type="bullet">
    /// <item><see cref="ProjectileType.Direct"/>：Full Cover 必挡；Half 给 CoverPenalty=1。</item>
    /// <item><see cref="ProjectileType.Arc"/>：Full Cover 必挡；Half Cover 忽略（不下惩罚）。</item>
    /// <item><see cref="ProjectileType.Beam"/>：同 Direct，但只命中首目标（实现留给上层）。</item>
    /// <item><see cref="ProjectileType.Chain"/>：N 目标弹跳；LOS 仅判定首目标 + 同 Direct。</item>
    /// <item><see cref="ProjectileType.GroundPropagation"/>：只看 Ground 层（Layer.Reality + Height=0）。</item>
    /// <item><see cref="ProjectileType.CrossPhase"/>：先走 attacker.Layer 全程，再走 defender.Layer 全部。</item>
    /// </list>
    ///
    /// <para/>
    /// **服务边界**：
    /// <list type="bullet">
    /// <item>不引用 <c>UnityEngine</c>；属于 Starfall.Core。</item>
    /// <item>不直接依赖 <see cref="MapState"/> 字段；MapSize 由调用方传入边界检查。</item>
    /// </list>
    /// </summary>
    public static class LineOfSightService
    {
        // ──────────── 结果结构 ────────────

        /// <summary>视线判定结果。</summary>
        public readonly struct Result : IEquatable<Result>
        {
            /// <summary>true = 视线直达（无 Full 阻挡）；false = 视线被阻挡。</summary>
            public readonly bool HasLineOfSight;

            /// <summary>true = attacker 高 defender ≥ 1（同 Layer）；附带 Half Cover 忽略。</summary>
            public readonly bool HasHighGroundBonus;

            /// <summary>掩体惩罚（0 / 1 / 2）。Half Cover 通常为 1，Full Cover 直接失败不返回此值。</summary>
            public readonly int CoverPenalty;

            /// <summary>阻挡视线格列表（按 Y → X 升序）；空列表表示无阻挡。</summary>
            public readonly IReadOnlyList<GridCoord> BlockingTiles;

            public Result(
                bool hasLineOfSight,
                bool hasHighGroundBonus,
                int coverPenalty,
                IReadOnlyList<GridCoord> blockingTiles)
            {
                HasLineOfSight = hasLineOfSight;
                HasHighGroundBonus = hasHighGroundBonus;
                CoverPenalty = coverPenalty;
                BlockingTiles = blockingTiles ?? Array.Empty<GridCoord>();
            }

            /// <summary>无阻挡、无高地、无掩体的"完全清晰"结果。</summary>
            public static Result Clear => new Result(true, false, 0, Array.Empty<GridCoord>());

            public bool Equals(Result other)
                => HasLineOfSight == other.HasLineOfSight
                   && HasHighGroundBonus == other.HasHighGroundBonus
                   && CoverPenalty == other.CoverPenalty
                   && ReferenceEquals(BlockingTiles, other.BlockingTiles);

            public override bool Equals(object obj) => obj is Result r && Equals(r);

            public override int GetHashCode()
            {
                unchecked
                {
                    int h = HasLineOfSight ? 1 : 0;
                    h = (h * 397) ^ (HasHighGroundBonus ? 1 : 0);
                    h = (h * 397) ^ CoverPenalty;
                    h = (h * 397) ^ (BlockingTiles?.Count ?? 0);
                    return h;
                }
            }

            public override string ToString()
                => $"LOS(clear={HasLineOfSight}, hg={HasHighGroundBonus}, pen={CoverPenalty}, blockers={BlockingTiles?.Count ?? 0})";
        }

        // ──────────── 公开入口 ────────────

        /// <summary>
        /// 基础视线判定（Direct 弹道语义，不跨层）。
        /// </summary>
        /// <param name="map">仅用于越界检查（用 <see cref="MapDefinition.Size"/>）。</param>
        /// <param name="from">攻击者坐标。</param>
        /// <param name="to">防御者坐标。</param>
        /// <param name="heights">高度查询；null = 视所有高度 = 0（地面）。</param>
        /// <param name="covers">掩体查询；null = 视所有掩体 = None。</param>
        /// <param name="blocking">阻挡查询；null = 视所有 tile = 不阻挡。</param>
        public static Result ComputeLineOfSight(
            MapState map,
            GridCoord from,
            GridCoord to,
            IHeightLookup heights,
            ICoverLookup covers,
            IBlockingLookup blocking)
        {
            if (map == null) throw new ArgumentNullException(nameof(map));
            return ComputeDirectInternal(map.Definition.Size, from, to, heights, covers, blocking);
        }

        /// <summary>基础视线（不依赖 MapState 的便捷重载）。</summary>
        public static Result ComputeLineOfSight(
            MapSize size,
            GridCoord from,
            GridCoord to,
            IHeightLookup heights,
            ICoverLookup covers,
            IBlockingLookup blocking)
        {
            return ComputeDirectInternal(size, from, to, heights, covers, blocking);
        }

        /// <summary>弹道分类视线判定（6 种 <see cref="ProjectileType"/>）。</summary>
        public static Result ComputeProjectileLOS(
            MapState map,
            GridCoord from,
            GridCoord to,
            ProjectileType projectile,
            IHeightLookup heights,
            ICoverLookup covers,
            IBlockingLookup blocking)
        {
            if (map == null) throw new ArgumentNullException(nameof(map));
            return ComputeProjectileInternal(map.Definition.Size, from, to, projectile, heights, covers, blocking);
        }

        // ──────────── 内部：Direct 实现 ────────────

        private static Result ComputeDirectInternal(
            MapSize size,
            GridCoord from,
            GridCoord to,
            IHeightLookup heights,
            ICoverLookup covers,
            IBlockingLookup blocking)
        {
            // 越界 → 失败
            if (!from.IsInBounds(size) || !to.IsInBounds(size))
            {
                return new Result(false, false, 0, new List<GridCoord> { from });
            }

            // 同 tile → 必清晰，无视高度差、掩体、阻挡
            if (from == to) return Result.Clear;

            // 跨 Layer → 默认阻挡（CrossPhase 不在此方法）
            if (from.Layer != to.Layer)
            {
                return new Result(false, false, 0, new List<GridCoord> { to });
            }

            // 同 Layer：收集路径上阻挡的格子
            var path = TraceSupercoverPath(from, to);
            var blockers = new List<GridCoord>();
            foreach (var p in path)
            {
                if (blocking != null && blocking.BlocksLineOfSight(p))
                {
                    blockers.Add(p);
                }
            }
            // 排序（稳定）：Y → X → Layer
            blockers.Sort();
            if (blockers.Count > 0)
            {
                return new Result(false, false, 0, blockers);
            }

            // 高地优势（仅同 Layer）
            int fromH = heights?.GetHeight(from) ?? 0;
            int toH = heights?.GetHeight(to) ?? 0;
            bool highGround = fromH - toH >= 1;

            // 掩体（仅看 defender tile）
            CoverLevel cover = covers?.GetCover(to) ?? CoverLevel.None;

            // Full Cover 必挡（无论 high ground，墙就是墙）
            if (cover == CoverLevel.Full)
            {
                var fullBlocker = new List<GridCoord> { to };
                return new Result(false, highGround, 0, fullBlocker);
            }

            // Half Cover：high ground 时忽略，否则 penalty=1
            int penalty = 0;
            if (cover == CoverLevel.Half && !highGround)
            {
                penalty = 1;
            }

            return new Result(true, highGround, penalty, Array.Empty<GridCoord>());
        }

        // ──────────── 内部：Projectile 分类 ────────────

        private static Result ComputeProjectileInternal(
            MapSize size,
            GridCoord from,
            GridCoord to,
            ProjectileType projectile,
            IHeightLookup heights,
            ICoverLookup covers,
            IBlockingLookup blocking)
        {
            // GroundPropagation 仅看 Ground 层（Layer.Reality + Height=0）
            if (projectile == ProjectileType.GroundPropagation)
            {
                // 强制 from / to 视为 ground layer
                var fromG = new GridCoord(from.X, from.Y, DimensionLayer.Reality);
                var toG = new GridCoord(to.X, to.Y, DimensionLayer.Reality);
                // 高度查询短路为 0
                IHeightLookup groundHeights = new ZeroHeightLookup();
                // 阻挡查询仅看 ground tile
                IBlockingLookup groundBlocking = new LayerHeightBlockingFilter(blocking, DimensionLayer.Reality, 0);
                return ComputeDirectInternal(size, fromG, toG, groundHeights, covers, groundBlocking);
            }

            // CrossPhase：弹道跨相位。分两段：
            //   Leg 1：在 attacker.Layer 走 (from → (to.X, to.Y, from.Layer))
            //   Leg 2：在 defender.Layer 走 ((from.X, from.Y, to.Layer) → to)
            // 表示弹道从 attacker 处跨相位进入 defender 所在维度。
            if (projectile == ProjectileType.CrossPhase)
            {
                // Leg 1：attacker.Layer
                var leg1End = new GridCoord(to.X, to.Y, from.Layer);
                var r1 = ComputeDirectInternal(size, from, leg1End, heights, covers, blocking);
                if (!r1.HasLineOfSight) return r1;

                // Leg 2：defender.Layer
                var leg2Start = new GridCoord(from.X, from.Y, to.Layer);
                var r2 = ComputeDirectInternal(size, leg2Start, to, heights, covers, blocking);
                if (!r2.HasLineOfSight) return r2;

                // CrossPhase 跨层 → 不算 high ground；惩罚取 defender tile 的掩体（Leg 2）。
                return new Result(
                    hasLineOfSight: true,
                    hasHighGroundBonus: false,
                    coverPenalty: r2.CoverPenalty,
                    blockingTiles: Array.Empty<GridCoord>());
            }

            // Direct / Arc / Beam / Chain：先按 Direct 计算，Arc 忽略 Half
            Result direct = ComputeDirectInternal(size, from, to, heights, covers, blocking);

            if (projectile == ProjectileType.Arc)
            {
                if (!direct.HasLineOfSight) return direct;
                // Half 忽略 → CoverPenalty = 0
                return new Result(true, direct.HasHighGroundBonus, 0, Array.Empty<GridCoord>());
            }

            // Direct / Beam / Chain：直接返回
            return direct;
        }

        // ──────────── 内部：Supercover 路径 ────────────

        /// <summary>
        /// Supercover 整数射线：从 (x0, y0) 到 (x1, y1) 枚举所有经过的格子，
        /// 含起点 + 终点。Layer 不参与，调用方需自行保证同 Layer。
        /// </summary>
        /// <remarks>
        /// **算法**（Bresenham 变体，无浮点）：
        /// <code>
        /// dx = |x1 - x0|;  dy = |y1 - y0|
        /// sx = x0 &lt; x1 ? +1 : -1;  sy = y0 &lt; y1 ? +1 : -1
        /// err = dx - dy
        /// loop:
        ///   emit (x, y)
        ///   if (x, y) == (x1, y1) break
        ///   e2 = 2 * err
        ///   if e2 &gt; -dy: err -= dy; x += sx
        ///   if e2 &lt;  dx: err += dx; y += sy
        /// </code>
        /// 这是经典的"Amanatides &amp; Woo"网格遍历；
        /// 输出确定、无随机、无分支依赖输入顺序。
        /// </remarks>
        public static List<GridCoord> TraceSupercoverPath(GridCoord from, GridCoord to)
        {
            return TraceSupercoverPath(from.X, from.Y, to.X, to.Y, from.Layer);
        }

        /// <summary>Layer 显式参数版本（跨 Layer 路径分析时使用）。</summary>
        public static List<GridCoord> TraceSupercoverPath(int x0, int y0, int x1, int y1, DimensionLayer layer)
        {
            var path = new List<GridCoord>();

            int x = x0;
            int y = y0;
            int dx = x1 - x0;
            int dy = y1 - y0;

            // 距离的绝对值（避免 Abs on int.MinValue）
            int absDx = dx < 0 ? -dx : dx;
            int absDy = dy < 0 ? -dy : dy;

            int sx = dx >= 0 ? 1 : -1;
            int sy = dy >= 0 ? 1 : -1;

            int err = absDx - absDy;

            // 单次 loop 包含 emit + advance。
            // 步数上限 = max(|dx|, |dy|) + 1（Supercover 主轴 + 副轴同时推进时的格子）。
            // 用 while(true) 由终点判断 break。
            while (true)
            {
                path.Add(new GridCoord(x, y, layer));
                if (x == x1 && y == y1) break;

                int e2 = 2 * err;
                if (e2 > -absDy)
                {
                    err -= absDy;
                    x += sx;
                }
                if (e2 < absDx)
                {
                    err += absDx;
                    y += sy;
                }
            }

            return path;
        }

        // ──────────── 内部：辅助 lookup 适配器 ────────────

        /// <summary>固定返回 0 的高度查询（用于 GroundPropagation 强制 ground）。</summary>
        private sealed class ZeroHeightLookup : IHeightLookup
        {
            public int GetHeight(GridCoord coord) => 0;
        }

        /// <summary>指定 Layer + 指定 Height 才查询原 blocking；其他坐标一律 false。
        /// 用于 GroundPropagation 强制只关心 Reality / 0 高度的阻挡。</summary>
        private sealed class LayerHeightBlockingFilter : IBlockingLookup
        {
            private readonly IBlockingLookup _inner;
            private readonly DimensionLayer _layer;
            private readonly int _height;

            public LayerHeightBlockingFilter(IBlockingLookup inner, DimensionLayer layer, int height)
            {
                _inner = inner;
                _layer = layer;
                _height = height;
            }

            public bool BlocksLineOfSight(GridCoord coord)
            {
                if (coord.Layer != _layer) return false;
                // 高度过滤：仅看 ground 层（height = 0）；其他高度视为不影响 ground 视线
                if (_height != 0) return false;
                return _inner != null && _inner.BlocksLineOfSight(coord);
            }
        }
    }
}
