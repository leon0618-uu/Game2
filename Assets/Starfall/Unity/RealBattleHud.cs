using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using Starfall.Unity.Presentation;

namespace Starfall.Unity
{
    /// <summary>
    /// 真实 HUD 表现（Task 16 + Task 18）：
    /// - 使用 uGUI Canvas 显示 TurnNumber / ActivePlayer / Outcome / Phase / 派生 HUD 数值；
    /// - Task 18 接管：AP / PV / CV / 激活单位 / 防守目标 / 伤害预览 / 当前输入模式 + 提示；
    /// - 不持有 BattleState（AGENTS.md §10.3 / ADR-0002 §3）；
    /// - Render 内部异常吞咽（ADR-0002 §4）。
    /// </summary>
    public class RealBattleHud : MonoBehaviour, IBattleHud
    {
        [SerializeField] private int _fontSize = 20;
        [SerializeField] private Vector2 _panelAnchor = new Vector2(0f, 1f); // top-left
        [SerializeField] private Vector2 _panelSize = new Vector2(420f, 320f);
        [SerializeField] private Vector2 _panelOffset = new Vector2(16f, -16f);

        private Canvas _canvas;
        private RectTransform _panelRect;
        private Text _turnText;
        private Text _playerText;
        private Text _phaseText;
        private Text _objectiveText;
        private Text _activeUnitText;
        private Text _damagePreviewText;
        private Text _modeText;
        private Text _messageText;
        private Text _outcomeText;

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

            // 3. Texts (Task 18: 9 行布局，y 从 -10 起、步长 -36)
            _turnText          = CreateText(panelGo.transform, "TurnText",          new Vector2(  0f, -10f), new Vector2(400f, 32f));
            _playerText        = CreateText(panelGo.transform, "PlayerText",        new Vector2(  0f, -46f), new Vector2(400f, 32f));
            _phaseText         = CreateText(panelGo.transform, "PhaseText",         new Vector2(  0f, -82f), new Vector2(400f, 32f));
            _objectiveText     = CreateText(panelGo.transform, "ObjectiveText",     new Vector2(  0f, -118f), new Vector2(400f, 32f));
            _activeUnitText    = CreateText(panelGo.transform, "ActiveUnitText",    new Vector2(  0f, -154f), new Vector2(400f, 32f));
            _damagePreviewText = CreateText(panelGo.transform, "DamagePreviewText", new Vector2(  0f, -190f), new Vector2(400f, 32f));
            _modeText          = CreateText(panelGo.transform, "ModeText",          new Vector2(  0f, -226f), new Vector2(400f, 32f));
            _messageText       = CreateText(panelGo.transform, "MessageText",       new Vector2(  0f, -256f), new Vector2(400f, 28f));
            _outcomeText       = CreateText(panelGo.transform, "OutcomeText",       new Vector2(  0f, -290f), new Vector2(400f, 32f));
        }

        private Text CreateText(Transform parent, string name, Vector2 anchoredPos, Vector2 size)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            var rt = go.AddComponent<RectTransform>();
            rt.anchorMin = new Vector2(0f, 1f);
            rt.anchorMax = new Vector2(0f, 1f);
            rt.pivot = new Vector2(0f, 1f);
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
            // 行 1: Turn
            _turnText.text = $"Turn: {s.TurnNumber}";
            _turnText.color = BoardPalette.HudText;

            // 行 2: Active
            _playerText.text = $"Active: {s.ActivePlayer}";
            _playerText.color = BoardPalette.HudText;

            // 行 3: PV（当前相位）
            _phaseText.text = $"PV (Phase): {s.CurrentPhase}";
            _phaseText.color = BoardPalette.HudAccentPhase;

            // 行 4: 目标
            _objectiveText.text = s.ObjectiveText;
            _objectiveText.color = BoardPalette.HudText;

            // 行 5: 激活单位（AP / CV / HP）
            if (s.ActiveUnit.HasValue)
            {
                var u = s.ActiveUnit.Value;
                _activeUnitText.text =
                    $"Active Unit #{u.UnitId}: HP {u.Hp}/{u.MaxHp}  AP {u.Ap}  CV {u.Cv}  Pos {u.Pos}";
                _activeUnitText.color = BoardPalette.HudText;
            }
            else
            {
                _activeUnitText.text = "Active Unit: (none selected)";
                _activeUnitText.color = new Color(1f, 1f, 1f, 0.55f);
            }

            // 行 6: 伤害预览（仅 AttackTarget 模式 + 悬停目标时显示）
            if (s.DamagePreview.HasValue && s.DamagePreview.Value >= 0)
            {
                _damagePreviewText.text = $"Damage Preview: {s.DamagePreview.Value}";
                _damagePreviewText.color = BoardPalette.HudAccentDamage;
            }
            else
            {
                _damagePreviewText.text = "Damage Preview: -";
                _damagePreviewText.color = new Color(1f, 1f, 1f, 0.45f);
            }

            // 行 7: 当前 Input 模式
            _modeText.text = $"Input Mode: {s.InputModeHint}";
            _modeText.color = BoardPalette.HudText;

            // 行 8: 输入提示（LastMessage）
            _messageText.text = string.IsNullOrEmpty(s.LastInputMessage) ? "(no message)" : s.LastInputMessage;
            _messageText.fontSize = Mathf.Max(12, _fontSize - 4);
            _messageText.color = new Color(1f, 1f, 1f, 0.7f);

            // 行 9: Outcome
            _outcomeText.text = $"Outcome: {s.Outcome}";
            _outcomeText.color = BoardPalette.OutcomeColor(s.Outcome);
        }
    }
}
