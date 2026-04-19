using FpsDemo.Combat;
using Unity.Netcode;
using UnityEngine;

namespace FpsDemo.Netcode
{
    /// <summary>
    /// 联机时 Hitscan 弹匣/备弹由服务器持有并通过 <see cref="NetworkVariable{T}"/> 同步（最多 4 槽）。
    /// 与 <see cref="PlayerHitscanNetBridge"/>、<see cref="FpsHitscanWeapon"/> 同挂在 Player 根。
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(NetworkObject))]
    [RequireComponent(typeof(FpsHitscanWeapon))]
    public sealed class HitscanWeaponAmmoSync : NetworkBehaviour
    {
        public const int MaxSyncedSlots = 4;

        private FpsHitscanWeapon _weapon;

        private readonly NetworkVariable<int> _mag0 = new NetworkVariable<int>(0, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);
        private readonly NetworkVariable<int> _mag1 = new NetworkVariable<int>(0, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);
        private readonly NetworkVariable<int> _mag2 = new NetworkVariable<int>(0, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);
        private readonly NetworkVariable<int> _mag3 = new NetworkVariable<int>(0, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);
        private readonly NetworkVariable<int> _res0 = new NetworkVariable<int>(0, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);
        private readonly NetworkVariable<int> _res1 = new NetworkVariable<int>(0, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);
        private readonly NetworkVariable<int> _res2 = new NetworkVariable<int>(0, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);
        private readonly NetworkVariable<int> _res3 = new NetworkVariable<int>(0, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);

        private void Awake()
        {
            _weapon = GetComponent<FpsHitscanWeapon>();
        }

        public override void OnNetworkSpawn()
        {
            if (IsServer)
                ServerInitializeFromWeaponConfig();
        }

        /// <summary>读同步弹匣（仅联机生成后有意义）。</summary>
        public int GetMagazine(int slotIndex)
        {
            return slotIndex switch
            {
                0 => _mag0.Value,
                1 => _mag1.Value,
                2 => _mag2.Value,
                3 => _mag3.Value,
                _ => 0
            };
        }

        /// <summary>读同步备弹。</summary>
        public int GetReserve(int slotIndex)
        {
            return slotIndex switch
            {
                0 => _res0.Value,
                1 => _res1.Value,
                2 => _res2.Value,
                3 => _res3.Value,
                _ => 0
            };
        }

        /// <summary>服务器：尝试扣一发；失败则不应解析命中。</summary>
        public bool ServerTryConsumeRound(int slotIndex)
        {
            if (!IsServer || slotIndex < 0 || slotIndex >= MaxSyncedSlots)
                return false;

            int m = GetMagazine(slotIndex);
            if (m <= 0)
                return false;

            SetMagazineServer(slotIndex, m - 1);
            return true;
        }

        private void ServerInitializeFromWeaponConfig()
        {
            if (_weapon == null)
                return;

            int n = Mathf.Min(MaxSyncedSlots, _weapon.SlotCount);
            if (_weapon.SlotCount > MaxSyncedSlots)
                Debug.LogWarning($"HitscanWeaponAmmoSync: 仅同步前 {MaxSyncedSlots} 个武器槽，当前 SlotCount={_weapon.SlotCount}。", this);

            for (int i = 0; i < MaxSyncedSlots; i++)
            {
                SetMagazineServer(i, 0);
                SetReserveServer(i, 0);
            }

            for (int i = 0; i < n; i++)
            {
                SetMagazineServer(i, _weapon.GetMagazineCapacityForSlot(i));
                SetReserveServer(i, _weapon.GetStartingReserveForSlot(i));
            }
        }

        private void SetMagazineServer(int slotIndex, int value)
        {
            value = Mathf.Max(0, value);
            switch (slotIndex)
            {
                case 0: _mag0.Value = value; break;
                case 1: _mag1.Value = value; break;
                case 2: _mag2.Value = value; break;
                case 3: _mag3.Value = value; break;
            }
        }

        private void SetReserveServer(int slotIndex, int value)
        {
            value = Mathf.Max(0, value);
            switch (slotIndex)
            {
                case 0: _res0.Value = value; break;
                case 1: _res1.Value = value; break;
                case 2: _res2.Value = value; break;
                case 3: _res3.Value = value; break;
            }
        }

        /// <summary>换弹计时结束：仅 Owner 发 Rpc，服务器补弹。</summary>
        public void RequestApplyReload(int slotIndex)
        {
            if (!IsOwner)
                return;
            ApplyReloadServerRpc(slotIndex);
        }

        [ServerRpc(RequireOwnership = true)]
        private void ApplyReloadServerRpc(int slotIndex)
        {
            ServerApplyReload(slotIndex);
        }

        private void ServerApplyReload(int slotIndex)
        {
            if (_weapon == null || slotIndex < 0 || slotIndex >= _weapon.SlotCount || slotIndex >= MaxSyncedSlots)
                return;

            int cap = _weapon.GetMagazineCapacityForSlot(slotIndex);
            int mag = GetMagazine(slotIndex);
            int res = GetReserve(slotIndex);
            int need = cap - mag;
            if (need <= 0 || res <= 0)
                return;

            int take = Mathf.Min(need, res);
            SetMagazineServer(slotIndex, mag + take);
            SetReserveServer(slotIndex, res - take);
        }

        /// <summary>复活等：本地玩家重置弹药时由 Owner 发 Rpc。</summary>
        public void RequestResetAmmoFromOwner()
        {
            if (!IsOwner)
                return;
            ResetAmmoToConfigServerRpc();
        }

        [ServerRpc(RequireOwnership = true)]
        private void ResetAmmoToConfigServerRpc()
        {
            ServerInitializeFromWeaponConfig();
        }

        /// <summary>仅服务器：按配置重填 NV（复活等，不发 Owner Rpc）。</summary>
        public void ServerReinitializeAmmoFromConfig()
        {
            if (!IsServer)
                return;
            ServerInitializeFromWeaponConfig();
        }
    }
}
