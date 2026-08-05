using System;
using System.Collections;
using System.IO;
using UnityEngine;
using UnityEngine.Networking;

namespace FpsDemo.AssetBundles
{
    /// <summary>
    /// Checks the remote version and downloads a complete update when needed.
    /// </summary>
    internal static class AssetBundleUpdateManager
    {
        internal const string RemoteVersionUrl = "http://localhost:8080/version.json";

        [Serializable]
        private sealed class LocalVersionInfo
        {
            public string version;
        }

        internal static IEnumerator CheckAndUpdate()
        {
            AssetBundleVersionManifest remoteVersion = null;
            yield return DownloadRemoteVersion(info => remoteVersion = info);

            if (remoteVersion == null || remoteVersion.files == null || remoteVersion.files.Count == 0)
                yield break;

            string persistentRoot = Path.Combine(Application.persistentDataPath, "AssetBundles");
            string currentVersionPath = Path.Combine(persistentRoot, "current.json");
            string localVersion = ReadLocalVersion(currentVersionPath);
            string validationReason = null;
            if (localVersion == remoteVersion.version &&
                AssetBundleVersionValidator.TryValidate(
                    Path.Combine(persistentRoot, remoteVersion.version),
                    AssetBundleRuntime.PlatformFolderName,
                    out validationReason))
            {
                Debug.Log("[AssetBundles] Local version is already current: " + localVersion);
                yield break;
            }

            if (localVersion == remoteVersion.version && !string.IsNullOrEmpty(validationReason))
                Debug.LogWarning("[AssetBundles] Local version needs repair: " + validationReason);

            string tempRoot = Path.Combine(
                persistentRoot,
                remoteVersion.version + ".downloading",
                AssetBundleRuntime.PlatformFolderName);

            if (Directory.Exists(tempRoot))
                Directory.Delete(tempRoot, true);
            Directory.CreateDirectory(tempRoot);

            bool downloadSucceeded = true;
            for (int i = 0; i < remoteVersion.files.Count; i++)
            {
                AssetBundleFileRecord file = remoteVersion.files[i];
                if (!IsSafeRelativePath(file.path))
                {
                    Debug.LogWarning("[AssetBundles] Rejected unsafe update path: " + file.path);
                    downloadSucceeded = false;
                    break;
                }

                string relativePath = file.path.Replace('/', Path.DirectorySeparatorChar);
                string destination = Path.Combine(tempRoot, relativePath);
                string destinationDirectory = Path.GetDirectoryName(destination);
                if (!Directory.Exists(destinationDirectory))
                    Directory.CreateDirectory(destinationDirectory);

                string url = RemoteVersionUrl.Substring(0, RemoteVersionUrl.LastIndexOf('/')) +
                             "/AssetBundles/" + remoteVersion.version + "/" +
                             AssetBundleRuntime.PlatformFolderName + "/" + file.path;

                byte[] data = null;
                yield return DownloadFile(url, file, bytes => data = bytes);
                if (data == null)
                {
                    downloadSucceeded = false;
                    break;
                }

                File.WriteAllBytes(destination, data);
                Debug.Log("[AssetBundles] Downloaded " + file.path);
            }

            if (!downloadSucceeded)
            {
                string failedRoot = Path.Combine(persistentRoot, remoteVersion.version + ".downloading");
                if (Directory.Exists(failedRoot))
                    Directory.Delete(failedRoot, true);
                Debug.LogWarning("[AssetBundles] Update failed. Existing local version remains active.");
                yield break;
            }

            string temporaryVersionRoot = Path.Combine(persistentRoot, remoteVersion.version + ".downloading");
            File.WriteAllText(
                Path.Combine(temporaryVersionRoot, "version.json"),
                JsonUtility.ToJson(remoteVersion, true));

            string finalVersionRoot = Path.Combine(persistentRoot, remoteVersion.version);
            if (Directory.Exists(finalVersionRoot))
                Directory.Delete(finalVersionRoot, true);
            Directory.Move(Path.Combine(persistentRoot, remoteVersion.version + ".downloading"), finalVersionRoot);

            string tempCurrentPath = currentVersionPath + ".tmp";
            File.WriteAllText(tempCurrentPath, JsonUtility.ToJson(new LocalVersionInfo
            {
                version = remoteVersion.version
            }));
            if (File.Exists(currentVersionPath))
                File.Replace(tempCurrentPath, currentVersionPath, null);
            else
                File.Move(tempCurrentPath, currentVersionPath);

            Debug.Log("[AssetBundles] Activated downloaded version: " + remoteVersion.version);
        }

        private static IEnumerator DownloadRemoteVersion(Action<AssetBundleVersionManifest> callback)
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
                    callback(null);
                    yield break;
                }

                AssetBundleVersionManifest versionInfo = null;
                try
                {
                    versionInfo = JsonUtility.FromJson<AssetBundleVersionManifest>(request.downloadHandler.text);
                }
                catch (Exception exception)
                {
                    Debug.LogWarning(
                        "[AssetBundles] Remote version JSON is invalid. Using local bundles. " +
                        exception.Message);
                    callback(null);
                    yield break;
                }

                if (versionInfo == null || string.IsNullOrWhiteSpace(versionInfo.version) ||
                    versionInfo.files == null || versionInfo.files.Count == 0)
                {
                    Debug.LogWarning("[AssetBundles] Remote version JSON is incomplete. Using local bundles.");
                    callback(null);
                    yield break;
                }

                Debug.Log("[AssetBundles] Remote version: " + versionInfo.version);
                callback(versionInfo);
            }
        }

        private static IEnumerator DownloadFile(string url, AssetBundleFileRecord file, Action<byte[]> callback)
        {
            using (UnityWebRequest request = UnityWebRequest.Get(url))
            {
                request.timeout = 15;
                yield return request.SendWebRequest();

                if (request.result != UnityWebRequest.Result.Success)
                {
                    Debug.LogWarning("[AssetBundles] File download failed: " + file.path + " - " + request.error);
                    callback(null);
                    yield break;
                }

                byte[] data = request.downloadHandler.data;
                if (data == null)
                {
                    Debug.LogWarning("[AssetBundles] File download returned no data: " + file.path);
                    callback(null);
                    yield break;
                }

                string hash = ComputeSha256(data);
                if (data.LongLength != file.size ||
                    !string.Equals(hash, file.hash, StringComparison.OrdinalIgnoreCase))
                {
                    Debug.LogWarning("[AssetBundles] File validation failed: " + file.path);
                    callback(null);
                    yield break;
                }

                callback(data);
            }
        }

        private static string ReadLocalVersion(string path)
        {
            if (!File.Exists(path))
                return null;

            try
            {
                LocalVersionInfo info = JsonUtility.FromJson<LocalVersionInfo>(File.ReadAllText(path));
                return info != null ? info.version : null;
            }
            catch (Exception exception)
            {
                Debug.LogWarning("[AssetBundles] Local current.json is invalid: " + exception.Message);
                return null;
            }
        }

        private static bool IsSafeRelativePath(string value)
        {
            return !string.IsNullOrWhiteSpace(value) &&
                   !value.StartsWith("/", StringComparison.Ordinal) &&
                   !value.Contains("..") &&
                   value.IndexOf(':') < 0;
        }

        private static string ComputeSha256(byte[] data)
        {
            using (var sha256 = System.Security.Cryptography.SHA256.Create())
            {
                byte[] hash = sha256.ComputeHash(data);
                return BitConverter.ToString(hash).Replace("-", string.Empty).ToLowerInvariant();
            }
        }
    }
}
