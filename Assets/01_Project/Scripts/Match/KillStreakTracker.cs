using System;
using System.Collections;
using FpsDemo.Combat;
using UnityEngine;

namespace FpsDemo.Match
{
    /// <summary>
    /// 挂在<strong>本地 Player 根</strong>：与 <see cref="MatchParticipant"/> 同物体，维护连杀计数（与音效/UI 共用一套规则）。
    /// 订阅 <see cref="CombatKillBus"/>；时间窗、死亡、局结束清零；通过 <see cref="StreakChanged"/> 通知。
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class KillStreakTracker : MonoBehaviour
    {
        /// <summary>当前本地玩家上的 <see cref="KillStreakTracker"/>（单机单本地）。</summary>
        public static KillStreakTracker Local { get; private set; }

        [Header("规则")]
        [Tooltip("两次击杀间隔不超过该秒数（unscaled）则视为连杀累加，否则从 1 重新计。")]
        [SerializeField] private float _streakWindowSeconds = 4f;

        [Header("引用（可空）")]
        [Tooltip("空则使用 MatchManager.Instance")]
        [SerializeField] private MatchManager _matchManager;

        [Tooltip("空则在本物体 GetComponent，再回退 ActiveParticipants")]
        [SerializeField] private MatchParticipant _localPlayerParticipant;

        [Tooltip("空则在参与者根上 GetComponent<Health>")]
        [SerializeField] private Health _localHealth;

        private MatchManager _resolvedMatch;
        private int _streak;
        private float _lastKillUnscaledTime;
        private bool _healthDiedSubscribed;

        /// <summary>当前连杀数（0 表示无连杀）。</summary>
        public int CurrentStreak => _streak;

        /// <summary>连杀数变化时触发（含清零时为 0）。</summary>
        public event Action<int> StreakChanged;

        private void Awake()
        {
            var mp = GetComponent<MatchParticipant>();
            if (mp != null && mp.IsLocalPlayer)
                Local = this;
        }

        /// <summary>联机：<see cref="MatchParticipant.SetLocalPlayerForNetworking"/> 在网络 Spawn 之后才确定 Owner，晚于本组件 <see cref="Awake"/>。</summary>
        internal void AssignLocalAfterNetworking()
        {
            Local = this;
        }

        /// <summary>联机：非 Owner 拷贝不应占用 <see cref="Local"/>。</summary>
        internal void ReleaseLocalIfThis()
        {
            if (Local == this)
                Local = null;
        }

        private void OnDestroy()
        {
            if (Local == this)
                Local = null;

            if (_healthDiedSubscribed && _localHealth != null)
                _localHealth.Died -= OnLocalPlayerDied;
        }

        private void Start()
        {
            ResolveLocalPlayer();
            if (_localHealth != null)
            {
                _localHealth.Died += OnLocalPlayerDied;
                _healthDiedSubscribed = true;
            }
            else
                StartCoroutine(ResolveHealthNextFrame());
        }

        private IEnumerator ResolveHealthNextFrame()
        {
            yield return null;
            ResolveLocalPlayer();
            if (_localHealth != null && !_healthDiedSubscribed)
            {
                _localHealth.Died += OnLocalPlayerDied;
                _healthDiedSubscribed = true;
            }
        }

        private void OnEnable()
        {
            CombatKillBus.KillCommitted += OnKillCommitted;
            _resolvedMatch = _matchManager != null ? _matchManager : MatchManager.Instance;
            if (_resolvedMatch != null)
                _resolvedMatch.MatchEnded += OnMatchEnded;
        }

        private void OnDisable()
        {
            CombatKillBus.KillCommitted -= OnKillCommitted;
            if (_resolvedMatch != null)
                _resolvedMatch.MatchEnded -= OnMatchEnded;
        }

        private void Update()
        {
            if (_streak <= 0)
                return;

            if (Time.unscaledTime - _lastKillUnscaledTime > _streakWindowSeconds)
                ResetStreak();
        }

        private void ResolveLocalPlayer()
        {
            if (_localPlayerParticipant == null)
            {
                _localPlayerParticipant = GetComponent<MatchParticipant>();
                if (_localPlayerParticipant == null)
                {
                    foreach (var p in MatchParticipant.ActiveParticipants)
                    {
                        if (p != null && p.IsLocalPlayer)
                        {
                            _localPlayerParticipant = p;
                            break;
                        }
                    }
                }
            }

            if (_localHealth == null && _localPlayerParticipant != null)
                _localHealth = _localPlayerParticipant.GetComponent<Health>();
        }

        private void OnKillCommitted(KillReport report)
        {
            var mm = _resolvedMatch != null ? _resolvedMatch : MatchManager.Instance;
            if (mm != null && mm.IsMatchOver)
                return;

            if (report.Killer == null)
                return;

            if (report.Victim != null && report.Killer == report.Victim)
                return;

            if (report.Killer != gameObject)
                return;

            var selfPart = GetComponent<MatchParticipant>();
            if (selfPart == null || !selfPart.IsLocalPlayer)
                return;

            float now = Time.unscaledTime;
            if (_streak > 0 && now - _lastKillUnscaledTime <= _streakWindowSeconds)
                _streak++;
            else
                _streak = 1;

            _lastKillUnscaledTime = now;
            StreakChanged?.Invoke(_streak);
        }

        private void OnLocalPlayerDied(KillReport _)
        {
            ResetStreak();
        }

        private void OnMatchEnded(MatchResult _)
        {
            ResetStreak();
        }

        private void ResetStreak()
        {
            if (_streak == 0)
                return;

            _streak = 0;
            StreakChanged?.Invoke(0);
        }
    }
}
