using AssetBundleFramework;
using UnityEngine;

namespace FpsDemo.AssetBundles
{
    /// <summary>
    /// Temporary learning probe. F6/F7 load the two impact VFX prefabs through AssetBundles.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class AssetBundleLoadTest : MonoBehaviour
    {
        private const string DamageableImpactPath =
            "Assets/01_Project/AssetBundleAssets/VFX/VFX_Blood_01.prefab";

        private const string WorldImpactPath =
            "Assets/01_Project/AssetBundleAssets/VFX/VFX_Classic_03.prefab";

        [SerializeField] private KeyCode _damageableImpactKey = KeyCode.F6;
        [SerializeField] private KeyCode _worldImpactKey = KeyCode.F7;
        [SerializeField] private float _lifetimeSeconds = 2f;

        private void Update()
        {
            if (Input.GetKeyDown(_damageableImpactKey))
                LoadAndSpawn(DamageableImpactPath);

            if (Input.GetKeyDown(_worldImpactKey))
                LoadAndSpawn(WorldImpactPath);
        }

        private void LoadAndSpawn(string assetPath)
        {
            if (!AssetBundleRuntime.IsInitialized)
            {
                Debug.LogError("[AssetBundles] Runtime is not initialized. Check the Bundle directory log first.", this);
                return;
            }

            ResourceManager.instance.LoadWithCallback(
                assetPath,
                async: true,
                resource => OnLoaded(resource, assetPath));
        }

        private void OnLoaded(IResource resource, string assetPath)
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
                Debug.LogError("[AssetBundles] Loaded asset was not a GameObject: " + assetPath, this);
                ResourceManager.instance.Unload(resource);
                return;
            }

            PlayParticleSystems(instance);
            Destroy(instance, Mathf.Max(0.1f, _lifetimeSeconds));
            Debug.Log("[AssetBundles] Spawned AB test prefab: " + assetPath, this);
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
