using FpsDemo.Combat;
using UnityEngine;

namespace FpsDemo.Ai
{
    /// <summary>
    /// 人机专用：订阅同物体上 <see cref="FpsAiHitscanWeapon"/> 的 <c>ShotResolved</c>，播放命中特效。
    /// 资源示例：Infima <c>P_IMP_Concrete</c>。挂在与 <see cref="FpsAiHitscanWeapon"/> 同一物体上。
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class AiHitscanVfxFeedback : MonoBehaviour
    {
        [SerializeField] private FpsAiHitscanWeapon _weapon;
        [SerializeField] private GameObject _impactPrefab;
        [SerializeField] private float _impactFxLifetimeSeconds = 2f;

        private void Awake()
        {
            if (_weapon == null)
                _weapon = GetComponent<FpsAiHitscanWeapon>();
        }

        private void OnEnable()
        {
            if (_weapon == null)
                return;

            _weapon.ShotResolved += OnShotResolved;
        }

        private void OnDisable()
        {
            if (_weapon == null)
                return;

            _weapon.ShotResolved -= OnShotResolved;
        }

        private void OnShotResolved(ShotHitInfo info)
        {
            if (_impactPrefab == null || !info.HasWorldHit)
                return;

            Quaternion rot = info.Normal.sqrMagnitude > 1e-6f
                ? Quaternion.LookRotation(info.Normal)
                : Quaternion.identity;

            GameObject go = Instantiate(_impactPrefab, info.Point, rot);
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
