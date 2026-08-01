using TMPro;
using Unity.Netcode;
using UnityEngine;

namespace FpsDemo.UI
{
    /// <summary>
    /// 显示本机到服务器的往返延迟（RTT，毫秒），数据来自 <see cref="Unity.Netcode.NetworkTransport.GetCurrentRtt"/>。
    /// 挂在 HUD Panel 子物体上并拖 <see cref="TMP_Text"/>；仅在作为客户端监听时显示（含 Host）。
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class ClientLatencyHud : MonoBehaviour
    {
        [SerializeField] private TMP_Text _label;
        [SerializeField] private string _format = "RTT: {0} ms";

        private void Awake()
        {
            if (_label == null)
                _label = GetComponent<TMP_Text>();
        }

        private void LateUpdate()
        {
            if (_label == null)
                return;

            var nm = NetworkManager.Singleton;
            bool show = nm != null && nm.IsListening && nm.IsClient;
            if (!show)
            {
                if (_label.enabled)
                    _label.enabled = false;
                return;
            }

            if (!_label.enabled)
                _label.enabled = true;

            if (!TryGetRoundTripTimeMs(out ulong ms))
            {
                _label.text = "RTT: -- ms";
                return;
            }

            _label.text = string.Format(_format, ms);
        }

        private static bool TryGetRoundTripTimeMs(out ulong rttMs)
        {
            rttMs = 0;
            var nm = NetworkManager.Singleton;
            if (nm == null)
                return false;

            var transport = nm.NetworkConfig != null ? nm.NetworkConfig.NetworkTransport : null;
            if (transport == null)
                return false;

            rttMs = transport.GetCurrentRtt(NetworkManager.ServerClientId);
            return true;
        }
    }
}
