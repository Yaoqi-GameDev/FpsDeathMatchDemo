using FpsDemo.Combat;
using Unity.Netcode;
using UnityEngine;

namespace FpsDemo.Netcode
{
    /// <summary>
    /// 联机时击杀只在服务器上进入 <see cref="CombatKillBus"/>；纯客户端收不到总线，连杀音效等依赖总线的逻辑会哑火。
    /// 在服务器击杀后由 <see cref="NetworkKillFeedBroadcaster"/> 调本组件，对凶手 <see cref="ClientRpc"/>，仅在非 Host 的 Owner 上补发一次本地 <see cref="CombatKillBus.Publish"/>。
    /// Host 已在服务器进程收到总线，RPC 内跳过，避免记分/连杀双触发。
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(NetworkObject))]
    public sealed class NetworkKillLocalSync : NetworkBehaviour
    {
        /// <summary>仅服务器：凶手为本物体时通知凶手客户端补发本地击杀事件。</summary>
        internal void ServerNotifyLocalKillFeedbackForOwner(in KillReport report)
        {
            if (!IsServer || !IsSpawned)
                return;
            if (report.Killer != gameObject)
                return;

            ulong victimId = ResolveNetworkObjectId(report.Victim);
            ulong killerId = ResolveNetworkObjectId(report.Killer);
            NotifyOwnerKillConfirmClientRpc(victimId, killerId);
        }

        private static ulong ResolveNetworkObjectId(GameObject go)
        {
            if (go == null)
                return 0;
            return go.TryGetComponent<NetworkObject>(out var no) ? no.NetworkObjectId : 0;
        }

        [ClientRpc]
        private void NotifyOwnerKillConfirmClientRpc(ulong victimNetworkObjectId, ulong killerNetworkObjectId)
        {
            if (!IsOwner)
                return;

            var nm = NetworkManager.Singleton;
            if (nm != null && nm.IsServer)
                return;

            if (nm == null || nm.SpawnManager == null)
                return;

            GameObject victimGo = TryGetSpawnedGameObject(victimNetworkObjectId);
            if (!nm.SpawnManager.SpawnedObjects.TryGetValue(killerNetworkObjectId, out var killerNo))
                return;

            CombatKillBus.Publish(new KillReport(victimGo, killerNo.gameObject));
        }

        private static GameObject TryGetSpawnedGameObject(ulong networkObjectId)
        {
            if (networkObjectId == 0)
                return null;
            var nm = NetworkManager.Singleton;
            if (nm == null || nm.SpawnManager == null)
                return null;
            return nm.SpawnManager.SpawnedObjects.TryGetValue(networkObjectId, out var no) ? no.gameObject : null;
        }
    }
}
