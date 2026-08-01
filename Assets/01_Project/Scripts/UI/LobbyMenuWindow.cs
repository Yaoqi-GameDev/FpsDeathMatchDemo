using FpsDemo.Netcode;
using TMPro;
using UIFramework;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.UI;

namespace FpsDemo.UI
{
    /// <summary>
    /// Lobby as a UIFramework <see cref="WindowController"/>. Prefab name / ScreenId: <c>LobbyMenu</c>.
    /// Button actions match the former <see cref="LobbyMenuView"/>.
    /// </summary>
    public sealed class LobbyMenuWindow : WindowController
    {
        public const string ScreenId = "LobbyMenu";
        public const string ContentRootName = "LobbyRoot";

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

        protected override void Awake()
        {
            TryWireFromHierarchyIfNeeded();
            base.Awake();
        }

        protected override void AddListeners()
        {
            Wire(_createRoomButton, OnCreateRoomClicked);
            Wire(_joinGameButton, OnJoinGameClicked);
            Wire(_singlePlayerButton, OnSinglePlayerClicked);
            Wire(_settingsButton, OnSettingsClicked);
        }

        protected override void RemoveListeners()
        {
            Unwire(_createRoomButton, OnCreateRoomClicked);
            Unwire(_joinGameButton, OnJoinGameClicked);
            Unwire(_singlePlayerButton, OnSinglePlayerClicked);
            Unwire(_settingsButton, OnSettingsClicked);
        }

        private void TryWireFromHierarchyIfNeeded()
        {
            var root = transform.Find(ContentRootName);
            if (root == null)
                root = transform;

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
            string portStr = _joinPortInput != null ? _joinPortInput.text.Trim() : "";
            LobbyNetSession.TryStartHostAndLoadMatch(SetHint, portStr);
            TryCloseAfterNetworkStarted();
        }

        private void OnJoinGameClicked()
        {
            string addr = _joinAddressInput != null && !string.IsNullOrWhiteSpace(_joinAddressInput.text)
                ? _joinAddressInput.text.Trim()
                : "127.0.0.1";
            string portStr = _joinPortInput != null ? _joinPortInput.text.Trim() : "";
            LobbyNetSession.TryStartClient(addr, portStr, SetHint);
            TryCloseAfterNetworkStarted();
        }

        private void OnSinglePlayerClicked()
        {
            LobbyNetSession.TryStartSoloMatch(SetHint);
            TryCloseAfterNetworkStarted();
        }

        private void OnSettingsClicked()
        {
            if (_settingsPlaceholderPanel != null)
                _settingsPlaceholderPanel.SetActive(!_settingsPlaceholderPanel.activeSelf);
            SetHint("Settings placeholder — audio / sensitivity later.");
            Debug.Log("[Lobby] Settings (placeholder).");
        }

        /// <summary>进对局后关掉大厅窗，避免 UIFrame DDOL 后仍盖在 DeathMatch 上。</summary>
        private void TryCloseAfterNetworkStarted()
        {
            var nm = NetworkManager.Singleton;
            if (nm != null && nm.IsListening)
                UI_Close();
        }

        private void SetHint(string message)
        {
            if (_hintText != null)
                _hintText.text = message;
        }
    }
}
