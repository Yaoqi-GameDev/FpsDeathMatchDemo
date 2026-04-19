using FpsDemo.Combat;
using FpsDemo.Match;
using FpsDemo.UI;
using Unity.Netcode;
using UnityEngine;

namespace FpsDemo.Netcode
{
    /// <summary>
    /// 联机击杀播报：仅在服务器订阅 <see cref="CombatKillBus"/>，通过 <see cref="ClientRpc"/> 让含 Host 在内的所有客户端刷新右上（与单机总线分离，避免纯客户端收不到事件）。
    /// 场景内空物体挂 <see cref="NetworkObject"/> + 本脚本（勿放在 Player 预制体上）。
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(NetworkObject))]
    public sealed class NetworkKillFeedBroadcaster : NetworkBehaviour
    {
        private void OnNetworkKillCommitted(KillReport report)
        {
            if (!IsServer)
                return;

            string killer = ResolveDisplayName(report.Killer);
            string victim = ResolveDisplayName(report.Victim);
            BroadcastKillFeedClientRpc(killer, victim);
        }

        public override void OnNetworkSpawn()
        {
            if (IsServer)
                CombatKillBus.KillCommitted += OnNetworkKillCommitted;
        }

        public override void OnNetworkDespawn()
        {
            if (IsServer)
                CombatKillBus.KillCommitted -= OnNetworkKillCommitted;
        }

        [ClientRpc]
        private void BroadcastKillFeedClientRpc(string killerDisplayName, string victimDisplayName)
        {
            DeathmatchHudView.AppendKillFeedFromNetwork(killerDisplayName, victimDisplayName);
        }

        private static string ResolveDisplayName(GameObject go)
        {
            if (go == null)
                return "?";

            var mp = go.GetComponent<MatchParticipant>();
            if (mp != null)
                return mp.DisplayName;

            return go.name;
        }
    }
}
