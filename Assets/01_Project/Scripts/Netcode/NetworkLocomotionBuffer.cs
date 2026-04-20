using FpsDemo.Fps;
using UnityEngine;

namespace FpsDemo.Netcode
{
    /// <summary>
    /// 仅在服务器上作为 <see cref="ILocomotionInputSource"/>：由 <see cref="PlayerLocomotionNetBridge"/> 的 ServerRpc 写入本帧快照。
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class NetworkLocomotionBuffer : MonoBehaviour, ILocomotionInputSource
    {
        private PlayerLocomotionInput _frame;

        /// <summary>服务器上一帧从 RPC 写入的 <see cref="PlayerLocomotionInput.ClientTick"/>。</summary>
        public uint LastAppliedClientTick { get; private set; }

        public void ApplyServerFrame(in PlayerLocomotionInput frame)
        {
            _frame = frame;
            LastAppliedClientTick = frame.ClientTick;
        }

        public bool TryGetFrame(out PlayerLocomotionInput frame)
        {
            frame = _frame;
            return true;
        }
    }
}
