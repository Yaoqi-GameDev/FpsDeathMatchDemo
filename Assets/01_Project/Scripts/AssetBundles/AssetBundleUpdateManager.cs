using System;
using System.Collections;
using UnityEngine;
using UnityEngine.Networking;

namespace FpsDemo.AssetBundles
{
    /// <summary>
    /// Queries the remote version manifest. Downloading is added in the next step.
    /// </summary>
    internal static class AssetBundleUpdateManager
    {
        internal const string RemoteVersionUrl = "http://localhost:8080/version.json";

        [Serializable]
        private sealed class RemoteVersionInfo
        {
            public string version;
        }

        internal static IEnumerator CheckRemoteVersion()
        {
            using (UnityWebRequest request = UnityWebRequest.Get(RemoteVersionUrl))
            {
                request.timeout = 3;
                yield return request.SendWebRequest();

                if (request.result != UnityWebRequest.Result.Success)
                {
                    Debug.LogWarning(
                        "[AssetBundles] Remote version check failed. Using local bundles. " +
                        request.error);
                    yield break;
                }

                RemoteVersionInfo versionInfo = null;
                try
                {
                    versionInfo = JsonUtility.FromJson<RemoteVersionInfo>(request.downloadHandler.text);
                }
                catch (Exception exception)
                {
                    Debug.LogWarning(
                        "[AssetBundles] Remote version JSON is invalid. Using local bundles. " +
                        exception.Message);
                    yield break;
                }

                if (versionInfo == null || string.IsNullOrWhiteSpace(versionInfo.version))
                {
                    Debug.LogWarning("[AssetBundles] Remote version JSON has no version. Using local bundles.");
                    yield break;
                }

                Debug.Log("[AssetBundles] Remote version: " + versionInfo.version);
            }
        }
    }
}
