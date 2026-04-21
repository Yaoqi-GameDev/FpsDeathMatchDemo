using UnityEngine;

namespace FpsDemo.Fps
{
    /// <summary>
    /// 单帧位移意图快照：<see cref="FpsPlayerMotor"/> 只消费本结构，不读键盘。
    /// 单机由 <see cref="FpsInputLocomotionSource"/> 从 <see cref="FpsInput"/> 组装；联机服务器由 <see cref="FpsDemo.Netcode.NetworkLocomotionBuffer"/> 从 RPC 写入。
    /// </summary>
    public struct PlayerLocomotionInput
    {
        /// <summary>
        /// 本步 <see cref="YawDelta"/> 施加前，身体绕世界 Y 的欧拉角（度）。
        /// 与联机包一并发往服务器，使服务器与本帧移动使用同一水平参考，减少仅靠增量积分带来的偏差。
        /// </summary>
        public float BodyYawY;

        public float YawDelta;
        public Vector2 MoveAxes;
        public bool SprintHeld;
        public bool JumpPressedThisFrame;
        public bool AimHeld;
        public bool FireHeld;
        public bool CrouchHeld;
        public bool CrouchPressedThisFrame;
        public bool InteractPressedThisFrame;

        /// <summary>联机客户端递增；单机恒为 0。用于预测与服务器状态对齐。</summary>
        public uint ClientTick;
    }
}
