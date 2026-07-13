using System.Collections.Generic;
using UnityEngine;
using Starfall.Core.Model;
using Starfall.Unity.Presentation;

namespace Starfall.Unity
{
    /// <summary>
    /// 真实棋盘表现（Task 16 + Task 18）：
    /// - 80 个 Tile（Quad）+ 单位（Capsule）+ 锚点多边形（LineRenderer）；
    /// - Tile 颜色随 TileState；
    /// - 单位颜色随 Phase + Owner；
    /// - Task 18 新增：合法落点 / 攻击目标 / 坠落预览 高亮层（半透明 Quad 叠加）；
    /// - Task 18 新增：3D TextMesh 伤害数字（攻击模式悬停时显示）；
    /// - 不持有 BattleState 引用（AGENTS.md §10.3 / ADR-0002 §3）；
    /// - Render 内部异常吞咽（ADR-0002 §4）。
    ///
    /// 视觉布局：
    /// - 棋盘以世界原点为中心；X 方向 8 格、Z 方向 10 格（Y 是高度）；
    /// - 每格边长 1；单位在瓦片中心；
    /// - 锚点多边形在格子平面上抬高 0.02 防 Z-fighting。
    /// </summary>
    public class RealBoardPresenter : MonoBehaviour, IBoardPresenter
    {
        [SerializeField] private float _tileSize = 1f;
        [SerializeField] private float _unitHeight = 0.6f;
        [SerializeField] private float _unitRadius = 0.32f;
        [SerializeField] private float _highlightY = 0.015f;   // 略高于 tile 防 Z-fighting
        [SerializeField] private float _damageY = 1.0f;        // 数字高度

        private Transform _tilesRoot;
        private Transform _unitsRoot;
        private Transform _anchorsRoot;
        private Transform _highlightsRoot;
        private Transform _damageRoot;

        private TileView[] _tileViews;       // index = y * Width + x
        private readonly Dictionary<int, UnitView> _unitViews = new Dictionary<int, UnitView>();
        private readonly Dictionary<int, LineRenderer> _anchorLines = new Dictionary<int, LineRenderer>();
        private readonly List<GameObject> _legalHighlightPool = new List<GameObject>();
        private readonly List<GameObject> _attackHighlightPool = new List<GameObject>();
        private readonly List<GameObject> _fallHighlightPool = new List<GameObject>();
        private TextMesh _damageNumberText;
        private GameObject _damageLabelGo;

        // 当前 board 尺寸（用于单位定位回调）
        private int _currentWidth;
        private int _currentHeight;

        private bool _initialized;

        public void Render(in BoardSnapshot snapshot, in IReadOnlyList<PresentationEvent> events)
        {
            try
            {
                EnsureLayout();
                DrawBoard(snapshot);
                DrawUnits(snapshot);
                DrawAnchors(snapshot);
                DrawHighlights(snapshot);
                DrawDamageNumber(snapshot);
            }
            catch (System.Exception ex)
            {
                // ADR-0002 §4：表现层异常吞咽，不影响 Core 结果。
                Debug.LogError($"[RealBoardPresenter] Render failed: {ex}");
            }
        }

        // ============== Layout ==============

        private void EnsureLayout()
        {
            if (_initialized) return;
            _initialized = true;

            _tilesRoot      = CreateChild("Tiles");
            _unitsRoot      = CreateChild("Units");
            _anchorsRoot    = CreateChild("Anchors");
            _highlightsRoot = CreateChild("Highlights");
            _damageRoot     = CreateChild("DamageLabels");
        }

        private Transform CreateChild(string name)
        {
            var go = new GameObject(name);
            go.transform.SetParent(transform, false);
            return go.transform;
        }

        // ============== Board ==============

