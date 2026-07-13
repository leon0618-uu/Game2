using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using Starfall.Core.Anchor;
using Starfall.Core.Command;
using Starfall.Core.Combat;
using Starfall.Core.Model;
using Starfall.Unity.Presentation;
using Starfall.Core.Undo;
using Starfall.Unity;

namespace Starfall.Unity.Input
{
    /// <summary>
    /// 战斗输入控制器（Task 17）。
    ///
    /// 职责（AGENTS.md §10.3 分层）：
    /// 1. 读取键盘 / 鼠标（UnityEngine.InputSystem）→ InputAction；
    /// 2. 喂给 InputStateMachine 得到 InputTransition；
    /// 3. 把 transition.Commands 翻译为真实 Command → Push 到 UndoStack → BattleRunner.Submit；
    /// 4. transition.ShouldEndTurn → BattleRunner.EndTurn；
    /// 5. transition.ShouldUndo → UndoStack.TryUndo（需要 Core 暴露 SetState，详见 §3.3）；
    /// 6. 通知 HUD（mode change / LastMessage）；
    /// 7. 在棋盘上画光标 / 已选单位指示器（独立 GameObject 层级，不污染 RealBoardPresenter）。
    ///
    /// 硬约束：
    /// - 不直接改 BattleState 任何字段（AGENTS.md §10.3 / §13）；
    /// - 不复制玩法规则（移动 / 攻击 / 邻接判定委托给 InputStateMachine + Core Command）；
    /// - 不新增 Unity Package（用 UnityEngine.InputSystem 1.19.0 已装）。
    ///
    /// M-35 input-bug-fix（M-35 视觉验收）:见类内 <see cref="ConfigureInputSystemForMvp"/>。
    /// 背景：InputSystem 1.19 在 Editor Play 模式下的默认 editorInputBehaviorInPlayMode
    /// 是 <c>PointersAndKeyboardsRespectGameViewFocus</c>——当 Game view 失焦时，键盘事件
    /// 会被路由到当前拥有焦点的 EditorWindow（Hierarchy / Inspector / Project / Console），
    /// 而不是游戏本身。这导致玩家点开 Project 看 JSON 后，按 M/F/A/D/方向键都无响应，
    /// HUD 一直停在 SelectUnit。修复：把行为切到 <c>AllDeviceInputAlwaysGoesToGameView</c>，
    /// 让 Editor Play 模式下的输入路由与 Player 构建保持一致。
    /// </summary>
    [DefaultExecutionOrder(100)]  // 在 BattleBootstrap 之后
    public class InputController : MonoBehaviour
    {
        // ============================================================
        // M-35 fix：启动时把 InputSystem 的 Editor 输入路由切到「游戏优先」
        // ============================================================
        // 必须在第一个场景加载前完成（RuntimeInitializeLoadType.BeforeSceneLoad），
        // 否则 InputController.Awake / Update 的 Keyboard.current 调用就拿不到键位。
        // 用 [RuntimeInitializeOnLoadMethod] 而非 BattleBootstrap.Awake 的原因：
        // 1. 不需要等场景里有 InputController / BattleBootstrap 实例；
        // 2. Editor 下无需 Play 也能在 EditMode→PlayMode 转换时立即生效；
        // 3. Player 构建中也会执行，无需在 Bootstrap 加 if (Application.isEditor) 分支。
#if UNITY_EDITOR || DEVELOPMENT_BUILD
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void ConfigureInputSystemForMvp()
        {
            try
            {
                var s = UnityEngine.InputSystem.InputSystem.settings;
                if (s == null) return;  // 极端情况：InputSystem 未初始化（不应该发生）
                var target = InputSettings.EditorInputBehaviorInPlayMode.AllDeviceInputAlwaysGoesToGameView;
                if (s.editorInputBehaviorInPlayMode != target)
                {
                    Debug.Log($"[InputController/M-35] editorInputBehaviorInPlayMode: " +
                              $"{s.editorInputBehaviorInPlayMode} → {target} (keyboards now always reach Game view).");
                    s.editorInputBehaviorInPlayMode = target;
                }
            }
            catch (System.Exception ex)
            {
                // 修复失败不能让游戏起不来；记 log 即可
                Debug.LogWarning($"[InputController/M-35] Failed to configure InputSystem settings: {ex.Message}");
            }

            // 安全网：构建中失焦（alt-tab）时游戏继续跑，避免 InputSystem 切到 background 行为。
            if (!Application.runInBackground)
            {
                Application.runInBackground = true;
            }
        }
#endif

