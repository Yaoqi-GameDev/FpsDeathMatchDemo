using UIFramework;
using UnityEngine;

namespace FpsDemo.UI
{
    /// <summary>
    /// Lobby 场景启动：确保 UIFrame，打开大厅窗（含从对局 Return 回来的路径）。
    /// </summary>
    [DisallowMultipleComponent]
    [DefaultExecutionOrder(-100)]
    public sealed class LobbyUiBootstrap : MonoBehaviour
    {
        [SerializeField] private UISettings _uiSettings;

        private void Awake()
        {
            var frame = UIFrameService.Ensure(_uiSettings);
            if (frame == null)
                return;

            UIFrameService.ShowLobbyMenu();
        }
    }
}
