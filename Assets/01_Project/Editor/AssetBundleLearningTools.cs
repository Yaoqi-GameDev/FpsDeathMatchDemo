using System.IO;
using FpsDemo.AssetBundles;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace FpsDemo.EditorTools
{
    /// <summary>Small editor helpers for the first AssetBundle integration exercise.</summary>
    public static class AssetBundleLearningTools
    {
        private static readonly string SourceBundleRoot = Path.GetFullPath(
            Path.Combine(Application.dataPath, "../../AssetBundle/Windows"));

        private static readonly string StreamingAssetsBundleRoot = Path.Combine(
            Application.dataPath, "StreamingAssets", "AssetBundles", AssetBundleRuntime.PlatformFolderName);

        [MenuItem("FpsDemo/Asset Bundles/Sync Windows Bundles To StreamingAssets")]
        public static void SyncWindowsBundlesToStreamingAssets()
        {
            if (!Directory.Exists(SourceBundleRoot))
            {
                Debug.LogError("[AssetBundles] Source Bundle directory was not found: " + SourceBundleRoot);
                return;
            }

            string[] sourceFiles = Directory.GetFiles(SourceBundleRoot, "*", SearchOption.AllDirectories);
            for (int i = 0; i < sourceFiles.Length; i++)
            {
                string sourceFile = sourceFiles[i];
                string relativePath = sourceFile.Substring(SourceBundleRoot.Length).TrimStart(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
                string destinationFile = Path.Combine(StreamingAssetsBundleRoot, relativePath);
                string destinationDirectory = Path.GetDirectoryName(destinationFile);
                if (!Directory.Exists(destinationDirectory))
                    Directory.CreateDirectory(destinationDirectory);

                File.Copy(sourceFile, destinationFile, overwrite: true);
            }

            AssetDatabase.Refresh();
            Debug.Log("[AssetBundles] Synced " + sourceFiles.Length + " files to " + StreamingAssetsBundleRoot);
        }

        [MenuItem("FpsDemo/Asset Bundles/Install Runtime Test In Open Scene")]
        public static void InstallRuntimeTestInOpenScene()
        {
            GameObject root = GameObject.Find("--DDOL--");
            if (root == null)
            {
                Debug.LogError("[AssetBundles] Open Lobby or DeathMatch first. The scene needs a --DDOL-- root.");
                return;
            }

            if (root.GetComponent<AssetBundleRuntime>() == null)
                root.AddComponent<AssetBundleRuntime>();
            if (root.GetComponent<AssetBundleLoadTest>() == null)
                root.AddComponent<AssetBundleLoadTest>();

            EditorUtility.SetDirty(root);
            EditorSceneManager.MarkSceneDirty(root.scene);
            Selection.activeGameObject = root;
            Debug.Log("[AssetBundles] Installed runtime and F8 test on --DDOL--. Save the scene before Play.");
        }
    }
}
