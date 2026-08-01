using FpsDemo.Match;
using FpsDemo.UI;
using UnityEngine;

namespace FpsDemo.Fps
{
    /// <summary>
    /// 竖直转 CameraPivot（俯仰）、光标锁定。水平转身体由 <see cref="FpsPlayerMotor"/> 根据 <see cref="PlayerLocomotionInput.YawDelta"/> 处理。
    /// 执行顺序晚于 <see cref="FpsPlayerMotor"/>，保证本帧俯仰在电机之后应用。
    /// </summary>
    [DefaultExecutionOrder(50)]
    public sealed class FpsPlayerLook : MonoBehaviour
    {
        [SerializeField] private FpsInput _input;
        [SerializeField] private Transform _cameraPivot;

        [Header("俯仰角限制（度）")]
        [SerializeField] private float _pitchMin = -89f;
        [SerializeField] private float _pitchMax = 89f;

        [Header("光标")]
        [SerializeField] private bool _lockCursorOnStart = true;

        private float _pitch;

        private void Start()
        {
            if (_lockCursorOnStart)
                LockCursor();
        }

        private void Update()
        {
            if (_input == null || _cameraPivot == null)
                return;

            bool matchOver = MatchManager.Instance != null && MatchManager.Instance.IsMatchOver;
            bool uiKeepsCursorFree = matchOver || InMatchPauseMenuWindowController.IsOpen;

            // ESC 由 InMatchPauseMenuInput 开暂停菜单；此处勿与点击 UI 抢光标。
            if (!uiKeepsCursorFree && Input.GetMouseButtonDown(0))
                LockCursor();

            if (matchOver || InMatchPauseMenuWindowController.IsOpen)
                return;

            float pitchDelta = _input.LookDelta.y;

            _pitch -= pitchDelta;
            _pitch = Mathf.Clamp(_pitch, _pitchMin, _pitchMax);
            _cameraPivot.localRotation = Quaternion.Euler(_pitch, 0f, 0f);
        }

        private static void LockCursor()
        {
            Cursor.lockState = CursorLockMode.Locked;
            Cursor.visible = false;
        }

        private static void UnlockCursor()
        {
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
        }

        /// <summary>复活点对齐：世界水平角设为 <paramref name="yawDegrees"/>，俯仰归零。</summary>
        public void SnapToWorldYaw(float yawDegrees)
        {
            transform.rotation = Quaternion.Euler(0f, yawDegrees, 0f);
            _pitch = 0f;
            if (_cameraPivot != null)
                _cameraPivot.localRotation = Quaternion.identity;
        }
    }
}
