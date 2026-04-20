using FpsDemo.Fps;
using Unity.Netcode;
using UnityEngine;

namespace FpsDemo.Netcode
{
    /// <summary>
    /// Owner 每帧从 <see cref="FpsInputLocomotionSource"/> 取样并 <see cref="ServerRpc"/> 到服务器写入 <see cref="NetworkLocomotionBuffer"/>。
    /// 可选：<b>客户端预测</b> — 纯客户端 Owner 本地跑 <see cref="FpsPlayerMotor"/>，并关闭本机 <c>NetworkTransform</c> 接收；
    /// 服务器同步权威位置/速度/tick，Owner 按误差分级：忽略 / 平滑 / 仅运动学校正 / 硬重置。
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

        [Header("校正分级（米 / 纯客户端 Owner）")]
        [Tooltip("小于该距离差：不校正。")]
        [SerializeField] private float _reconcileIgnoreBelow = 0.06f;

        [Tooltip("在 (忽略, 本值] 内：每帧向权威位置/速度插值（软跟）。")]
        [SerializeField] private float _reconcileBlendUntil = 0.32f;

        [Tooltip("大于该距离：硬同步并重置滑铲/梯子等到安全状态（橡皮筋最明显时触发）。")]
        [SerializeField] private float _reconcileHardResetAbove = 0.85f;

        [Tooltip("软跟时，每帧朝权威位置插值的系数（越大跟得越紧）。")]
        [SerializeField] private float _blendPositionSharpness = 10f;

        [Tooltip("软跟时，速度朝权威速度插值的系数。")]
        [SerializeField] private float _blendVelocitySharpness = 8f;

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

        private readonly NetworkVariable<float> _authorityVelX = new NetworkVariable<float>(
            0f,
            NetworkVariableReadPermission.Everyone,
            NetworkVariableWritePermission.Server);

        private readonly NetworkVariable<float> _authorityVelY = new NetworkVariable<float>(
            0f,
            NetworkVariableReadPermission.Everyone,
            NetworkVariableWritePermission.Server);

        private readonly NetworkVariable<float> _authorityVelZ = new NetworkVariable<float>(
            0f,
            NetworkVariableReadPermission.Everyone,
            NetworkVariableWritePermission.Server);

        private bool _disabledNetworkTransformForPrediction;
        private FpsInputLocomotionSource _localSource;

        public bool ClientPredictionEnabled => _clientPrediction;

        private void Awake()
        {
            if (_buffer == null)
                _buffer = GetComponent<NetworkLocomotionBuffer>();
            if (_motor == null)
                _motor = GetComponent<FpsPlayerMotor>();
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
                if (_motor != null)
                {
                    Vector3 v = _motor.LocomotionVelocity;
                    _authorityVelX.Value = v.x;
                    _authorityVelY.Value = v.y;
                    _authorityVelZ.Value = v.z;
                }

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
                if (_motor != null)
                {
                    Vector3 v = _motor.LocomotionVelocity;
                    _authorityVelX.Value = v.x;
                    _authorityVelY.Value = v.y;
                    _authorityVelZ.Value = v.z;
                }

                _serverAuthorityTick.Value = _buffer.LastAppliedClientTick;
                return;
            }

            if (!IsOwner || !_clientPrediction || !_disabledNetworkTransformForPrediction || _motor == null)
                return;
            if (_serverAuthorityTick.Value == 0)
                return;

            Vector3 authPos = new Vector3(_authorityPosX.Value, _authorityPosY.Value, _authorityPosZ.Value);
            Vector3 authVel = new Vector3(_authorityVelX.Value, _authorityVelY.Value, _authorityVelZ.Value);

            float err = Vector3.Distance(transform.position, authPos);

            float ign = Mathf.Max(0.001f, _reconcileIgnoreBelow);
            float blendEnd = Mathf.Max(ign + 0.001f, _reconcileBlendUntil);
            float hard = Mathf.Max(blendEnd + 0.001f, _reconcileHardResetAbove);

            if (err <= ign)
                return;

            float dt = Time.deltaTime;

            if (err <= blendEnd)
            {
                float tPos = 1f - Mathf.Exp(-_blendPositionSharpness * dt);
                float tVel = 1f - Mathf.Exp(-_blendVelocitySharpness * dt);
                Vector3 pos = Vector3.Lerp(transform.position, authPos, tPos);
                Vector3 vel = Vector3.Lerp(_motor.LocomotionVelocity, authVel, tVel);
                _motor.ApplyAuthoritativeKinematics(pos, vel);
                return;
            }

            if (err <= hard)
            {
                _motor.ApplyAuthoritativeKinematics(authPos, authVel);
                return;
            }

            _motor.ApplyAuthoritativeHardResync(authPos, authVel);
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
