using System.Collections.Generic;
using Starfall.Core.Anchor;

namespace Starfall.Core.Map.Anchor
{
    /// <summary>
    /// doc2 MAP-12 <see cref="AnchorLink"/> 深拷贝器。
    /// <para/>
    /// **复制策略**：
    /// <list type="bullet">
    /// <item><see cref="AnchorLink.Id"/>：按值复制（<see cref="AnchorLinkId"/> 是 readonly struct）。</item>
    /// <item><see cref="AnchorLink.Polygon"/>：构造新 <see cref="ConstellationPolygon"/>
    ///       （同 Id + 同 vertices 列表 — 由于 <see cref="ConstellationPolygon"/> 构造期
    ///       已规范化，新实例的 <see cref="ConstellationPolygon.Vertices"/> 与源独立）。</item>
    /// <item><see cref="AnchorLink.CurrentState"/> / <see cref="AnchorLink.StateTick"/> /
    ///       <see cref="AnchorLink.PostStateHash"/>：按值复制；通过
    ///       <see cref="AnchorLink.TransitionTo"/>（同状态自迁移总是合法）同步到克隆实例。</item>
    /// <item><see cref="AnchorLink.InitialState"/>：按值复制（构造期传入）。</item>
    /// </list>
    /// <para/>
    /// **集合深拷贝**（<see cref="DeepCloneAll"/>）：每个元素独立克隆；返回新 <see cref="List{T}"/>。
    /// <para/>
    /// **静态纯函数**：相同输入 → 相同输出，无副作用。
    /// </summary>
    public static class AnchorLinkCloner
    {
        /// <summary>深拷贝单个 <see cref="AnchorLink"/>；null → null。</summary>
        public static AnchorLink DeepClone(AnchorLink source)
        {
            if (source == null) return null;

            // 重建 Polygon：构造期已规范化顶点，故新实例与源独立。
            var sourceVerts = source.Polygon.Vertices;
            var newPolyVerts = new List<ConstellationVertex>(sourceVerts.Count);
            for (int i = 0; i < sourceVerts.Count; i++)
                newPolyVerts.Add(sourceVerts[i]);

            var newPoly = new ConstellationPolygon(source.Polygon.Id, newPolyVerts);

            // 1) 用 InitialState 构造（保证 InitialState 字段独立）
            var clone = new AnchorLink(
                source.Id,
                newPoly,
                initialState: source.InitialState,
                initialTick: source.StateTick,
                initialPostStateHash: source.PostStateHash);

            // 2) 同步 CurrentState（若已迁移）
            //    同状态自迁移总是合法（AnchorLinkStateMachine），故即使 InitialState == CurrentState
            //    也可以安全调用 TransitionTo(CurrentState, StateTick, PostStateHash)。
            if (clone.CurrentState != source.CurrentState
                || clone.StateTick != source.StateTick
                || clone.PostStateHash != source.PostStateHash)
            {
                clone.TransitionTo(source.CurrentState, source.StateTick, source.PostStateHash);
            }

            return clone;
        }

        /// <summary>深拷贝 <see cref="AnchorLink"/> 集合；null → null。</summary>
        public static List<AnchorLink> DeepCloneAll(IReadOnlyList<AnchorLink> source)
        {
            if (source == null) return null;
            var list = new List<AnchorLink>(source.Count);
            for (int i = 0; i < source.Count; i++)
                list.Add(DeepClone(source[i]));
            return list;
        }
    }
}