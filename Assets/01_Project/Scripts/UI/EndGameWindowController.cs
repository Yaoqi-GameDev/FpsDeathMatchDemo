using FpsDemo.Match;
using TMPro;
using UIFramework;
using UnityEngine;
using UnityEngine.UI;

namespace FpsDemo.UI
{
    /// <summary>
    /// Match end / again. Prefab name / ScreenId: <c>EndGameWindowController</c>.
    /// Match-end window; Again calls <see cref="MatchManager.RestartMatch"/>.
    /// </summary>
    public sealed class EndGameWindowController : WindowController<EndGameWindowProperties>
    {
        public const string ScreenId = "EndGameWindowController";

        [SerializeField] private TMP_Text _summaryText;
        [SerializeField] private Button _againButton;

        protected override void AddListeners()
        {
            if (_againButton != null)
                _againButton.onClick.AddListener(OnAgainClicked);
        }

        protected override void RemoveListeners()
        {
            if (_againButton != null)
                _againButton.onClick.RemoveListener(OnAgainClicked);
        }

        protected override void OnPropertiesSet()
        {
            if (_summaryText == null || Properties == null)
                return;
            _summaryText.text = Properties.SummaryText ?? "";
        }

        private void OnAgainClicked()
        {
            UI_Close();
            if (MatchManager.Instance != null)
                MatchManager.Instance.RestartMatch();
        }
    }
}
