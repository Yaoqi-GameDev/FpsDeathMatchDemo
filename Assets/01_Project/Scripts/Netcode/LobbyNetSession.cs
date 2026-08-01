using System;
using FpsDemo.Core;
using FpsDemo.UI;
using Unity.Netcode;
using Unity.Netcode.Transports.UTP;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace FpsDemo.Netcode
{
    /// <summary>
    /// Lobby entry points: create room (host), join (client), solo (local host). English user messages only.
    /// DeathMatch has no in-scene Player — characters spawn only via <see cref="NetworkManager"/> PlayerPrefab after Host/Client starts.
    /// </summary>
    public static class LobbyNetSession
    {
        public const string LobbySceneName = "Lobby";
        public const int LobbySceneBuildIndex = 0;
        public const string MatchSceneName = "DeathMatch";
        public const int MatchSceneBuildIndex = 1;

        /// <summary>Host starts first, then loads the match scene for everyone who joins later.</summary>
        /// <param name="listenPortText">可选；与 Join 共用 Port 输入时，主机在该 UDP 端口监听。空则沿用 Transport 当前 Port（默认 7777）。</param>
        public static void TryStartHostAndLoadMatch(Action<string> setHint, string listenPortText = null)
        {
            if (!TryGetNetworkManager(out var nm, setHint))
                return;

            if (nm.IsClient || nm.IsServer)
            {
                ReportError(setHint, "Already connected. Restart the application or disconnect first.");
                return;
            }

            var utp = nm.NetworkConfig.NetworkTransport as UnityTransport;
            if (utp == null)
                utp = nm.GetComponent<UnityTransport>();
            if (utp != null)
                ApplyHostListenOnAllInterfaces(utp, listenPortText);

            GameSessionContext.SetOfflineSession(false);

            if (!nm.StartHost())
            {
                ReportError(setHint, "Failed to start host (StartHost returned false). Check port not in use.");
                return;
            }

            nm.SceneManager.LoadScene(MatchSceneName, LoadSceneMode.Single);
            setHint?.Invoke("Hosting — loading match.");
        }

        /// <summary>
        /// Solo play: same pipeline as <see cref="TryStartHostAndLoadMatch"/> (local Host + PlayerPrefab spawn).
        /// Prefer this over a plain <c>LoadScene</c> — that would enter DeathMatch with no controllable player.
        /// </summary>
        public static void TryStartSoloMatch(Action<string> setHint)
        {
            TryStartHostAndLoadMatch(setHint, listenPortText: null);
        }

        /// <summary>
        /// Leave match: close UI (no anim), shutdown NGO but <b>keep</b> NetworkManager (DDOL),
        /// load Lobby. Scene 里若再有一份 NM，由 <see cref="LobbyNetworkManagerGuard"/> 销毁重复项。
        /// LobbyUiBootstrap 会 <see cref="UIFrameService.ShowLobbyMenu"/>。
        /// </summary>
        public static void TryReturnToLobby()
        {
            Time.timeScale = 1f;
            UIFrameService.UnlockCursor();
            UIFrameService.CloseAllWindowsImmediate();
            if (UIFrameService.Frame != null)
                UIFrameService.Frame.HideAllPanels(animate: false);
            UIFrameService.UnlockCursor();

            var nm = NetworkManager.Singleton;
            if (nm != null && nm.IsListening)
                nm.Shutdown();

            SceneManager.LoadScene(LobbySceneName);
        }

        /// <summary>Client uses transport address/port; do not call LoadScene — NGO syncs the active scene from the host.</summary>
        public static void TryStartClient(string address, string portText, Action<string> setHint)
        {
            if (!TryGetNetworkManager(out var nm, setHint))
                return;

            if (nm.IsClient || nm.IsServer)
            {
                ReportError(setHint, "Already connected. Restart the application or disconnect first.");
                return;
            }

            var utp = nm.NetworkConfig.NetworkTransport as UnityTransport;
            if (utp == null)
                utp = nm.GetComponent<UnityTransport>();

            if (utp == null)
            {
                ReportError(setHint, "UnityTransport not found on NetworkManager.");
                return;
            }

            if (!TryParsePort(portText, out ushort port))
            {
                ReportError(setHint, "Invalid port. Use a number between 1 and 65535.");
                return;
            }

            string addr = string.IsNullOrWhiteSpace(address) ? "127.0.0.1" : address.Trim();
            utp.SetConnectionData(addr, port);

            GameSessionContext.SetOfflineSession(false);

            if (!nm.StartClient())
            {
                ReportError(setHint, "Failed to start client (StartClient returned false).");
                return;
            }

            setHint?.Invoke($"Connecting to {addr}:{port}…");
        }

        /// <summary>
        /// 若 <see cref="ConnectionAddressData.ServerListenAddress"/> 为 127.0.0.1，主机只接受本机连接，局域网无法连入。
        /// 改为 <c>0.0.0.0</c> 在所有网卡上监听 UDP（与 ping 通否无关，需放行本端口防火墙）。
        /// </summary>
        private static void ApplyHostListenOnAllInterfaces(UnityTransport utp, string listenPortText)
        {
            var d = utp.ConnectionData;
            d.ServerListenAddress = "0.0.0.0";
            if (!string.IsNullOrWhiteSpace(listenPortText)
                && ushort.TryParse(listenPortText.Trim(), out ushort p)
                && p > 0)
            {
                d.Port = p;
            }

            utp.ConnectionData = d;
        }

        private static bool TryGetNetworkManager(out NetworkManager nm, Action<string> setHint)
        {
            nm = NetworkManager.Singleton;
            if (nm != null)
                return true;

            ReportError(setHint, "NetworkManager not found. Add it to the Lobby scene.");
            return false;
        }

        private static bool TryParsePort(string text, out ushort port)
        {
            port = 7777;
            if (string.IsNullOrWhiteSpace(text))
                return true;

            if (!ushort.TryParse(text.Trim(), out port) || port == 0)
                return false;
            return true;
        }

        private static void ReportError(Action<string> setHint, string englishMessage)
        {
            setHint?.Invoke(englishMessage);
            Debug.LogWarning("[Lobby] " + englishMessage);
#if UNITY_EDITOR
            UnityEditor.EditorUtility.DisplayDialog("Lobby", englishMessage, "OK");
#endif
        }
    }
}
