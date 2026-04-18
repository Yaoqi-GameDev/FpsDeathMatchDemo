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

        public void ApplyServerFrame(in PlayerLocomotionInput frame)
        {
            _frame = frame;
        }

        public bool TryGetFrame(out PlayerLocomotionInput frame)
        {
            frame = _frame;
            return true;
        }
    }
}
