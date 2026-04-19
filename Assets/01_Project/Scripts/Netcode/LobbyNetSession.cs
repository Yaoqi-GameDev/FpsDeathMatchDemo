using System;
using FpsDemo.Core;
using Unity.Netcode;
using Unity.Netcode.Transports.UTP;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace FpsDemo.Netcode
{
    /// <summary>
    /// Lobby button actions: host + load match, client connect, or offline load. English user messages only.
    /// </summary>
    public static class LobbyNetSession
    {
        public const string MatchSceneName = "DeathMatch";
        public const int MatchSceneBuildIndex = 1;

        /// <summary>Host starts first, then loads the match scene for everyone who joins later.</summary>
        public static void TryStartHostAndLoadMatch(Action<string> setHint)
        {
            if (!TryGetNetworkManager(out var nm, setHint))
                return;

            if (nm.IsClient || nm.IsServer)
            {
                ReportError(setHint, "Already connected. Restart the application or disconnect first.");
                return;
            }

            GameSessionContext.SetOfflineSession(false);

            if (!nm.StartHost())
            {
                ReportError(setHint, "Failed to start host (StartHost returned false). Check port not in use.");
                return;
            }

            nm.SceneManager.LoadScene(MatchSceneName, LoadSceneMode.Single);
            setHint?.Invoke("Hosting — loading match.");
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

        /// <summary>No NGO session; load DeathMatch for local/offline flow. Clears any active listen state first.</summary>
        public static void TryLoadOfflineMatch(Action<string> setHint)
        {
            var nm = NetworkManager.Singleton;
            if (nm != null && (nm.IsClient || nm.IsServer))
                nm.Shutdown();

            GameSessionContext.SetOfflineSession(true);
            SceneManager.LoadScene(MatchSceneBuildIndex);
            setHint?.Invoke("Loading offline match.");
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
