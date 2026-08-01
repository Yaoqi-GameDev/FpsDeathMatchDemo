using UIFramework;
using UnityEngine;
using UnityEngine.EventSystems;

namespace FpsDemo.UI
{
    /// <summary>
    /// 挂在 Lobby 场景（非 DDOL 物体）上：确保全局 UIFrame，打开大厅窗，并关掉旧 Canvas / 场景 EventSystem。
    /// </summary>
    [DisallowMultipleComponent]
    [DefaultExecutionOrder(-100)]
    public sealed class LobbyUiBootstrap : MonoBehaviour
    {
        [SerializeField] private UISettings _uiSettings;

        [Tooltip("打开 LobbyMenu 前是否 HideAll。")]
        [SerializeField] private bool _hideAllBeforeOpen = true;

        [Tooltip("成功启动框架 UI 后禁用带 LobbyMenuView 的旧 Canvas。")]
        [SerializeField] private bool _disableLegacyLobbyCanvas = true;

        [Tooltip("禁用场景里多余的 EventSystem（UIFrame 自带一份）。")]
        [SerializeField] private bool _disableSceneEventSystems = true;

        private void Awake()
        {
            var frame = UIFrameService.Ensure(_uiSettings);
            if (frame == null)
                return;

            if (_disableLegacyLobbyCanvas)
                DisableLegacyLobbyCanvas();

            if (_disableSceneEventSystems)
                DisableExtraEventSystems(frame.transform);

            if (_hideAllBeforeOpen)
                frame.HideAll(animate: false);

            if (!frame.IsScreenRegistered(LobbyMenuWindow.ScreenId))
            {
                Debug.LogError(
                    "[LobbyUiBootstrap] Screen '" + LobbyMenuWindow.ScreenId +
                    "' is not registered. Run menu: FpsDemo/UI/Build LobbyMenu Prefab And Wire UISettings.",
                    this);
                return;
            }

            frame.OpenWindow(LobbyMenuWindow.ScreenId);
        }

        private static void DisableLegacyLobbyCanvas()
        {
#pragma warning disable CS0618
            var views = Object.FindObjectsOfType<LobbyMenuView>();
#pragma warning restore CS0618
            for (int i = 0; i < views.Length; i++)
            {
                var v = views[i];
                if (v == null)
                    continue;
                var canvas = v.GetComponent<Canvas>();
                if (canvas != null)
                    canvas.gameObject.SetActive(false);
                else
                    v.gameObject.SetActive(false);
            }
        }

        private static void DisableExtraEventSystems(Transform uiFrameRoot)
        {
#pragma warning disable CS0618
            var systems = Object.FindObjectsOfType<EventSystem>();
#pragma warning restore CS0618
            for (int i = 0; i < systems.Length; i++)
            {
                var es = systems[i];
                if (es == null)
                    continue;
                if (uiFrameRoot != null && es.transform.IsChildOf(uiFrameRoot))
                    continue;
                es.gameObject.SetActive(false);
            }
        }
    }
}
