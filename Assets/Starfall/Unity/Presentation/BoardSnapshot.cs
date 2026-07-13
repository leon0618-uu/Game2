using System.Collections.Generic;
using Starfall.Core.Model;
using Starfall.Unity.Input;

namespace Starfall.Unity.Presentation
{
    /// <summary>给 Presenter 用的棋盘快照（按 (Y,X) 升序迭代）。</summary>
    /// <remarks>
    /// Task 16 扩展：加入 Units + Anchors（Task 16 之前仅含 Tiles）。
    /// Task 18 扩展：
    /// - Units 携带 PV/AP/CV 派生值（<see cref="UnitSnapshot"/> 6 参构造）；
    /// - <see cref="LegalMoves"/> / <see cref="AttackTargets"/> / <see cref="FallPreviews"/> 由
    ///   <see cref="LegalPreviewHelper"/> 计算，Presenter 仅渲染，不复制规则；
    /// - <see cref="InputModeHint"/> / <see cref="SelectedUnitIdForPreview"/> 由
    ///   BattleBootstrap 注入 InputState，保证 Presenter 渲染不依赖 InputController 引用。
    /// AGENTS.md §11 确定性：Units 按 UnitId 升序、Anchors 按 ZoneId 升序、预览集合按 (Y, X) 升序。
    /// </remarks>
    public readonly struct BoardSnapshot
    {
        public int Width { get; }
        public int Height { get; }
        public IReadOnlyList<TileSnapshot> Tiles { get; }
        public IReadOnlyList<UnitSnapshot> Units { get; }
        public IReadOnlyList<AnchorSnapshot> Anchors { get; }

        // ===== Task 18 预览 =====
        /// <summary>当前选中单位在 MoveTarget 模式下的合法落点（(Y, X) 升序）。</summary>
        public IReadOnlyList<GridPos> LegalMoves { get; }
        /// <summary>当前选中单位在 AttackTarget 模式下的可攻击目标（(Y, X) 升序）。</summary>
        public IReadOnlyList<AttackTarget> AttackTargets { get; }
        /// <summary>回合结束若不干预将坠落的格子（(Y, X) 升序）。</summary>
        public IReadOnlyList<FallPreview> FallPreviews { get; }
        /// <summary>当前 Input 模式（SelectUnit/MoveTarget/AttackTarget/...），用于高亮决策。</summary>
        public InputMode InputModeHint { get; }
        /// <summary>当前已选单位（用于绘制合法格 / 攻击目标 / 伤害数字）。</summary>
        public int? SelectedUnitIdForPreview { get; }
        /// <summary>光标所在格（用于在攻击模式下读取待预览伤害目标）。</summary>
        public GridPos? CursorForPreview { get; }

        public BoardSnapshot(int width, int height, IReadOnlyList<TileSnapshot> tiles)
            : this(width, height, tiles, System.Array.Empty<UnitSnapshot>(), System.Array.Empty<AnchorSnapshot>())
        {
        }

        public BoardSnapshot(
            int width,
            int height,
            IReadOnlyList<TileSnapshot> tiles,
            IReadOnlyList<UnitSnapshot> units,
            IReadOnlyList<AnchorSnapshot> anchors)
            : this(width, height, tiles, units, anchors,
                   System.Array.Empty<GridPos>(),
                   System.Array.Empty<AttackTarget>(),
                   System.Array.Empty<FallPreview>(),
                   InputMode.None, null, null)
        {
        }

        public BoardSnapshot(
            int width,
            int height,
            IReadOnlyList<TileSnapshot> tiles,
            IReadOnlyList<UnitSnapshot> units,
            IReadOnlyList<AnchorSnapshot> anchors,
            IReadOnlyList<GridPos> legalMoves,
            IReadOnlyList<AttackTarget> attackTargets,
            IReadOnlyList<FallPreview> fallPreviews,
            InputMode inputModeHint,
            int? selectedUnitIdForPreview,
            GridPos? cursorForPreview)
        {
            Width = width;
            Height = height;
            Tiles = tiles ?? System.Array.Empty<TileSnapshot>();
            Units = units ?? System.Array.Empty<UnitSnapshot>();
            Anchors = anchors ?? System.Array.Empty<AnchorSnapshot>();
            LegalMoves = legalMoves ?? System.Array.Empty<GridPos>();
            AttackTargets = attackTargets ?? System.Array.Empty<AttackTarget>();
            FallPreviews = fallPreviews ?? System.Array.Empty<FallPreview>();
            InputModeHint = inputModeHint;
            SelectedUnitIdForPreview = selectedUnitIdForPreview;
            CursorForPreview = cursorForPreview;
        }

        public static BoardSnapshot FromState(BattleState state)
        {
            // Tiles: 按 (Y, X) 升序（Board.TilesInDeterministicOrder）
            var tiles = new List<TileSnapshot>();
            foreach (var kv in state.Board.TilesInDeterministicOrder())
                tiles.Add(new TileSnapshot(kv.Key, kv.Value));

            // Units: 按 UnitId 升序，携带派生 PV/AP/CV
            var sortedUnits = new List<UnitState>(state.Units);
            sortedUnits.Sort((a, b) => a.UnitId.CompareTo(b.UnitId));
            var units = new List<UnitSnapshot>(sortedUnits.Count);
            foreach (var u in sortedUnits)
            {
                var d = LegalPreviewHelper.DeriveStats(u, state.Statuses);
                units.Add(new UnitSnapshot(u.UnitId, u.Pos, u.Hp, u.MaxHp, u.Phase, u.Owner, d.Pv, d.Ap, d.Cv));
            }

            // Anchors: 按 ZoneId 升序
            var anchors = new List<AnchorSnapshot>();
            foreach (var z in state.Anchors.ZonesInOrder)
                anchors.Add(new AnchorSnapshot(z.ZoneId, z.Owner, z.Vertices));

            return new BoardSnapshot(state.Board.Width, state.Board.Height, tiles, units, anchors);
        }

        /// <summary>
        /// Task 18 完整版：从 BattleState + 选中的单位 id 派生预览字段。
        /// </summary>
        public static BoardSnapshot FromStateWithPreview(
            BattleState state,
            int? selectedUnitId,
            InputMode inputMode,
            GridPos? cursor,
            int moveRadius = LegalPreviewHelper.DefaultMoveRadius)
        {
            // 基础字段（复用 FromState）
            var baseSnap = FromState(state);

            // 选中的单位：null → 不计算
            IReadOnlyList<GridPos> legal = System.Array.Empty<GridPos>();
            IReadOnlyList<AttackTarget> attacks = System.Array.Empty<AttackTarget>();
            if (selectedUnitId.HasValue && inputMode == InputMode.MoveTarget)
            {
                var sel = FindUnit(state, selectedUnitId.Value);
                if (sel != null)
                {
                    var occupied = new List<GridPos>(state.Units.Count);
                    foreach (var u in state.Units) if (u.UnitId != sel.UnitId) occupied.Add(u.Pos);
                    legal = LegalPreviewHelper.Reachable(state.Board, sel.Pos, occupied, moveRadius);
                }
            }
            else if (selectedUnitId.HasValue && inputMode == InputMode.AttackTarget)
            {
                attacks = LegalPreviewHelper.AdjacentEnemies(state, selectedUnitId.Value);
            }

            // 坠落预览：取首个己方单位的 phase 作为 active phase（若无己方单位则取 Light）
            Phase activePhase = Phase.Light;
            foreach (var u in state.Units)
            {
                if (u.Owner == state.ActivePlayer) { activePhase = u.Phase; break; }
            }
            var falls = LegalPreviewHelper.FallTargets(state, activePhase);

            return new BoardSnapshot(
                baseSnap.Width, baseSnap.Height, baseSnap.Tiles, baseSnap.Units, baseSnap.Anchors,
                legal, attacks, falls, inputMode, selectedUnitId, cursor);
        }

        private static UnitState FindUnit(BattleState s, int id)
        {
            for (int i = 0; i < s.Units.Count; i++)
                if (s.Units[i].UnitId == id) return s.Units[i];
            return null;
        }
    }
}
