using System.Collections.Generic;

namespace Starfall.Core.Decree
{
    /// <summary>
    /// 全局律令注册表。DecreeId 升序迭代（确定性）。
    /// </summary>
    public sealed class DecreeRegistry
    {
        private readonly Dictionary<int, Decree> _decrees = new Dictionary<int, Decree>();
        public IReadOnlyList<Decree> DecreesInOrder
        {
            get
            {
                var list = new List<Decree>(_decrees.Values);
                list.Sort((a, b) => a.DecreeId.CompareTo(b.DecreeId));
                return list;
            }
        }

        public void Issue(Decree d)
        {
            if (d == null) throw new System.ArgumentNullException(nameof(d));
            _decrees[d.DecreeId] = d;
        }

        public bool Revoke(int decreeId) => _decrees.Remove(decreeId);
    }
}
