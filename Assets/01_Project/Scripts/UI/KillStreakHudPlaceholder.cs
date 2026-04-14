using System.Collections;
using FpsDemo.Match;
using TMPro;
using UnityEngine;

namespace FpsDemo.UI
{
    /// <summary>
    /// 连杀占位 HUD：订阅 <see cref="KillStreakTracker.StreakChanged"/>（通常由 <see cref="KillStreakTracker.Local"/> 解析）。
    /// 挂在 <c>DeathmatchHUD</c> / Canvas 下即可。
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class KillStreakHudPlaceholder : MonoBehaviour
    {
        [Header("显示")]
        [SerializeField] private TMP_Text _streakText;

        [Tooltip("例如 连杀 x{0}；{0} 为当前连杀数。")]
        [SerializeField] private string _format = "连杀 x{0}";

        [Header("数据")]
        [Tooltip("空则使用 KillStreakTracker.Local")]
        [SerializeField] private KillStreakTracker _tracker;

        private bool _listening;

        private void OnEnable()
        {
            TryBind();
        }

        private void Start()
        {
            StartCoroutine(DelayedBind());
        }

        private IEnumerator DelayedBind()
        {
            yield return null;
            if (!_listening)
                TryBind();
        }

        private void OnDisable()
        {
            Unbind();
        }

        private void TryBind()
        {
            if (_listening)
                return;

            if (_tracker == null)
                _tracker = KillStreakTracker.Local;

            if (_tracker == null)
            {
                Refresh(0);
                return;
            }

            _tracker.StreakChanged += OnStreakChanged;
            _listening = true;
            Refresh(_tracker.CurrentStreak);
        }

        private void Unbind()
        {
            if (!_listening || _tracker == null)
                return;

            _tracker.StreakChanged -= OnStreakChanged;
            _listening = false;
        }

        private void OnStreakChanged(int streak)
        {
            Refresh(streak);
        }

        private void Refresh(int streak)
        {
            if (_streakText == null)
                return;

            if (streak <= 0)
            {
                _streakText.gameObject.SetActive(false);
                return;
            }

            _streakText.gameObject.SetActive(true);
            _streakText.text = string.Format(_format, streak);
        }
    }
}
