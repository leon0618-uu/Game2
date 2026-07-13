using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using Starfall.Unity.Presentation;

namespace Starfall.Unity
{
    /// <summary>
    /// 真实 HUD 表现（Task 16）：
    /// - 使用 uGUI Canvas 显示 TurnNumber / ActivePlayer / Outcome / Phase 摘要；
    /// - 不持有 BattleState（AGENTS.md §10.3 / ADR-0002 §3）；
    /// - Render 内部异常吞咽（ADR-0002 §4）；
    /// - Task 18 之前仅显示简化版：详细数值（AP / PV / CV / 撤离目标进度）由 Task 18 接管。
    /// </summary>
    public class RealBattleHud : MonoBehaviour, IBattleHud
    {
        [SerializeField] private int _fontSize = 20;
        [SerializeField] private Vector2 _panelAnchor = new Vector2(0f, 1f); // top-left
        [SerializeField] private Vector2 _panelSize = new Vector2(380f, 200f);
        [SerializeField] private Vector2 _panelOffset = new Vector2(16f, -16f);

        private Canvas _canvas;
        private RectTransform _panelRect;
        private Text _turnText;
        private Text _playerText;
        private Text _phaseText;
        private Text _outcomeText;
        private Text _summaryText;

        private HudSnapshot _lastSnapshot;
        private bool _initialized;

        public void Render(in HudSnapshot snapshot, in IReadOnlyList<PresentationEvent> events)
        {
            try
            {
                EnsureLayout();
                _lastSnapshot = snapshot;
                UpdateTexts(snapshot);
            }
            catch (System.Exception ex)
            {
                // ADR-0002 §4：表现层异常吞咽
                Debug.LogError($"[RealBattleHud] Render failed: {ex}");
            }
        }

        // ============== Layout ==============

        private void EnsureLayout()
        {
            if (_initialized) return;
            _initialized = true;

            // 1. Canvas
            var canvasGo = new GameObject("StarfallHudCanvas");
            canvasGo.transform.SetParent(transform, false);
            _canvas = canvasGo.AddComponent<Canvas>();
            _canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            _canvas.sortingOrder = 100;
            canvasGo.AddComponent<CanvasScaler>().uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            ((CanvasScaler)canvasGo.GetComponent<CanvasScaler>()).referenceResolution = new Vector2(1920f, 1080f);
            canvasGo.AddComponent<GraphicRaycaster>();

            // 2. Panel (背景)
            var panelGo = new GameObject("Panel");
            panelGo.transform.SetParent(canvasGo.transform, false);
            _panelRect = panelGo.AddComponent<RectTransform>();
            _panelRect.anchorMin = _panelAnchor;
            _panelRect.anchorMax = _panelAnchor;
            _panelRect.pivot = new Vector2(0f, 1f);
            _panelRect.anchoredPosition = _panelOffset;
            _panelRect.sizeDelta = _panelSize;
            var bg = panelGo.AddComponent<Image>();
            bg.color = BoardPalette.HudBackground;

            // 3. Texts
            _turnText    = CreateText(panelGo.transform, "TurnText",   new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(0f, 1f),   new Vector2(  0f, -10f), new Vector2(360f, 36f));
            _playerText  = CreateText(panelGo.transform, "PlayerText", new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(0f, 1f),   new Vector2(  0f, -50f), new Vector2(360f, 36f));
            _phaseText   = CreateText(panelGo.transform, "PhaseText",  new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(0f, 1f),   new Vector2(  0f, -90f), new Vector2(360f, 36f));
            _outcomeText = CreateText(panelGo.transform, "OutcomeText",new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(0f, 1f),   new Vector2(  0f, -130f), new Vector2(360f, 36f));
            _summaryText = CreateText(panelGo.transform, "SummaryText",new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(0f, 1f),   new Vector2(  0f, -170f), new Vector2(360f, 24f));
        }

        private Text CreateText(Transform parent, string name, Vector2 aMin, Vector2 aMax, Vector2 pivot, Vector2 anchoredPos, Vector2 size)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            var rt = go.AddComponent<RectTransform>();
            rt.anchorMin = aMin;
            rt.anchorMax = aMax;
            rt.pivot = pivot;
            rt.anchoredPosition = anchoredPos;
            rt.sizeDelta = size;
            var t = go.AddComponent<Text>();
            // Unity 6 内置默认字体：优先 LegacyRuntime.ttf，回退 Arial.ttf
            t.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            if (t.font == null) t.font = Resources.GetBuiltinResource<Font>("Arial.ttf");
            t.fontSize = _fontSize;
            t.color = BoardPalette.HudText;
            t.alignment = TextAnchor.MiddleLeft;
            t.horizontalOverflow = HorizontalWrapMode.Overflow;
            t.verticalOverflow = VerticalWrapMode.Overflow;
            return t;
        }

        // ============== Update ==============

        private void UpdateTexts(in HudSnapshot s)
        {
            _turnText.text    = $"Turn: {s.TurnNumber}";
            _playerText.text  = $"Active: {s.ActivePlayer}";
            // Phase 不在 HudSnapshot 中（MVP 简化），占位提示玩家当前阵营的简写
            _phaseText.text   = $"Active Side: {(s.ActivePlayer == Starfall.Core.Model.Owner.Player ? "Player" : "Enemy")}";
            _outcomeText.text = $"Outcome: {s.Outcome}";
            _outcomeText.color = BoardPalette.OutcomeColor(s.Outcome);
            _summaryText.text = "[Task 16 HUD] 详细数值由 Task 18 接管";
            _summaryText.fontSize = Mathf.Max(12, _fontSize - 6);
            _summaryText.color = new Color(1f, 1f, 1f, 0.55f);
        }
    }
}