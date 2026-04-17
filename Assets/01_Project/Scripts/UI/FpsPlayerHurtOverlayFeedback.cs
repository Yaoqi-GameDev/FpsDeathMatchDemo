using System.Collections;
using FpsDemo.Combat;
using FpsDemo.Match;
using UnityEngine;
using UnityEngine.UI;

namespace FpsDemo.UI
{
    /// <summary>
    /// 本地玩家受伤：订阅 <see cref="Health.Damaged"/>，全屏 <see cref="Image"/> 短暂泛红后淡出。
    /// 仅绑定 <see cref="MatchParticipant.IsLocalPlayer"/> 的 <see cref="Health"/>；建议 Canvas Sort Order 低于死亡全屏灰幕。
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class FpsPlayerHurtOverlayFeedback : MonoBehaviour
    {
        [Header("数据")]
        [Tooltip("空则在运行时从 ActiveParticipants 找 IsLocalPlayer 的 Health（OnEnable 与 Start 各尝试一次）。")]
        [SerializeField] private Health _playerHealth;

        [Header("UI")]
        [Tooltip("全屏拉伸 Image；Raycast Target 建议关闭。")]
        [SerializeField] private Image _overlayImage;

        [Header("样式")]
        [SerializeField] private Color _peakColor = new Color(0.85f, 0.05f, 0.05f, 0.42f);
        [SerializeField] private Color _clearColor = new Color(0f, 0f, 0f, 0f);
        [Tooltip("从峰值淡出到透明（秒，unscaled）。")]
        [SerializeField] private float _fadeOutSeconds = 0.45f;

        private Coroutine _fadeRoutine;
        private bool _listening;

        private void Awake()
        {
            if (_overlayImage == null)
                _overlayImage = GetComponent<Image>();
        }

        private void OnEnable()
        {
            TrySubscribeToDamage();
        }

        private void Start()
        {
            TrySubscribeToDamage();
        }

        private void OnDisable()
        {
            UnsubscribeFromDamage();

            if (_fadeRoutine != null)
            {
                StopCoroutine(_fadeRoutine);
                _fadeRoutine = null;
            }

            if (_overlayImage != null)
                _overlayImage.color = _clearColor;
        }

        private void TrySubscribeToDamage()
        {
            ResolvePlayerHealthIfNeeded();
            if (_playerHealth == null || !IsLocalPlayerHealth(_playerHealth))
                return;
            if (_listening)
                return;

            _playerHealth.Damaged += OnPlayerDamaged;
            _listening = true;
            if (_overlayImage != null)
                _overlayImage.color = _clearColor;
        }

        private void UnsubscribeFromDamage()
        {
            if (_playerHealth != null && _listening)
                _playerHealth.Damaged -= OnPlayerDamaged;
            _listening = false;
        }

        private void ResolvePlayerHealthIfNeeded()
        {
            if (_playerHealth != null)
                return;

            for (int i = 0; i < MatchParticipant.ActiveParticipants.Count; i++)
            {
                var mp = MatchParticipant.ActiveParticipants[i];
                if (mp == null || !mp.IsLocalPlayer)
                    continue;
                _playerHealth = mp.GetComponent<Health>();
                if (_playerHealth != null)
                    return;
            }

            var tagged = GameObject.FindGameObjectWithTag("Player");
            if (tagged != null)
                _playerHealth = tagged.GetComponent<Health>();
        }

        private static bool IsLocalPlayerHealth(Health health)
        {
            if (health == null)
                return false;
            if (health.GetComponent<MatchParticipant>() is { } mp)
                return mp.IsLocalPlayer;
            return health.CompareTag("Player");
        }

        private void OnPlayerDamaged(float _, GameObject __)
        {
            if (_overlayImage == null)
                return;

            if (_fadeRoutine != null)
                StopCoroutine(_fadeRoutine);
            _fadeRoutine = StartCoroutine(FadeOutRoutine());
        }

        private IEnumerator FadeOutRoutine()
        {
            _overlayImage.color = _peakColor;
            float d = Mathf.Max(0.01f, _fadeOutSeconds);
            float t = 0f;
            while (t < d)
            {
                t += Time.unscaledDeltaTime;
                float u = Mathf.Clamp01(t / d);
                _overlayImage.color = Color.Lerp(_peakColor, _clearColor, u);
                yield return null;
            }

            _overlayImage.color = _clearColor;
            _fadeRoutine = null;
        }
    }
}