        [Header("Bindings")]
        [Tooltip("BattleBootstrap 同对象或场景内 BattleBootstrap 引用；为空则在 Awake 寻找。")]
        [SerializeField] private BattleBootstrap _bootstrap;

        [Tooltip("Undo 栈深度（默认 50，匹配 Starfall.Core.Undo.UndoStack 默认值）。")]
        [SerializeField] private int _undoDepth = 50;

        [Header("Cursor Visuals")]
        [SerializeField] private float _cursorTileSize = 1f;
        [SerializeField] private float _cursorHeight = 0.05f;   // 略高于 tile 防 Z-fighting
        [SerializeField] private float _cursorYOffset = 0.0f;

        // ---- 运行时 ----
        private InputStateMachine _machine;
        private InputState _state;
        private UndoStack _undo;
        private GameObject _cursorGo;
        private GameObject _selectionGo;
        private Transform _cursorRoot;
        private Camera _camera;

        // 模式切换日志（被 BattleBootstrap.RenderPresenters 取代之前的简单 fallback）
        private string _lastMessage;

        // 节流：避免按住方向键刷屏
        private int _lastCursorMoveFrame = -1;

        public InputState State => _state;

        // ============================================================
        // Lifecycle
        // ============================================================

        private void Awake()
        {
            _machine = new InputStateMachine();
            _state = InputState.Initial();
            _undo = new UndoStack(_undoDepth);
            _camera = Camera.main;

            if (_bootstrap == null)
            {
                _bootstrap = GetComponent<BattleBootstrap>();
                if (_bootstrap == null) _bootstrap = FindAnyObjectByType<BattleBootstrap>();
            }

            EnsureCursorVisuals();
        }

        private void Start()
        {
            // 等待 Bootstrap 完成 Awake 装载 Runner
            if (_bootstrap != null && _bootstrap.Runner != null)
            {
                _state = new InputState(
                    mode: InputStateMachine.InitialMode,
                    cursor: CenterOfBoard(_bootstrap.Runner.State),
                    selectedUnitId: null,
                    decreeZoneCursor: 0,
                    lastMessage: "[Start] SelectUnit");
                UpdateCursorVisual();
            }
            else
            {
                _state = _state.WithMode(InputMode.None, "[Start] waiting for BattleBootstrap");
            }
        }

        private void Update()
        {
            if (_bootstrap == null || _bootstrap.Runner == null) return;
            if (_bootstrap.Runner.Outcome != BattleOutcome.Ongoing) return;

            // 鼠标 → 光标（最高优先级）
            HandleMouseCursor();

            // 键盘（仅在按键 down 触发；按住不连发，由 _lastCursorMoveFrame 节流）
            HandleKeyboard();

            UpdateCursorVisual();
            UpdateSelectionVisual();
        }

        // ============================================================
        // Input reading
        // ============================================================

        private void HandleMouseCursor()
        {
            var mouse = Mouse.current;
            if (mouse == null) return;
            var pos = mouse.position.ReadValue();
            // Update() 中：仅当鼠标移动过或按下时才更新
            if (mouse.delta.ReadValue().sqrMagnitude > 0.001f || mouse.leftButton.wasPressedThisFrame)
            {
                var gp = ScreenToGrid(pos);
                if (gp.HasValue)
                {
                    _state = _state.WithCursor(gp);
                    _lastCursorMoveFrame = Time.frameCount;
                }
            }
        }

