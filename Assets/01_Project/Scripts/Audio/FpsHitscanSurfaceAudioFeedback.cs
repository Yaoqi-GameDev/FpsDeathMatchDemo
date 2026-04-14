using FpsDemo.Combat;
using UnityEngine;

namespace FpsDemo.Audio
{
    /// <summary>
    /// 仅玩家：订阅 <see cref="FpsHitscanWeapon.ShotResolved"/>，按命中类型播放不同 2D 命中音（可伤害体 / 环境）。
    /// 人机请使用 <see cref="FpsAiHitscanWeapon"/> 另行挂载变体；本组件不处理 AI。
    /// </summary>
    [DefaultExecutionOrder(-39)]
    public sealed class FpsHitscanSurfaceAudioFeedback : MonoBehaviour
    {
        [SerializeField] private FpsHitscanWeapon _weapon;
        [SerializeField] private AudioClip _hitDamageableClip;
        [SerializeField] private AudioClip _hitWorldClip;

        private void Awake()
        {
            if (_weapon == null)
                _weapon = GetComponent<FpsHitscanWeapon>();
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
            if (!info.HasWorldHit)
                return;

            AudioClip clip = info.HitDamageable ? _hitDamageableClip : _hitWorldClip;
            if (clip == null || AudioManager.Instance == null)
                return;

            AudioManager.Instance.PlayOneShot2D(clip);
        }
    }
}
