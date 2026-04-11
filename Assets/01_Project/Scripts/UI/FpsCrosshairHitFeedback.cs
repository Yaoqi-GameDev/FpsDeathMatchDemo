using System.Collections;
using FpsDemo.Combat;
using UnityEngine;
using UnityEngine.UI;

namespace FpsDemo.UI
{
    /// <summary>
    /// 订阅 <see cref="FpsHitscanWeapon.ShotHitDamageable"/>，命中可受伤目标时短暂改变准星 <see cref="Image"/> 颜色。
    /// </summary>
    public sealed class FpsCrosshairHitFeedback : MonoBehaviour
    {
        [SerializeField] private FpsHitscanWeapon _weapon;
        [SerializeField] private Image _crosshairImage;

        [SerializeField] private Color _normalColor = new Color(1f, 1f, 1f, 0.85f);
        [SerializeField] private Color _hitColor = new Color(1f, 0.85f, 0.2f, 1f);
        [SerializeField] private float _flashDurationSeconds = 0.1f;

        private Coroutine _flashRoutine;

        private void Awake()
        {
            if (_crosshairImage == null)
                _crosshairImage = GetComponent<Image>();
        }

        private void OnEnable()
        {
            if (_weapon == null)
                return;

            _weapon.ShotHitDamageable += OnShotHitDamageable;
            if (_crosshairImage != null)
                _crosshairImage.color = _normalColor;
        }

        private void OnDisable()
        {
            if (_weapon == null)
                return;

            _weapon.ShotHitDamageable -= OnShotHitDamageable;
            if (_flashRoutine != null)
            {
                StopCoroutine(_flashRoutine);
                _flashRoutine = null;
            }

            if (_crosshairImage != null)
                _crosshairImage.color = _normalColor;
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
