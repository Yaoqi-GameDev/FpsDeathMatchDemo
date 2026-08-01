using System.Collections.Generic;
using FpsDemo.Combat;
using FpsDemo.Match;
using FpsDemo.Netcode;
using TMPro;
using UIFramework;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.UI;

namespace FpsDemo.UI
{
    /// <summary>
    /// Deathmatch HUD panel. Prefab / ScreenId: <c>DeathmatchHudPanelController</c>.
    /// Core: timer / leaderboard / kill feed / health.
    /// Same prefab also hosts: <see cref="AmmoHub"/>, <see cref="FpsCrosshairHitFeedback"/>,
    /// <see cref="KillStreakHudPlaceholder"/>, <see cref="ClientLatencyHud"/>.
    /// Hurt flash is a separate Prioritary panel: <see cref="HurtOverlayPanelController"/>.
    /// Network kill feed via <see cref="AppendKillFeedFromNetwork"/>.
    /// </summary>
    public sealed class DeathmatchHudPanelController : PanelController
    {
        public const string ScreenId = "DeathmatchHudPanelController";

        private struct FeedEntry
        {
            public string Text;
            public Color Color;
            public float ExpireAtUnscaled;
        }

        [Header("数据")]
        [SerializeField] private MatchManager _matchManager;
        [SerializeField] private MatchParticipant _localPlayerParticipant;
        [SerializeField] private Health _playerHealth;

        [Header("顶栏：剩余时间")]
        [SerializeField] private TMP_Text _matchTimerText;

        [Header("顶栏：排行")]
        [SerializeField] private TMP_Text _leaderboardText;

        [Header("右上：击杀播报（0 = 最新）")]
        [SerializeField] private TMP_Text[] _killFeedLines;
        [SerializeField] private float _killFeedHoldSeconds = 4f;
        [SerializeField] private Color _killFeedNormalColor = Color.white;
        [SerializeField] private Color _killFeedSelfInvolvedColor = new Color(1f, 0.85f, 0.2f);

        [Header("左下：血量")]
        [SerializeField] private Slider _healthSlider;
        [SerializeField] private TMP_Text _healthNumber;

        private readonly List<FeedEntry> _feed = new List<FeedEntry>(6);
        private MatchManager _resolvedMatch;

        private static bool UseNetworkKillFeed =>
            NetworkManager.Singleton != null && NetworkManager.Singleton.IsListening;

        /// <summary>联机 ClientRpc 入口。</summary>
        public static void AppendKillFeedFromNetwork(string killerDisplayName, string victimDisplayName)
        {
#pragma warning disable CS0618
            var hud = Object.FindObjectOfType<DeathmatchHudPanelController>(includeInactive: true);
#pragma warning restore CS0618
            if (hud == null)
                return;
            hud.AppendKillFeedLine(killerDisplayName, victimDisplayName);
        }

        protected override void AddListeners()
        {
            CombatKillBus.KillCommitted += OnKillCommitted;
        }

        protected override void RemoveListeners()
        {
            CombatKillBus.KillCommitted -= OnKillCommitted;
        }

        private void Start()
        {
            _resolvedMatch = _matchManager != null ? _matchManager : MatchManager.Instance;
            TryResolvePlayerHealth();
        }

        private void Update()
        {
            if (!IsVisible)
                return;

            float u = Time.unscaledTime;
            for (int i = _feed.Count - 1; i >= 0; i--)
            {
                if (u >= _feed[i].ExpireAtUnscaled)
                    _feed.RemoveAt(i);
            }

            TryResolvePlayerHealth();
            RefreshTimerAndLeaderboard();
            RefreshKillFeedLines();
            RefreshHealth();
        }

        private void TryResolvePlayerHealth()
        {
            if (_playerHealth != null)
                return;

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

            if (_localPlayerParticipant != null)
                _playerHealth = _localPlayerParticipant.GetComponent<Health>();
        }

        private void RefreshTimerAndLeaderboard()
        {
            var mm = _resolvedMatch != null ? _resolvedMatch : MatchManager.Instance;
            if (mm == null)
                return;

            if (_matchTimerText != null)
            {
                float t = Mathf.Max(0f, mm.RemainingMatchSeconds);
                int m = (int)(t / 60f);
                int s = (int)(t % 60f);
                _matchTimerText.text = $"{m:00}:{s:00}";
            }

            if (_leaderboardText != null)
            {
                var rows = BuildLeaderboardRows(mm);
                var lines = new List<string>(rows.Count);
                int rank = 1;
                foreach (var row in rows)
                {
                    lines.Add($"{rank}. {row.name}  {row.kills}");
                    rank++;
                }

                string target = mm.TargetKills > 0 ? $"Target {mm.TargetKills} Kills" : "";
                _leaderboardText.text = string.IsNullOrEmpty(target)
                    ? string.Join("\n", lines)
                    : $"{target}\n" + string.Join("\n", lines);
            }
        }

