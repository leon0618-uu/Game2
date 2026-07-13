using UnityEngine;

namespace Starfall.Unity
{
    /// <summary>
    /// Task 16 自动相机配置：检测场景中无 Camera 或 Camera 未对准棋盘时，
    /// 自动放置一台俯瞰相机（保证 PlayMode 直接看到 8×10 棋盘）。
    ///
    /// 使用方式：把该组件挂到任意 GameObject（或与 BattleBootstrap 同对象），
    /// 启动时若场景中无 Camera 则创建一台对准世界原点的透视相机。
    /// MVP 简化版：固定 8×10 棋盘；其他棋盘尺寸可在 Task 17 由用户调参。
    /// </summary>
    [DefaultExecutionOrder(-50)] // 在 BattleBootstrap 之前
    public class BattleCameraAutoSetup : MonoBehaviour
    {
        [SerializeField] private bool _createIfMissing = true;
        [SerializeField] private float _distance = 12f;
        [SerializeField] private float _angle = 50f;
        [SerializeField] private Color _background = new Color(0.08f, 0.09f, 0.12f, 1f);

        private void Awake()
        {
            var cam = Camera.main;
            if (cam == null && _createIfMissing)
            {
                var go = new GameObject("BattleCamera (auto)");
                cam = go.AddComponent<Camera>();
                cam.clearFlags = CameraClearFlags.SolidColor;
                cam.backgroundColor = _background;
                cam.tag = "MainCamera";
                go.AddComponent<AudioListener>();
                Debug.Log("[BattleCameraAutoSetup] Created Main Camera.");
            }
            if (cam == null) return;

            // 8×10 棋盘以原点为中心；相机放 (0, sinY*d, -cosY*d) 看 (0,0,0)
            float rad = _angle * Mathf.Deg2Rad;
            float y = Mathf.Sin(rad) * _distance;
            float z = -Mathf.Cos(rad) * _distance;
            cam.transform.position = new Vector3(0f, y, z);
            cam.transform.rotation = Quaternion.Euler(_angle, 0f, 0f);
            cam.nearClipPlane = 0.3f;
            cam.farClipPlane = 100f;
            cam.fieldOfView = 60f;
        }
    }
}