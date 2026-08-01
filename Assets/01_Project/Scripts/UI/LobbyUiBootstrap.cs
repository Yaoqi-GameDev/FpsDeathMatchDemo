using UIFramework;
using UnityEngine;

namespace FpsDemo.UI
{
    /// <summary>
    /// 挂在 Lobby 场景（非 DDOL 物体）上：确保全局 UIFrame 并打开大厅窗。
    /// </summary>
    [DisallowMultipleComponent]
    [DefaultExecutionOrder(-100)]
    public sealed class LobbyUiBootstrap : MonoBehaviour
    {
        [SerializeField] private UISettings _uiSettings;

        [Tooltip("打开大厅前是否 HideAll。")]
        [SerializeField] private bool _hideAllBeforeOpen = true;

        private void Awake()
        {
            var frame = UIFrameService.Ensure(_uiSettings);
            if (frame == null)
                return;

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
    }
}
