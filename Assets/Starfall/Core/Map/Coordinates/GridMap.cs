using System;
using System.Collections.Generic;

namespace Starfall.Core.Map.Coordinates
{
    /// <summary>
    /// 双层网格容器（doc2 MAP-01 §4.4）。
    ///
    /// 内部使用 <see cref="Dictionary{TKey, TValue}"/> 按 <see cref="GridCoord"/> 索引 T；
    /// 不预分配整张表（48×64×2 = 6144 格的最大预分配对 MVP / 测试都偏重），
    /// 仅记录 MapSize 用于越界检查。
    ///
    /// 确定性遍历：所有返回 IEnumerable 的方法都按 GridCoord.CompareTo 排序后输出，
    /// 避免依赖 Dictionary 的内部哈希顺序；这是 AGENTS.md §11 强制要求。
    ///
    /// 与 MVP 的 <see cref="Starfall.Core.Model.BoardState"/>（嵌入 BattleState）共存，
    /// 不破坏 179 既有测试。
    /// </summary>
    public sealed class GridMap<T>
    {
        private readonly Dictionary<GridCoord, T> _data;

        public MapSize Size { get; }

        /// <summary>已设置的非空格子数。</summary>
        public int Count => _data.Count;

        public GridMap(MapSize size)
        {
            Size = size;
            _data = new Dictionary<GridCoord, T>();
        }

        /// <summary>拷贝构造函数（用于 DeepClone）。</summary>
        private GridMap(MapSize size, Dictionary<GridCoord, T> data)
        {
            Size = size;
            // 用 EqualityComparer.Default 但因为 GridCoord 已实现 IEquatable 会走其重载。
            _data = new Dictionary<GridCoord, T>(data);
        }

        // ──────────── 索引 / 越界 ────────────

        private static void CheckBounds(GridCoord c, MapSize size)
        {
            if (!c.IsInBounds(size))
                throw new ArgumentOutOfRangeException(
                    nameof(c), c,
                    $"GridCoord ({c.X}, {c.Y}) is out of bounds for map {size}.");
        }

        public bool Contains(GridCoord c) => _data.ContainsKey(c);

        public T this[GridCoord c]
        {
            get
            {
                CheckBounds(c, Size);
                return _data[c];
            }
            set
            {
                CheckBounds(c, Size);
                _data[c] = value;
            }
        }

        public void Set(GridCoord c, T value)
        {
            CheckBounds(c, Size);
            _data[c] = value;
        }

        public bool TryGet(GridCoord c, out T value)
        {
            return _data.TryGetValue(c, out value);
        }

        // ──────────── 确定性遍历 ────────────

        /// <summary>
        /// 所有已设置的坐标，按 GridCoord.CompareTo 升序输出。
        /// 等价于“按 Y → X → Layer 排序的全部格点子集”。
        /// </summary>
        public IEnumerable<GridCoord> AllCoords()
        {
            // 先拷一份键列表再排序，避免直接对 Dictionary 迭代时修改它。
            // 此处不预分配 ArrayList，避免引入 System.Collections 之外的依赖。
            var keys = new List<GridCoord>(_data.Keys);
            keys.Sort();
            return keys;
        }

        /// <summary>
        /// 所有 (coord, value) 对，按 GridCoord.CompareTo 升序输出。
        /// 用于序列化、确定性哈希、Anchor / Decree 注册顺序。
        /// </summary>
        public IEnumerable<KeyValuePair<GridCoord, T>> AllEntries()
        {
            var keys = new List<GridCoord>(_data.Keys);
            keys.Sort();
            foreach (var k in keys)
                yield return new KeyValuePair<GridCoord, T>(k, _data[k]);
        }

        // ──────────── 复制 / 清空 ────────────

        /// <summary>
        /// 深拷贝容器本身 + 内部数据字典。
        /// 如果 <typeparamref name="T"/> 是引用类型，元素本身不会被克隆——
        /// 调用方需自行决定是否深克隆元素（与 <see cref="Starfall.Core.Model.Cloner"/> 一致）。
        /// </summary>
        public GridMap<T> DeepClone() => new GridMap<T>(Size, _data);

        public void Clear() => _data.Clear();

        // ──────────── 诊断 ────────────

        public override string ToString() => $"GridMap<{typeof(T).Name}>({Size}, count={Count})";
    }
}