        private void HandleKeyboard()
        {
            var kb = Keyboard.current;
            if (kb == null) return;

            // Meta actions
            if (kb.escapeKey.wasPressedThisFrame)        Apply(InputAction.Cancel);
            if (kb.zKey.wasPressedThisFrame)             Apply(InputAction.Undo);
            if (kb.spaceKey.wasPressedThisFrame)         Apply(InputAction.EndTurn);

            // Confirm
            if (kb.enterKey.wasPressedThisFrame)         Apply(InputAction.Confirm);
            if (Mouse.current != null && Mouse.current.leftButton.wasPressedThisFrame) Apply(InputAction.Confirm);

            // 命令模式键（M/F/D）
            if (kb.mKey.wasPressedThisFrame)             Apply(InputAction.EnterMove);
            if (kb.fKey.wasPressedThisFrame)             Apply(InputAction.EnterPhaseFlip);
            if (kb.dKey.wasPressedThisFrame)             Apply(InputAction.EnterDecree);

            // A 键：SelectUnit → AttackTarget，其他模式 → CursorLeft
            if (kb.aKey.wasPressedThisFrame)
            {
                if (_state.Mode == InputMode.SelectUnit)
                    Apply(InputAction.EnterAttack);
                else
                    Apply(InputAction.CursorLeft);
            }

            // 光标移动（箭头 + WASD 中非 A 的三个 + DecreeSelect 模式下的 W/S）
            if (kb.upArrowKey.wasPressedThisFrame)       Apply(InputAction.CursorUp);
            if (kb.downArrowKey.wasPressedThisFrame)     Apply(InputAction.CursorDown);
            if (kb.leftArrowKey.wasPressedThisFrame)     Apply(InputAction.CursorLeft);
            if (kb.rightArrowKey.wasPressedThisFrame)    Apply(InputAction.CursorRight);
            if (kb.wKey.wasPressedThisFrame)
            {
                if (_state.Mode == InputMode.DecreeSelect) Apply(InputAction.DecreeCyclePrev);
                else Apply(InputAction.CursorUp);
            }
            if (kb.sKey.wasPressedThisFrame)
            {
                if (_state.Mode == InputMode.DecreeSelect) Apply(InputAction.DecreeCycleNext);
                else Apply(InputAction.CursorDown);
            }
        }

        private GridPos? ScreenToGrid(Vector2 screenPos)
        {
            if (_camera == null) _camera = Camera.main;
            if (_camera == null) return null;
            var s = _bootstrap?.Runner?.State;
            if (s == null) return null;

            Ray ray = _camera.ScreenPointToRay(screenPos);
            Plane ground = new Plane(Vector3.up, Vector3.zero);
            if (!ground.Raycast(ray, out float enter)) return null;
            Vector3 world = ray.GetPoint(enter);

            int width = s.Board.Width;
            int height = s.Board.Height;
            // 与 RealBoardPresenter.GridToLocal 互逆：gridX = world.x / tileSize + (width-1)/2
            int x = Mathf.RoundToInt(world.x / _cursorTileSize + (width - 1) * 0.5f);
            int y = Mathf.RoundToInt(world.z / _cursorTileSize + (height - 1) * 0.5f);
            if (x < 0 || y < 0 || x >= width || y >= height) return null;
            return new GridPos(x, y);
        }

        // ============================================================
        // Apply transition
        // ============================================================

        private void Apply(InputAction action)
        {
            if (_bootstrap == null || _bootstrap.Runner == null) return;
            var state = _bootstrap.Runner.State;

            // 提交命令前：把当前 BattleState 深拷贝进 UndoStack（Undo 边界）
            // 仅在会产生 BattleState 变化的动作上 push；元动作（Esc/Z/Space）不 push。
            if (WillMutateState(action))
            {
                _undo.Push(state);
            }

            var transition = _machine.ProcessAction(_state, action, state);
            _state = transition.Next;
            _lastMessage = _state.LastMessage;

            // 提交 Command 蓝图
            if (transition.Commands != null && transition.Commands.Count > 0)
            {
                foreach (var plan in transition.Commands)
                {
                    // 注意：DecreeHold 的 _decrees 注册已下沉到 CommandBuilder.BuildDecreeHold，
                    // InputController 不再直接写 BattleState.Decrees（AGENTS.md §10.3）。
                    var cmd = CommandBuilder.Build(plan, state);
                    if (cmd != null)
                    {
                        var result = _bootstrap.Runner.Submit(cmd);
                        if (result == CommandResult.Illegal)
                        {
                            // 命令非法：弹出刚才 push 的快照，避免 UndoStack 误污染
                            _undo.TryUndo(out _);
                            _state = _state.WithMessage($"[Illegal] {plan.GetType().Name} rejected");
                        }
                    }
                }
            }

            if (transition.ShouldEndTurn)
            {
                _bootstrap.Runner.EndTurn();
            }

            if (transition.ShouldUndo)
            {
                DoUndo();
            }

            // 重新渲染（每次 mode / 命令 / 撤销都触发；Presenter 内部 dedupe 由 Render 自己负责）
            _bootstrap.RenderPresenters(BuildPresentationEvents(transition));
        }