        private void DrawBoard(in BoardSnapshot snapshot)
        {
            int width = snapshot.Width;
            int height = snapshot.Height;
            _currentWidth = width;
            _currentHeight = height;

            if (_tileViews == null || _tileViews.Length != width * height)
            {
                ClearChildren(_tilesRoot);
                _tileViews = new TileView[width * height];
                for (int y = 0; y < height; y++)
                {
                    for (int x = 0; x < width; x++)
                    {
                        int idx = y * width + x;
                        _tileViews[idx] = CreateTileView(x, y, width, height);
                    }
                }
            }

            // 同步 tile state 到 GameObject
            var tileStateMap = BuildTileStateMap(snapshot.Tiles, width, height);
            for (int y = 0; y < height; y++)
            {
                for (int x = 0; x < width; x++)
                {
                    int idx = y * width + x;
                    if (_tileViews[idx] == null) continue;
                    tileStateMap.TryGetValue(new GridPos(x, y), out TileState st);
                    _tileViews[idx].SetState(st);
                }
            }
        }

        private TileView CreateTileView(int x, int y, int width, int height)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Quad);
            go.name = $"Tile_{x}_{y}";
            var col = go.GetComponent<Collider>();
            if (col != null) Object.Destroy(col);
            go.transform.SetParent(_tilesRoot, false);
            go.transform.localPosition = GridToLocal(x, y, 0f, width, height);
            go.transform.localRotation = Quaternion.Euler(90f, 0f, 0f); // Quad 朝 +Z；旋为水平
            go.transform.localScale = Vector3.one * _tileSize * 0.98f;

