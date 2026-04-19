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
            _cachedSpawnPoints = null;
            if (IsServer)
                StartCoroutine(ServerInitialSpawnAfterSceneReady());
        }

        /// <summary>
        /// Host loading DeathMatch from Lobby uses NGO scene load (async). One frame was often too early:
        /// <see cref="MatchSpawnPoints"/> is not in the hierarchy yet, <see cref="Object.FindObjectOfType{T}"/> returns null
        /// and spawn teleport is skipped. Direct-play DeathMatch + StartHost had the scene already loaded — behaviour differed.
        /// </summary>
        private IEnumerator ServerInitialSpawnAfterSceneReady()
        {
            const int maxWaitFrames = 120;
            for (var i = 0; i < maxWaitFrames; i++)
            {
                yield return null;
                if (!IsServer || !IsSpawned)
                    yield break;

                var msp = ResolveMatchSpawnPoints();
                if (msp == null || !msp.HasAnyValidPoint)
                    continue;

                if (msp.TryPickSpawnPointForRespawn(gameObject, out Transform spawnTf) && spawnTf != null)
                    ApplyServerTeleport(spawnTf.position, spawnTf.eulerAngles.y);
                yield break;
            }

            Debug.LogWarning(
                "PlayerRespawnNetBridge: MatchSpawnPoints not found or empty after waiting; initial spawn position was not applied. " +
                "Ensure DeathMatch contains an active MatchSpawnPoints with spawn transforms.",
                this);
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
            ServerApplyFullMatchRoundReset(ResolveMatchSpawnPoints());
        }

        /// <summary>
        /// 服务器：传送 + 击杀 NV 清零 + 满血与弹药 + Owner 表现收尾。
        /// NGO 重载同一场景时玩家物体常不再次 <see cref="OnNetworkSpawn"/>，需由 <see cref="NetMatchManager"/> 在 <see cref="NetworkSceneManager.OnLoadEventCompleted"/> 统一调用。
        /// </summary>
        internal void ServerApplyFullMatchRoundReset(MatchSpawnPoints msp)
        {
            if (!IsServer || !IsSpawned)
                return;

            if (msp != null && msp.HasAnyValidPoint &&
                msp.TryPickSpawnPointForRespawn(gameObject, out Transform spawnTf) && spawnTf != null)
            {
                ApplyServerTeleport(spawnTf.position, spawnTf.eulerAngles.y);
            }

            if (TryGetComponent<PlayerMatchStatsNet>(out var statsNet))
                statsNet.ServerResetKillsForNewRound();

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

#if UNITY_2022_3_OR_NEWER
            _cachedSpawnPoints = Object.FindFirstObjectByType<MatchSpawnPoints>(FindObjectsInactive.Exclude);
#else
            _cachedSpawnPoints = Object.FindObjectOfType<MatchSpawnPoints>();
#endif
            return _cachedSpawnPoints;
        }
    }
}
