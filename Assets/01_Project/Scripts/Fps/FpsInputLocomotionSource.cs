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
        private ParrelSyncClientStrafeBot _strafeBot;

        private void Awake()
        {
            if (_input == null)
                _input = GetComponent<FpsInput>();
            _strafeBot = GetComponent<ParrelSyncClientStrafeBot>();
        }

        public bool TryGetFrame(out PlayerLocomotionInput frame)
        {
            frame = default;
            if (_input == null)
                return false;

            frame.YawDelta = _input.LookDelta.x;
            // 早于本帧 FpsPlayerMotor.SimulationStep 采样，与服务器「先对齐 BodyYawY 再 Rotate(YawDelta)」一致。
            frame.BodyYawY = transform.eulerAngles.y;
            frame.MoveAxes = _input.MoveAxes;
            frame.SprintHeld = _input.SprintHeld;
            frame.JumpPressedThisFrame = _input.JumpPressedThisFrame;
            frame.AimHeld = _input.AimHeld;
            frame.FireHeld = _input.FireHeld;
            frame.CrouchHeld = _input.CrouchHeld;
            frame.CrouchPressedThisFrame = _input.CrouchPressedThisFrame;
            frame.InteractPressedThisFrame = _input.InteractPressedThisFrame;
            frame.ClientTick = 0;

            // 联机测试用假走位：必须在此覆盖（与 FpsInput 同帧先后无关），使 PlayerLocomotionNetBridge 的 ServerRpc 与 FpsPlayerMotor 读到同一组 Move/Sprint。
            if (_strafeBot == null)
                _strafeBot = GetComponent<ParrelSyncClientStrafeBot>();
            if (_strafeBot != null
                && _input.GameplayInputEnabled
                && _strafeBot.ShouldInjectLocomotion)
            {
                frame.MoveAxes = _strafeBot.GetSyntheticMoveAxes();
                frame.SprintHeld = _strafeBot.SyntheticSprintHeld;
            }

            return true;
        }
    }
}
