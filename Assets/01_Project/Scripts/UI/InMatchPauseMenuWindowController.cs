using FpsDemo.Fps;
using FpsDemo.Match;
using FpsDemo.Netcode;
using UIFramework;
using UnityEngine;
using UnityEngine.UI;

namespace FpsDemo.UI
{
    /// <summary>
    /// In-match pause menu (ESC). Prefab / ScreenId: <c>InMatchPauseMenuWindowController</c>.
    /// Resume / Settings (same <see cref="SettingsWindowController"/> as lobby) / Return to Lobby.
    /// </summary>
    public sealed class InMatchPauseMenuWindowController : WindowController
    {
        public const string ScreenId = "InMatchPauseMenuWindowController";

        /// <summary>Pause menu visible — used by look/input to keep cursor free.</summary>
        public static bool IsOpen { get; private set; }

        /// <summary>Returning to lobby — do not re-lock cursor when this window closes.</summary>
        private static bool _skipResumeGameplayOnHide;

        [SerializeField] private Button _resumeButton;
        [SerializeField] private Button _settingsButton;
        [SerializeField] private Button _returnToLobbyButton;

        protected override void AddListeners()
        {
            if (_resumeButton != null)
                _resumeButton.onClick.AddListener(OnResumeClicked);
            if (_settingsButton != null)
                _settingsButton.onClick.AddListener(OnSettingsClicked);
            if (_returnToLobbyButton != null)
                _returnToLobbyButton.onClick.AddListener(OnReturnToLobbyClicked);
        }

        protected override void RemoveListeners()
        {
            if (_resumeButton != null)
                _resumeButton.onClick.RemoveListener(OnResumeClicked);
            if (_settingsButton != null)
                _settingsButton.onClick.RemoveListener(OnSettingsClicked);
            if (_returnToLobbyButton != null)
                _returnToLobbyButton.onClick.RemoveListener(OnReturnToLobbyClicked);
        }

        protected override void OnPropertiesSet()
        {
            IsOpen = true;
            ApplyPausedGameplayState(paused: true);
        }

        protected override void WhileHiding()
        {
            // Lobby 启动会 HideAll 所有已注册窗（含从未打开的暂停窗）。
            // 只有真正打开过（IsOpen）再关掉时，才恢复游戏光标/输入。
            if (_skipResumeGameplayOnHide)
            {
                _skipResumeGameplayOnHide = false;
                IsOpen = false;
                return;
            }

            if (!IsOpen)
                return;

            IsOpen = false;
            if (MatchManager.Instance == null || !MatchManager.Instance.IsMatchOver)
                ApplyPausedGameplayState(paused: false);
        }

        private void OnResumeClicked()
        {
            UI_Close();
        }

        private void OnSettingsClicked()
        {
            var frame = UIFrameService.Frame;
            if (frame == null)
                return;
            if (!frame.IsScreenRegistered(SettingsWindowController.ScreenId))
            {
                Debug.LogError(
                    "[Pause] Settings screen not registered. Run FpsDemo/UI/Enrich UI Framework Features.",
                    this);
                return;
            }

            frame.OpenWindow(SettingsWindowController.ScreenId);
        }

        private void OnReturnToLobbyClicked()
        {
            // CloseAllWindows 会先 WhileHiding；若再 Lock 光标，回大厅后点不了 UI。
            _skipResumeGameplayOnHide = true;
            LobbyNetSession.TryReturnToLobby();
        }

        private static void ApplyPausedGameplayState(bool paused)
        {
            if (paused)
            {
                Cursor.lockState = CursorLockMode.None;
                Cursor.visible = true;
            }
            else
            {
                Cursor.lockState = CursorLockMode.Locked;
                Cursor.visible = false;
            }

            foreach (var p in MatchParticipant.ActiveParticipants)
            {
                if (p == null || !p.IsLocalPlayer)
                    continue;
                if (p.TryGetComponent<FpsInput>(out var input))
                    input.GameplayInputEnabled = !paused;
            }
        }
    }
}
