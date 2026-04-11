using UnityEngine;

namespace FpsDemo.Audio
{
    /// <summary>
    /// 全局 2D 一次性音效（<see cref="AudioSource.PlayOneShot"/>）。场景中放一个实例，拖入 <see cref="AudioSource"/>。
    /// 武器等脚本只传 <see cref="AudioClip"/>，不负责持有发声组件。
    /// </summary>
    [DefaultExecutionOrder(-200)]
    public sealed class AudioManager : MonoBehaviour
    {
        public static AudioManager Instance { get; private set; }

        [Tooltip("用于 PlayOneShot；Spatial Blend 建议 0（2D）。")]
        [SerializeField] private AudioSource _oneShot2D;

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }

            Instance = this;
        }

        private void OnDestroy()
        {
            if (Instance == this)
                Instance = null;
        }

        /// <summary>叠加播放，适合开火等短音效。</summary>
        public void PlayOneShot2D(AudioClip clip)
        {
            if (clip == null || _oneShot2D == null)
                return;

            _oneShot2D.PlayOneShot(clip);
        }
    }
}
