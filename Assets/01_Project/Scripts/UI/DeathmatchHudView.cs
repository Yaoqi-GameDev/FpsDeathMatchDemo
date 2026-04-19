using System.Collections.Generic;
using FpsDemo.Combat;
using FpsDemo.Match;
using TMPro;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.UI;

namespace FpsDemo.UI
{
    /// <summary>
    /// 死斗 HUD：顶栏剩余时间与击杀排行、右上击杀播报（最多 5 条）、左下玩家血量条。
    /// 挂在 Canvas 下空物体上，在 Inspector 拖引用；击杀条使用 <b>unscaled</b> 时间，与 <c>timeScale=0</c> 结算兼容。
    /// 联机时本地玩家在 <see cref="NetworkBehaviour.OnNetworkSpawn"/> 之后才标记为本地，需在 <see cref="Update"/> 中持续解析 <see cref="Health"/>。
    /// 联机击杀播报由 <see cref="FpsDemo.Netcode.NetworkKillFeedBroadcaster"/> 发 <see cref="ClientRpc"/>，调用 <see cref="AppendKillFeedFromNetwork"/>；单机仍订阅 <see cref="CombatKillBus"/>。
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class DeathmatchHudView : MonoBehaviour
    {
        private struct FeedEntry
        {
            public string Text;
            public Color Color;
            public float ExpireAtUnscaled;
        }

        [Header("数据")]
        [Tooltip("空则使用 MatchManager.Instance")]
        [SerializeField] private MatchManager _matchManager;

        [Tooltip("空则在 Start 时从 ActiveParticipants 找 IsLocalPlayer")]
        [SerializeField] private MatchParticipant _localPlayerParticipant;

        [Tooltip("空则在本地参与者根上 GetComponent<Health>")]
        [SerializeField] private Health _playerHealth;

        [Header("顶栏：剩余时间")]
        [SerializeField] private TMP_Text _matchTimerText;

        [Header("顶栏：排行（多行 TMP）")]
        [SerializeField] private TMP_Text _leaderboardText;

        [Header("右上：击杀播报（元素 0 = 最新一条，向下变旧）")]
        [SerializeField] private TMP_Text[] _killFeedLines;

        [Header("右上：样式")]
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

        private void Start()
        {
            _resolvedMatch = _matchManager != null ? _matchManager : MatchManager.Instance;
            TryResolvePlayerHealth();
        }

        private void OnEnable()
        {
            CombatKillBus.KillCommitted += OnKillCommitted;
        }

        private void OnDisable()
        {
            CombatKillBus.KillCommitted -= OnKillCommitted;
        }

        /// <summary>联机：<see cref="FpsDemo.Netcode.NetworkKillFeedBroadcaster"/> 的 ClientRpc 调用；与单机总线共用插入逻辑。</summary>
        public static void AppendKillFeedFromNetwork(string killerDisplayName, string victimDisplayName)
        {
            var hud = Object.FindObjectOfType<DeathmatchHudView>();
            if (hud == null)
                return;
            hud.AppendKillFeedLine(killerDisplayName, victimDisplayName);
        }

        private void Update()
        {
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
                var rows = mm.GetLeaderboardDescending();
                var lines = new List<string>(rows.Count);
                int rank = 1;
                foreach (var row in rows)
                {
                    string name = row.participant != null ? row.participant.DisplayName : "?";
                    lines.Add($"{rank}. {name}  {row.kills}");
                    rank++;
                }

                string target = mm.TargetKills > 0 ? $"Target {mm.TargetKills} Kills" : "";
                _leaderboardText.text = string.IsNullOrEmpty(target)
                    ? string.Join("\n", lines)
                    : $"{target}\n" + string.Join("\n", lines);
            }
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

        private string ResolveDisplayName(GameObject go)
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
