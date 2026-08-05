using System;
using System.Collections;
using System.IO;
using AssetBundleFramework;
using UnityEngine;

namespace FpsDemo.AssetBundles
{
    /// <summary>
    /// Keeps the imported AssetBundle framework alive and drives its async work.
    /// Attach this to the existing --DDOL-- root so it survives scene changes.
    /// </summary>
    [DisallowMultipleComponent]
    [DefaultExecutionOrder(-450)]
    public sealed class AssetBundleRuntime : MonoBehaviour
    {
        public const string PlatformFolderName = "Windows";

        public static AssetBundleRuntime Instance { get; private set; }
        public static bool IsInitialized { get; private set; }

        private string _bundleRoot;

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(this);
                return;
            }

            Instance = this;
            StartCoroutine(Bootstrap());
        }

        private IEnumerator Bootstrap()
        {
            yield return AssetBundleUpdateManager.CheckRemoteVersion();
            Initialize();
        }

        private void OnDestroy()
        {
            if (Instance == this)
            {
                Instance = null;
                IsInitialized = false;
            }
        }

        private void Update()
        {
            if (IsInitialized)
                ResourceManager.instance.Update();
        }

        private void LateUpdate()
        {
            if (IsInitialized)
                ResourceManager.instance.LateUpdate();
        }

        private void Initialize()
        {
            _bundleRoot = AssetBundlePathResolver.ResolveBundleRoot(PlatformFolderName);

            if (!Directory.Exists(_bundleRoot))
            {
                Debug.LogError(
                    "[AssetBundles] Bundle directory was not found: " + _bundleRoot +
                    ". Run FpsDemo/Asset Bundles/Sync Windows Bundles To StreamingAssets after building bundles.",
                    this);
                enabled = false;
                return;
            }

            try
            {
                ResourceManager.instance.Initialize(PlatformFolderName, GetBundleFilePath, editor: false, offset: 0);
                IsInitialized = true;
                Debug.Log("[AssetBundles] Initialized from " + _bundleRoot, this);
            }
            catch (Exception e)
            {
                Debug.LogException(e, this);
                enabled = false;
            }
        }

        private string GetBundleFilePath(string bundleName)
        {
            return Path.Combine(_bundleRoot, bundleName);
        }
    }
}
