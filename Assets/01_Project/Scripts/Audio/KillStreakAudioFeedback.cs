using FpsDemo.Match;
using UnityEngine;

namespace FpsDemo.Audio
{
    /// <summary>
    /// 挂在<strong>本地 Player 根</strong>：订阅同物体上 <see cref="KillStreakTracker.StreakChanged"/>，
    /// 按当前连杀档位经 <see cref="AudioManager.PlayOneShot2D"/> 播放单杀～五杀（<see cref="_killStreakClips"/> 下标 0～4）。
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class KillStreakAudioFeedback : MonoBehaviour
    {
        [Header("音效（顺序：单杀 → 五杀，共 5 条）")]
        [SerializeField] private AudioClip[] _killStreakClips = new AudioClip[5];

        [Tooltip("空则同物体 GetComponent")]
        [SerializeField] private KillStreakTracker _tracker;

        private void Awake()
        {
            if (_tracker == null)
                _tracker = GetComponent<KillStreakTracker>();
        }

        private void OnEnable()
        {
            if (_tracker != null)
                _tracker.StreakChanged += OnStreakChanged;
        }

        private void OnDisable()
        {
            if (_tracker != null)
                _tracker.StreakChanged -= OnStreakChanged;
        }

        private void OnStreakChanged(int streak)
        {
            if (streak <= 0)
                return;

            if (_killStreakClips == null || _killStreakClips.Length == 0)
                return;

            int tier = Mathf.Clamp(streak, 1, 5);
            int idx = tier - 1;
            if (idx >= _killStreakClips.Length)
                idx = _killStreakClips.Length - 1;

            var clip = _killStreakClips[idx];
            if (clip == null)
                return;

            if (AudioManager.Instance != null)
                AudioManager.Instance.PlayOneShot2D(clip);
        }
    }
}
