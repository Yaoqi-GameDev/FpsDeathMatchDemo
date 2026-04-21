using TMPro;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.UI;

namespace FpsDemo.UI
{
    /// <summary>
    /// 屏幕左上角显示本机到服务器的往返延迟（RTT，毫秒），数据来自 <see cref="Unity.Netcode.NetworkTransport.GetCurrentRtt"/>（UnityTransport）。
    /// 挂在任意常驻物体上（如 NetworkManager 或 HUD 根）；仅在网络已作为 <b>客户端</b> 监听时显示（含 Host；纯专用服务器不显示）。
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class ClientLatencyHud : MonoBehaviour
    {
        [SerializeField] private float _marginLeft = 12f;
        [SerializeField] private float _marginTop = 12f;
        [SerializeField] private float _fontSize = 22f;
        [SerializeField] private string _format = "RTT: {0} ms";

        [SerializeField] private Color _textColor = new Color(1f, 1f, 1f, 0.92f);

        private TextMeshProUGUI _label;
        private Canvas _canvas;

        private void Awake()
        {
            BuildUi();
        }

        private void BuildUi()
        {
            var canvasGo = new GameObject("ClientLatencyHudCanvas");
            canvasGo.transform.SetParent(transform, false);
            _canvas = canvasGo.AddComponent<Canvas>();
            _canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            _canvas.sortingOrder = 1000;
            var scaler = canvasGo.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            canvasGo.AddComponent<GraphicRaycaster>();

            var textGo = new GameObject("LatencyText");
            textGo.transform.SetParent(canvasGo.transform, false);
            var rt = textGo.AddComponent<RectTransform>();
            rt.anchorMin = new Vector2(0f, 1f);
            rt.anchorMax = new Vector2(0f, 1f);
            rt.pivot = new Vector2(0f, 1f);
            rt.anchoredPosition = new Vector2(_marginLeft, -_marginTop);
            rt.sizeDelta = new Vector2(480f, 40f);

            _label = textGo.AddComponent<TextMeshProUGUI>();
            _label.fontSize = _fontSize;
            _label.alignment = TextAlignmentOptions.TopLeft;
            _label.color = _textColor;
            if (TMP_Settings.defaultFontAsset != null)
                _label.font = TMP_Settings.defaultFontAsset;
            _label.text = "RTT: -- ms";
        }

        private void LateUpdate()
        {
            if (_label == null || _canvas == null)
                return;

            var nm = NetworkManager.Singleton;
            bool show = nm != null && nm.IsListening && nm.IsClient;
            _canvas.enabled = show;

            if (!show)
                return;

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
