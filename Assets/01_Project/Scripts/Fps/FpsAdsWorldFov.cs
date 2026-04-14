using UnityEngine;

namespace FpsDemo.Fps
{
    /// <summary>
    /// 仅驱动「世界」主相机的 <see cref="Camera.fieldOfView"/>：按住瞄准时过渡到 ADS FOV，松开回到腰射 FOV。
    /// 手臂 / Viewmodel 相机请保持独立 FOV，勿挂在此引用上。
    /// </summary>
    public sealed class FpsAdsWorldFov : MonoBehaviour
    {
        [Header("引用")]
        [Tooltip("渲染场景的主相机（勿填手臂专用相机）。")]
        [SerializeField] private Camera _worldCamera;
        [SerializeField] private FpsInput _input;

        [Header("FOV（度）")]
        [Tooltip("为 0 时在 Start 从 World Camera 当前值读取为腰射 FOV。")]
        [SerializeField] private float _hipFov;
        [SerializeField] private float _adsFov = 72f;

        [Header("过渡")]
        [Tooltip("每秒改变的 FOV 度数，越大越快。")]
        [SerializeField] private float _blendSpeedDegreesPerSecond = 120f;

        private void Awake()
        {
            if (_input == null)
                _input = GetComponent<FpsInput>();
            if (_input == null)
                _input = GetComponentInParent<FpsInput>();

            if (_worldCamera == null)
                _worldCamera = GetComponent<Camera>();
        }

        private void Start()
        {
            if (_worldCamera == null)
                return;

            if (_hipFov <= 0f)
                _hipFov = _worldCamera.fieldOfView;
        }

        private void OnDisable()
        {
            if (_worldCamera != null && _hipFov > 0f)
                _worldCamera.fieldOfView = _hipFov;
        }

        private void LateUpdate()
        {
            if (_worldCamera == null || _input == null)
                return;

            float target = _hipFov;
            if (_input.GameplayInputEnabled && _input.AimHeld)
                target = _adsFov;

            float fov = _worldCamera.fieldOfView;
            fov = Mathf.MoveTowards(fov, target, _blendSpeedDegreesPerSecond * Time.deltaTime);
            _worldCamera.fieldOfView = fov;
        }
    }
}
