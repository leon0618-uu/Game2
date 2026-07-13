#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Starfall.Unity;

namespace Starfall.EditorTools
{
    /// <summary>
    /// M-35 视觉验收辅助：在编辑器菜单 Tools/MVP/Setup Battle Scene 下生成
    /// 已挂载 BattleBootstrap 的场景，启用后用户只需打开场景按 Play 即可。
    /// 也支持命令行 -executeMethod 自动化（Task 20 验收脚本）。
    /// </summary>
    public static class MVPPlayModeHelper
    {
        private const string ScenePath = "Assets/Scenes/MVP_Battle.unity";

        [MenuItem("Tools/MVP/Setup Battle Scene")]
        public static void SetupScene()
        {
            // 1. 创建目录
            if (!System.IO.Directory.Exists("Assets/Scenes"))
                System.IO.Directory.CreateDirectory("Assets/Scenes");

            // 2. 创建新场景
            var scene = EditorSceneManager.NewScene(NewSceneSetup.DefaultGameObjects, NewSceneMode.Single);

            // 3. 主相机参数（俯视视角）
            var cam = Camera.main;
            if (cam == null)
            {
                var camGo = new GameObject("Battle Camera");
                cam = camGo.AddComponent<Camera>();
                cam.tag = "MainCamera";
                cam.transform.position = new Vector3(4.5f, 9.5f, -10f);
                cam.transform.rotation = Quaternion.Euler(45f, 0f, 0f);
                cam.orthographic = false;
                cam.fieldOfView = 60f;
                cam.backgroundColor = new Color(0.1f, 0.1f, 0.15f, 1f);
                cam.clearFlags = CameraClearFlags.SolidColor;
            }

            // 4. 主光源
            var existingLight = Object.FindFirstObjectByType<Light>();
            if (existingLight == null)
            {
                var lightGo = new GameObject("Battle Light");
                var light = lightGo.AddComponent<Light>();
                light.type = LightType.Directional;
                light.intensity = 1.2f;
                light.color = Color.white;
                lightGo.transform.rotation = Quaternion.Euler(50f, -30f, 0f);
            }

            // 5. BattleBootstrap GameObject（核心入口）
            var battleGo = new GameObject("BattleBootstrap");
            var bootstrap = battleGo.AddComponent<BattleBootstrap>();
            // BattleBootstrap 内部 Start() 会自动：加载 JSON、构建 BattleState、挂 Presenter / HUD / InputController

            // 6. EventSystem（如果有 UI 的话）
            var existingEventSystem = Object.FindFirstObjectByType<UnityEngine.EventSystems.EventSystem>();
            if (existingEventSystem == null)
            {
                var esGo = new GameObject("EventSystem");
                esGo.AddComponent<UnityEngine.EventSystems.EventSystem>();
                esGo.AddComponent<UnityEngine.InputSystem.UI.InputSystemUIInputModule>();
            }

            // 7. 保存场景
            EditorSceneManager.SaveScene(scene, ScenePath);
            Debug.Log($"[MVPPlayModeHelper] 场景已创建: {ScenePath}");
            Debug.Log("[MVPPlayModeHelper] 请打开此场景，然后按 Play。");
        }

        /// <summary>
        /// 命令行入口：unity -batchmode -nographics -projectPath ... -executeMethod Starfall.EditorTools.MVPPlayModeHelper.SetupScene -quit
        /// </summary>
        public static void SetupSceneCommandLine()
        {
            SetupScene();
        }
    }
}
#endif
