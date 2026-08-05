using System;
using System.Collections.Generic;
using System.IO;
using System.Security.Cryptography;
using UnityEngine;

namespace FpsDemo.AssetBundles
{
    [Serializable]
    internal sealed class AssetBundleVersionManifest
    {
        public string version;
        public string platform;
        public List<AssetBundleFileRecord> files;
    }

    [Serializable]
    internal sealed class AssetBundleFileRecord
    {
        public string path;
        public long size;
        public string hash;
    }

    internal static class AssetBundleVersionValidator
    {
        internal static bool TryValidate(string versionRoot, string platformFolderName, out string reason)
        {
            reason = null;
            string manifestPath = Path.Combine(versionRoot, "version.json");
            if (!File.Exists(manifestPath))
            {
                reason = "version.json is missing";
                return false;
            }

            AssetBundleVersionManifest manifest;
            try
            {
                manifest = JsonUtility.FromJson<AssetBundleVersionManifest>(File.ReadAllText(manifestPath));
            }
            catch (Exception exception)
            {
                reason = "version.json is invalid: " + exception.Message;
                return false;
            }

            if (manifest == null || string.IsNullOrWhiteSpace(manifest.version) ||
                manifest.files == null || manifest.files.Count == 0)
            {
                reason = "version.json is incomplete";
                return false;
            }

            string bundleRoot = Path.Combine(versionRoot, platformFolderName);
            if (!Directory.Exists(bundleRoot))
            {
                reason = "platform directory is missing";
                return false;
            }

            for (int i = 0; i < manifest.files.Count; i++)
            {
                AssetBundleFileRecord file = manifest.files[i];
                if (!IsSafeRelativePath(file.path))
                {
                    reason = "unsafe file path: " + file.path;
                    return false;
                }

                string localPath = Path.Combine(bundleRoot, file.path.Replace('/', Path.DirectorySeparatorChar));
                if (!File.Exists(localPath))
                {
                    reason = "file is missing: " + file.path;
                    return false;
                }

                FileInfo fileInfo = new FileInfo(localPath);
                if (fileInfo.Length != file.size)
                {
                    reason = "file size mismatch: " + file.path;
                    return false;
                }

                string hash = ComputeSha256(File.ReadAllBytes(localPath));
                if (!string.Equals(hash, file.hash, StringComparison.OrdinalIgnoreCase))
                {
                    reason = "file hash mismatch: " + file.path;
                    return false;
                }
            }

            return true;
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
            using (SHA256 sha256 = SHA256.Create())
            {
                byte[] hash = sha256.ComputeHash(data);
                return BitConverter.ToString(hash).Replace("-", string.Empty).ToLowerInvariant();
            }
        }
    }
}
