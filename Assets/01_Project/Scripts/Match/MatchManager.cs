using System;
using System.Collections;
using System.Collections.Generic;
using FpsDemo.Combat;
using FpsDemo.Fps;
using FpsDemo.Netcode;
using FpsDemo.UI;
using UIFramework;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace FpsDemo.Match
{
    /// <summary>
    /// 死斗局内规则：登记 <see cref="MatchParticipant"/>、订阅 <see cref="CombatKillBus"/> 计击杀、倒计时与目标击杀、结束时暂停并打开结算窗。
    /// 场景内<strong>单个</strong>实例；挂在空物体上即可。
    /// </summary>
    [DefaultExecutionOrder(-20)]
    public sealed class MatchManager : MonoBehaviour
    {
        public static MatchManager Instance { get; private set; }

        [Header("规则")]
        [SerializeField] private float _matchDurationSeconds = 300f;
        [SerializeField] private int _targetKills = 20;
        [Tooltip("有人达到目标击杀后，延迟多少秒（真实时间）再进入结算。")]
        [SerializeField] private float _targetReachedBroadcastSeconds = 3f;

        [Header("UI（框架）")]
        [Tooltip("若从 Lobby 进房则已有 UIFrame；直开 DeathMatch 时用此资产 Ensure。")]
        [SerializeField] private UISettings _uiSettings;

        /// <summary>本局剩余秒数。单机：本地递减；联机：读 <see cref="NetMatchManager"/> 同步值。</summary>
        public float RemainingMatchSeconds =>
            NetMatchManager.ControlsMatchTimer && NetMatchManager.Instance != null
                ? NetMatchManager.Instance.RemainingSecondsSynced
                : _localRemaining;

        /// <summary>Inspector 配置的对局时长（供 <see cref="NetMatchManager"/> 初始化 NV）。</summary>
        public float ConfiguredMatchDurationSeconds => _matchDurationSeconds;

        /// <summary>目标击杀数（与 Inspector 一致）。</summary>
        public int TargetKills => _targetKills;

        public bool IsMatchOver =>
            NetMatchManager.ControlsMatchTimer && NetMatchManager.Instance != null
                ? NetMatchManager.Instance.MatchEndedSynced
                : _state == MatchState.Ended;

        private float _localRemaining;

        /// <summary>一局结束；订阅者勿阻塞主线程。</summary>
        public event Action<MatchResult> MatchEnded;

        private enum MatchState
        {
            Running,
            PendingBroadcast,
            Ended,
        }

        private MatchState _state = MatchState.Running;
        private readonly Dictionary<MatchParticipant, int> _kills = new Dictionary<MatchParticipant, int>(16);
        private Coroutine _broadcastRoutine;

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Debug.LogWarning("MatchManager: 场景中已有实例，将销毁重复物体。", this);
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

        private void Start()
        {
            _localRemaining = Mathf.Max(0f, _matchDurationSeconds);
            _kills.Clear();
            foreach (var p in MatchParticipant.ActiveParticipants)
                _kills[p] = 0;

            if (_kills.Count == 0)
                Debug.LogWarning("MatchManager: 场上没有 MatchParticipant，击杀将无法记分。请在 Player / 人机根上挂载 MatchParticipant。");

            // 再来一局后 UIFrame 可能仍开着上一局结算窗。
            CloseEndGameWindowIfOpen();
            ShowDeathmatchHudPanel();

            NetMatchManager.NotifyMatchSceneReadyForPossibleRematchReset();

            var nm = NetworkManager.Singleton;
            if (nm != null && nm.IsListening && nm.IsServer && GetComponent<MatchLagCompensationService>() == null)
                gameObject.AddComponent<MatchLagCompensationService>();
        }

        /// <summary>
        /// 参战者启用时登记（联机玩家晚于本组件 <see cref="Start"/> 生成时也会调用）。已存在则保持击杀数不变。
        /// </summary>
        public static void RegisterParticipant(MatchParticipant p)
        {
            if (p == null || Instance == null || Instance._state != MatchState.Running)
                return;

            if (Instance._kills.ContainsKey(p))
                return;

            Instance._kills[p] = 0;
        }

        /// <summary>参战者禁用时从记分表移除（断线、销毁等）。</summary>
        public static void UnregisterParticipant(MatchParticipant p)
        {
            if (p == null || Instance == null)
                return;

            Instance._kills.Remove(p);
        }

        private void OnEnable()
        {
            CombatKillBus.KillCommitted += OnKillCommitted;
        }

        private void OnDisable()
        {
            CombatKillBus.KillCommitted -= OnKillCommitted;
        }

        private void Update()
        {
            if (_state != MatchState.Running)
                return;

            if (NetMatchManager.ControlsMatchTimer)
                return;

            _localRemaining -= Time.unscaledDeltaTime;
            if (_localRemaining <= 0f)
            {
                _localRemaining = 0f;
                EndMatchByTimeUp();
            }
        }

        /// <summary>仅服务器：由 <see cref="NetMatchManager"/> 在倒计时归零时调用。</summary>
        public void NotifyAuthorityTimeExpiredFromNetwork()
        {
            if (NetworkManager.Singleton == null || !NetworkManager.Singleton.IsServer)
                return;
            EndMatchByTimeUp();
        }

        private void OnKillCommitted(KillReport report)
        {
            if (_state != MatchState.Running)
                return;

            var nm = NetworkManager.Singleton;
            if (nm != null && nm.IsListening && !nm.IsServer)
                return;

            if (report.Killer == null)
                return;

            if (report.Victim != null && report.Killer == report.Victim)
                return;

            var killerPart = report.Killer.GetComponentInParent<MatchParticipant>();
            if (killerPart == null)
                return;

            if (!_kills.ContainsKey(killerPart))
                return;

            _kills[killerPart]++;

            var statsNet = killerPart.GetComponent<PlayerMatchStatsNet>();
            if (statsNet != null && statsNet.IsSpawned)
                statsNet.ServerNotifyKillScored();

            if (_kills[killerPart] >= _targetKills)
                BeginTargetReachedSequence();
        }

        private void BeginTargetReachedSequence()
        {
            if (_state != MatchState.Running)
                return;

            _state = MatchState.PendingBroadcast;
            if (_broadcastRoutine != null)
                StopCoroutine(_broadcastRoutine);
            _broadcastRoutine = StartCoroutine(TargetReachedBroadcastThenEnd());
        }

        private IEnumerator TargetReachedBroadcastThenEnd()
        {
            yield return new WaitForSecondsRealtime(Mathf.Max(0f, _targetReachedBroadcastSeconds));
            _broadcastRoutine = null;
            EndMatch(MatchEndReason.TargetKillsReached);
        }

        private void EndMatchByTimeUp()
        {
            if (_state != MatchState.Running)
                return;

            EndMatch(MatchEndReason.TimeExpired);
        }

        private void EndMatch(MatchEndReason reason)
        {
            if (_state == MatchState.Ended)
                return;

            if (NetworkManager.Singleton != null && NetworkManager.Singleton.IsServer)
                NetMatchManager.Instance?.MarkEndedOnServer();

            _state = MatchState.Ended;
            if (_broadcastRoutine != null)
            {
                StopCoroutine(_broadcastRoutine);
                _broadcastRoutine = null;
            }

            var winners = new List<MatchParticipant>();
            int winKills = 0;

            if (reason == MatchEndReason.TargetKillsReached)
            {
                foreach (var kv in _kills)
                {
                    if (kv.Value >= _targetKills)
                        winners.Add(kv.Key);
                }

                if (winners.Count == 0)
                {
                    var best = GetTopByKills();
                    winners.AddRange(best.list);
                    winKills = best.maxKills;
                }
                else
                {
                    winKills = 0;
                    foreach (var w in winners)
                        winKills = Mathf.Max(winKills, _kills[w]);
                }
            }
            else
            {
                var best = GetTopByKills();
                winners.AddRange(best.list);
                winKills = best.maxKills;
            }

            // 不再用 timeScale=0 冻结全场（易与 UI / 协程搅在一起）；结算靠禁本地玩法输入 + 解锁光标。
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
            DisableLocalGameplayInput();

            var result = new MatchResult(reason, winners, winKills);
            MatchEnded?.Invoke(result);

            ShowEndGameWindow(result);
        }

        private (List<MatchParticipant> list, int maxKills) GetTopByKills()
        {
            int max = 0;
            foreach (var kv in _kills)
            {
                if (kv.Value > max)
                    max = kv.Value;
            }

            var list = new List<MatchParticipant>();
            foreach (var kv in _kills)
            {
                if (kv.Value == max)
                    list.Add(kv.Key);
            }

            return (list, max);
        }

        /// <summary>按击杀降序，供 HUD 排行使用。</summary>
        public List<(MatchParticipant participant, int kills)> GetLeaderboardDescending()
        {
            var list = new List<(MatchParticipant participant, int kills)>();
            foreach (var kv in _kills)
                list.Add((kv.Key, kv.Value));

            list.Sort((a, b) => b.kills.CompareTo(a.kills));
            return list;
        }

        public int GetKills(MatchParticipant participant)
        {
            return participant != null && _kills.TryGetValue(participant, out int k) ? k : 0;
        }

        private void ShowDeathmatchHudPanel()
        {
            var frame = UIFrameService.Frame ?? UIFrameService.Ensure(_uiSettings);
            if (frame == null)
            {
                Debug.LogWarning("[MatchHud] No UIFrame; Deathmatch HUD panel skipped.");
                return;
            }

            UIFrameService.ConfigureForGameView(frame);

            if (!frame.IsScreenRegistered(DeathmatchHudPanelController.ScreenId))
            {
                Debug.LogError(
                    "[MatchHud] Screen '" + DeathmatchHudPanelController.ScreenId +
                    "' not registered. Run FpsDemo/UI/Build DeathmatchHudPanelController Prefab And Wire UISettings, then restart Play.");
                return;
            }

            frame.ShowPanel(DeathmatchHudPanelController.ScreenId);
        }

        private void ShowEndGameWindow(MatchResult result)
        {
            string reasonText = result.Reason == MatchEndReason.TargetKillsReached
                ? "Target kills reached"
                : "Time expired";
            string names = result.Winners.Count == 0
                ? "None"
                : string.Join(", ", GetWinnerNames(result.Winners));
            string summary = $"{reasonText}\nWinners: {names}\nKills: {result.WinningKillCount}";

            var frame = UIFrameService.Frame ?? UIFrameService.Ensure(_uiSettings);
            if (frame == null)
            {
                Debug.LogWarning("[MatchEnd] No UIFrame. " + summary);
                return;
            }

            UIFrameService.ConfigureForGameView(frame);

            if (frame.IsScreenRegistered(DeathmatchHudPanelController.ScreenId))
                frame.HidePanel(DeathmatchHudPanelController.ScreenId);

            if (!frame.IsScreenRegistered(EndGameWindowController.ScreenId))
            {
                Debug.LogError(
                    "[MatchEnd] Screen '" + EndGameWindowController.ScreenId +
                    "' not registered. Run FpsDemo/UI/Build EndGameWindowController Prefab And Wire UISettings, then restart Play.");
                Debug.Log("[MatchEnd] " + summary);
                return;
            }

            frame.OpenWindow(EndGameWindowController.ScreenId, new EndGameWindowProperties(summary));
        }

        private static void CloseEndGameWindowIfOpen()
        {
            var frame = UIFrameService.Frame;
            if (frame == null || !frame.IsScreenRegistered(EndGameWindowController.ScreenId))
                return;
            frame.CloseAllWindows(animate: false);
        }

        private static void DisableLocalGameplayInput()
        {
            foreach (var p in MatchParticipant.ActiveParticipants)
            {
                if (p == null || !p.IsLocalPlayer)
                    continue;
                if (p.TryGetComponent<FpsInput>(out var input))
                    input.GameplayInputEnabled = false;
            }
        }

        private static IEnumerable<string> GetWinnerNames(IReadOnlyList<MatchParticipant> winners)
        {
            foreach (var w in winners)
                yield return w != null ? w.DisplayName : "?";
        }

        /// <summary>再来一局：恢复时间缩放并重新加载当前场景。联机时由服务器经 <see cref="NetMatchManager"/> 用 NGO 场景管理加载，客户端才会同步。</summary>
        public void RestartMatch()
        {
            CloseEndGameWindowIfOpen();
            Time.timeScale = 1f;
            if (NetMatchManager.ControlsMatchTimer && NetMatchManager.Instance != null)
            {
                NetMatchManager.Instance.RequestRestartMatch();
                return;
            }

            SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex);
        }
    }
}
