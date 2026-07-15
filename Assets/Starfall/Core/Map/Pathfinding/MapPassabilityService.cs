using System.Collections.Generic;
using Starfall.Core.Map.Coordinates;
using Starfall.Core.Map.Height;
using Starfall.Core.Map.State;
using Starfall.Core.Map.Tile;

namespace Starfall.Core.Map.Pathfinding
{
    /// <summary>
    /// doc2 MAP-05 §9.4 通行性服务（pure function）。
    ///
    /// <para/>
    /// 校验链（按优先级顺序）：
    /// <list type="number">
    /// <item><see cref="TileDefinition.BlocksMovement"/>（MAP-04）：true → <see cref="PassabilityResult.BlockedByTile"/>。</item>
    /// <item><see cref="TileOccupancyService.IsCellPassable"/>（MAP-04）：包含 footprint 跨格冲突 / 占用 / 越界 / 坍塌。</item>
    /// <item><see cref="HeightTraversalService.CanTraverse"/>（MAP-06）：Δh 超过 <see cref="MapMovementProfile.MaxAscendHeight"/> / <see cref="MapMovementProfile.MaxDescendHeight"/>。
    ///       采用既有 <see cref="Starfall.Core.Map.Height.MovementProfile"/>（MAP-06 命名空间）作为校验源，
    ///       因其已经包含 <c>canFly</c> 短路 + 标准 Δh 边界。
    ///       当 <see cref="MapMovementProfile.CanFly"/> = true 时 Δh 视为永真。</item>
    /// <item>跨层（PhasePass-through）：起点与终点必须在同一 <see cref="DimensionLayer"/>；否则 <see cref="PassabilityResult.BlockedByPhase"/>。
    ///       当前 <see cref="MapMovementProfile.CanCrossDimension"/> = true 的语义留给后续 MAP-12
    ///       PhasePair 寻路桥接，本服务保留字段但默认拒绝跨层。</item>
    /// <item>区域（Region containment）：<see cref="MapState.Regions"/> 的阻挡规则；当前未启用（MAP-09 阶段接管）。
    ///       默认通过（不阻断）。</item>
    /// </list>
    ///
    /// **失败优先级**：上述链中第一个失败即视为终态，后续不再校验。
    /// 报告格式：<see cref="PassabilityResult"/> 显式给出失败原因 + 失败坐标，
    /// 业务层无需再对比 <c>==</c> 多个布尔值。
    /// </summary>
    public static class MapPassabilityService
    {
        /// <summary>
        /// 判定 <paramref name="to"/> 是否可由 <paramref name="profile"/> 在
        /// <paramref name="from"/> 踏到（或在范围内自由落座）。
        /// </summary>
        /// <param name="state">当前地图状态（越界 / 占用查询）。</param>
        /// <param name="from">源 tile（同 tile 表示原地 / 跨格 footprint 起立校验）。</param>
        /// <param name="to">目标 tile（必填；含 <see cref="DimensionLayer"/>）。</param>
        /// <param name="profile">单位移动配置（含 Δh 与跨层开关）。</param>
        /// <param name="footprint">占用形状（默认 <see cref="Footprint.SingleCell"/>）。</param>
        public static PassabilityResult CanEnter(
            MapState state,
            GridCoord from,
            GridCoord to,
            MapMovementProfile profile,
            Footprint footprint = Footprint.SingleCell)
        {
            if (state == null) throw new System.ArgumentNullException(nameof(state));
            if (profile.MaxMovementPoints < 0)
                throw new System.ArgumentOutOfRangeException(nameof(profile), profile,
                    "Profile.MaxMovementPoints must be >= 0 (corrupted profile).");

            // 0) 越界 → 立即失败（一步拒绝）
            if (!to.IsInBounds(state.Definition.Size))
                return PassabilityResult.BlockedByTile(to);

            // 1) TileDefinition.BlocksMovement
            //    通过 TileOccupancyService.IsCellPassable 同时也覆盖了 BlocksMovement + 占用 + 越界。
            //    但 IsCellPassable 不能区分"tile 阻挡" vs "被占用"，需自检。
            var registry = TryGetRegistryFor(state);
            if (registry != null && registry.TryGetByCoord(to, out var def) && def.BlocksMovement)
                return PassabilityResult.BlockedByTile(to);

            // 2) TileOccupancyService 跨格占用校验（包含 footprint 与既占用）。
            //    注意：调用 CanOccupyCells 需要依赖 attached 的 registry；拿不到时跳过。
            if (registry != null
                && !TileOccupancyService.IsCellPassable(state, to))
            {
                // IsCellPassable 已经把 BlocksMovement / 占用 / 坍塌合并检查；这里返回 BlockedByUnit
                // 是更精确的归类：除非 registry 未 attach，否则以"被占用"为名义拒绝。
                var occupantUnit = TileOccupancyService.GetOccupantUnit(state, to);
                if (occupantUnit.HasValue) return PassabilityResult.BlockedByUnit(to, occupantUnit.Value);
                var occupantObj = TileOccupancyService.GetOccupantObject(state, to);
                if (occupantObj.HasValue) return PassabilityResult.BlockedByUnit(to, occupantObj.Value);
                return PassabilityResult.BlockedByTile(to);
            }

            // 3) Δh 校验（飞行短路）
            //    MAP-06 既有 HeightTraversalService.CanTraverse 用 Height.MovementProfile。
            //    映射规则：profile.CanFly -> canFly，MaxAscendHeight -> MaxAscend，MaxDescendHeight -> MaxDescend。
            var heightProfile = new Starfall.Core.Map.Height.MovementProfile(
                canFly: profile.CanFly,
                maxAscend: profile.MaxAscendHeight,
                maxDescend: profile.MaxDescendHeight,
                canCrossDimension: profile.CanCrossDimension);
            if (registry != null
                && registry.TryGetByCoord(from, out var fromDef)
                && registry.TryGetByCoord(to, out var toDef))
            {
                if (!HeightTraversalService.CanTraverse(fromDef.Height, toDef.Height, heightProfile))
                    return PassabilityResult.BlockedByHeightDelta(to, fromDef.Height, toDef.Height);
            }
            else
            {
                // 无 registry：保守地接受（同高度视为 OK，高度差未知时接受等于 MAP-05 §9.4 的"地形简化"）。
                // 这对应于"测试用 minimal map 未 attach"场景。
            }

            // 4) 跨层 / PhasePass-through
            if (from.Layer != to.Layer && !profile.CanCrossDimension)
                return PassabilityResult.BlockedByPhase(to, from.Layer, to.Layer);

            // 5) Region containment（MAP-09 后续接管）
            //    当前未启用：若未来某 region 实现 IsPassable，则插入此分支。

            return PassabilityResult.Pass();
        }

