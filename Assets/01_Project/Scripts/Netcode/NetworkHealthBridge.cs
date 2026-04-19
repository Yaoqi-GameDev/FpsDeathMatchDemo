using FpsDemo.Combat;
using Unity.Netcode;
using UnityEngine;

namespace FpsDemo.Netcode
{
    /// <summary>
    /// 联机血量权威：服务器写入 <see cref="NetworkVariable{T}"/>，客户端将数值镜像到 <see cref="Health"/>（供 HUD / 受击 / 死亡表现）。
    /// 服务器侧任意对 <see cref="Health"/> 的扣血应最终反映到 <see cref="NetworkCurrent"/>（本组件订阅 <see cref="Health.Damaged"/>）。
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(NetworkObject))]
    [RequireComponent(typeof(Health))]
    public sealed class NetworkHealthBridge : NetworkBehaviour
    {
        private Health _health;

        private readonly NetworkVariable<float> _networkCurrent = new NetworkVariable<float>(
            100f,
            NetworkVariableReadPermission.Everyone,
            NetworkVariableWritePermission.Server);

        /// <summary>与服务器同步的当前血量（未生成时可读本地 <see cref="Health"/>）。</summary>
        public float NetworkCurrent => _networkCurrent.Value;

        private void Awake()
        {
            _health = GetComponent<Health>();
        }

        public override void OnNetworkSpawn()
        {
            _networkCurrent.OnValueChanged += OnNetworkHealthChanged;

            if (IsServer)
            {
                _health.Damaged += OnServerHealthDamaged;
                _networkCurrent.Value = _health.Current;
            }
        }

        public override void OnNetworkDespawn()
        {
            _networkCurrent.OnValueChanged -= OnNetworkHealthChanged;

            if (IsServer && _health != null)
                _health.Damaged -= OnServerHealthDamaged;
        }

        private void OnServerHealthDamaged(float _, GameObject __)
        {
            _networkCurrent.Value = _health.Current;
        }

        private void OnNetworkHealthChanged(float previous, float current)
        {
            if (IsServer)
                return;

            _health.ApplyMirrorFromNetwork(current);
        }

        /// <summary>本地 <see cref="PlayerDeathRespawn"/> 在 <see cref="Health.ReviveFull"/> 之后调用，使服务器血量与 NV 与客户端一致。</summary>
        public void NotifyLocalReviveAfterDeath()
        {
            var nm = NetworkManager.Singleton;
            if (nm == null || !nm.IsListening || !IsSpawned)
                return;

            if (IsServer)
            {
                _networkCurrent.Value = _health.Current;
                return;
            }

            RequestReviveSyncServerRpc();
        }

        [ServerRpc(RequireOwnership = true)]
        private void RequestReviveSyncServerRpc()
        {
            _health.ReviveFull();
            _networkCurrent.Value = _health.Current;
        }

#if UNITY_EDITOR || DEVELOPMENT_BUILD
        private void Update()
        {
            // 仅「本机拥有的玩家」处理 F9：Host 上会跑所有玩家的 NetworkBehaviour，否则会一次按键给全场玩家扣血。
            if (!IsServer || !IsSpawned || !IsOwner)
                return;

            if (Input.GetKeyDown(KeyCode.F9))
                DebugApplyDamageServer(10f);
        }
#endif

        /// <summary>仅服务器：对 <see cref="Health"/> 扣血并同步 NV（编辑器/Development 下按 F9 亦调用）。</summary>
        public void DebugApplyDamageServer(float amount)
        {
            if (!IsServer || amount <= 0f)
                return;

            _health.ApplyDamage(amount, null);
        }
    }
}
