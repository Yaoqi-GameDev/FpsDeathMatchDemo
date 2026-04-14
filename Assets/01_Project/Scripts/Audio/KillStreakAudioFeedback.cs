using System.Collections;
using FpsDemo.Combat;
using FpsDemo.Match;
using UnityEngine;

namespace FpsDemo.Audio
{
    /// <summary>
    /// 挂在<strong>本地 Player 根</strong>（与 <see cref="FpsWeaponAudioObserver"/> 同类）：订阅 <see cref="CombatKillBus"/>，
    /// 仅当本物体为击杀 <c>Killer</c> 时在<strong>时间窗内</strong>累加连杀并经 <see cref="AudioManager.PlayOneShot2D"/> 播放
    /// 单杀～五杀（<see cref="_killStreakClips"/> 下标 0～4）。超时、本地死亡、局结束会清零连杀。
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class KillStreakAudioFeedback : MonoBehaviour
    {
        [Header("规则")]
        [Tooltip("两次击杀间隔不超过该秒数（unscaled）则视为连杀累加，否则从 1 重新计。")]
        [SerializeField] private float _streakWindowSeconds = 4f;

        [Header("音效（顺序：单杀 → 五杀，共 5 条）")]
        [SerializeField] private AudioClip[] _killStreakClips = new AudioClip[5];

        [Header("引用（可空，脚本会尽量自动解析）")]
        [Tooltip("空则使用 MatchManager.Instance")]
        [SerializeField] private MatchManager _matchManager;

        [Tooltip("空则在本物体上 GetComponent，再回退 ActiveParticipants")]
        [SerializeField] private MatchParticipant _localPlayerParticipant;

        [Tooltip("空则在参与者根上 GetComponent<Health>")]
        [SerializeField] private Health _localHealth;

        private MatchManager _resolvedMatch;
        private int _streak;
        private float _lastKillUnscaledTime;
        private bool _healthDiedSubscribed;

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

        private void OnDestroy()
        {
            if (_healthDiedSubscribed && _localHealth != null)
                _localHealth.Died -= OnLocalPlayerDied;
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
                _streak = 0;
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

            if (_killStreakClips == null || _killStreakClips.Length == 0)
                return;

            float now = Time.unscaledTime;
            if (_streak > 0 && now - _lastKillUnscaledTime <= _streakWindowSeconds)
                _streak++;
            else
                _streak = 1;

            _lastKillUnscaledTime = now;

            int tier = Mathf.Clamp(_streak, 1, 5);
            int idx = tier - 1;
            if (idx >= _killStreakClips.Length)
                idx = _killStreakClips.Length - 1;

            var clip = _killStreakClips[idx];
            if (clip == null)
                return;

            if (AudioManager.Instance != null)
                AudioManager.Instance.PlayOneShot2D(clip);
        }

        private void OnLocalPlayerDied(KillReport _)
        {
            _streak = 0;
        }

        private void OnMatchEnded(MatchResult _)
        {
            _streak = 0;
        }
    }
}
