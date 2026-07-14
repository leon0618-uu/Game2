using Starfall.Core.Map.Coordinates;

namespace Starfall.Core.Map.LineOfSight
{
    /// <summary>
    /// doc2 MAP-06 高度查询接口（数据层解耦）。
    ///
    /// <para/>
    /// <see cref="LineOfSightService"/> 与 <see cref="Cover.CoverQueryService"/>
    /// 不直接依赖 <see cref="State.MapState"/> 的内部结构；
    /// 任何能按 <see cref="GridCoord"/> 返回高度的 provider 都可作为输入。
    ///
    /// <para/>
    /// **本轮实现**：调用方构造一个简单的 <c>Dictionary&lt;GridCoord, int&gt;</c>
    /// 适配器；后续 MAP-04 引入 <c>TileDef.Height</c> 字段后，由
    /// <see cref="Data.MapDataLoader"/> 提供正式实现。
    ///
    /// <para/>
    /// **设计原则**：返回 <see cref="int"/> 而非 <see cref="Height.HeightLevel"/>，
    /// 避免 LOS 服务引用 Height 子模块的反向耦合；调用方负责把
    /// <c>int</c> 装箱为 <c>HeightLevel</c>。
    /// </summary>
    public interface IHeightLookup
    {
        /// <summary>取指定坐标的高度值（0..4）；越界返回 0（地面层）。</summary>
        int GetHeight(GridCoord coord);
    }

    /// <summary>
    /// doc2 MAP-06 掩体查询接口（数据层解耦）。
    ///
    /// <para/>
    /// 返回 <c>null</c> 表示"该 tile 无掩体信息"（视为
    /// <see cref="Cover.CoverLevel.None"/>）。这是与 <c>TileDef.Cover</c>
    /// 字段尚未实现的对接方式：JSON 没填 cover 字段就退化为 None。
    /// </summary>
    public interface ICoverLookup
    {
        /// <summary>取指定坐标的掩体等级；null = 无掩体信息。</summary>
        Cover.CoverLevel? GetCover(GridCoord coord);
    }

    /// <summary>
    /// doc2 MAP-06 视线阻挡查询接口（数据层解耦）。
    ///
    /// <para/>
    /// <c>true</c> = 该 tile 完全阻挡视线（首格阻挡即停）。
    /// 这是 <c>TileDef.BlocksLineOfSight</c> 字段尚未实现时的最小代理；
    /// 后续由 Data 层构造 <c>Dictionary&lt;GridCoord, bool&gt;</c> 适配器。
    /// </summary>
    public interface IBlockingLookup
    {
        bool BlocksLineOfSight(GridCoord coord);
    }
}
