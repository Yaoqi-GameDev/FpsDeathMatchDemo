using FpsDemo.Match;
using UIFramework;
using UnityEngine;

namespace FpsDemo.UI
{
    /// <summary>
    /// ESC toggles in-match pause menu. Hang on <see cref="MatchManager"/> (or any always-active match object).
    /// </summary>
    [DisallowMultipleComponent]
    [DefaultExecutionOrder(-50)]
    public sealed class InMatchPauseMenuInput : MonoBehaviour
    {
        private void Update()
        {
            if (!Input.GetKeyDown(KeyCode.Escape))
                return;

            if (MatchManager.Instance != null && MatchManager.Instance.IsMatchOver)
                return;

            var frame = UIFrameService.Frame;
            if (frame == null)
                return;

            if (!frame.IsScreenRegistered(InMatchPauseMenuWindowController.ScreenId))
            {
                Debug.LogWarning(
                    "[Pause] Screen '" + InMatchPauseMenuWindowController.ScreenId +
                    "' not registered. Run FpsDemo/UI/Enrich UI Framework Features.");
                return;
            }

            // Top window first: Settings → close Settings; Pause → resume; else open Pause.
            if (IsScreenVisible(SettingsWindowController.ScreenId))
            {
                frame.CloseWindow(SettingsWindowController.ScreenId);
                return;
            }

            if (IsScreenVisible(InMatchPauseMenuWindowController.ScreenId)
                || InMatchPauseMenuWindowController.IsOpen)
            {
                frame.CloseWindow(InMatchPauseMenuWindowController.ScreenId);
                return;
            }

            if (IsScreenVisible(EndGameWindowController.ScreenId))
                return;

            frame.OpenWindow(InMatchPauseMenuWindowController.ScreenId);
        }

        private static bool IsScreenVisible(string screenId)
        {
            if (screenId == InMatchPauseMenuWindowController.ScreenId)
                return InMatchPauseMenuWindowController.IsOpen;

#pragma warning disable CS0618
            if (screenId == SettingsWindowController.ScreenId)
            {
                var s = Object.FindObjectOfType<SettingsWindowController>(includeInactive: true);
                return s != null && s.IsVisible;
            }

            if (screenId == EndGameWindowController.ScreenId)
            {
                var e = Object.FindObjectOfType<EndGameWindowController>(includeInactive: true);
                return e != null && e.IsVisible;
            }
#pragma warning restore CS0618

            return false;
        }
    }
}
