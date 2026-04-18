using FpsDemo.Fps;
using Unity.Netcode;
using UnityEngine;

namespace FpsDemo.Netcode
{
    /// <summary>
    /// Owner 每帧从 <see cref="FpsInputLocomotionSource"/> 取样并 <see cref="ServerRpc"/> 到服务器写入 <see cref="NetworkLocomotionBuffer"/>。
    /// </summary>
    [DefaultExecutionOrder(-10)]
    [DisallowMultipleComponent]
    [RequireComponent(typeof(NetworkObject))]
    [RequireComponent(typeof(NetworkLocomotionBuffer))]
    public sealed class PlayerLocomotionNetBridge : NetworkBehaviour
    {
        [SerializeField] private FpsInputLocomotionSource _localSource;
        [SerializeField] private NetworkLocomotionBuffer _buffer;

        private void Awake()
        {
            if (_localSource == null)
                _localSource = GetComponent<FpsInputLocomotionSource>();
            if (_buffer == null)
                _buffer = GetComponent<NetworkLocomotionBuffer>();
        }

        private void Update()
        {
            if (!IsOwner || _localSource == null || _buffer == null)
                return;

            if (!_localSource.TryGetFrame(out var frame))
                return;

            SubmitLocomotionServerRpc(
                frame.YawDelta,
                frame.MoveAxes,
                frame.SprintHeld,
                frame.JumpPressedThisFrame,
                frame.AimHeld,
                frame.FireHeld,
                frame.CrouchHeld,
                frame.CrouchPressedThisFrame,
                frame.InteractPressedThisFrame);
        }

        [ServerRpc(RequireOwnership = true)]
        private void SubmitLocomotionServerRpc(
            float yawDelta,
            Vector2 moveAxes,
            bool sprintHeld,
            bool jumpPressed,
            bool aimHeld,
            bool fireHeld,
            bool crouchHeld,
            bool crouchPressed,
            bool interactPressed)
        {
            _buffer.ApplyServerFrame(new PlayerLocomotionInput
            {
                YawDelta = yawDelta,
                MoveAxes = moveAxes,
                SprintHeld = sprintHeld,
                JumpPressedThisFrame = jumpPressed,
                AimHeld = aimHeld,
                FireHeld = fireHeld,
                CrouchHeld = crouchHeld,
                CrouchPressedThisFrame = crouchPressed,
                InteractPressedThisFrame = interactPressed
            });
        }
    }
}
