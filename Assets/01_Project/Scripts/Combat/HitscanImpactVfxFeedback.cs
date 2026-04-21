using FpsDemo.Ai;
using UnityEngine;

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

        [SerializeField] private GameObject _impactDamageablePrefab;
        [SerializeField] private GameObject _impactWorldPrefab;
        [SerializeField] private float _impactFxLifetimeSeconds = 2f;

        private void Awake()
        {
            if (_playerWeapon == null)
                _playerWeapon = GetComponent<FpsHitscanWeapon>();
            if (_aiWeapon == null)
                _aiWeapon = GetComponent<FpsAiHitscanWeapon>();

            if (_playerWeapon == null && _aiWeapon == null)
                enabled = false;
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

            GameObject prefab = info.HitDamageable ? _impactDamageablePrefab : _impactWorldPrefab;
            if (prefab == null)
                return;

            Quaternion rot = info.Normal.sqrMagnitude > 1e-6f
                ? Quaternion.LookRotation(info.Normal)
                : Quaternion.identity;

            GameObject go = Instantiate(prefab, info.Point, rot);
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
