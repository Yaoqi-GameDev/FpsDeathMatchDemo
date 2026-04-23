using Unity.Netcode;
using UnityEngine;

namespace FpsDemo.Fps
{
    /// <summary>
    /// 多开编辑器（如 ParrelSync：主机 + 多个客户端）联机测试时，在<b>其中一个</b>客户端的玩家上勾选启用，
    /// 使该客户端自动左右平移（可选按住疾跑），便于当活动靶。与 <see cref="FpsInput"/> 同物体；实际注入在
    /// <see cref="FpsInputLocomotionSource.TryGetFrame"/>。须加在 <b>NetworkManager 会生成的 Player 预制体</b>上（只改场景里不生成的那一份无效）；
    /// 并勾选 <see cref="_enableAutoStrafe"/>。纯客户端本机要能动：<see cref="FpsDemo.Netcode.PlayerLocomotionNetBridge"/> 的 Client Prediction 须开启。
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(FpsInput))]
    public sealed class ParrelSyncClientStrafeBot : MonoBehaviour
    {
        [Header("启用")]
        [Tooltip("只在希望当「自动走位靶子」的那一个编辑器窗口里勾选；其它克隆保持关闭。")]
        [SerializeField] private bool _enableAutoStrafe;

        [Tooltip("联机时仅在本机拥有该 NetworkObject 时注入。未开 Host/Client 时（NetworkObject 可能尚未 Spawn）仍会注入。纯单机无 NetworkObject 时也可不勾选。")]
        [SerializeField] private bool _onlyIfNetworkOwner = true;

        [Header("走位")]
        [SerializeField] private float _strafePeriodSeconds = 4f;
        [SerializeField] private bool _holdSprint = true;

        private NetworkObject _netObject;

        private void Awake()
        {
            _netObject = GetComponent<NetworkObject>();
        }

        /// <summary>由 <see cref="FpsInputLocomotionSource"/> 在 <c>TryGetFrame</c> 里查询。</summary>
        public bool ShouldInjectLocomotion =>
            _enableAutoStrafe
            && isActiveAndEnabled
            && MayRunForCurrentNetworkRole();

        private bool MayRunForCurrentNetworkRole()
        {
            if (!_onlyIfNetworkOwner)
                return true;
            if (_netObject == null)
                return true;

            var nm = NetworkManager.Singleton;
            bool inSession = nm != null && (nm.IsClient || nm.IsServer);
            if (!inSession)
                return true;

            return _netObject.IsSpawned && _netObject.IsOwner;
        }

        /// <summary>平面移动：水平 -1~1，前后为 0。</summary>
        public Vector2 GetSyntheticMoveAxes()
        {
            if (_strafePeriodSeconds <= 0.01f)
                return Vector2.zero;
            float angular = Mathf.PI * 2f / _strafePeriodSeconds;
            float h = Mathf.Sin(Time.time * angular);
            return new Vector2(h, 0f);
        }

        public bool SyntheticSprintHeld => _holdSprint;
    }
}
