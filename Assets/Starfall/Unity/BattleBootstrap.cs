using UnityEngine;
using Starfall.Core.Combat;
using Starfall.Core.Model;
using Starfall.Data;
using Starfall.Data.Definition;
using Starfall.Data.Loading;
using Starfall.Data.Validation;
using Starfall.Unity.Presentation;

namespace Starfall.Unity
{
    /// <summary>
    /// Unity 入口（Task 10 / 15 / 16 / 17 / 18 合并版）：
    /// 1. 从 StreamingAssets/data/*.json 加载 BattleDefinition；
    /// 2. DefinitionValidator 校验；
    /// 3. BattleStateBuilder 构建 BattleState；
    /// 4. 构造 BattleRunner（默认 SimpleEnemyAI）；
    /// 5. 把 BoardPresenter + BattleHud 注入并首次 Render；
    /// 6. Task 18 注入：把 InputState 桥接到 BoardSnapshot / HudSnapshot，使 Presenter 渲染合法格 / 攻击目标 / 坠落预览。
    ///
    /// 硬约束（AGENTS.md §10.3 / ADR-0002 §3）：
    /// - 不持有 BattleState 引用——只持有 BattleRunner；
    /// - JSON 损坏不崩溃，错误可见（log + 异常吞咽）；
    /// - Presenter 不直接写 BattleState 任何字段。
    /// </summary>
    public class BattleBootstrap : MonoBehaviour
    {
        [SerializeField] private string _battleDefinitionPath = "data/battle_default";
        [SerializeField] private MonoBehaviour _boardPresenter;  // 实现 IBoardPresenter
        [SerializeField] private MonoBehaviour _battleHud;       // 实现 IBattleHud

        public BattleRunner Runner { get; private set; }
        public string LastError { get; private set; }

        // Task 18：缓存 InputController 引用，用于把 InputState 桥接到快照
        private Starfall.Unity.Input.InputController _inputController;

        private bool _started;

        private void Awake()
        {
            try
            {
                var def = LoadDefinition(_battleDefinitionPath);
                DefinitionValidator.Validate(def, _battleDefinitionPath);
                var state = BattleStateBuilder.Build(def);
                Runner = new BattleRunner(state);
            }
            catch (System.Exception ex)
            {
                // 损坏 JSON / 校验失败不崩溃：捕获后输出可见错误。
                LastError = ex.Message;
                Debug.LogError($"[BattleBootstrap] Failed to load battle '{_battleDefinitionPath}': {ex}");
                Runner = null;
            }

            // Task 16: 若未手动绑定 presenter / hud，则自动添加 Real* 实现，
            // 方便 PlayMode 直接看到棋盘 + HUD（不依赖 Scene 手动配置）。
            if (_boardPresenter == null)
            {
                var bp = gameObject.AddComponent<RealBoardPresenter>();
                _boardPresenter = bp;
                Debug.Log("[BattleBootstrap] Auto-attached RealBoardPresenter (Task 16 default).");
            }
            if (_battleHud == null)
            {
                var bh = gameObject.AddComponent<RealBattleHud>();
                _battleHud = bh;
                Debug.Log("[BattleBootstrap] Auto-attached RealBattleHud (Task 16 default).");
            }
            // Task 17: 若场景内尚未挂 InputController，则自动添加。
            // InputController 持有自己的 UndoStack + 模式状态机；BattleBootstrap 不需要额外暴露接口。
            if (FindAnyObjectByType<Starfall.Unity.Input.InputController>() == null)
            {
                var ic = gameObject.AddComponent<Starfall.Unity.Input.InputController>();
                Debug.Log("[BattleBootstrap] Auto-attached InputController (Task 17 default).");
            }
        }

        private void Start()
        {
            if (_started) return;
            _started = true;
            // 缓存 InputController 引用（必须在 InputController.Awake 之后；同对象或 FindAnyObjectByType 都行）
            _inputController = GetComponent<Starfall.Unity.Input.InputController>();
            if (_inputController == null) _inputController = FindAnyObjectByType<Starfall.Unity.Input.InputController>();

            if (Runner == null)
            {
                Debug.LogWarning($"[BattleBootstrap] Runner is null (LastError='{LastError}'). Skipping first Render.");
                return;
            }
            RenderPresenters(System.Array.Empty<Presentation.PresentationEvent>());
        }

        /// <summary>
        /// 外部命令执行成功后的统一渲染入口（Task 18 由 InputDispatcher 调用）。
        /// </summary>
        public void RenderPresenters(System.Collections.Generic.IReadOnlyList<Presentation.PresentationEvent> events)
        {
            if (Runner == null) return;
            try
            {
                // 1. 收集 InputState 上下文（Task 18）
                int? selectedUnitId = null;
                var inputMode = Starfall.Unity.Input.InputMode.None;
                string lastMessage = string.Empty;
                GridPos? cursor = null;
                if (_inputController != null)
                {
                    var s = _inputController.State;
                    if (s != null)
                    {
                        selectedUnitId = s.SelectedUnitId;
                        inputMode = s.Mode;
                        lastMessage = s.LastMessage;
                        cursor = s.Cursor;
                    }
                }

                // 2. 预览感知的快照（合法格 / 攻击目标 / 坠落）
                var boardSnap = BoardSnapshot.FromStateWithPreview(
                    Runner.State, selectedUnitId, inputMode, cursor);

                // 3. 伤害预览（仅 AttackTarget 模式 + 悬停攻击目标时计算）
                int? damagePreview = null;
                if (inputMode == Starfall.Unity.Input.InputMode.AttackTarget
                    && selectedUnitId.HasValue
                    && cursor.HasValue)
                {
                    // 找到 cursor 处的目标单位
                    foreach (var t in boardSnap.AttackTargets)
                    {
                        if (t.Pos == cursor.Value)
                        {
                            damagePreview = LegalPreviewHelper.PreviewDamage(
                                Runner.State, selectedUnitId.Value, t.UnitId);
                            break;
                        }
                    }
                }

                // 4. HUD 快照
                var hudSnap = HudSnapshot.FromStateWithInput(
                    Runner.State, Runner.Outcome, selectedUnitId, inputMode, lastMessage, damagePreview);

                if (_boardPresenter is Presentation.IBoardPresenter bp)
                {
                    try { bp.Render(boardSnap, events); }
                    catch (System.Exception ex) { Debug.LogError($"[BattleBootstrap] BoardPresenter.Render failed: {ex}"); }
                }
                if (_battleHud is Presentation.IBattleHud bh)
                {
                    try { bh.Render(hudSnap, events); }
                    catch (System.Exception ex) { Debug.LogError($"[BattleBootstrap] BattleHud.Render failed: {ex}"); }
                }
            }
            catch (System.Exception ex)
            {
                Debug.LogError($"[BattleBootstrap] RenderPresenters failed: {ex}");
            }
        }

        private static BattleDefinition LoadDefinition(string path)
        {
            var fullPath = System.IO.Path.Combine(Application.streamingAssetsPath, path + ".json");
            return JsonBattleLoader.Load(fullPath);
        }
    }
}
