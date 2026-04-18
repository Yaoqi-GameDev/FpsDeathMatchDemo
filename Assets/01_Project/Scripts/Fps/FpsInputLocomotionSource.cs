using UnityEngine;

namespace FpsDemo.Fps
{
    /// <summary>
    /// 从 <see cref="FpsInput"/> 组装 <see cref="PlayerLocomotionInput"/>；水平转角增量使用 <see cref="FpsInput.LookDelta"/> 的 x（与旧版 <see cref="FpsPlayerLook"/> 转身体一致）。
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(FpsInput))]
    public sealed class FpsInputLocomotionSource : MonoBehaviour, ILocomotionInputSource
    {
        [SerializeField] private FpsInput _input;

        private void Awake()
        {
            if (_input == null)
                _input = GetComponent<FpsInput>();
        }

        public bool TryGetFrame(out PlayerLocomotionInput frame)
        {
            frame = default;
            if (_input == null)
                return false;

            frame.YawDelta = _input.LookDelta.x;
            frame.MoveAxes = _input.MoveAxes;
            frame.SprintHeld = _input.SprintHeld;
            frame.JumpPressedThisFrame = _input.JumpPressedThisFrame;
            frame.AimHeld = _input.AimHeld;
            frame.FireHeld = _input.FireHeld;
            frame.CrouchHeld = _input.CrouchHeld;
            frame.CrouchPressedThisFrame = _input.CrouchPressedThisFrame;
            frame.InteractPressedThisFrame = _input.InteractPressedThisFrame;
            return true;
        }
    }
}
