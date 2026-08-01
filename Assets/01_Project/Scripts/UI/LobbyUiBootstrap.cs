using UIFramework;
using UnityEngine;

namespace FpsDemo.UI
{
    /// <summary>
    /// 挂在 Lobby 场景（非 DDOL 物体）上：确保全局 UIFrame，打开大厅窗，并关掉旧 Canvas。
    /// </summary>
    [DisallowMultipleComponent]
    [DefaultExecutionOrder(-100)]
    public sealed class LobbyUiBootstrap : MonoBehaviour
    {
        [SerializeField] private UISettings _uiSettings;

        [Tooltip("打开大厅前是否 HideAll。")]
        [SerializeField] private bool _hideAllBeforeOpen = true;

        [Tooltip("禁用场景里名为 Canvas 且含 LobbyRoot 的旧大厅（已被框架 UI 替代）。")]
        [SerializeField] private bool _disableLegacyLobbyCanvas = true;

        private void Awake()
        {
            var frame = UIFrameService.Ensure(_uiSettings);
            if (frame == null)
                return;

            if (_disableLegacyLobbyCanvas)
                DisableLegacyLobbyCanvas();

            if (_hideAllBeforeOpen)
                frame.HideAll(animate: false);

            if (!frame.IsScreenRegistered(LobbyMenuWindowController.ScreenId))
            {
                Debug.LogError(
                    "[LobbyUiBootstrap] Screen '" + LobbyMenuWindowController.ScreenId +
                    "' is not registered. Run menu: FpsDemo/UI/Build LobbyMenuWindowController Prefab And Wire UISettings.",
                    this);
                return;
            }

            frame.OpenWindow(LobbyMenuWindowController.ScreenId);
        }

        private static void DisableLegacyLobbyCanvas()
        {
#pragma warning disable CS0618
            var canvases = Object.FindObjectsOfType<Canvas>();
#pragma warning restore CS0618
            for (int i = 0; i < canvases.Length; i++)
            {
                var c = canvases[i];
                if (c == null)
                    continue;
                if (c.GetComponentInParent<UIFrame>() != null)
                    continue;
                if (c.transform.Find(LobbyMenuWindowController.ContentRootName) == null)
                    continue;
                c.gameObject.SetActive(false);
            }
        }
    }
}