            var view = go.AddComponent<TileView>();
            view.MeshRenderer = go.GetComponent<MeshRenderer>();
            view.MeshRenderer.sharedMaterial = GetOrCreateDefaultMaterial();
            return view;
        }

        private static Dictionary<GridPos, TileState> BuildTileStateMap(IReadOnlyList<TileSnapshot> tiles, int w, int h)
        {
            var map = new Dictionary<GridPos, TileState>(w * h);
            for (int y = 0; y < h; y++)
                for (int x = 0; x < w; x++)
                    map[new GridPos(x, y)] = TileState.Normal;
            if (tiles == null) return map;
            foreach (var t in tiles)
            {
                if (t.Pos.X >= 0 && t.Pos.X < w && t.Pos.Y >= 0 && t.Pos.Y < h)
                    map[t.Pos] = t.State;
            }
            return map;
        }

        // ============== Units ==============

        private void DrawUnits(in BoardSnapshot snapshot)
        {
            var present = new HashSet<int>();
            if (snapshot.Units != null)
            {
                foreach (var u in snapshot.Units)
                {
                    present.Add(u.UnitId);
                    if (!_unitViews.TryGetValue(u.UnitId, out var view))
                    {
                        view = CreateUnitView(u.UnitId);
                        _unitViews[u.UnitId] = view;
                    }
                    view.ApplySnapshot(u, _currentWidth, _currentHeight, _unitHeight * 0.5f);
                }
            }
            // 移除已不存在的单位
            if (_unitViews.Count != present.Count)
            {
                var toRemove = new List<int>();
                foreach (var kv in _unitViews)
                    if (!present.Contains(kv.Key)) toRemove.Add(kv.Key);
                foreach (var id in toRemove)
                {
                    if (_unitViews.TryGetValue(id, out var v)) v.Destroy();
                    _unitViews.Remove(id);
                }
            }
        }

        private UnitView CreateUnitView(int unitId)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Capsule);
            go.name = $"Unit_{unitId}";
            var col = go.GetComponent<Collider>();
            if (col != null) Object.Destroy(col);
            go.transform.SetParent(_unitsRoot, false);
            go.transform.localScale = new Vector3(_unitRadius * 2f, _unitHeight * 0.5f, _unitRadius * 2f);

            var view = go.AddComponent<UnitView>();
            view.MeshRenderer = go.GetComponent<MeshRenderer>();
            view.MeshRenderer.sharedMaterial = GetOrCreateDefaultMaterial();
            return view;
        }

        // ============== Anchors ==============

        private void DrawAnchors(in BoardSnapshot snapshot)
        {
            var present = new HashSet<int>();
            if (snapshot.Anchors != null)
            {
                foreach (var a in snapshot.Anchors)
                {
                    present.Add(a.ZoneId);
                    if (!_anchorLines.TryGetValue(a.ZoneId, out var lr))
                    {
                        lr = CreateAnchorLine(a.ZoneId, a.Owner);
                        _anchorLines[a.ZoneId] = lr;
                    }
                    UpdateAnchorLine(lr, a);
                }
            }
            if (_anchorLines.Count != present.Count)
            {
                var toRemove = new List<int>();
                foreach (var kv in _anchorLines)
                    if (!present.Contains(kv.Key)) toRemove.Add(kv.Key);
                foreach (var id in toRemove)
                {
                    if (_anchorLines.TryGetValue(id, out var lr))
                    {
                        if (lr != null) Object.Destroy(lr.gameObject);
                    }
                    _anchorLines.Remove(id);
                }
            }
        }

        private LineRenderer CreateAnchorLine(int zoneId, string owner)
        {
            var go = new GameObject($"Anchor_{zoneId}");
            go.transform.SetParent(_anchorsRoot, false);
            var lr = go.AddComponent<LineRenderer>();
            lr.useWorldSpace = false;
            lr.loop = true;
            lr.widthMultiplier = 0.06f;
            lr.material = GetOrCreateDefaultMaterial();
            lr.startColor = lr.endColor = BoardPalette.AnchorColor(owner);
            return lr;
        }

        private void UpdateAnchorLine(LineRenderer lr, in AnchorSnapshot a)
        {
            int n = a.Vertices?.Count ?? 0;
            if (n < 3)
            {
                lr.positionCount = 0;
                return;
            }
            lr.positionCount = n;
            for (int i = 0; i < n; i++)
            {
                var p = a.Vertices[i];
                lr.SetPosition(i, GridToLocal(p.X, p.Y, 0.02f, _currentWidth, _currentHeight));
            }
            lr.startColor = lr.endColor = BoardPalette.AnchorColor(a.Owner);
        }

        // ============== Task 18: Highlights ==============

        private void DrawHighlights(in BoardSnapshot snapshot)
        {
            int legalCount = snapshot.LegalMoves?.Count ?? 0;
            int attackCount = snapshot.AttackTargets?.Count ?? 0;
            int fallCount = snapshot.FallPreviews?.Count ?? 0;

            // 1. 合法落点
            EnsurePoolCount(_legalHighlightPool, legalCount, "LegalHL", BoardPalette.HighlightLegalMove);
            for (int i = 0; i < legalCount; i++)
            {
                var p = snapshot.LegalMoves[i];
                var go = _legalHighlightPool[i];
                go.transform.localPosition = GridToLocal(p.X, p.Y, _highlightY, _currentWidth, _currentHeight);
                go.SetActive(true);
            }
            // 2. 攻击目标
            EnsurePoolCount(_attackHighlightPool, attackCount, "AttackHL", BoardPalette.HighlightAttackTarget);
            for (int i = 0; i < attackCount; i++)
            {
                var p = snapshot.AttackTargets[i].Pos;
                var go = _attackHighlightPool[i];
                go.transform.localPosition = GridToLocal(p.X, p.Y, _highlightY, _currentWidth, _currentHeight);
                go.SetActive(true);
            }
            // 3. 坠落预览
            EnsurePoolCount(_fallHighlightPool, fallCount, "FallHL", BoardPalette.HighlightFallRisk);
            for (int i = 0; i < fallCount; i++)
            {
                var p = snapshot.FallPreviews[i].Pos;
                var go = _fallHighlightPool[i];
                go.transform.localPosition = GridToLocal(p.X, p.Y, _highlightY, _currentWidth, _currentHeight);
                go.SetActive(true);
            }
        }

        private void EnsurePoolCount(List<GameObject> pool, int desired, string baseName, Color color)
        {
            // 增长
            while (pool.Count < desired)
            {
                var go = GameObject.CreatePrimitive(PrimitiveType.Quad);
                go.name = $"{baseName}_{pool.Count}";
                var col = go.GetComponent<Collider>();
                if (col != null) Object.Destroy(col);
                go.transform.SetParent(_highlightsRoot, false);
                go.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
                go.transform.localScale = Vector3.one * _tileSize * 0.85f;
                var mr = go.GetComponent<MeshRenderer>();
                var mat = new Material(GetOrCreateHighlightShader());
                mat.color = color;
                mr.sharedMaterial = mat;
                pool.Add(go);
            }
            // 同步颜色 + 回收多余
            for (int i = 0; i < pool.Count; i++)
            {
                if (pool[i] == null) continue;
                if (i < desired)
                {
                    var mr = pool[i].GetComponent<MeshRenderer>();
                    if (mr != null && mr.sharedMaterial != null) mr.sharedMaterial.color = color;
                    pool[i].SetActive(true);
                }
                else
                {
                    pool[i].SetActive(false);
                }
            }
        }

        private static Shader _cachedHighlightShader;
        private static Shader GetOrCreateHighlightShader()
        {
            if (_cachedHighlightShader != null) return _cachedHighlightShader;
            _cachedHighlightShader = Shader.Find("Universal Render Pipeline/Unlit");
            if (_cachedHighlightShader == null) _cachedHighlightShader = Shader.Find("Unlit/Color");
            if (_cachedHighlightShader == null) _cachedHighlightShader = Shader.Find("Sprites/Default");
            if (_cachedHighlightShader == null) _cachedHighlightShader = Shader.Find("Standard");
            return _cachedHighlightShader;
        }

        // ============== Task 18: Damage number (3D world text) ==============

        private void DrawDamageNumber(in BoardSnapshot snapshot)
        {
            // 仅在 AttackTarget 模式 + 悬停攻击目标时显示数字
            bool visible = snapshot.InputModeHint == Starfall.Unity.Input.InputMode.AttackTarget
                           && snapshot.SelectedUnitIdForPreview.HasValue
                           && snapshot.CursorForPreview.HasValue
                           && snapshot.AttackTargets != null;
            int? targetId = null;
            GridPos cursor = default;
            if (visible)
            {
                cursor = snapshot.CursorForPreview.Value;
                for (int i = 0; i < snapshot.AttackTargets.Count; i++)
                {
                    if (snapshot.AttackTargets[i].Pos == cursor)
                    {
                        targetId = snapshot.AttackTargets[i].UnitId;
                        break;
                    }
                }
            }

            if (!visible || !targetId.HasValue)
            {
                if (_damageLabelGo != null) _damageLabelGo.SetActive(false);
                return;
            }

            // 渲染伤害数字：TextMesh 不需要 Canvas，兼容 batchmode / PlayMode
            if (_damageLabelGo == null)
            {
                _damageLabelGo = new GameObject("DamageLabel");
                _damageLabelGo.transform.SetParent(_damageRoot, false);
                _damageNumberText = _damageLabelGo.AddComponent<TextMesh>();
                _damageNumberText.fontSize = 64;
                _damageNumberText.characterSize = 0.4f;
                _damageNumberText.color = BoardPalette.DamagePreviewText;
                _damageNumberText.alignment = TextAlignment.Center;
                _damageNumberText.anchor = TextAnchor.MiddleCenter;
                _damageNumberText.fontStyle = FontStyle.Bold;
                var mr = _damageLabelGo.GetComponent<MeshRenderer>();
                if (mr != null) mr.sharedMaterial = GetOrCreateDefaultMaterial();
            }

            int dmg = LegalPreviewHelper.PreviewDamage(
                BuildStateFromSnapshot(snapshot),
                snapshot.SelectedUnitIdForPreview.Value,
                targetId.Value);
            _damageNumberText.text = dmg >= 0 ? dmg.ToString() : "?";
            _damageNumberText.transform.localPosition = GridToLocal(cursor.X, cursor.Y, _damageY, _currentWidth, _currentHeight);
            _damageLabelGo.SetActive(true);
        }

        // 从 BoardSnapshot 反推一个 BattleState 用于伤害预览。
        // 仅在 Damage 数字场景下调用；非持久状态、不进入 Hash。
        private static Core.Model.BattleState BuildStateFromSnapshot(in BoardSnapshot s)
        {
            var board = new Core.Model.BoardState(s.Width, s.Height, EmptyTileMap(s));
            var state = new Core.Model.BattleState(0, Core.Model.Owner.Player, board, null);
            foreach (var u in s.Units)
            {
                state.AddUnit(new Core.Model.UnitState(u.UnitId, u.Pos, u.Hp, u.Hp, u.Phase, u.Owner));
            }
            return state;
        }

        private static System.Collections.Generic.IDictionary<Core.Model.GridPos, Core.Model.TileState> EmptyTileMap(in BoardSnapshot s)
        {
            var d = new Dictionary<Core.Model.GridPos, Core.Model.TileState>(s.Width * s.Height);
            for (int y = 0; y < s.Height; y++)
                for (int x = 0; x < s.Width; x++)
                    d[new Core.Model.GridPos(x, y)] = Core.Model.TileState.Normal;
            return d;
        }

        // ============== Helpers ==============

        private Vector3 GridToLocal(int x, int y, float yLift, int width, int height)
        {
            float wx = x - (width - 1) * 0.5f;
            float wz = y - (height - 1) * 0.5f;
            return new Vector3(wx * _tileSize, yLift, wz * _tileSize);
        }

        private static void ClearChildren(Transform t)
        {
            if (t == null) return;
            for (int i = t.childCount - 1; i >= 0; i--)
                Object.Destroy(t.GetChild(i).gameObject);
        }

        // 默认材质：MVP 阶段用 Primitive 自带默认材质；URP 兼容通过 PropertyBlock + _BaseColor。
        private static Material _cachedDefaultMat;
        private static Material GetOrCreateDefaultMaterial()
        {
            if (_cachedDefaultMat != null) return _cachedDefaultMat;
            var shader = Shader.Find("Universal Render Pipeline/Lit");
            if (shader == null) shader = Shader.Find("Standard");
            if (shader == null) shader = Shader.Find("Sprites/Default");
            _cachedDefaultMat = new Material(shader);
            _cachedDefaultMat.color = Color.white;
            return _cachedDefaultMat;
        }

        // ====== 内部 MonoBehaviour 子组件：Tile / Unit ======

        private class TileView : MonoBehaviour
        {
            public MeshRenderer MeshRenderer;
            private MaterialPropertyBlock _mpb;
            private static readonly int _colorId = Shader.PropertyToID("_BaseColor");
            private static readonly int _legacyColorId = Shader.PropertyToID("_Color");
            private Color _currentColor = new Color(-1f, -1f, -1f, -1f);

            public void SetState(TileState state)
            {
                var c = BoardPalette.TileColor(state);
                if (_currentColor == c) return;
                _currentColor = c;
                if (MeshRenderer == null) return;
                if (_mpb == null) _mpb = new MaterialPropertyBlock();
                MeshRenderer.GetPropertyBlock(_mpb);
                _mpb.SetColor(_colorId, c);   // URP
                _mpb.SetColor(_legacyColorId, c); // Built-in fallback
                if (MeshRenderer.sharedMaterial != null)
                    MeshRenderer.sharedMaterial.color = c;
                MeshRenderer.SetPropertyBlock(_mpb);
            }
        }

        private class UnitView : MonoBehaviour
        {
            public MeshRenderer MeshRenderer;
            private MaterialPropertyBlock _mpb;
            private static readonly int _colorId = Shader.PropertyToID("_BaseColor");
            private static readonly int _legacyColorId = Shader.PropertyToID("_Color");
            private Color _currentColor = new Color(-1f, -1f, -1f, -1f);

            public void ApplySnapshot(in UnitSnapshot u, int width, int height, float yWorld)
            {
                float wx = u.Pos.X - (width - 1) * 0.5f;
                float wz = u.Pos.Y - (height - 1) * 0.5f;
                transform.localPosition = new Vector3(wx, yWorld, wz);

                var color = BoardPalette.UnitColor(u.Phase, u.Owner);
                if (_currentColor == color) return;
                _currentColor = color;
                if (MeshRenderer == null) return;
                if (_mpb == null) _mpb = new MaterialPropertyBlock();
                MeshRenderer.GetPropertyBlock(_mpb);
                _mpb.SetColor(_colorId, color);
                _mpb.SetColor(_legacyColorId, color);
                if (MeshRenderer.sharedMaterial != null)
                    MeshRenderer.sharedMaterial.color = color;
                MeshRenderer.SetPropertyBlock(_mpb);
            }

            public void Destroy()
            {
                if (this != null && gameObject != null) Object.Destroy(gameObject);
            }
        }
    }
}
