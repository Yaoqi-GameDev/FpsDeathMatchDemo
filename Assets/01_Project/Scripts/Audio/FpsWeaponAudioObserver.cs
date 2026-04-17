using FpsDemo.Combat;
using UnityEngine;

namespace FpsDemo.Audio
{
    /// <summary>
    /// 订阅 <see cref="FpsHitscanWeapon"/> 的 C# 事件，将开火/换弹/空枪（<see cref="FpsHitscanWeapon.DryFire"/>）映射为 <see cref="AudioClip"/>，经 <see cref="AudioManager"/> 播放。
    /// 与武器解耦：武器不引用音频资源。
    /// </summary>
    [DefaultExecutionOrder(-39)]
    public sealed class FpsWeaponAudioObserver : MonoBehaviour
    {
        [SerializeField] private FpsHitscanWeapon _weapon;
        [SerializeField] private AudioClip _fireClip;
        [SerializeField] private AudioClip _reloadClip;
        [Tooltip("弹匣空且本帧按下开火时（与武器 DryFire 一致）；未拖则不播。")]
        [SerializeField] private AudioClip _dryFireClip;

        private void Awake()
        {
            if (_weapon == null)
                _weapon = GetComponent<FpsHitscanWeapon>();
        }

        private void OnEnable()
        {
            if (_weapon == null)
                return;

            _weapon.ShotFired += HandleShotFired;
            _weapon.ReloadStarted += HandleReloadStarted;
            _weapon.DryFire += HandleDryFire;
        }

        private void OnDisable()
        {
            if (_weapon == null)
                return;

            _weapon.ShotFired -= HandleShotFired;
            _weapon.ReloadStarted -= HandleReloadStarted;
            _weapon.DryFire -= HandleDryFire;
        }

        private void HandleShotFired()
        {
            Play(_fireClip);
        }

        private void HandleReloadStarted()
        {
            Play(_reloadClip);
        }

        private void HandleDryFire()
        {
            Play(_dryFireClip);
        }

        private static void Play(AudioClip clip)
        {
            if (clip == null || AudioManager.Instance == null)
                return;

            AudioManager.Instance.PlayOneShot2D(clip);
        }
    }
}
