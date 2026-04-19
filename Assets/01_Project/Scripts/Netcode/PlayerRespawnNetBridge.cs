using System.Collections;
using FpsDemo.Combat;
using FpsDemo.Fps;
using FpsDemo.Match;
using Unity.Netcode;
using UnityEngine;

namespace FpsDemo.Netcode
{
    /// <summary>
    /// 联机复活与开局出生：服务器从场景 <see cref="MatchSpawnPoints"/> 选点并传送，再满血、同步弹药；Owner 仅负责死亡表现与 <see cref="PlayerDeathRespawn"/> 收尾。
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(NetworkObject))]
    [DefaultExecutionOrder(10)]
    public sealed class PlayerRespawnNetBridge : NetworkBehaviour
    {
        private Health _health;
        private CharacterController _controller;
        private PlayerDeathRespawn _deathRespawn;
        private MatchSpawnPoints _cachedSpawnPoints;

        private void Awake()
        {
            _health = GetComponent<Health>();
            _controller = GetComponent<CharacterController>();
            _deathRespawn = GetComponent<PlayerDeathRespawn>();
        }

        public override void OnNetworkSpawn()
        {
            if (IsServer)
                StartCoroutine(ServerInitialSpawnNextFrame());
        }

        private IEnumerator ServerInitialSpawnNextFrame()
        {
            yield return null;
            if (!IsServer || !IsSpawned)
                yield break;

            var msp = ResolveMatchSpawnPoints();
            if (msp == null || !msp.HasAnyValidPoint)
                yield break;

            if (msp.TryPickSpawnPointForRespawn(gameObject, out Transform spawnTf) && spawnTf != null)
                ApplyServerTeleport(spawnTf.position, spawnTf.eulerAngles.y);
        }

        /// <summary>由 Owner 的 <see cref="PlayerDeathRespawn"/> 在延迟结束后调用。</summary>
        public void RequestRespawnFromOwner()
        {
            if (!IsOwner || !IsSpawned)
                return;
            RequestRespawnServerRpc();
        }

        [ServerRpc(RequireOwnership = true)]
        private void RequestRespawnServerRpc()
        {
            var msp = ResolveMatchSpawnPoints();
            if (msp != null && msp.HasAnyValidPoint &&
                msp.TryPickSpawnPointForRespawn(gameObject, out Transform spawnTf) && spawnTf != null)
            {
                ApplyServerTeleport(spawnTf.position, spawnTf.eulerAngles.y);
            }

            if (TryGetComponent<NetworkHealthBridge>(out var netHb))
                netHb.ServerReviveAndSyncNetworkHealth();
            else
                _health.ReviveFull();

            if (TryGetComponent<FpsPlayerMotor>(out var motor))
                motor.ResetStateForRespawn();

            if (TryGetComponent<FpsHitscanWeapon>(out var weapon))
                weapon.ApplyRespawnDefaultsOnServer();

            NotifyRespawnPresentationClientRpc();
        }

        [ClientRpc]
        private void NotifyRespawnPresentationClientRpc()
        {
            if (!IsOwner || _deathRespawn == null)
                return;
            _deathRespawn.FinishRespawnPresentationAfterServerAuthority();
        }

        private void ApplyServerTeleport(Vector3 worldPos, float yawDegrees)
        {
            if (_controller != null)
                _controller.enabled = false;

            transform.position = worldPos;
            Vector3 e = transform.eulerAngles;
            e.y = yawDegrees;
            transform.eulerAngles = e;

            if (_controller != null)
                _controller.enabled = true;
        }

        private MatchSpawnPoints ResolveMatchSpawnPoints()
        {
            if (_cachedSpawnPoints != null)
                return _cachedSpawnPoints;

            _cachedSpawnPoints = Object.FindObjectOfType<MatchSpawnPoints>();
            return _cachedSpawnPoints;
        }
    }
}