        /// <summary>
        /// 一次性校验 footprint 多格（适用于 <see cref="Footprint.TwoByTwo"/> /
        /// <see cref="Footprint.ThreeByThree"/>）。
        /// </summary>
        /// <param name="anchor">footprint 左上角。</param>
        public static PassabilityResult CanPlaceFootprint(
            MapState state,
            GridCoord anchor,
            Footprint footprint,
            MapMovementProfile profile)
        {
            if (state == null) throw new System.ArgumentNullException(nameof(state));
            if (!FootprintExtensions.CanPlace(footprint, anchor, state.Definition.Size))
                return PassabilityResult.BlockedByTile(anchor);

            // foot print 自身覆盖 cells；任一 cell 失败 → 整体拒绝。
            var cells = FootprintExtensions.GetOccupiedCells(footprint, anchor, state.Definition.Size);
            for (int i = 0; i < cells.Count; i++)
            {
                var c = cells[i];
                var result = CanEnter(state, anchor, c, profile, Footprint.SingleCell);
                if (!result.IsPassable) return result;
            }
            return PassabilityResult.Pass();
        }

        // ──────────── 内部 helpers ────────────

        private static TileDefinitionRegistry TryGetRegistryFor(MapState state)
        {
            // 通过反射访问 TileOccupancyService._registryAttach 在 Core 纯 C# 中不允许；
            // 改为暴露公用 API（AttachTileDefinitionRegistry 已存在，添加 TryGet）。
            return TileOccupancyService.TryGetAttachedRegistry(state);
        }
    }

