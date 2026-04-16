using System;
using FpsDemo.Data;
using FpsDemo.Fps;
using UnityEngine;

namespace FpsDemo.Combat
{
    /// <summary>
    /// 第一人称 Hitscan：从 <see cref="Camera"/> 中心射线，命中 <see cref="IDamageable"/>，调用 <c>ApplyDamage(伤害, 伤害来源)</c>；伤害来源为 <see cref="Transform.root"/>（与 <c>Player</c> 根一致）。
    /// 射线<strong>包含</strong> Player 层，以便打人机/他人；<strong>同一角色根</strong>上的命中视为自伤并跳过（<c>TryResolveShot</c>）。
    /// 读 <see cref="FpsInput"/>；支持多份 <see cref="HitscanWeaponConfig"/> 与切枪（<b>1</b>/<b>2</b>），每槽独立弹药；可选拖「武器模型根」显隐。命中解析见 <see cref="HitscanShotResolver"/>；人机请用 <c>FpsAiHitscanWeapon</c>。
    /// 事件：<see cref="ShotHitDamageable"/>、<see cref="ShotResolved"/>、<see cref="ShotFired"/>（顺序）、<see cref="ReloadStarted"/>、
    /// <see cref="DryFire"/>（弹匣空时本帧按下开火）、<see cref="WeaponSlotChanged"/>（槽位变化后，参数为新下标）。
    /// </summary>
    [DefaultExecutionOrder(-40)]
    public sealed class FpsHitscanWeapon : MonoBehaviour
    {
        [SerializeField] private FpsInput _input;
        [SerializeField] private Camera _camera;

        [Header("数据（多槽位）")]
        [Tooltip("槽 0、1… 对应切枪键；至少 1 份。")]
        [SerializeField] private HitscanWeaponConfig[] _configs;

        [Tooltip("与 _configs 下标对应；可为空。用于切枪时 SetActive 显示当前武器模型。")]
        [SerializeField] private GameObject[] _weaponVisualRoots;

        [Header("射线")]
        [Tooltip("未在 Inspector 勾选任何层时，使用 Physics.DefaultRaycastLayers（含 Player，便于命中角色；自伤由脚本按根物体跳过）。")]
        [SerializeField] private LayerMask _hitLayers;

        [Header("伤害")]
        [Tooltip("可选：部位倍率配置；未拖则使用内建默认（头 2 / 上身 1 / 四肢 0.7）。武器只填基础伤害。")]
        [SerializeField] private BodyDamageMultiplierConfig _bodyDamageMultiplierConfig;

        [Header("动画 / 桥接（可选）")]
        [Tooltip("弹匣为 0 且本帧按下开火时触发 DryFire（与 ShotFired 互斥）；用于 FP 空枪动画等。")]
        [SerializeField] private bool _emitDryFireWhenEmpty = true;

        /// <summary>成功扣弹并发射一次（射线已执行）；订阅者勿阻塞主线程。</summary>
        public event Action ShotFired;

        /// <summary>换弹流程已开始（计时器已启动）。</summary>
        public event Action ReloadStarted;

        /// <summary>弹匣为空时本帧按下开火（<see cref="FpsInput.FirePressedThisFrame"/>）；换弹进行中不触发。</summary>
        public event Action DryFire;

        /// <summary>当前槽位已切换；参数为新 <see cref="CurrentWeaponIndex"/>。</summary>
        public event Action<int> WeaponSlotChanged;

        /// <summary>本发射线结束后：是否命中带 <see cref="IDamageable"/> 的物体（未命中或只打到环境则为 <c>false</c>）。</summary>
        public event Action<bool> ShotHitDamageable;

        /// <summary>射线已解析：命中点、法线、是否伤害体等；供弹孔/粒子/UI 订阅。</summary>
        public event Action<ShotHitInfo> ShotResolved;

        /// <summary>当前武器配置（切枪后为当前槽）。</summary>
        public HitscanWeaponConfig ActiveConfig => _configs[_currentIndex];

        /// <summary>当前槽位（0 起步），与切枪键一致。</summary>
        public int CurrentWeaponIndex => _currentIndex;

        public int AmmoInMagazine => _magazinePerSlot[_currentIndex];
        public int ReserveAmmo => _reservePerSlot[_currentIndex];
        public bool IsReloading { get; private set; }

        /// <summary>槽位对应的武器显隐根（与 <see cref="_weaponVisualRoots"/> 一致）；供视图层桥接取 <see cref="Animator"/>。</summary>
        public GameObject GetWeaponVisualRoot(int index)
        {
            if (_weaponVisualRoots == null || index < 0 || index >= _weaponVisualRoots.Length)
                return null;
            return _weaponVisualRoots[index];
        }

        private int _currentIndex;
        private int[] _magazinePerSlot;
        private int[] _reservePerSlot;

        private float _nextFireTime;
        private float _reloadEndTime;

        private HitscanWeaponConfig Current => _configs[_currentIndex];

