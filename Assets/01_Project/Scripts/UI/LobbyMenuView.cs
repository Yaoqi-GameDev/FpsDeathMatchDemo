using FpsDemo.Netcode;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace FpsDemo.UI
{
    /// <summary>
    /// Lobby menu: host / join / offline / settings. Wire buttons and fields in the Inspector,
    /// or use the same object names as the editor builder under <c>LobbyRoot</c> (optional auto-wire).
    /// All user-visible strings should stay English (font coverage).
    /// </summary>
    public sealed class LobbyMenuView : MonoBehaviour
    {
        public const string LobbyRootName = "LobbyRoot";

        [Header("Buttons")]
        [SerializeField] private Button _createRoomButton;
        [SerializeField] private Button _joinGameButton;
        [SerializeField] private Button _singlePlayerButton;
        [SerializeField] private Button _settingsButton;

        [Header("Join — address / port")]
        [SerializeField] private TMP_InputField _joinAddressInput;
        [SerializeField] private TMP_InputField _joinPortInput;

        [Header("Optional")]
        [SerializeField] private TMP_Text _hintText;
        [SerializeField] private GameObject _settingsPlaceholderPanel;

        private void Awake()
        {
            EnsureCanvasSane();
            TryWireFromHierarchyIfNeeded();
        }

        /// <summary>Fills missing references from children named by the editor setup (Inspector wiring preferred).</summary>
        private void TryWireFromHierarchyIfNeeded()
        {
            var root = transform.Find(LobbyRootName);
            if (root == null)
                return;

            if (_createRoomButton == null)
                _createRoomButton = FindUnderRoot<Button>(root, "BtnCreateRoom");
            if (_joinGameButton == null)
                _joinGameButton = FindUnderRoot<Button>(root, "BtnJoinGame");
            if (_singlePlayerButton == null)
                _singlePlayerButton = FindUnderRoot<Button>(root, "BtnSinglePlayer");
            if (_settingsButton == null)
                _settingsButton = FindUnderRoot<Button>(root, "BtnSettings");

            if (_joinAddressInput == null)
                _joinAddressInput = FindUnderRoot<TMP_InputField>(root, "AddressField");
            if (_joinPortInput == null)
                _joinPortInput = FindUnderRoot<TMP_InputField>(root, "PortField");

            if (_hintText == null)
                _hintText = FindUnderRoot<TMP_Text>(root, "Hint");

            if (_settingsPlaceholderPanel == null)
            {
                var panel = transform.Find("SettingsPanel");
                if (panel != null)
                    _settingsPlaceholderPanel = panel.gameObject;
            }
        }

        private static T FindUnderRoot<T>(Transform lobbyRoot, string objectName) where T : Component
        {
            foreach (var tr in lobbyRoot.GetComponentsInChildren<Transform>(true))
            {
                if (tr.name != objectName)
                    continue;
                var c = tr.GetComponent<T>();
                if (c != null)
                    return c;
            }

            return null;
        }

        private void OnEnable()
        {
            Wire(_createRoomButton, OnCreateRoomClicked);
            Wire(_joinGameButton, OnJoinGameClicked);
            Wire(_singlePlayerButton, OnSinglePlayerClicked);
            Wire(_settingsButton, OnSettingsClicked);
        }

        private void OnDisable()
        {
            Unwire(_createRoomButton, OnCreateRoomClicked);
            Unwire(_joinGameButton, OnJoinGameClicked);
            Unwire(_singlePlayerButton, OnSinglePlayerClicked);
            Unwire(_settingsButton, OnSettingsClicked);
        }

        private static void Wire(Button b, UnityEngine.Events.UnityAction a)
        {
            if (b != null)
                b.onClick.AddListener(a);
        }

        private static void Unwire(Button b, UnityEngine.Events.UnityAction a)
        {
            if (b != null)
                b.onClick.RemoveListener(a);
        }

        private void OnCreateRoomClicked()
        {
            LobbyNetSession.TryStartHostAndLoadMatch(SetHint);
        }

        private void OnJoinGameClicked()
        {
            string addr = _joinAddressInput != null && !string.IsNullOrWhiteSpace(_joinAddressInput.text)
                ? _joinAddressInput.text.Trim()
                : "127.0.0.1";
            string portStr = _joinPortInput != null ? _joinPortInput.text.Trim() : "";
            LobbyNetSession.TryStartClient(addr, portStr, SetHint);
        }

        private void OnSinglePlayerClicked()
        {
            LobbyNetSession.TryLoadOfflineMatch(SetHint);
        }

        private void OnSettingsClicked()
        {
            if (_settingsPlaceholderPanel != null)
                _settingsPlaceholderPanel.SetActive(!_settingsPlaceholderPanel.activeSelf);
            SetHint("Settings placeholder — audio / sensitivity later.");
            Debug.Log("[Lobby] Settings (placeholder).");
        }

        private void SetHint(string message)
        {
            if (_hintText != null)
                _hintText.text = message;
        }

        private void EnsureCanvasSane()
        {
            var rt = GetComponent<RectTransform>();
            if (rt == null)
                return;
            if (rt.localScale.sqrMagnitude < 0.01f)
                rt.localScale = Vector3.one;
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;
        }
    }
}
