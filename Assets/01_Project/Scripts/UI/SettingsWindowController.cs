using TMPro;
using UIFramework;
using UnityEngine;
using UnityEngine.UI;

namespace FpsDemo.UI
{
    /// <summary>
    /// Settings window. Prefab / ScreenId: <c>SettingsWindowController</c>.
    /// Opened from lobby; uses window history so closing restores the lobby (HideOnForegroundLost).
    /// </summary>
    public sealed class SettingsWindowController : WindowController
    {
        public const string ScreenId = "SettingsWindowController";

        [SerializeField] private TMP_Text _bodyText;
        [SerializeField] private Button _closeButton;

        protected override void AddListeners()
        {
            if (_closeButton != null)
                _closeButton.onClick.AddListener(OnCloseClicked);
        }

        protected override void RemoveListeners()
        {
            if (_closeButton != null)
                _closeButton.onClick.RemoveListener(OnCloseClicked);
        }

        protected override void OnPropertiesSet()
        {
            if (_bodyText == null)
                return;
            _bodyText.text =
                "Settings (placeholder)\n\n" +
                "Sensitivity / invert Y / audio can plug in here later.\n" +
                "Close returns to the previous window (lobby or pause menu).";
        }

        private void OnCloseClicked()
        {
            UI_Close();
        }
    }
}