        // ============================================================
        // Test / automation entry point
        // ============================================================
        /// <summary>
        /// M-35 demo 自动化入口：把一个 <see cref="InputAction"/> 喂进和真实键盘相同的处理路径。
        /// 在 <see cref="Apply"/> 之外暴露公开签名，让 PlayMode 测试不依赖反射就能模拟按键；
        /// 不复制玩法逻辑（仍走 <see cref="_machine"/> + <see cref="CommandBuilder"/> + <see cref="BattleRunner"/>）。
        /// 由 <c>Starfall.Tests.PlayMode</c> 的 M35DemoScript 调用。
        /// </summary>
        public void Press(InputAction action) => Apply(action);

        private static bool WillMutateState(InputAction action)
        {
            switch (action)
            {
                case InputAction.Confirm:
                case InputAction.EnterMove:
                case InputAction.EnterPhaseFlip:
                case InputAction.EnterAttack:
                case InputAction.EnterDecree:
                case InputAction.EndTurn:
                    return true;
                default:
                    return false;
            }
        }

        private void DoUndo()
        {
            // ★ Undo 阻塞点：
            // BattleRunner.State 是只读属性（auto-property with { get; }），
            // 外部无法直接替换回上一个深拷贝。
            //
            // 解决路径（需要 Core 协作）：
            //   方案 A：Core 提供 BattleRunner.RestoreState(BattleState snapshot) — 由 Lead 派 gameplay 添加；
            //   方案 B：Core 把 BattleRunner.State 改为 { get; private set; } + 加 RestoreState 方法。
            //
            // 当前（Task 17 内）：调用 UndoStack.TryUndo 取出快照，存到 _pendingRestoredState，
            // 等 Lead 派 gameplay 添加 BattleRunner.RestoreState 后，由 InputController.RestoreRunnerState()
            // 把快照写回 Runner。
            //
            // 现阶段的临时行为：弹出快照 + 记录消息 + 重置模式；下一次 Submit 仍在旧 state 上执行，
            // 直到 Core 暴露 SetState 后再做最终生效。
            if (_undo.TryUndo(out var restored))
            {
                _pendingRestoredState = restored;
                _state = _state.WithMessage("[Undo] pending BattleRunner.RestoreState (Core 协作阻塞)");
                Debug.LogWarning("[InputController] Undo snapshot popped but BattleRunner.State has no setter. " +
                                 "Need Lead → gameplay to add BattleRunner.RestoreState(BattleState).");
            }
            else
            {
                _state = _state.WithMessage("[Undo] stack empty");
            }
        }

        private BattleState _pendingRestoredState;
        public bool HasPendingRestore => _pendingRestoredState != null;
        public BattleState ConsumePendingRestore()
        {
            var s = _pendingRestoredState;
            _pendingRestoredState = null;
            return s;
        }

        private static IReadOnlyList<PresentationEvent> BuildPresentationEvents(InputTransition t)
        {
            // MVP：不解析 BattleEvent（Runner.Events 在 RenderPresenters 之前已被 BattleBootstrap 调用过）；
            // 这里只返回空集合。RealBoardPresenter 收到空集合时按状态全量重绘。
            return System.Array.Empty<PresentationEvent>();
        }

        // ============================================================
        // Cursor visual
        // ============================================================

