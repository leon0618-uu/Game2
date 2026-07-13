namespace Starfall.Core.Model
{
    public static class BattleStateComparer
    {
        public static bool Equals(BattleState a, BattleState b)
        {
            if (ReferenceEquals(a, b)) return true;
            if (a == null || b == null) return false;
            if (a.PostStateHash != b.PostStateHash) return false;
            if (a.TurnNumber != b.TurnNumber) return false;
            if (a.ActivePlayer != b.ActivePlayer) return false;
            // Task 19 关卡阶段字段：Hash 已经参与混合；此处显式比对以便 Equals 在 Hash 巧合相同时仍正确
            if (a.CurrentPhase != b.CurrentPhase) return false;
            if (a.GuardsCompleted != b.GuardsCompleted) return false;
            if (a.GuardsRequired != b.GuardsRequired) return false;
            if (a.ExitTile.HasValue != b.ExitTile.HasValue) return false;
            if (a.ExitTile.HasValue)
            {
                if (a.ExitTile.Value.X != b.ExitTile.Value.X) return false;
                if (a.ExitTile.Value.Y != b.ExitTile.Value.Y) return false;
            }
            if (a.Board.Width != b.Board.Width) return false;
            if (a.Board.Height != b.Board.Height) return false;
            if (a.Units.Count != b.Units.Count) return false;

            // 单元按 (UnitId) 升序比对（不需要重新排序，因为我们按相同 hash 链）
            var aUnits = new System.Collections.Generic.List<UnitState>(a.Units);
            var bUnits = new System.Collections.Generic.List<UnitState>(b.Units);
            aUnits.Sort((x, y) => x.UnitId.CompareTo(y.UnitId));
            bUnits.Sort((x, y) => x.UnitId.CompareTo(y.UnitId));
            for (int i = 0; i < aUnits.Count; i++)
            {
                var ua = aUnits[i];
                var ub = bUnits[i];
                if (ua.UnitId != ub.UnitId) return false;
                if (ua.Pos.X != ub.Pos.X || ua.Pos.Y != ub.Pos.Y) return false;
                if (ua.Hp != ub.Hp || ua.MaxHp != ub.MaxHp) return false;
                if (ua.Phase != ub.Phase) return false;
                if (ua.Owner != ub.Owner) return false;
            }

            // 瓦片按 (Y, X) 升序比对
            foreach (var kv in a.Board.TilesInDeterministicOrder())
            {
                if (!b.Board.Tiles.TryGetValue(kv.Key, out var bs)) return false;
                if (bs != kv.Value) return false;
            }
            return true;
        }

        public static int GetHashCode(BattleState s)
        {
            if (s == null) return 0;
            return (int)(s.PostStateHash ^ (s.PostStateHash >> 32));
        }
    }
}
