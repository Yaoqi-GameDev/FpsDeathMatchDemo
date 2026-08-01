using FpsDemo.Match;
using TMPro;
using UnityEngine;

namespace FpsDemo.UI
{
    /// <summary>
    /// 连杀占位 HUD：订阅 <see cref="KillStreakTracker.StreakChanged"/>（通常由 <see cref="KillStreakTracker.Local"/> 解析）。
    /// Tracker 可晚于 UI 生成；在 <see cref="LateUpdate"/> 持续尝试绑定。
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class KillStreakHudPlaceholder : MonoBehaviour
    {
        [Header("显示")]
        [SerializeField] private TMP_Text _streakText;

        [Tooltip("例如 Killx{0}；{0} 为当前连杀数。")]
        [SerializeField] private string _format = "Killx{0}";

        [Header("数据")]
        [Tooltip("空则使用 KillStreakTracker.Local")]
        [SerializeField] private KillStreakTracker _tracker;

        private bool _listening;

        private void OnEnable()
        {
            TryBind();
        }

        private void LateUpdate()
        {
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

            // 勿对「脚本同物体」SetActive(false)，否则 LateUpdate 再也跑不起来、也收不到连杀事件。
            if (streak <= 0)
            {
                _streakText.text = "";
                _streakText.enabled = false;
                return;
            }

            _streakText.enabled = true;
            _streakText.text = string.Format(_format, streak);
        }
    }
}
