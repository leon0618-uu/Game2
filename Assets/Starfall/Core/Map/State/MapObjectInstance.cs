using System;
using Starfall.Core.Map.Coordinates;

namespace Starfall.Core.Map.State
{
    /// <summary>
    /// doc2 MAP-02 占位类型 + MAP-10 对象雏形。
    ///
    /// <para/>
    /// MAP-02 仅要求 MapState 持有 <c>Objects</c> 集合并能按 ObjectId 排序；完整 12 类对象 /
    /// 状态机 / Footprint 在 MAP-10 实现。这里给出最小字段集，使集合克隆 / 哈希 / 序列化
    /// 路径在 MAP-02 阶段即可对齐 doc2 §3.4 验收标准。
    ///
    /// <para/>
    /// 字段语义：
    /// <list type="bullet">
    /// <item><c>ObjectId</c>：唯一 ID（>=0），用于确定性排序与哈希。</item>
    /// <item><c>ObjectType</c>：doc2 §10.1 12 类对象之一（字符串占位；MAP-10 引入枚举）。</item>
    /// <item><c>Anchor</c>：对象占据的格子（多格对象在 MAP-10 引入 Footprint，本阶段单格）。</item>
    /// </list>
    /// </summary>
    public sealed class MapObjectInstance
    {
        public int ObjectId { get; }
        public string ObjectType { get; }
        public GridCoord Anchor { get; }

        public MapObjectInstance(int objectId, string objectType, GridCoord anchor)
        {
            if (objectId < 0)
                throw new ArgumentException("ObjectId must be >= 0", nameof(objectId));
            if (objectType == null)
                throw new ArgumentNullException(nameof(objectType));

            ObjectId = objectId;
            ObjectType = objectType;
            Anchor = anchor;
        }

        public override string ToString()
            => $"MapObjectInstance(Id={ObjectId}, Type={ObjectType}, Anchor={Anchor})";
    }
}
