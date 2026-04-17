using FpsDemo.Ai;
using UnityEngine;

namespace FpsDemo.Audio
{
    /// <summary>
    /// 订阅 <see cref="FpsAiHitscanWeapon"/> 的 <c>ShotFired</c> / <c>ReloadStarted</c>，
    /// 在本地 <see cref="AudioSource"/> 上 <c>PlayOneShot</c>（需 <b>Spatial Blend = 1</b> 的 3D 音源，枪声才随位置变化）。
    /// 与 <see cref="FpsWeaponAudioObserver"/>（全局 2D）分工：玩家自机用后者，人机用本组件。
    /// </summary>
    [DefaultExecutionOrder(-39)]
    public sealed class FpsAiWeaponSpatialAudio : MonoBehaviour
    {
        [SerializeField] private FpsAiHitscanWeapon _weapon;
        [Tooltip("人机枪声发声点；Spatial Blend 建议为 1（全 3D）。")]
        [SerializeField] private AudioSource _audioSource3D;
        [SerializeField] private AudioClip _fireClip;
        [SerializeField] private AudioClip _reloadClip;

        private void Awake()
        {
            if (_weapon == null)
                _weapon = GetComponent<FpsAiHitscanWeapon>();
            if (_audioSource3D == null)
                _audioSource3D = GetComponent<AudioSource>();
        }

        private void OnEnable()
        {
            if (_weapon == null)
                return;

            _weapon.ShotFired += HandleShotFired;
            _weapon.ReloadStarted += HandleReloadStarted;
        }

        private void OnDisable()
        {
            if (_weapon == null)
                return;

            _weapon.ShotFired -= HandleShotFired;
            _weapon.ReloadStarted -= HandleReloadStarted;
        }

        private void HandleShotFired()
        {
            Play(_fireClip);
        }

        private void HandleReloadStarted()
        {
            Play(_reloadClip);
        }

        private void Play(AudioClip clip)
        {
            if (clip == null || _audioSource3D == null)
                return;

            _audioSource3D.PlayOneShot(clip);
        }
    }
}
