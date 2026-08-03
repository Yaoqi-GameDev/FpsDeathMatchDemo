using AssetBundleFramework;
using UnityEngine;

namespace FpsDemo.AssetBundles
{
    /// <summary>
    /// Temporary learning probe. F8 loads a visible cube through an actual AssetBundle.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class AssetBundleLoadTest : MonoBehaviour
    {
        private const string TestPrefabPath =
            "Assets/01_Project/AssetBundleAssets/AB_VisibleCube.prefab";

        [SerializeField] private KeyCode _loadKey = KeyCode.F8;
        [SerializeField] private float _lifetimeSeconds = 2f;

        private void Update()
        {
            if (Input.GetKeyDown(_loadKey))
                LoadAndSpawn();
        }

        private void LoadAndSpawn()
        {
            if (!AssetBundleRuntime.IsInitialized)
            {
                Debug.LogError("[AssetBundles] Runtime is not initialized. Check the Bundle directory log first.", this);
                return;
            }

            ResourceManager.instance.LoadWithCallback(TestPrefabPath, async: true, OnLoaded);
        }

        private void OnLoaded(IResource resource)
        {
            if (resource == null)
            {
                Debug.LogError("[AssetBundles] Test resource returned null.", this);
                return;
            }

            Camera camera = FindWorldCamera();
            Vector3 position = camera != null
                ? camera.transform.position + camera.transform.forward * 2f
                : Vector3.zero;
            Quaternion rotation = camera != null ? camera.transform.rotation : Quaternion.identity;

            GameObject instance = resource.Instantiate(position, rotation, autoUnload: true);
            if (instance == null)
            {
                Debug.LogError("[AssetBundles] Loaded asset was not a GameObject: " + TestPrefabPath, this);
                ResourceManager.instance.Unload(resource);
                return;
            }

            PlayParticleSystems(instance);
            Destroy(instance, Mathf.Max(0.1f, _lifetimeSeconds));
            Debug.Log("[AssetBundles] Spawned AB test prefab: " + TestPrefabPath, this);
        }

        private static Camera FindWorldCamera()
        {
            const int defaultLayerMask = 1 << 0;
            Camera result = null;

            foreach (Camera camera in Camera.allCameras)
            {
                if (camera == null || !camera.enabled || !camera.gameObject.activeInHierarchy)
                    continue;

                if ((camera.cullingMask & defaultLayerMask) == 0)
                    continue;

                if (result == null || camera.depth > result.depth)
                    result = camera;
            }

            return result;
        }

        private static void PlayParticleSystems(GameObject root)
        {
            ParticleSystem[] systems = root.GetComponentsInChildren<ParticleSystem>(true);
            for (int i = 0; i < systems.Length; i++)
            {
                if (systems[i] != null)
                    systems[i].Play(true);
            }
        }
    }
}
