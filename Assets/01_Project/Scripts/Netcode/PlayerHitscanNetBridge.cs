using FpsDemo.Combat;
using Unity.Netcode;
using UnityEngine;

namespace FpsDemo.Netcode
{
    /// <summary>
    /// Owner 将本帧射线发给服务器，由 <see cref="FpsHitscanWeapon.ServerResolveShot"/> 在服务端做 Hitscan 扣血。
    /// 挂在 Player 根（与 <see cref="NetworkObject"/>、<see cref="FpsHitscanWeapon"/> 同物体）。
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(NetworkObject))]
    [RequireComponent(typeof(HitscanWeaponAmmoSync))]
    public sealed class PlayerHitscanNetBridge : NetworkBehaviour
    {
        [Header("延迟补偿（仅服务器）")]
        [Tooltip("判伤前将目标位置回溯约该秒数（近似单向延迟）；需场景内有 MatchLagCompensationService。")]
        [SerializeField] private float _lagCompensationRewindSeconds = 0.1f;

        private FpsHitscanWeapon _weapon;
        private HitscanWeaponAmmoSync _ammoSync;

        private void Awake()
        {
            _weapon = GetComponent<FpsHitscanWeapon>();
            _ammoSync = GetComponent<HitscanWeaponAmmoSync>();
        }

        /// <summary>由 <see cref="FpsHitscanWeapon"/> 在 Owner 上调用。</summary>
        public void RequestHitscanShot(Ray ray, int weaponSlotIndex)
        {
            if (!IsOwner || !IsSpawned || _weapon == null)
                return;

            SubmitHitscanShotServerRpc(ray.origin, ray.direction, weaponSlotIndex);
        }

        [ServerRpc(RequireOwnership = true)]
        private void SubmitHitscanShotServerRpc(Vector3 origin, Vector3 direction, int weaponSlotIndex)
        {
            if (_weapon == null)
                _weapon = GetComponent<FpsHitscanWeapon>();
            if (_ammoSync == null)
                _ammoSync = GetComponent<HitscanWeaponAmmoSync>();
            if (_weapon == null || _ammoSync == null)
                return;

            if (!_ammoSync.ServerTryConsumeRound(weaponSlotIndex))
                return;

            var ray = new Ray(origin, direction.sqrMagnitude > 0.0001f ? direction.normalized : Vector3.forward);
            _weapon.ServerResolveShot(weaponSlotIndex, ray, _lagCompensationRewindSeconds, out _, out _);
        }
    }
}
