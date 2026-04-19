using FpsDemo.Match;
using Unity.Netcode;
using UnityEngine;

namespace FpsDemo.Netcode
{
    /// <summary>
    /// 参战者击杀数：服务器权威 <see cref="NetworkVariable{T}"/>，供所有客户端 HUD 排行读取；与 <see cref="MatchManager"/> 本地记分表同步递增。
    /// 挂在 Player 根（与 <see cref="NetworkObject"/>、<see cref="MatchParticipant"/> 同物体）。
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(NetworkObject))]
    [RequireComponent(typeof(MatchParticipant))]
    public sealed class PlayerMatchStatsNet : NetworkBehaviour
    {
        private readonly NetworkVariable<int> _networkKills = new NetworkVariable<int>(
            0,
            NetworkVariableReadPermission.Everyone,
            NetworkVariableWritePermission.Server);

        /// <summary>同步击杀数（客户端与服务器均可读）。</summary>
        public int NetworkKills => _networkKills.Value;

        /// <summary>仅服务器在 <see cref="MatchManager"/> 确认击杀后调用。</summary>
        internal void ServerNotifyKillScored()
        {
            if (!IsServer || !IsSpawned)
                return;
            _networkKills.Value++;
        }

        /// <summary>新对局（场景重载但玩家未重新 Spawn）时由服务器清零。</summary>
        internal void ServerResetKillsForNewRound()
        {
            if (!IsServer || !IsSpawned)
                return;
            _networkKills.Value = 0;
        }
    }
}
