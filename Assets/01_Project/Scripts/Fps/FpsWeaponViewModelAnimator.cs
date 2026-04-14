using FpsDemo.Combat;
using UnityEngine;

namespace FpsDemo.Fps
{
    /// <summary>
    /// 第一人称武器「中间人」：订阅 <see cref="FpsHitscanWeapon"/> 的事件，把事件翻译成
    /// 手臂 / 武器模型上 <see cref="Animator"/> 的 Layer + 状态播放。
    /// 疾跑时同步 <c>Running</c>（<see cref="FpsPlayerMotor.ShouldDriveArmsSprintRunningPose"/>）；按住开火或瞄准时关 <c>Running</c>（仅手臂姿势）。
    /// 瞄准时同步 Infima <c>Aim</c>（Bool）与 <c>Aiming</c>（Float）；<c>Aiming</c> 默认脚本插值，避免每帧 0/1 硬切。
    /// </summary>
    [DefaultExecutionOrder(-38)]
    public sealed class FpsWeaponViewModelAnimator : MonoBehaviour
    {
        [Header("来源")]
        [SerializeField] private FpsHitscanWeapon _weapon;
        [Tooltip("空则同物体 GetComponent；用于开火时关 Running 等")]
        [SerializeField] private FpsInput _input;
        [Tooltip("空则同物体 GetComponent；疾跑持枪需贴地 + WASD 移动，见 FpsPlayerMotor.ShouldDriveArmsSprintRunningPose")]
        [SerializeField] private FpsPlayerMotor _motor;

        [Header("Animator")]
        [SerializeField] private Animator _armsAnimator;
        [Tooltip("若空，则在当前武器根的子层级中查找 Animator")]
        [SerializeField] private Animator _weaponAnimator;

        [Header("切槽：手臂 RuntimeAnimatorController（与槽 0、1… 对齐；可空或同一份）")]
        [SerializeField] private RuntimeAnimatorController[] _armsControllersPerSlot;

        [Header("Layer / 状态名（与 Animator Controller 中一致）")]
        [SerializeField] private WeaponViewModelAnimRouting _routing = new WeaponViewModelAnimRouting();

        [Header("行为")]
        [SerializeField] private float _crossFadeDuration = 0.05f;
        [SerializeField] private bool _playSwitchWeaponAnimationOnSlotChanged = true;

        [Header("疾跑持枪（Infima：Layer Locomotion + 参数 Running）")]
        [SerializeField] private bool _syncSprintToArmsRunning = true;
        [Tooltip("按住开火时关闭 Running，仅影响手臂姿势；不修改角色移动速度")]
        [SerializeField] private bool _clearRunningWhileFireHeld = true;
        [SerializeField] private string _runningParameterName = "Running";

        [Header("瞄准（Infima 手臂：Aim Bool + Aiming Float；武器：Aiming Float）")]
        [SerializeField] private bool _syncAimToAnimator = true;
        [Tooltip("按住瞄准时关闭 Running 姿势（与开火类似）")]
        [SerializeField] private bool _clearRunningWhileAiming = true;
        [SerializeField] private string _aimBoolParameterName = "Aim";
        [SerializeField] private string _aimingFloatParameterName = "Aiming";
        [Tooltip("若为 false，Aiming 每帧直接 0/1（易「硬切」）；为 true 时在脚本里插值。")]
        [SerializeField] private bool _smoothAimingParameter = true;
        [Tooltip("Aiming 从 0→1 的速度（0～1 量纲/秒）。过小拖沓，过大接近硬切。")]
        [SerializeField] private float _aimingBlendInSpeed = 10f;
        [Tooltip("Aiming 从 1→0 的速度（通常可略快于进入）。")]
        [SerializeField] private float _aimingBlendOutSpeed = 12f;
        [Tooltip("平滑后的 Aiming 大于该值才置 Aim=true，避免抖动。")]
        [SerializeField] private float _aimBoolSmoothedThreshold = 0.02f;

        private Animator _cachedWeaponAnimator;
        private int _runningParamHash;
        private int _aimBoolParamHash;
        private int _aimingFloatParamHash;
        private float _smoothedAiming;

