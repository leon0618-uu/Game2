using System.Collections.Generic;

namespace Starfall.Core.Status
{
    /// <summary>按 (StatusId, RemainingTurns, InstanceId) 升序（ADR-0001 §Decision 4）。</summary>
    public sealed class StatusInstanceComparer : IComparer<StatusInstance>
    {
        public static readonly StatusInstanceComparer Instance = new StatusInstanceComparer();
        public int Compare(StatusInstance a, StatusInstance b)
        {
            int c = ((int)a.Kind).CompareTo((int)b.Kind);
            if (c != 0) return c;
            c = a.RemainingTurns.CompareTo(b.RemainingTurns);
            if (c != 0) return c;
            return a.InstanceId.CompareTo(b.InstanceId);
        }
    }
}