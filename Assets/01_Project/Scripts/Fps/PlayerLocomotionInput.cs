using UnityEngine;

namespace FpsDemo.Fps
{
    /// <summary>
    /// 单帧位移意图快照：<see cref="FpsPlayerMotor"/> 只消费本结构，不读键盘。
    /// 单机由 <see cref="FpsInputLocomotionSource"/> 从 <see cref="FpsInput"/> 组装；联机服务器由 <see cref="FpsDemo.Netcode.NetworkLocomotionBuffer"/> 从 RPC 写入。
    /// </summary>
    public struct PlayerLocomotionInput
    {
        public float YawDelta;
        public Vector2 MoveAxes;
        public bool SprintHeld;
        public bool JumpPressedThisFrame;
        public bool AimHeld;
        public bool FireHeld;
        public bool CrouchHeld;
        public bool CrouchPressedThisFrame;
        public bool InteractPressedThisFrame;
    }
}
