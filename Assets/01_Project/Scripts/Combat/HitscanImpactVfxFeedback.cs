using System.Collections;
using FpsDemo.Ai;
using FpsDemo.AssetBundles;
using UnityEngine;
using AssetBundleFramework;

namespace FpsDemo.Combat
{
    /// <summary>
    /// 订阅同物体上 <see cref="FpsHitscanWeapon"/> 或 <see cref="FpsAiHitscanWeapon"/> 的 <c>ShotResolved</c>：
    /// 命中可伤害体（敌人等）用 <see cref="_impactDamageablePrefab"/>，否则用环境特效 <see cref="_impactWorldPrefab"/>。
    /// 玩家联机时 <see cref="FpsHitscanWeapon"/> 的解析结果来自服务端判伤回传；人机仍为本地解析。
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class HitscanImpactVfxFeedback : MonoBehaviour
    {
        [SerializeField] private FpsHitscanWeapon _playerWeapon;
        [SerializeField] private FpsAiHitscanWeapon _aiWeapon;

        //替换为ab包资源 而不是直接拖引用
        private IResource _impactDamageableResource;
        private IResource _impactWorldResource;
        [SerializeField] private float _impactFxLifetimeSeconds = 2f;

        //ab包资源路径
        private const string DamageableImpactPath = "Assets/01_Project/AssetBundleAssets/VFX/VFX_Blood_01.prefab";
        private const string WorldImpactPath = "Assets/01_Project/AssetBundleAssets/VFX/VFX_Classic_03.prefab";


        private void Awake()
        {
            if (_playerWeapon == null)
                _playerWeapon = GetComponent<FpsHitscanWeapon>();
            if (_aiWeapon == null)
                _aiWeapon = GetComponent<FpsAiHitscanWeapon>();

            if (_playerWeapon == null && _aiWeapon == null){
                Debug.LogError("HitscanImpactVfxFeedback组件没有绑定FpsHitscanWeapon或FpsAiHitscanWeapon组件");
                enabled = false;
                return;
            }
                

            //加载ab包资源
            StartCoroutine(LoadResourcesWhenRuntimeReady());
        }

        private IEnumerator LoadResourcesWhenRuntimeReady()
        {
            while (!AssetBundleRuntime.IsInitialized)
                yield return null;

            ResourceManager.instance.LoadWithCallback(
                DamageableImpactPath,
                async: true,
                resource => _impactDamageableResource = resource);

            ResourceManager.instance.LoadWithCallback(
                WorldImpactPath,
                async: true,
                resource => _impactWorldResource = resource);
        }

        private void OnEnable()
        {
            if (_playerWeapon != null)
                _playerWeapon.ShotResolved += OnShotResolved;
            if (_aiWeapon != null)
                _aiWeapon.ShotResolved += OnShotResolved;
        }

        private void OnDisable()
        {
            if (_playerWeapon != null)
                _playerWeapon.ShotResolved -= OnShotResolved;
            if (_aiWeapon != null)
                _aiWeapon.ShotResolved -= OnShotResolved;
        }

        private void OnShotResolved(ShotHitInfo info)
        {
            if (!info.HasWorldHit)
                return;

            bool useBloodImpact = info.HitDamageable;
            IResource resource = useBloodImpact
                    ? _impactDamageableResource
                    : _impactWorldResource;
            if (resource == null)
                return;

            Quaternion rot = info.Normal.sqrMagnitude > 1e-6f
                ? Quaternion.LookRotation(info.Normal)
                : Quaternion.identity;

            GameObject go = resource.Instantiate(info.Point, rot);
            if (go == null){
                Debug.LogError("HitscanImpactVfxFeedback组件加载ab包资源失败");
                return;
            }
            PlayParticleIfAny(go);
            Destroy(go, Mathf.Max(0.1f, _impactFxLifetimeSeconds));
        }

        private static void PlayParticleIfAny(GameObject root)
        {
            var systems = root.GetComponentsInChildren<ParticleSystem>(true);
            for (int i = 0; i < systems.Length; i++)
            {
                if (systems[i] != null)
                    systems[i].Play(true);
            }
        }

    }
}
