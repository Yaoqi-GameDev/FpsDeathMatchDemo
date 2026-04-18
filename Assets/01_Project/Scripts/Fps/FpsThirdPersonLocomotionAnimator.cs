using Unity.Netcode;
using UnityEngine;

namespace FpsDemo.Fps
{
    /// <summary>
    /// 将 <see cref="FpsPlayerMotor"/> 的 <c>speed</c> / <c>IsGrounded</c> / <c>VerticalSpeed</c> 等写入 Animator；<c>IsCrouch</c>、<c>IsAiming</c> 与 Motor 本帧 locomotion 一致（联机服务端与 <see cref="FpsDemo.Netcode.NetworkLocomotionBuffer"/> RPC 同步，勿仅用 <see cref="FpsInput"/>）。
    /// 挂在 Player 根（与 <see cref="FpsPlayerMotor"/> 同物体）；<see cref="_thirdPersonRoot"/> 指向全身模型根（如 X Bot），避免误隐藏第一人称手臂。
    /// 联机时仅服务端写入 Animator，客户端由 <see cref="Unity.Netcode.Components.NetworkAnimator"/> 同步，避免与远端表现争抢参数导致抽搐。
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class FpsThirdPersonLocomotionAnimator : MonoBehaviour
    {
        [Header("来源")]
        [SerializeField] private FpsPlayerMotor _motor;
        [Tooltip("第三人称全身根（如 X Bot），Animator 在其上或子级；用于收集要隐藏的 Renderer，勿用 Player 根。")]
        [SerializeField] private Transform _thirdPersonRoot;
        [Tooltip("空则在 Third Person Root 上查找 Animator（含子物体）。")]
        [SerializeField] private Animator _thirdPersonAnimator;

        [Header("Animator")]
        [SerializeField] private string _speedParameterName = "speed";
        [SerializeField] private string _crouchParameterName = "IsCrouch";
        [SerializeField] private string _aimingParameterName = "IsAiming";

        [Header("跳跃（Animator 内需同名 Bool / Float）")]
        [Tooltip("关闭则不写 IsGrounded / VerticalSpeed，便于尚未接跳跃时保持 Animator 干净。")]
        [SerializeField] private bool _syncJumpParameters = true;
        [SerializeField] private string _groundedParameterName = "IsGrounded";
        [Tooltip("世界空间 Y 速度（m/s），向上为正；用于 jump up / loop / down 等条件或 Blend Tree。")]
        [SerializeField] private string _verticalSpeedParameterName = "VerticalSpeed";

        [Header("本机隐藏第三人称")]
        [Tooltip("单机本机：隐藏 Third Person Root 下网格，只留 FP 手臂；联机远端将来关掉此项或调用 SetVisibleForRemoteCopy。")]
        [SerializeField] private bool _hideMeshForOwner = true;
        [Tooltip("要隐藏的 Renderer；空则仅在 Third Person Root 下收集 SkinnedMeshRenderer + MeshRenderer。")]
        [SerializeField] private Renderer[] _thirdPersonRenderers;

        private int _speedHash;
        private int _crouchHash;
        private int _aimingHash;
        private int _groundedHash;
        private int _verticalSpeedHash;
        private FpsInput _input;

        private void Awake()
        {
            if (_motor == null)
                _motor = GetComponent<FpsPlayerMotor>();
            if (_input == null)
                _input = GetComponent<FpsInput>();

            if (_thirdPersonRoot != null && _thirdPersonAnimator == null)
                _thirdPersonAnimator = _thirdPersonRoot.GetComponentInChildren<Animator>(true);
            if (_thirdPersonAnimator == null)
                _thirdPersonAnimator = GetComponentInChildren<Animator>(true);

            _speedHash = Animator.StringToHash(_speedParameterName);
            _crouchHash = Animator.StringToHash(_crouchParameterName);
            _aimingHash = Animator.StringToHash(_aimingParameterName);
            _groundedHash = Animator.StringToHash(_groundedParameterName);
            _verticalSpeedHash = Animator.StringToHash(_verticalSpeedParameterName);

            if (_hideMeshForOwner && (_thirdPersonRenderers == null || _thirdPersonRenderers.Length == 0))
            {
                if (_thirdPersonRoot != null)
                {
                    var smr = _thirdPersonRoot.GetComponentsInChildren<SkinnedMeshRenderer>(true);
                    var mr = _thirdPersonRoot.GetComponentsInChildren<MeshRenderer>(true);
                    int n = smr.Length + mr.Length;
                    if (n > 0)
                    {
                        _thirdPersonRenderers = new Renderer[n];
                        for (int i = 0; i < smr.Length; i++)
                            _thirdPersonRenderers[i] = smr[i];
                        for (int i = 0; i < mr.Length; i++)
                            _thirdPersonRenderers[smr.Length + i] = mr[i];
                    }
                }
            }
        }

        private void OnEnable()
        {
            ApplyOwnerVisibility();
        }

        private void LateUpdate()
        {
            if (_thirdPersonAnimator == null || !_thirdPersonAnimator.isActiveAndEnabled)
                return;

            if (!ShouldApplyLocomotionToAnimator())
                return;

            if (_motor == null || !GameplayAllowed())
            {
                _thirdPersonAnimator.SetFloat(_speedHash, 0f);
                _thirdPersonAnimator.SetBool(_crouchHash, false);
                _thirdPersonAnimator.SetBool(_aimingHash, false);
                if (_syncJumpParameters)
                {
                    _thirdPersonAnimator.SetBool(_groundedHash, true);
                    _thirdPersonAnimator.SetFloat(_verticalSpeedHash, 0f);
                }
                return;
            }

            float horizontal = _motor.HorizontalSpeed;
            _thirdPersonAnimator.SetFloat(_speedHash, horizontal);

            bool crouch = _motor.LocomotionCrouchHeld;
            _thirdPersonAnimator.SetBool(_crouchHash, crouch);

            bool aim = _motor.LocomotionAimHeld;
            _thirdPersonAnimator.SetBool(_aimingHash, aim);

            if (_syncJumpParameters)
            {
                _thirdPersonAnimator.SetBool(_groundedHash, _motor.IsGrounded);
                _thirdPersonAnimator.SetFloat(_verticalSpeedHash, _motor.VerticalVelocity);
            }
        }

        private void OnDisable()
        {
            if (!ShouldApplyLocomotionToAnimator())
                return;

            if (_thirdPersonAnimator != null)
            {
                _thirdPersonAnimator.SetBool(_crouchHash, false);
                _thirdPersonAnimator.SetBool(_aimingHash, false);
                if (_syncJumpParameters)
                {
                    _thirdPersonAnimator.SetBool(_groundedHash, true);
                    _thirdPersonAnimator.SetFloat(_verticalSpeedHash, 0f);
                }
            }
        }

        private bool GameplayAllowed()
        {
            return _input == null || _input.GameplayInputEnabled;
        }

        /// <summary>单机或未开连接：本地写 Animator；已联机：仅服务端写，客户端交给 NetworkAnimator。</summary>
        private static bool ShouldApplyLocomotionToAnimator()
        {
            var nm = NetworkManager.Singleton;
            if (nm == null || !nm.IsListening)
                return true;
            return nm.IsServer;
        }

        private void ApplyOwnerVisibility()
        {
            if (!_hideMeshForOwner)
                return;
            if (_thirdPersonRenderers == null)
                return;
            foreach (var r in _thirdPersonRenderers)
            {
                if (r != null)
                    r.enabled = false;
            }
        }

        /// <summary>联机远端显示全身：关闭隐藏并启用 Renderer。</summary>
        public void SetVisibleForRemoteCopy(bool visible)
        {
            _hideMeshForOwner = !visible;
            if (_thirdPersonRenderers == null)
                return;
            foreach (var r in _thirdPersonRenderers)
            {
                if (r != null)
                    r.enabled = visible;
            }
        }
    }
}