        private void Awake()
        {
            if (_configs == null || _configs.Length == 0)
            {
                Debug.LogError("FpsHitscanWeapon: _configs 至少填 1 份 HitscanWeaponConfig。", this);
                enabled = false;
                return;
            }

            for (int i = 0; i < _configs.Length; i++)
            {
                if (_configs[i] == null)
                {
                    Debug.LogError($"FpsHitscanWeapon: _configs[{i}] 为空。", this);
                    enabled = false;
                    return;
                }
            }

            if (_hitLayers.value == 0)
                _hitLayers = Physics.DefaultRaycastLayers;

            _magazinePerSlot = new int[_configs.Length];
            _reservePerSlot = new int[_configs.Length];
            for (int i = 0; i < _configs.Length; i++)
            {
                _magazinePerSlot[i] = _configs[i].MagazineSize;
                _reservePerSlot[i] = Mathf.Max(0, _configs[i].StartingReserveAmmo);
            }

            _currentIndex = 0;
            ApplyWeaponVisuals();
        }

        private void Update()
        {
            if (IsReloading && Time.time >= _reloadEndTime)
                FinishReload();

            if (_input == null || _camera == null)
                return;

            if (IsReloading)
                return;

            if (_configs.Length > 1)
            {
                if (_input.WeaponSlot1PressedThisFrame)
                    TrySwitchWeapon(0);
                if (_input.WeaponSlot2PressedThisFrame)
                    TrySwitchWeapon(1);
            }

            if (_input.ReloadPressedThisFrame)
                TryBeginReload();

            if (IsReloading)
                return;

            if (AmmoInMagazine <= 0)
            {
                if (_emitDryFireWhenEmpty && _input.FirePressedThisFrame)
                    DryFire?.Invoke();

                return;
            }

            if (!_input.FireHeld)
                return;

            float rate = Current.FireRatePerSecond;
            float interval = rate > 0.001f ? 1f / rate : 0f;
            if (Time.time < _nextFireTime)
                return;

            FireOnce();
            _nextFireTime = Time.time + interval;
        }

        /// <summary>复活等：各槽弹药与备弹恢复为 <see cref="HitscanWeaponConfig"/> 开局值，结束换弹并回到槽 0。</summary>
        public void ResetAmmoToConfigDefaults()
        {
            if (_configs == null || _configs.Length == 0 || _magazinePerSlot == null)
                return;

            IsReloading = false;
            _nextFireTime = 0f;
            int prevSlot = _currentIndex;
            _currentIndex = 0;

            for (int i = 0; i < _configs.Length; i++)
            {
                if (_configs[i] == null)
                    continue;
                _magazinePerSlot[i] = _configs[i].MagazineSize;
                _reservePerSlot[i] = Mathf.Max(0, _configs[i].StartingReserveAmmo);
            }

            ApplyWeaponVisuals();
            if (prevSlot != 0)
                WeaponSlotChanged?.Invoke(_currentIndex);
        }

        private void TrySwitchWeapon(int index)
        {
            if (index < 0 || index >= _configs.Length || index == _currentIndex)
                return;

            IsReloading = false;
            _currentIndex = index;
            _nextFireTime = 0f;
            ApplyWeaponVisuals();
            WeaponSlotChanged?.Invoke(_currentIndex);
        }

        private void ApplyWeaponVisuals()
        {
            if (_weaponVisualRoots == null || _weaponVisualRoots.Length == 0)
                return;

            int n = Mathf.Min(_weaponVisualRoots.Length, _configs.Length);
            for (int i = 0; i < n; i++)
            {
                if (_weaponVisualRoots[i] != null)
                    _weaponVisualRoots[i].SetActive(i == _currentIndex);
            }
        }

        private void TryBeginReload()
        {
            if (AmmoInMagazine >= Current.MagazineSize)
                return;

            int need = Current.MagazineSize - AmmoInMagazine;
            if (need <= 0 || ReserveAmmo <= 0)
                return;

            IsReloading = true;
            _reloadEndTime = Time.time + Mathf.Max(0.01f, Current.ReloadDurationSeconds);
            ReloadStarted?.Invoke();
        }

        private void FinishReload()
        {
            IsReloading = false;

            int need = Current.MagazineSize - AmmoInMagazine;
            if (need <= 0 || ReserveAmmo <= 0)
                return;

            int take = Mathf.Min(need, ReserveAmmo);
            _magazinePerSlot[_currentIndex] += take;
            _reservePerSlot[_currentIndex] -= take;
        }

        private void FireOnce()
        {
            _magazinePerSlot[_currentIndex]--;

            Ray ray = new Ray(_camera.transform.position, _camera.transform.forward);
            HitscanShotResolver.Resolve(
                ray,
                Current.MaxRange,
                _hitLayers,
                transform,
                Current.DamagePerShot,
                _bodyDamageMultiplierConfig,
                out bool hitDamageable,
                out ShotHitInfo shotInfo);

            ShotHitDamageable?.Invoke(hitDamageable);
            ShotResolved?.Invoke(shotInfo);
            ShotFired?.Invoke();
        }
    }
}
