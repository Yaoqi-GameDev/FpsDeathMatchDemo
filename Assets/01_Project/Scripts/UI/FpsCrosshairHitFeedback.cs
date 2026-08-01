using System.Collections;
using FpsDemo.Combat;
using FpsDemo.Match;
using UnityEngine;
using UnityEngine.UI;

namespace FpsDemo.UI
{
    /// <summary>
    /// 订阅 <see cref="FpsHitscanWeapon.ShotHitDamageable"/>，命中可受伤目标时短暂改变准星 <see cref="Image"/> 颜色。
    /// <see cref="_weapon"/> 可空：联机玩家晚生成时在 <see cref="LateUpdate"/> 持续解析本地武器。
    /// </summary>
    public sealed class FpsCrosshairHitFeedback : MonoBehaviour
    {
        [Tooltip("可空：空则运行时解析本地参战者的 FpsHitscanWeapon")]
        [SerializeField] private FpsHitscanWeapon _weapon;
        [SerializeField] private Image _crosshairImage;

        [SerializeField] private Color _normalColor = new Color(1f, 1f, 1f, 0.85f);
        [SerializeField] private Color _hitColor = new Color(1f, 0.85f, 0.2f, 1f);
        [SerializeField] private float _flashDurationSeconds = 0.1f;

        private Coroutine _flashRoutine;
        private bool _listening;

        private void Awake()
        {
            if (_crosshairImage == null)
                _crosshairImage = GetComponent<Image>();
        }

        private void OnEnable()
        {
            TrySubscribe();
            if (_crosshairImage != null)
                _crosshairImage.color = _normalColor;
        }

        private void LateUpdate()
        {
            if (!_listening)
                TrySubscribe();
        }

        private void OnDisable()
        {
            Unsubscribe();
            if (_flashRoutine != null)
            {
                StopCoroutine(_flashRoutine);
                _flashRoutine = null;
            }

            if (_crosshairImage != null)
                _crosshairImage.color = _normalColor;
        }

        private void TrySubscribe()
        {
            if (_listening)
                return;

            TryResolveWeapon();
            if (_weapon == null)
                return;

            _weapon.ShotHitDamageable += OnShotHitDamageable;
            _listening = true;
        }

        private void Unsubscribe()
        {
            if (!_listening || _weapon == null)
            {
                _listening = false;
                return;
            }

            _weapon.ShotHitDamageable -= OnShotHitDamageable;
            _listening = false;
        }

        private void TryResolveWeapon()
        {
            if (_weapon != null)
                return;

            foreach (var p in MatchParticipant.ActiveParticipants)
            {
                if (p != null && p.IsLocalPlayer)
                {
                    _weapon = p.GetComponent<FpsHitscanWeapon>();
                    break;
                }
            }
        }

        private void OnShotHitDamageable(bool hitDamageable)
        {
            if (!hitDamageable || _crosshairImage == null)
                return;

            if (_flashRoutine != null)
                StopCoroutine(_flashRoutine);
            _flashRoutine = StartCoroutine(FlashRoutine());
        }

        private IEnumerator FlashRoutine()
        {
            _crosshairImage.color = _hitColor;
            yield return new WaitForSeconds(_flashDurationSeconds);
            _crosshairImage.color = _normalColor;
            _flashRoutine = null;
        }
    }
}