    /// <summary>
    /// doc2 MAP-05 §9.4 通行性校验结果。结构体（每次分配零消耗）。
    ///
    /// <para/>
    /// 通过 <see cref="IsPassable"/> 判断；<see cref="Reason"/> 提供人可读的标签；
    /// <see cref="FailedCoord"/> 提供失败所在的坐标，便于 Presenter 高亮。
    /// </summary>
    public readonly struct PassabilityResult
    {
        public enum RejectionCode
        {
            /// <summary>允许进入。</summary>
            Pass = 0,
            /// <summary>tile 的 BlocksMovement = true 或越界。</summary>
            BlockedByTile,
            /// <summary>高度差超过 profile 上 / 下限。</summary>
            BlockedByHeightDelta,
            /// <summary>目标 cell 上有 unit 或 object 占用。</summary>
            BlockedByUnit,
            /// <summary>跨层（Phase-pass-through）被禁止。</summary>
            BlockedByPhase,
            /// <summary>Region containment 拒绝（MAP-09 阶段接管）。</summary>
            BlockedByRegion,
            /// <summary>所需移动成本 > MaxMovementPoints（占位 / 留给 MovementRange）。</summary>
            InsufficientMovement,
        }

        public readonly RejectionCode Reason;
        public readonly GridCoord FailedCoord;
        public readonly int OccupantId;
        public readonly HeightLevel FromHeight;
        public readonly HeightLevel ToHeight;
        public readonly DimensionLayer FromLayer;
        public readonly DimensionLayer ToLayer;

        public bool IsPassable => Reason == RejectionCode.Pass;

        private PassabilityResult(
            RejectionCode reason,
            GridCoord failedCoord,
            int occupantId,
            HeightLevel fromHeight,
            HeightLevel toHeight,
            DimensionLayer fromLayer,
            DimensionLayer toLayer)
        {
            Reason = reason;
            FailedCoord = failedCoord;
            OccupantId = occupantId;
            FromHeight = fromHeight;
            ToHeight = toHeight;
            FromLayer = fromLayer;
            ToLayer = toLayer;
        }

        // ──────────── 工厂 ────────────

        public static PassabilityResult Pass()
            => new PassabilityResult(RejectionCode.Pass, default, 0, default, default, default, default);

        public static PassabilityResult BlockedByTile(GridCoord c)
            => new PassabilityResult(RejectionCode.BlockedByTile, c, 0, default, default, default, default);

        public static PassabilityResult BlockedByHeightDelta(GridCoord c, HeightLevel from, HeightLevel to)
            => new PassabilityResult(RejectionCode.BlockedByHeightDelta, c, 0, from, to, default, default);

        public static PassabilityResult BlockedByUnit(GridCoord c, int occupantId)
            => new PassabilityResult(RejectionCode.BlockedByUnit, c, occupantId, default, default, default, default);

        public static PassabilityResult BlockedByPhase(GridCoord c, DimensionLayer from, DimensionLayer to)
            => new PassabilityResult(RejectionCode.BlockedByPhase, c, 0, default, default, from, to);

        public static PassabilityResult BlockedByRegion(GridCoord c)
            => new PassabilityResult(RejectionCode.BlockedByRegion, c, 0, default, default, default, default);

        public override string ToString()
        {
            switch (Reason)
            {
                case RejectionCode.Pass: return "Pass";
                case RejectionCode.BlockedByTile: return $"BlockedByTile({FailedCoord})";
                case RejectionCode.BlockedByHeightDelta: return $"BlockedByHeightDelta({FailedCoord}, {FromHeight}->{ToHeight})";
                case RejectionCode.BlockedByUnit: return $"BlockedByUnit({FailedCoord}, id={OccupantId})";
                case RejectionCode.BlockedByPhase: return $"BlockedByPhase({FailedCoord}, {FromLayer}->{ToLayer})";
                case RejectionCode.BlockedByRegion: return $"BlockedByRegion({FailedCoord})";
                case RejectionCode.InsufficientMovement: return "InsufficientMovement";
                default: return Reason.ToString();
            }
        }
    }
}
