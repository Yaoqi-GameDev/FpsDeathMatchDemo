using System.Collections;
using System.Collections.Generic;
using FpsDemo.Match;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace FpsDemo.Netcode
{
    /// <summary>
    /// Server-authoritative match timer and ended flag for online play. Clients read <see cref="NetworkVariable{T}"/> only.
    /// Co-located on the same GameObject as <see cref="MatchManager"/> in DeathMatch (scene-placed NetworkObject).
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(NetworkObject))]
    [DefaultExecutionOrder(-15)]
    public sealed class NetMatchManager : NetworkBehaviour
    {
        public static NetMatchManager Instance { get; private set; }

        /// <summary>
        /// NGO 重载 DeathMatch 时，玩家 <see cref="NetworkObject"/> 往往不会再次 <see cref="NetworkBehaviour.OnNetworkSpawn"/>，
        /// 需在 <see cref="NetworkSceneManager.OnLoadEventCompleted"/> 里对全体玩家做一次「新对局」传送与状态重置。
        /// </summary>
        private static bool _pendingRematchPlayerResetAfterLoad;

        private static bool _pendingRematchResetConsumed;

        private readonly NetworkVariable<float> _remainingSeconds = new NetworkVariable<float>(
            300f,
            NetworkVariableReadPermission.Everyone,
            NetworkVariableWritePermission.Server);

        private readonly NetworkVariable<bool> _matchEnded = new NetworkVariable<bool>(
            false,
            NetworkVariableReadPermission.Everyone,
            NetworkVariableWritePermission.Server);

        /// <summary>True when NGO session is active and this scene object is spawned (replicated).</summary>
        public static bool ControlsMatchTimer =>
            NetworkManager.Singleton != null &&
            NetworkManager.Singleton.IsListening &&
            Instance != null &&
            Instance.IsSpawned;

        public float RemainingSecondsSynced => _remainingSeconds.Value;

        public bool MatchEndedSynced => _matchEnded.Value;

        public override void OnNetworkSpawn()
        {
            Instance = this;

            if (NetworkManager.Singleton != null && NetworkManager.Singleton.SceneManager != null)
                NetworkManager.Singleton.SceneManager.OnLoadEventCompleted += OnNetcodeSceneLoadCompleted;

            if (!IsServer)
                return;

            _matchEnded.Value = false;
            float duration = 300f;
            if (MatchManager.Instance != null)
                duration = Mathf.Max(0f, MatchManager.Instance.ConfiguredMatchDurationSeconds);
            _remainingSeconds.Value = duration;
        }

        public override void OnNetworkDespawn()
        {
            if (NetworkManager.Singleton != null && NetworkManager.Singleton.SceneManager != null)
                NetworkManager.Singleton.SceneManager.OnLoadEventCompleted -= OnNetcodeSceneLoadCompleted;

            if (Instance == this)
                Instance = null;
        }

        private void OnDestroy()
        {
            if (NetworkManager.Singleton != null && NetworkManager.Singleton.SceneManager != null)
                NetworkManager.Singleton.SceneManager.OnLoadEventCompleted -= OnNetcodeSceneLoadCompleted;

            if (Instance == this)
                Instance = null;
        }

        /// <summary>
        /// <see cref="MatchManager.Start"/> 兜底：若 NGO 回调顺序导致未在 LoadCompleted 中消费，仍在本场景就绪后再试一次。
        /// </summary>
        internal static void NotifyMatchSceneReadyForPossibleRematchReset()
        {
            if (Instance == null || !Instance.IsServer || !Instance.IsSpawned)
                return;
            Instance.TryConsumePendingRematchPlayerResets();
        }

        private void OnNetcodeSceneLoadCompleted(
            string sceneName,
            LoadSceneMode loadSceneMode,
            List<ulong> clientsCompleted,
            List<ulong> clientsTimedOut)
        {
            if (!IsServer || !IsSpawned)
                return;
            if (sceneName != LobbyNetSession.MatchSceneName)
                return;
            TryConsumePendingRematchPlayerResets();
        }

        private void TryConsumePendingRematchPlayerResets()
        {
            if (!_pendingRematchPlayerResetAfterLoad || _pendingRematchResetConsumed)
                return;
            if (!IsServer || !IsSpawned)
                return;

            _pendingRematchResetConsumed = true;
            _pendingRematchPlayerResetAfterLoad = false;
            StartCoroutine(ServerRematchTeleportAllConnectedPlayers());
        }

        private IEnumerator ServerRematchTeleportAllConnectedPlayers()
        {
            const int maxFrames = 120;
            for (var i = 0; i < maxFrames; i++)
            {
                yield return null;
                if (!IsServer || !IsSpawned)
                    yield break;

                var nm = NetworkManager.Singleton;
                if (nm == null)
                    yield break;

                MatchSpawnPoints msp;
#if UNITY_2022_3_OR_NEWER
                msp = Object.FindFirstObjectByType<MatchSpawnPoints>(FindObjectsInactive.Exclude);
#else
                msp = Object.FindObjectOfType<MatchSpawnPoints>();
#endif
                if (msp == null || !msp.HasAnyValidPoint)
                    continue;

                foreach (var client in nm.ConnectedClients.Values)
                {
                    var playerObject = client.PlayerObject;
                    if (playerObject == null)
                        continue;
                    var bridge = playerObject.GetComponent<PlayerRespawnNetBridge>();
                    bridge?.ServerApplyFullMatchRoundReset(msp);
                }

                yield break;
            }

            Debug.LogWarning(
                "[NetMatchManager] Rematch: MatchSpawnPoints not ready — players may keep previous positions. " +
                "Check DeathMatch has MatchSpawnPoints with valid transforms.",
                this);
        }

        private void Update()
        {
            if (!IsServer || !IsSpawned || _matchEnded.Value)
                return;

            float r = _remainingSeconds.Value - Time.unscaledDeltaTime;
            if (r <= 0f)
            {
                _remainingSeconds.Value = 0f;
                MatchManager.Instance?.NotifyAuthorityTimeExpiredFromNetwork();
            }
            else
            {
                _remainingSeconds.Value = r;
            }
        }

        /// <summary>Called from <see cref="MatchManager"/> when the server ends the match (time, kills, etc.).</summary>
        public void MarkEndedOnServer()
        {
            if (!IsServer || !IsSpawned)
                return;
            _matchEnded.Value = true;
        }

        /// <summary>
        /// Reload DeathMatch for everyone. Must use <see cref="NetworkManager.SceneManager"/> while online
        /// (same as <see cref="LobbyNetSession.TryStartHostAndLoadMatch"/>); plain <see cref="SceneManager.LoadScene"/> does not sync clients.
        /// </summary>
        public void RequestRestartMatch()
        {
            Time.timeScale = 1f;

            if (!IsSpawned)
            {
                SceneManager.LoadScene(LobbyNetSession.MatchSceneBuildIndex);
                return;
            }

            var nm = NetworkManager.Singleton;
            if (nm == null || !nm.IsListening)
            {
                SceneManager.LoadScene(LobbyNetSession.MatchSceneBuildIndex);
                return;
            }

            if (nm.IsServer)
            {
                MarkPendingRematchPlayerReset();
                LoadDeathMatchSceneAsServer(nm);
                return;
            }

            RequestRestartMatchServerRpc();
        }

        private static void MarkPendingRematchPlayerReset()
        {
            _pendingRematchPlayerResetAfterLoad = true;
            _pendingRematchResetConsumed = false;
        }

        private static void LoadDeathMatchSceneAsServer(NetworkManager nm)
        {
            nm.SceneManager.LoadScene(LobbyNetSession.MatchSceneName, LoadSceneMode.Single);
        }

        [ServerRpc(RequireOwnership = false)]
        private void RequestRestartMatchServerRpc(ServerRpcParams serverRpcParams = default)
        {
            if (!IsServer || !IsSpawned)
                return;

            var nm = NetworkManager.Singleton;
            if (nm == null || !nm.IsListening)
                return;

            MarkPendingRematchPlayerReset();
            LoadDeathMatchSceneAsServer(nm);
        }
    }
}