        private static List<(string name, int kills)> BuildLeaderboardRows(MatchManager mm)
        {
            var nm = NetworkManager.Singleton;
            if (nm == null || !nm.IsListening)
            {
                var classic = mm.GetLeaderboardDescending();
                var list = new List<(string name, int kills)>(classic.Count);
                foreach (var row in classic)
                {
                    string name = row.participant != null ? row.participant.DisplayName : "?";
                    list.Add((name, row.kills));
                }

                return list;
            }

            var byParticipant = new Dictionary<MatchParticipant, int>(16);

            foreach (var netObj in nm.SpawnManager.SpawnedObjects.Values)
            {
                if (netObj == null || !netObj.IsSpawned)
                    continue;
                var stats = netObj.GetComponent<PlayerMatchStatsNet>();
                var mp = netObj.GetComponent<MatchParticipant>();
                if (stats == null || mp == null)
                    continue;
                byParticipant[mp] = stats.NetworkKills;
            }

            foreach (var p in MatchParticipant.ActiveParticipants)
            {
                if (p == null || byParticipant.ContainsKey(p))
                    continue;
                byParticipant[p] = mm.GetKills(p);
            }

            var rows = new List<(string name, int kills)>(byParticipant.Count);
            foreach (var kv in byParticipant)
                rows.Add((kv.Key != null ? kv.Key.DisplayName : "?", kv.Value));

            rows.Sort((a, b) => b.kills.CompareTo(a.kills));
            return rows;
        }

        private void RefreshHealth()
        {
            if (_healthSlider == null && _healthNumber == null)
                return;

            if (_playerHealth == null)
            {
                if (_healthSlider != null)
                    _healthSlider.gameObject.SetActive(false);
                if (_healthNumber != null)
                    _healthNumber.text = "";
                return;
            }

            if (_healthSlider != null)
            {
                _healthSlider.gameObject.SetActive(true);
                float max = _playerHealth.Max;
                _healthSlider.normalizedValue = max > 0.001f ? _playerHealth.Current / max : 0f;
            }

            if (_healthNumber != null)
                _healthNumber.text = $"{Mathf.CeilToInt(_playerHealth.Current)} / {Mathf.CeilToInt(_playerHealth.Max)}";
        }

        private void OnKillCommitted(KillReport report)
        {
            if (UseNetworkKillFeed)
                return;

            AppendKillFeedLine(ResolveDisplayName(report.Killer), ResolveDisplayName(report.Victim), IsLocalInvolved(report));
        }

        private void AppendKillFeedLine(string killerDisplayName, string victimDisplayName, bool localPlayerInvolved)
        {
            if (_killFeedLines == null || _killFeedLines.Length == 0)
                return;

            var mm = _resolvedMatch != null ? _resolvedMatch : MatchManager.Instance;
            if (mm != null && mm.IsMatchOver)
                return;

            Color c = localPlayerInvolved ? _killFeedSelfInvolvedColor : _killFeedNormalColor;

            _feed.Insert(
                0,
                new FeedEntry
                {
                    Text = $"{killerDisplayName}  →  {victimDisplayName}",
                    Color = c,
                    ExpireAtUnscaled = Time.unscaledTime + Mathf.Max(0.1f, _killFeedHoldSeconds),
                });

            while (_feed.Count > _killFeedLines.Length)
                _feed.RemoveAt(_feed.Count - 1);
        }

        private void AppendKillFeedLine(string killerDisplayName, string victimDisplayName)
        {
            TryResolvePlayerHealth();
            bool self = IsLocalInvolvedByDisplayNames(killerDisplayName, victimDisplayName);
            AppendKillFeedLine(killerDisplayName, victimDisplayName, self);
        }

        private bool IsLocalInvolvedByDisplayNames(string killerDisplayName, string victimDisplayName)
        {
            if (_localPlayerParticipant == null)
                return false;
            string me = _localPlayerParticipant.DisplayName;
            return killerDisplayName == me || victimDisplayName == me;
        }

        private void RefreshKillFeedLines()
        {
            if (_killFeedLines == null)
                return;

            for (int i = 0; i < _killFeedLines.Length; i++)
            {
                var line = _killFeedLines[i];
                if (line == null)
                    continue;

                if (i < _feed.Count)
                {
                    line.gameObject.SetActive(true);
                    line.text = _feed[i].Text;
                    line.color = _feed[i].Color;
                }
                else
                {
                    line.text = "";
                    line.gameObject.SetActive(false);
                }
            }
        }

        private static string ResolveDisplayName(GameObject go)
        {
            if (go == null)
                return "?";

            var mp = go.GetComponent<MatchParticipant>();
            if (mp != null)
                return mp.DisplayName;

            return go.name;
        }

        private bool IsLocalInvolved(KillReport report)
        {
            if (_localPlayerParticipant == null)
                return false;

            GameObject root = _localPlayerParticipant.Root;
            if (root == null)
                return false;

            if (report.Killer != null && report.Killer == root)
                return true;

            if (report.Victim != null && report.Victim == root)
                return true;

            return false;
        }
    }
}
