using System;
using System.IO;
using UnityEngine;

namespace FpsDemo.AssetBundles
{
    /// <summary>
    /// Selects a complete downloaded AssetBundle version when one is available.
    /// The built-in StreamingAssets version remains the fallback.
    /// </summary>
    internal static class AssetBundlePathResolver
    {
        private const string BundleFolderName = "AssetBundles";
        private const string CurrentVersionFileName = "current.json";
        private const string ManifestBundleFileName = "manifest.ab";

        [Serializable]
        private sealed class CurrentVersionInfo
        {
            public string version;
        }

        internal static string ResolveBundleRoot(string platformFolderName)
        {
            string streamingRoot = Path.Combine(
                Application.streamingAssetsPath,
                BundleFolderName,
                platformFolderName);

            string persistentBundleRoot = Path.Combine(
                Application.persistentDataPath,
                BundleFolderName);
            string versionFilePath = Path.Combine(persistentBundleRoot, CurrentVersionFileName);

            if (!File.Exists(versionFilePath))
            {
                Debug.Log("[AssetBundles] No downloaded version. Using StreamingAssets: " + streamingRoot);
                return streamingRoot;
            }

            try
            {
                CurrentVersionInfo versionInfo = JsonUtility.FromJson<CurrentVersionInfo>(
                    File.ReadAllText(versionFilePath));
                string version = versionInfo != null ? versionInfo.version : null;

                if (!IsSafeFolderName(version))
                {
                    Debug.LogWarning("[AssetBundles] Invalid downloaded version. Using StreamingAssets.");
                    return streamingRoot;
                }

                string downloadedRoot = Path.Combine(persistentBundleRoot, version, platformFolderName);
                if (IsCompleteBundleRoot(downloadedRoot, platformFolderName))
                {
                    Debug.Log("[AssetBundles] Using downloaded version " + version + ": " + downloadedRoot);
                    return downloadedRoot;
                }

                Debug.LogWarning(
                    "[AssetBundles] Downloaded version " + version +
                    " is incomplete. Using StreamingAssets instead.");
            }
            catch (Exception exception)
            {
                Debug.LogWarning(
                    "[AssetBundles] Failed to read " + versionFilePath +
                    ". Using StreamingAssets instead. " + exception.Message);
            }

            return streamingRoot;
        }

        private static bool IsCompleteBundleRoot(string bundleRoot, string platformFolderName)
        {
            return Directory.Exists(bundleRoot) &&
                   File.Exists(Path.Combine(bundleRoot, ManifestBundleFileName)) &&
                   File.Exists(Path.Combine(bundleRoot, platformFolderName));
        }

        private static bool IsSafeFolderName(string value)
        {
            return !string.IsNullOrWhiteSpace(value) &&
                   value == Path.GetFileName(value) &&
                   value.IndexOfAny(Path.GetInvalidFileNameChars()) < 0;
        }
    }
}
