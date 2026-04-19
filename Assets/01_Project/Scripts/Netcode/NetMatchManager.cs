using FpsDemo.Match;
using Unity.Netcode;
using UnityEngine;

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
            if (Instance == this)
                Instance = null;
        }

        private void OnDestroy()
        {
            if (Instance == this)
                Instance = null;
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
    }
}
