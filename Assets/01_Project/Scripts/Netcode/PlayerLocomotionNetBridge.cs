using FpsDemo.Fps;
using Unity.Netcode;
using UnityEngine;

namespace FpsDemo.Netcode
{
    /// <summary>
    /// Owner 每帧从 <see cref="FpsInputLocomotionSource"/> 取样并 <see cref="ServerRpc"/> 到服务器写入 <see cref="NetworkLocomotionBuffer"/>。
    /// 可选：<b>客户端预测</b> — 纯客户端 Owner 本地跑 <see cref="FpsPlayerMotor"/>，并关闭本机 <c>NetworkTransform</c> 接收，避免被服务器位置覆盖；
    /// 服务器每帧写入权威位置/tick，Owner 偏差过大时拉回（最小校正）。
    /// </summary>
    [DefaultExecutionOrder(-10)]
    [DisallowMultipleComponent]
    [RequireComponent(typeof(NetworkObject))]
    [RequireComponent(typeof(NetworkLocomotionBuffer))]
    public sealed class PlayerLocomotionNetBridge : NetworkBehaviour
    {
        [Header("客户端预测（仅纯客户端 Owner）")]
        [Tooltip("开启后 Owner 客户端本地 CharacterController 与服务器同逻辑移动；Host 仍为纯服务器模拟。")]
        [SerializeField] private bool _clientPrediction = true;

        [Tooltip("预测位置与服务器权威位置超过该距离（米）时拉回。")]
        [SerializeField] private float _reconcilePositionThreshold = 0.75f;

        [SerializeField] private NetworkLocomotionBuffer _buffer;
        [SerializeField] private FpsPlayerMotor _motor;

        private uint _nextClientTick = 1;

        private readonly NetworkVariable<uint> _serverAuthorityTick = new NetworkVariable<uint>(
            0,
            NetworkVariableReadPermission.Everyone,
            NetworkVariableWritePermission.Server);

        private readonly NetworkVariable<float> _authorityPosX = new NetworkVariable<float>(
            0f,
            NetworkVariableReadPermission.Everyone,
            NetworkVariableWritePermission.Server);

        private readonly NetworkVariable<float> _authorityPosY = new NetworkVariable<float>(
            0f,
            NetworkVariableReadPermission.Everyone,
            NetworkVariableWritePermission.Server);

        private readonly NetworkVariable<float> _authorityPosZ = new NetworkVariable<float>(
            0f,
            NetworkVariableReadPermission.Everyone,
            NetworkVariableWritePermission.Server);

        private CharacterController _characterController;
        private bool _disabledNetworkTransformForPrediction;
        private FpsInputLocomotionSource _localSource;

        public bool ClientPredictionEnabled => _clientPrediction;

        private void Awake()
        {
            if (_buffer == null)
                _buffer = GetComponent<NetworkLocomotionBuffer>();
            if (_motor == null)
                _motor = GetComponent<FpsPlayerMotor>();
            _characterController = GetComponent<CharacterController>();
            _localSource = GetComponent<FpsInputLocomotionSource>();
        }

        public override void OnNetworkSpawn()
        {
            if (IsOwner && !IsServer && _clientPrediction)
                DisableLocalNetworkTransformIfNeeded();

            if (IsServer && IsSpawned)
            {
                Vector3 p = transform.position;
                _authorityPosX.Value = p.x;
                _authorityPosY.Value = p.y;
                _authorityPosZ.Value = p.z;
                _serverAuthorityTick.Value = 0;
            }
        }

        private void DisableLocalNetworkTransformIfNeeded()
        {
            foreach (var mb in GetComponents<MonoBehaviour>())
            {
                if (mb == null || mb == this)
                    continue;
                if (mb.GetType().Name != "NetworkTransform")
                    continue;
                mb.enabled = false;
                _disabledNetworkTransformForPrediction = true;
                break;
            }
        }

        private void Update()
        {
            if (!IsOwner || _buffer == null)
                return;

            if (_localSource == null || !_localSource.TryGetFrame(out var frame))
                return;

            uint tick = _nextClientTick++;

            SubmitLocomotionServerRpc(
                tick,
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

        private void LateUpdate()
        {
            if (!IsSpawned)
                return;

            if (IsServer)
            {
                Vector3 p = transform.position;
                _authorityPosX.Value = p.x;
                _authorityPosY.Value = p.y;
                _authorityPosZ.Value = p.z;
                _serverAuthorityTick.Value = _buffer.LastAppliedClientTick;
                return;
            }

            if (!IsOwner || !_clientPrediction || !_disabledNetworkTransformForPrediction)
                return;
            if (_serverAuthorityTick.Value == 0)
                return;

            Vector3 auth = new Vector3(_authorityPosX.Value, _authorityPosY.Value, _authorityPosZ.Value);
            float thr = Mathf.Max(0.05f, _reconcilePositionThreshold);
            if ((transform.position - auth).sqrMagnitude <= thr * thr)
                return;

            if (_characterController != null)
                _characterController.enabled = false;
            transform.position = auth;
            if (_characterController != null)
                _characterController.enabled = true;

            _motor?.ResetStateForRespawn();
        }

        [ServerRpc(RequireOwnership = true)]
        private void SubmitLocomotionServerRpc(
            uint clientTick,
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
                ClientTick = clientTick,
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
