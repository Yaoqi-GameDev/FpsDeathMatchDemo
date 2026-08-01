using Unity.Netcode;
using UnityEngine;

namespace FpsDemo.Netcode
{
    /// <summary>
    /// 挂在 Lobby 场景的 NetworkManager 上：若已有常驻 Singleton，则销毁场景里这份，避免回大厅出现两个 NM / 两套导航。
    /// </summary>
    [DisallowMultipleComponent]
    [DefaultExecutionOrder(-400)]
    [RequireComponent(typeof(NetworkManager))]
    public sealed class LobbyNetworkManagerGuard : MonoBehaviour
    {
        private void Awake()
        {
            var nm = GetComponent<NetworkManager>();
            var existing = NetworkManager.Singleton;
            if (existing != null && existing != nm)
            {
                Debug.Log(
                    "[LobbyNetworkManagerGuard] Reusing existing NetworkManager; destroying scene duplicate.",
                    this);
                Destroy(gameObject);
            }
        }
    }
}