        private void Awake()
        {
            if (_weapon == null)
                _weapon = GetComponent<FpsHitscanWeapon>();
            if (_input == null)
                _input = GetComponent<FpsInput>();
            if (_motor == null)
                _motor = GetComponent<FpsPlayerMotor>();
            _runningParamHash = Animator.StringToHash(_runningParameterName);
            _aimBoolParamHash = Animator.StringToHash(_aimBoolParameterName);
            _aimingFloatParamHash = Animator.StringToHash(_aimingFloatParameterName);
        }

        private void LateUpdate()
        {
            if (_armsAnimator == null || _input == null)
                return;

            bool running = _motor != null && _motor.ShouldDriveArmsSprintRunningPose;
            if (_clearRunningWhileFireHeld && _input.FireHeld)
                running = false;
            if (_syncAimToAnimator && _clearRunningWhileAiming && _input.AimHeld)
                running = false;

            if (_syncSprintToArmsRunning)
                _armsAnimator.SetBool(_runningParamHash, running);

            if (_syncAimToAnimator)
            {
                bool aimHeld = _input.AimHeld;
                float target = aimHeld ? 1f : 0f;
                if (_smoothAimingParameter)
                {
                    float speed = aimHeld ? _aimingBlendInSpeed : _aimingBlendOutSpeed;
                    _smoothedAiming = Mathf.MoveTowards(_smoothedAiming, target, speed * Time.deltaTime);
                }
                else
                {
                    _smoothedAiming = target;
                }

                bool aimBool = _smoothedAiming > _aimBoolSmoothedThreshold;
                _armsAnimator.SetBool(_aimBoolParamHash, aimBool);
                _armsAnimator.SetFloat(_aimingFloatParamHash, _smoothedAiming);

                if (_routing.playWeaponAnimator)
                {
                    var w = ResolveWeaponAnimator();
                    if (w != null)
                        w.SetFloat(_aimingFloatParamHash, _smoothedAiming);
                }
            }
        }

        private void OnEnable()
        {
            if (_weapon == null)
                return;

            _weapon.ShotFired += OnShotFired;
            _weapon.ReloadStarted += OnReloadStarted;
            _weapon.DryFire += OnDryFire;
            _weapon.WeaponSlotChanged += OnWeaponSlotChanged;

            ApplyArmsControllerForSlot(_weapon.CurrentWeaponIndex);
            RefreshWeaponAnimatorCache();
        }

        private void OnDisable()
        {
            if (_weapon != null)
            {
                _weapon.ShotFired -= OnShotFired;
                _weapon.ReloadStarted -= OnReloadStarted;
                _weapon.DryFire -= OnDryFire;
                _weapon.WeaponSlotChanged -= OnWeaponSlotChanged;
            }

            if (_armsAnimator != null)
            {
                if (_syncSprintToArmsRunning)
                    _armsAnimator.SetBool(_runningParamHash, false);
                if (_syncAimToAnimator)
                {
                    _smoothedAiming = 0f;
                    _armsAnimator.SetBool(_aimBoolParamHash, false);
                    _armsAnimator.SetFloat(_aimingFloatParamHash, 0f);
                }
            }

            if (_syncAimToAnimator && _routing.playWeaponAnimator)
            {
                var w = ResolveWeaponAnimator();
                if (w != null)
                    w.SetFloat(_aimingFloatParamHash, 0f);
            }
        }

        private void OnShotFired()
        {
            PlayArmsThenWeapon(_routing.stateFire, _routing.armsOverlayLayer);
        }

        private void OnDryFire()
        {
            if (string.IsNullOrEmpty(_routing.stateFireEmpty))
                return;
            PlayArmsThenWeapon(_routing.stateFireEmpty, _routing.armsOverlayLayer);
        }

        private void OnReloadStarted()
        {
            if (_weapon == null)
                return;

            bool emptyMag = _weapon.AmmoInMagazine == 0;
            string state = emptyMag ? _routing.stateReloadEmpty : _routing.stateReload;
            if (string.IsNullOrEmpty(state))
                state = _routing.stateReload;
            PlayArmsThenWeapon(state, _routing.armsActionsLayer);
        }

        private void OnWeaponSlotChanged(int newIndex)
        {
            ApplyArmsControllerForSlot(newIndex);
            RefreshWeaponAnimatorCache();

            if (_playSwitchWeaponAnimationOnSlotChanged &&
                _armsAnimator != null &&
                !string.IsNullOrEmpty(_routing.stateAfterWeaponSwitch))
            {
                TryCrossFade(
                    _armsAnimator,
                    _routing.armsHolsterLayer,
                    _routing.stateAfterWeaponSwitch,
                    _crossFadeDuration);
            }
        }

