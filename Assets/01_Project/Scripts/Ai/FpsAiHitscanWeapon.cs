using System;
using FpsDemo.Combat;
using FpsDemo.Data;
using UnityEngine;

namespace FpsDemo.Ai
{
    /// <summary>
    /// 人机专用 Hitscan：与 <see cref="FpsHitscanWeapon"/> 共用 <see cref="HitscanShotResolver"/>，**不**依赖 <see cref="FpsDemo.Fps.FpsInput"/> / <see cref="Camera"/>。
    /// 联机场景可不挂；玩家武器请使用 <see cref="FpsHitscanWeapon"/>。
    /// </summary>
    [DefaultExecutionOrder(-40)]
    public sealed class FpsAiHitscanWeapon : MonoBehaviour
    {
        [Header("数据")]
        [SerializeField] private HitscanWeaponConfig _config;

        [Header("射线")]
        [Tooltip("未勾选时使用 Physics.DefaultRaycastLayers。")]
        [SerializeField] private LayerMask _hitLayers;

        [Header("伤害")]
        [Tooltip("可选：部位倍率配置；未拖则使用内建默认（头 2 / 上身 1 / 四肢 0.7）。")]
        [SerializeField] private BodyDamageMultiplierConfig _bodyDamageMultiplierConfig;

        /// <summary>成功扣弹并发射一次（射线已执行）。</summary>
        public event Action ShotFired;

        /// <summary>换弹流程已开始。</summary>
        public event Action ReloadStarted;

        /// <summary>本发射线结束后：是否命中可伤害体。</summary>
        public event Action<bool> ShotHitDamageable;

        /// <summary>射线已解析。</summary>
        public event Action<ShotHitInfo> ShotResolved;

        public int AmmoInMagazine => _magazine;
        public int ReserveAmmo => _reserve;
        public bool IsReloading => _reloading;

        private int _magazine;
        private int _reserve;
        private bool _reloading;
        private float _reloadEndTime;

        private void Awake()
        {
            if (_config == null)
            {
                Debug.LogError("FpsAiHitscanWeapon: 请拖入 HitscanWeaponConfig。", this);
                enabled = false;
                return;
            }

            if (_hitLayers.value == 0)
                _hitLayers = Physics.DefaultRaycastLayers;

            _magazine = _config.MagazineSize;
            _reserve = Mathf.Max(0, _config.StartingReserveAmmo);
        }

        private void Update()
        {
            if (_reloading && Time.time >= _reloadEndTime)
                FinishReload();
        }

        /// <summary>由 <see cref="FpsAiHitscanShooter"/> 等调用；扣弹与命中与玩家一致。</summary>
        public bool TryFireFromAimTransform(Transform aim, out bool hitDamageable, out ShotHitInfo shotInfo)
        {
            hitDamageable = false;
            shotInfo = default;

            if (aim == null || _reloading || _magazine <= 0)
                return false;

            _magazine--;
            Ray ray = new Ray(aim.position, aim.forward);
            HitscanShotResolver.Resolve(
                ray,
                _config.MaxRange,
                _hitLayers,
                transform,
                _config.DamagePerShot,
                _bodyDamageMultiplierConfig,
                out hitDamageable,
                out shotInfo);

            ShotHitDamageable?.Invoke(hitDamageable);
            ShotResolved?.Invoke(shotInfo);
            ShotFired?.Invoke();
            return true;
        }

        /// <summary>与玩家按 R 换弹条件相同。</summary>
        public bool TryStartReload()
        {
            if (_reloading)
                return false;

            TryBeginReload();
            return _reloading;
        }

        private void TryBeginReload()
        {
            if (_magazine >= _config.MagazineSize)
                return;

            int need = _config.MagazineSize - _magazine;
            if (need <= 0 || _reserve <= 0)
                return;

            _reloading = true;
            _reloadEndTime = Time.time + Mathf.Max(0.01f, _config.ReloadDurationSeconds);
            ReloadStarted?.Invoke();
        }

        private void FinishReload()
        {
            _reloading = false;

            int need = _config.MagazineSize - _magazine;
            if (need <= 0 || _reserve <= 0)
                return;

            int take = Mathf.Min(need, _reserve);
            _magazine += take;
            _reserve -= take;
        }
    }
}