        private void EnsureCursorVisuals()
        {
            _cursorRoot = new GameObject("InputControllerVisuals").transform;
            _cursorRoot.SetParent(transform, false);

            _cursorGo = GameObject.CreatePrimitive(PrimitiveType.Quad);
            var col = _cursorGo.GetComponent<Collider>();
            if (col != null) Destroy(col);
            _cursorGo.name = "Cursor";
            _cursorGo.transform.SetParent(_cursorRoot, false);
            _cursorGo.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
            _cursorGo.transform.localScale = Vector3.one * _cursorTileSize * 0.95f;
            var cursorMr = _cursorGo.GetComponent<MeshRenderer>();
            var cursorMat = new Material(GetShader());
            cursorMat.color = new Color(1f, 0.95f, 0.2f, 0.65f);
            cursorMr.sharedMaterial = cursorMat;

            _selectionGo = GameObject.CreatePrimitive(PrimitiveType.Quad);
            var col2 = _selectionGo.GetComponent<Collider>();
            if (col2 != null) Destroy(col2);
            _selectionGo.name = "Selection";
            _selectionGo.transform.SetParent(_cursorRoot, false);
            _selectionGo.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
            _selectionGo.transform.localScale = Vector3.one * _cursorTileSize * 0.98f;
            var selMr = _selectionGo.GetComponent<MeshRenderer>();
            var selMat = new Material(GetShader());
            selMat.color = new Color(0.3f, 1f, 0.5f, 0.45f);
            selMr.sharedMaterial = selMat;

            _selectionGo.SetActive(false);
        }

        private static Shader GetShader()
        {
            var s = Shader.Find("Universal Render Pipeline/Lit");
            if (s == null) s = Shader.Find("Standard");
            if (s == null) s = Shader.Find("Sprites/Default");
            return s;
        }

        private void UpdateCursorVisual()
        {
            if (_cursorGo == null || _state.Cursor == null) return;
            var pos = GridToLocal(_state.Cursor.Value);
            _cursorGo.transform.localPosition = pos + Vector3.up * _cursorHeight;
            _cursorGo.SetActive(true);
        }

        private void UpdateSelectionVisual()
        {
            if (_selectionGo == null) return;
            if (_bootstrap == null || _bootstrap.Runner == null) { _selectionGo.SetActive(false); return; }
            if (_state.SelectedUnitId == null) { _selectionGo.SetActive(false); return; }
            var u = FindUnit(_bootstrap.Runner.State, _state.SelectedUnitId.Value);
            if (u == null) { _selectionGo.SetActive(false); return; }
            _selectionGo.transform.localPosition = GridToLocal(u.Pos) + Vector3.up * (_cursorHeight - 0.01f);
            _selectionGo.SetActive(true);
        }

        private Vector3 GridToLocal(GridPos gp)
        {
            var s = _bootstrap?.Runner?.State;
            int w = s?.Board.Width ?? 8;
            int h = s?.Board.Height ?? 10;
            float wx = gp.X - (w - 1) * 0.5f;
            float wz = gp.Y - (h - 1) * 0.5f;
            return new Vector3(wx * _cursorTileSize, _cursorYOffset, wz * _cursorTileSize);
        }

        private static GridPos CenterOfBoard(BattleState s)
            => new GridPos(s.Board.Width / 2, s.Board.Height / 2);

        private static UnitState FindUnit(BattleState s, int unitId)
        {
            foreach (var u in s.Units)
                if (u.UnitId == unitId) return u;
            return null;
        }

        // ============================================================
        // HUD notification (Task 17 简化版 — 仅 Log；Task 18 由 RealBattleHud 接管详细布局)
        // ============================================================

        private void OnGUI()
        {
            // MVP：仅在 ScreenSpace-Overlay Canvas 不可用时，用 IMGUI 把模式 / 提示打在屏幕左上角
            if (_state == null) return;
            var label = $"Mode: {_state.Mode}\nMsg: {_state.LastMessage}\n" +
                        $"Cursor: {_state.Cursor}  Sel: {_state.SelectedUnitId}";
            GUI.Label(new Rect(10, 10, 600, 80), label);
        }
    }
}