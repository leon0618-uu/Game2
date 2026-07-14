using System;
using System.Collections.Generic;
using Starfall.Core.Map.Coordinates;

namespace Starfall.Core.Map.Tile
{
    /// <summary>
    /// doc2 MAP-04 §4.3 单位 / 大型对象的占地形状枚举。
    ///
    /// <para/>
    /// **三种基本形状**：
    /// <list type="bullet">
    /// <item><see cref="SingleCell"/> = 1：标准单格单位（玩家步兵 / 多数敌人）。</item>
    /// <item><see cref="TwoByTwo"/> = 4：双格单位（小型机甲 / 重装兵）。</item>
    /// <item><see cref="ThreeByThree"/> = 9：三格单位（大型机甲 / Boss）。</item>
    /// </list>
    ///
    /// <para/>
    /// **锚点约定**：<see cref="FootprintExtensions.GetOccupiedCells"/> 的 <c>anchor</c>
    /// 表示"占地形状的左上角"（同 doc2 MAP-04 验收矩阵约定）。ThreeByThree 占用
    /// <c>(anchor.X, anchor.Y)</c> 至 <c>(anchor.X+2, anchor.Y+2)</c>，全部 9 格在同一
    /// <see cref="DimensionLayer"/>。
    ///
    /// <para/>
    /// **数值约定**：byte 值 = 实际占用格数（1 / 4 / 9），便于 <c>Footprint</c> 字段在
    /// 序列化时直接读取数值做预算与可视化渲染；禁止任意赋值非法 byte 值。
    /// </summary>
    public enum Footprint : byte
    {
        /// <summary>单格（1 格）。标准单位。</summary>
        SingleCell = 1,

        /// <summary>2×2 方阵（4 格）。小型机甲 / 重装。</summary>
        TwoByTwo = 4,

        /// <summary>3×3 方阵（9 格）。大型机甲 / Boss。</summary>
        ThreeByThree = 9,
    }

    /// <summary>
    /// <see cref="Footprint"/> 形状的查询 / 校验辅助方法。
    /// 提供格点枚举、越界检测与跨层检测；所有方法纯函数，无副作用。
    /// </summary>
    public static class FootprintExtensions
    {
        /// <summary>
        /// 按 anchor（占地形状的左上角）枚举该 <see cref="Footprint"/> 实际占据的格子。
        ///
        /// <para/>
        /// **顺序约定**（AGENTS.md §11）：从 (anchor.Y, anchor.X) 开始，先按 Y 升序再按 X 升序，
        /// 锚点自身排在最前。例：<see cref="Footprint.TwoByTwo"/> 在 anchor = (1, 1) 时
        /// 顺序为 (1,1) → (2,1) → (1,2) → (2,2)。
        ///
        /// <para/>
        /// **Layer 规则**：所有占据格共享 <c>anchor.Layer</c>；跨层视为非法，
        /// 调用方应在构造 <see cref="GridCoord"/> 时显式传入 <see cref="DimensionLayer"/>。
        ///
        /// <para/>
        /// **越界检查**：当任何占据格超出 <paramref name="size"/> 范围时抛
        /// <see cref="ArgumentOutOfRangeException"/>，便于单元测试在构造期捕获错误。
        /// </para>
        /// </summary>
        /// <param name="footprint">占地形状。</param>
        /// <param name="anchor">占地形状的左上角坐标。</param>
        /// <param name="size">地图尺寸（用于越界检查）。</param>
        /// <returns>占据格列表（稳定排序）；长度 = <see cref="Footprint"/> 数值。</returns>
        public static IReadOnlyList<GridCoord> GetOccupiedCells(
            Footprint footprint,
            GridCoord anchor,
            MapSize size)
        {
            int count = (int)footprint;
            int side = (int)Math.Round(Math.Sqrt(count));
            if (side <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(footprint), footprint,
                    $"Unknown Footprint value: {(byte)footprint}");
            }

            var result = new List<GridCoord>(count);
            for (int dy = 0; dy < side; dy++)
            {
                for (int dx = 0; dx < side; dx++)
                {
                    var cell = new GridCoord(anchor.X + dx, anchor.Y + dy, anchor.Layer);
                    if (!cell.IsInBounds(size))
                    {
                        throw new ArgumentOutOfRangeException(nameof(anchor), anchor,
                            $"Footprint {footprint} cell {cell} is out of bounds for map {size}.");
                    }
                    result.Add(cell);
                }
            }
            // 由于外层循环 dy → dx，list 已自然按 (Y, X) 升序，
            // 此处仍显式排序以避免 future refactor 破坏稳定顺序约定。
            result.Sort();
            return result;
        }

        /// <summary>
        /// 占地形状的边长（方阵边长）。
        /// <see cref="Footprint.SingleCell"/> = 1、<see cref="Footprint.TwoByTwo"/> = 2、
        /// <see cref="Footprint.ThreeByThree"/> = 3。
        /// </summary>
        public static int GetSideLength(Footprint footprint)
        {
            switch (footprint)
            {
                case Footprint.SingleCell: return 1;
                case Footprint.TwoByTwo: return 2;
                case Footprint.ThreeByThree: return 3;
                default:
                    throw new ArgumentOutOfRangeException(nameof(footprint), footprint,
                        $"Unknown Footprint value: {(byte)footprint}");
            }
        }

        /// <summary>
        /// 校验 anchor 是否能让 <paramref name="footprint"/> 完全落在
        /// <paramref name="size"/> 内（不越界）。这是 <see cref="GetOccupiedCells"/>
        /// 的"先检查后枚举"版本，供 <see cref="TileOccupancyService"/> 在批量放置时使用。
        /// </summary>
        public static bool CanPlace(Footprint footprint, GridCoord anchor, MapSize size)
        {
            int side = GetSideLength(footprint);
            // anchor 自身 + (side - 1) 增量都不能越界。
            // 即 anchor.X + (side - 1) < size.Width && anchor.Y + (side - 1) < size.Height。
            if (anchor.X < 0 || anchor.Y < 0) return false;
            if (anchor.X + side > size.Width) return false;
            if (anchor.Y + side > size.Height) return false;
            return true;
        }
    }
}