        private void PlayArmsThenWeapon(string stateName, string armsLayerName)
        {
            if (string.IsNullOrEmpty(stateName))
                return;

            if (_armsAnimator != null)
                TryCrossFade(_armsAnimator, armsLayerName, stateName, _crossFadeDuration);

            if (!_routing.playWeaponAnimator)
                return;

            var w = ResolveWeaponAnimator();
            if (w == null)
                return;

            string weaponLayer = string.IsNullOrEmpty(_routing.weaponLayer) ? armsLayerName : _routing.weaponLayer;
            TryCrossFade(w, weaponLayer, stateName, _crossFadeDuration);
        }

        private static bool TryCrossFade(Animator animator, string layerName, string stateName, float duration)
        {
            if (animator == null || !animator.isActiveAndEnabled || string.IsNullOrEmpty(stateName))
                return false;

            int layer = ResolveLayerIndex(animator, layerName);
            if (!string.IsNullOrEmpty(layerName) && layer < 0)
            {
                Debug.LogWarning(
                    $"FpsWeaponViewModelAnimator: '{animator.name}' 无 Layer「{layerName}」。",
                    animator);
                return false;
            }

            if (layer < 0)
                layer = 0;

            int hash = Animator.StringToHash(stateName);
            if (!animator.HasState(layer, hash))
            {
                Debug.LogWarning(
                    $"FpsWeaponViewModelAnimator: '{animator.name}' 的 Layer {layer} 无状态「{stateName}」。",
                    animator);
                return false;
            }

            animator.CrossFade(stateName, duration, layer, 0f);
            return true;
        }

        private static int ResolveLayerIndex(Animator animator, string layerName)
        {
            if (animator == null || string.IsNullOrEmpty(layerName))
                return 0;

            for (int i = 0; i < animator.layerCount; i++)
            {
                if (animator.GetLayerName(i) == layerName)
                    return i;
            }

            return -1;
        }

        private void ApplyArmsControllerForSlot(int slotIndex)
        {
            if (_armsAnimator == null || _armsControllersPerSlot == null || _armsControllersPerSlot.Length == 0)
                return;
            if (slotIndex < 0 || slotIndex >= _armsControllersPerSlot.Length)
                return;

            var c = _armsControllersPerSlot[slotIndex];
            if (c == null)
                return;

            if (_armsAnimator.runtimeAnimatorController != c)
            {
                _armsAnimator.runtimeAnimatorController = c;
                _armsAnimator.Rebind();
            }
        }

        private void RefreshWeaponAnimatorCache()
        {
            _cachedWeaponAnimator = null;
            if (_weaponAnimator != null)
            {
                _cachedWeaponAnimator = _weaponAnimator;
                return;
            }

            if (_weapon == null)
                return;

            var root = _weapon.GetWeaponVisualRoot(_weapon.CurrentWeaponIndex);
            if (root == null)
                return;

            _cachedWeaponAnimator = root.GetComponentInChildren<Animator>(true);
        }

        private Animator ResolveWeaponAnimator()
        {
            if (_weaponAnimator != null)
                return _weaponAnimator;
            return _cachedWeaponAnimator;
        }
    }

    /// <summary>
    /// 手臂与武器模型可能使用不同 Controller，故 Layer 名分开配置；状态名通常仍一致（如 Fire / Reload）。
    /// </summary>
    [System.Serializable]
    public sealed class WeaponViewModelAnimRouting
    {
        [Header("手臂")]
        public string armsOverlayLayer = "Layer Overlay";
        public string armsActionsLayer = "Layer Actions";
        public string armsHolsterLayer = "Layer Holster";

        [Header("状态名")]
        public string stateFire = "Fire";
        public string stateFireEmpty = "Fire Empty";
        public string stateReload = "Reload";
        public string stateReloadEmpty = "Reload Empty";
        public string stateAfterWeaponSwitch = "Unholster";

        [Header("武器模型 Animator（可选）")]
        public bool playWeaponAnimator = true;
        public string weaponLayer = "Layer Base";
    }
}
