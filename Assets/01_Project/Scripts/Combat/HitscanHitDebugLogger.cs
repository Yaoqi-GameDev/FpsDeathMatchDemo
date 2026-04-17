using FpsDemo.Ai;
using UnityEngine;

namespace FpsDemo.Combat
{
    /// <summary>
    /// 调试：订阅 Hitscan 的 <see cref="FpsHitscanWeapon.ShotResolved"/> / <see cref="FpsAiHitscanWeapon.ShotResolved"/>，
    /// 在控制台输出命中部位、倍率、基础伤害、最终伤害。调完可移除组件或关闭日志开关。
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class HitscanHitDebugLogger : MonoBehaviour
    {
        [SerializeField] private bool _logWhenHitDamageable = true;
        [SerializeField] private bool _logWorldHitsWithoutDamage;
        [Tooltip("为空则在本物体上 GetComponent。")]
        [SerializeField] private FpsHitscanWeapon _playerWeapon;
        [Tooltip("人机武器；为空则在本物体上 GetComponent。")]
        [SerializeField] private FpsAiHitscanWeapon _aiWeapon;

        private void Awake()
        {
            if (_playerWeapon == null)
                _playerWeapon = GetComponent<FpsHitscanWeapon>();
            if (_aiWeapon == null)
                _aiWeapon = GetComponent<FpsAiHitscanWeapon>();
        }

        private void OnEnable()
        {
            if (_playerWeapon != null)
                _playerWeapon.ShotResolved += OnShotResolved;
            if (_aiWeapon != null)
                _aiWeapon.ShotResolved += OnShotResolved;
        }

        private void OnDisable()
        {
            if (_playerWeapon != null)
                _playerWeapon.ShotResolved -= OnShotResolved;
            if (_aiWeapon != null)
                _aiWeapon.ShotResolved -= OnShotResolved;
        }

        private void OnShotResolved(ShotHitInfo info)
        {
            if (!info.HasWorldHit)
            {
                Debug.Log("[Hitscan][Debug] 未命中任何物体。");
                return;
            }

            if (info.HitDamageable)
            {
                if (!_logWhenHitDamageable)
                    return;

                string col = info.HitCollider != null ? info.HitCollider.name : "(null)";
                Debug.Log(
                    $"[Hitscan][Debug] 命中可伤害体 | 部位={info.BodyRegion} | 倍率={info.DamageMultiplierApplied:F3} | " +
                    $"基础={info.BaseDamagePerShot:F2} | 最终伤害={info.FinalDamageApplied:F2} | Collider={col}",
                    info.HitCollider);
                return;
            }

            if (_logWorldHitsWithoutDamage)
            {
                string col = info.HitCollider != null ? info.HitCollider.name : "(null)";
                Debug.Log($"[Hitscan][Debug] 命中环境/遮挡（未扣血）| Collider={col}", info.HitCollider);
            }
        }
    }
}
