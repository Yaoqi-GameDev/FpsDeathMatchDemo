using FpsDemo.Netcode;
using Unity.Netcode;
using UnityEngine;

namespace FpsDemo.Fps
{
    /// <summary>
    /// 第一人称移动：走路、蹲、疾跑、滑铲、梯子、跳跃与连跳惩罚。
    /// 输入只来自 <see cref="ILocomotionInputSource"/>（单机：<see cref="FpsInputLocomotionSource"/>；联机服务器：<see cref="NetworkLocomotionBuffer"/>）。
    /// 水平转角由本组件在每帧开始施加 <see cref="PlayerLocomotionInput.YawDelta"/>（原 <see cref="FpsPlayerLook"/> 的身体 yaw 已迁入此处）。
    /// </summary>
    [RequireComponent(typeof(CharacterController))]
    [RequireComponent(typeof(FpsInputLocomotionSource))]
    public sealed class FpsPlayerMotor : MonoBehaviour
    {
        private enum MotorMode
        {
            Normal,
            Sliding,
            Ladder
        }

        [SerializeField] private FpsInputLocomotionSource _localLocomotionSource;
        [SerializeField] private NetworkLocomotionBuffer _networkBuffer;
        [SerializeField] private Transform _cameraPivot;

        [Header("走路 / 疾跑")]
        [SerializeField] private float _walkSpeed = 4.5f;
        [SerializeField] private float _sprintSpeed = 8f;
        [Tooltip("开镜（AimHeld）时视为非疾跑：目标速度为走路，且不参与疾跑宽限/滑铲条件中的「疾跑」；第三人称 Animator 的 speed 会随实际水平速度落在走路区间。")]
        [SerializeField] private bool _limitSpeedToWalkWhileAiming = true;
        [Tooltip("按住开火（FireHeld）时同样视为非疾跑，与开镜限制一致。")]
        [SerializeField] private bool _limitSpeedToWalkWhileFiring = true;
        [Tooltip("水平速度朝目标靠近的速率（加速与减速同一参数）。")]
        [SerializeField] private float _acceleration = 50f;

        [Header("蹲伏（按住 Ctrl；疾跑或疾跑宽限内为滑铲，否则蹲）")]
        [SerializeField] private float _crouchSpeedMultiplier = 0.45f;
        [Tooltip("站立胶囊高度（米）。")]
        [SerializeField] private float _standingHeight = 1.7f;
        [SerializeField] private Vector3 _standingCenter = new Vector3(0f, 0.85f, 0f);
        [SerializeField] private float _crouchHeight = 1.15f;
        [SerializeField] private Vector3 _crouchCenter = new Vector3(0f, 0.575f, 0f);
        [SerializeField] private float _standingCameraPivotLocalY = 1.275f;
        [SerializeField] private float _crouchCameraPivotLocalY = 0.85f;

        [Header("滑铲（本帧按下 Ctrl + 正在疾跑或疾跑刚结束）")]
        [Tooltip("松开 Shift 后，仍有多少秒视为「疾跑刚结束」，此时按 Ctrl 仍可滑铲。")]
        [SerializeField] private float _sprintSlideGraceSeconds = 0.35f;
        [SerializeField] private float _slideDuration = 0.45f;
        [SerializeField] private float _slideSpeed = 11f;
        [SerializeField] private float _slideHeight = 1.1f;
        [SerializeField] private Vector3 _slideCenter = new Vector3(0f, 0.55f, 0f);
        [SerializeField] private float _slideCameraPivotLocalY = 0.75f;
        [SerializeField] private float _slideCooldown = 0.35f;
        [Header("滑铲跳（滑铲中按跳跃）")]
        [SerializeField] private float _slideJumpUpVelocity = 6.2f;
        [Tooltip("水平速度 = 滑铲速度 × 本倍数；1 与滑铲同速，>1 略强于蹬出（改滑铲速度时水平会一起变）。")]
        [SerializeField] private float _slideJumpHorizontalMultiplier = 1f;

        [Header("梯子")]
        [SerializeField] private float _ladderStrafeMultiplier = 0.35f;
        [SerializeField] private float _ladderJumpOffUp = 4f;
        [SerializeField] private float _ladderJumpOffForward = 3f;

        [Header("重力与跳跃")]
        [SerializeField] private float _gravity = -28f;
        [SerializeField] private float _jumpVelocity = 6.375f;

        [Header("连跳惩罚")]
        [SerializeField] private float _jumpHeightDecayPerChain = 0.82f;
        [SerializeField] private float _jumpForwardDecayPerChain = 0.85f;
        [SerializeField] private float _groundedTimeToResetChain = 0.12f;

        private CharacterController _controller;
        private Vector3 _velocity;
        private float _groundedTimer;
        private int _jumpChainIndex;

        private MotorMode _mode = MotorMode.Normal;
        private float _slideTimeLeft;
        private Vector3 _slideDirHorizontal;
        private float _slideCooldownLeft;

        private FpsLadder _ladderInRange;
        private FpsLadder _activeLadder;

        /// <summary>松 Shift 后倒计时；按住 Shift 时重置为满。</summary>
        private float _sprintGraceTimer;

        private PlayerLocomotionInput _lastFrame;

        /// <summary>当前水平速度大小（m/s，XZ），供第三人称全身 Animator 等与移动动画对齐。</summary>
        public float HorizontalSpeed
        {
            get
            {
                if (_controller == null)
                    return 0f;
                return new Vector3(_velocity.x, 0f, _velocity.z).magnitude;
            }
        }

        /// <summary>是否常规地面移动（非滑铲、非爬梯）。</summary>
        public bool IsNormalLocomotion => _mode == MotorMode.Normal;

        /// <summary>是否贴地（CharacterController）。</summary>
        public bool IsGrounded => _controller != null && _controller.isGrounded;

        /// <summary>竖直速度（m/s，向上为正），供第三人称跳跃/下落与 Animator 对齐。</summary>
        public float VerticalVelocity => _controller == null ? 0f : _velocity.y;

        /// <summary>走路目标速度（与 Inspector 一致），第三人称 Blend Tree 阈值可对齐。</summary>
        public float ConfigWalkSpeed => _walkSpeed;

        /// <summary>疾跑目标速度（与 Inspector 一致）。</summary>
        public float ConfigSprintSpeed => _sprintSpeed;

        /// <summary>本帧 locomotion 输入中的开镜（与 <see cref="_lastFrame"/> 一致；联机服务端来自 <see cref="NetworkLocomotionBuffer"/> RPC）。</summary>
        public bool LocomotionAimHeld => _lastFrame.AimHeld;

        /// <summary>本帧 locomotion 输入中的蹲伏（与 <see cref="_lastFrame"/> 一致；联机服务端来自 RPC）。</summary>
        public bool LocomotionCrouchHeld => _lastFrame.CrouchHeld;

        private void Awake()
        {
            _controller = GetComponent<CharacterController>();
            if (_localLocomotionSource == null)
                _localLocomotionSource = GetComponent<FpsInputLocomotionSource>();
        }

        private void Start()
        {
            ApplyCapsuleStanding();
        }

        private void Update()
        {
            var source = ResolveLocomotionSource();
            if (source == null || !source.TryGetFrame(out var frame))
                return;

            _lastFrame = frame;

            if (!ShouldSimulateMotorPhysics())
                return;

            transform.Rotate(0f, frame.YawDelta, 0f, Space.World);

            _slideCooldownLeft = Mathf.Max(0f, _slideCooldownLeft - Time.deltaTime);

            if (_mode == MotorMode.Sliding)
            {
                UpdateSliding(in frame);
                return;
            }

            if (_mode == MotorMode.Ladder)
            {
                UpdateLadder(in frame);
                return;
            }

            UpdateNormal(in frame);
        }

        /// <summary>仅服务器（含 Host）跑 <see cref="CharacterController"/>；Owner 客户端只更新 <see cref="_lastFrame"/> 供手臂动画等。</summary>
        private bool ShouldSimulateMotorPhysics()
        {
            var no = GetComponent<NetworkObject>();
            if (no == null || !no.IsSpawned)
                return true;
            return NetworkManager.Singleton != null && NetworkManager.Singleton.IsServer;
        }

        private ILocomotionInputSource ResolveLocomotionSource()
        {
            var netObj = GetComponent<NetworkObject>();
            if (_networkBuffer != null && netObj != null && netObj.IsSpawned
                && NetworkManager.Singleton != null && NetworkManager.Singleton.IsServer)
                return _networkBuffer;
            return _localLocomotionSource;
        }

        /// <summary>由 <see cref="FpsLadder"/> 的触发器调用。</summary>
        public void NotifyLadderEnter(FpsLadder ladder)
        {
            _ladderInRange = ladder;
        }

        /// <summary>由 <see cref="FpsLadder"/> 的触发器调用。</summary>
        public void NotifyLadderExit(FpsLadder ladder)
        {
            if (_ladderInRange == ladder)
                _ladderInRange = null;

            if (_activeLadder == ladder && _mode == MotorMode.Ladder)
                ExitLadder(jumpOff: false);
        }

        /// <summary>
        /// 供手臂 Animator（如 Infima <c>Running</c>）：仅当贴地、正常移动（非滑铲/爬梯）、按住疾跑且 WASD 有移动输入时为真；
        /// 开镜 / 开火且勾选对应走路速限制时为假（与移动速度一致）。
        /// </summary>
        public bool ShouldDriveArmsSprintRunningPose
        {
            get
            {
                if (_controller == null)
                    return false;
                if (_mode != MotorMode.Normal)
                    return false;
                if (ShouldSimulateMotorPhysics() && !_controller.isGrounded)
                    return false;
                if (!_lastFrame.SprintHeld)
                    return false;
                if (_limitSpeedToWalkWhileAiming && _lastFrame.AimHeld)
                    return false;
                if (_limitSpeedToWalkWhileFiring && _lastFrame.FireHeld)
                    return false;
                return _lastFrame.MoveAxes.sqrMagnitude > 0.0001f;
            }
        }

        private void UpdateNormal(in PlayerLocomotionInput input)
        {
            bool grounded = _controller.isGrounded;
            if (grounded && _velocity.y < 0f)
                _velocity.y = -2f;

            if (grounded)
            {
                _groundedTimer += Time.deltaTime;
                if (_groundedTimer >= _groundedTimeToResetChain)
                    _jumpChainIndex = 0;
            }
            else
            {
                _groundedTimer = 0f;
            }

            Vector2 axes = input.MoveAxes;
            bool sprinting = input.SprintHeld
                && (!_limitSpeedToWalkWhileAiming || !input.AimHeld)
                && (!_limitSpeedToWalkWhileFiring || !input.FireHeld);

            if (sprinting)
                _sprintGraceTimer = _sprintSlideGraceSeconds;
            else
                _sprintGraceTimer = Mathf.Max(0f, _sprintGraceTimer - Time.deltaTime);

            bool slideEligible = sprinting || _sprintGraceTimer > 0f;

            if (_ladderInRange != null && input.InteractPressedThisFrame)
            {
                EnterLadder();
                return;
            }

            bool trySlide = input.CrouchPressedThisFrame
                && grounded
                && _slideCooldownLeft <= 0f
                && slideEligible;

            if (trySlide)
            {
                StartSlide();
                return;
            }

            bool crouch = input.CrouchHeld && !slideEligible;
            ApplyCapsuleForNormal(crouch);

            float speed = sprinting ? _sprintSpeed : _walkSpeed;
            if (crouch && grounded)
                speed *= _crouchSpeedMultiplier;

            Vector3 move = transform.right * axes.x + transform.forward * axes.y;
            move.y = 0f;
            if (move.sqrMagnitude > 1f)
                move.Normalize();

            Vector3 targetHorizontal = move * speed;
            Vector3 horizontal = new Vector3(_velocity.x, 0f, _velocity.z);
            horizontal = Vector3.MoveTowards(horizontal, targetHorizontal, _acceleration * Time.deltaTime);
            _velocity.x = horizontal.x;
            _velocity.z = horizontal.z;

            if (input.JumpPressedThisFrame && grounded)
            {
                float heightMul = Mathf.Pow(_jumpHeightDecayPerChain, _jumpChainIndex);
                float forwardMul = Mathf.Pow(_jumpForwardDecayPerChain, _jumpChainIndex);
                _velocity.y = _jumpVelocity * heightMul;
                _velocity.x += move.x * _walkSpeed * 0.35f * forwardMul;
                _velocity.z += move.z * _walkSpeed * 0.35f * forwardMul;
                _jumpChainIndex++;
            }

            _velocity.y += _gravity * Time.deltaTime;
            _controller.Move(_velocity * Time.deltaTime);
        }

        private void StartSlide()
        {
            _mode = MotorMode.Sliding;
            _slideTimeLeft = _slideDuration;

            Vector3 f = transform.forward;
            f.y = 0f;
            _slideDirHorizontal = f.sqrMagnitude > 0.0001f ? f.normalized : Vector3.forward;

            _controller.height = _slideHeight;
            _controller.center = _slideCenter;
            SetCameraPivotY(_slideCameraPivotLocalY);

            Vector3 horizontal = _slideDirHorizontal * _slideSpeed;
            _velocity.x = horizontal.x;
            _velocity.z = horizontal.z;
            if (_controller.isGrounded)
                _velocity.y = -2f;
        }

        private void UpdateSliding(in PlayerLocomotionInput input)
        {
            if (input.JumpPressedThisFrame)
            {
                EndSlideEarly();
                float horizSpeed = _slideSpeed * _slideJumpHorizontalMultiplier;
                Vector3 carry = _slideDirHorizontal * horizSpeed;
                _velocity.x = carry.x;
                _velocity.z = carry.z;
                _velocity.y = _slideJumpUpVelocity;
                _jumpChainIndex++;
                _velocity.y += _gravity * Time.deltaTime;
                _controller.Move(_velocity * Time.deltaTime);
                return;
            }

            _slideTimeLeft -= Time.deltaTime;

            Vector3 horizontal = _slideDirHorizontal * _slideSpeed;
            _velocity.x = horizontal.x;
            _velocity.z = horizontal.z;
            _velocity.y += _gravity * Time.deltaTime;

            _controller.Move(_velocity * Time.deltaTime);

            if (_slideTimeLeft <= 0f)
            {
                EndSlideEarly();
            }
        }

        private void EndSlideEarly()
        {
            _slideCooldownLeft = _slideCooldown;
            _mode = MotorMode.Normal;
            ApplyCapsuleStanding();
        }

        private void EnterLadder()
        {
            _activeLadder = _ladderInRange;
            _mode = MotorMode.Ladder;
            _velocity = Vector3.zero;
        }

        private void ExitLadder(bool jumpOff)
        {
            _mode = MotorMode.Normal;
            _activeLadder = null;
            ApplyCapsuleStanding();

            if (jumpOff)
            {
                Vector3 push = transform.forward * _ladderJumpOffForward;
                push.y = _ladderJumpOffUp;
                _velocity = push;
            }
        }

        private void UpdateLadder(in PlayerLocomotionInput input)
        {
            if (_activeLadder == null)
            {
                _mode = MotorMode.Normal;
                ApplyCapsuleStanding();
                return;
            }

            if (input.InteractPressedThisFrame)
            {
                ExitLadder(jumpOff: false);
                return;
            }

            if (input.JumpPressedThisFrame)
            {
                ExitLadder(jumpOff: true);
                _controller.Move(_velocity * Time.deltaTime);
                return;
            }

            Vector2 axes = input.MoveAxes;
            Vector3 up = _activeLadder.WorldUp;
            Vector3 right = _activeLadder.WorldRight;

            Vector3 climb = up * (axes.y * _activeLadder.ClimbSpeed)
                + right * (axes.x * _activeLadder.ClimbSpeed * _ladderStrafeMultiplier);

            _controller.Move(climb * Time.deltaTime);
        }

        private void ApplyCapsuleForNormal(bool crouch)
        {
            if (crouch)
            {
                _controller.height = _crouchHeight;
                _controller.center = _crouchCenter;
                SetCameraPivotY(_crouchCameraPivotLocalY);
            }
            else
            {
                ApplyCapsuleStanding();
            }
        }

        private void ApplyCapsuleStanding()
        {
            _controller.height = _standingHeight;
            _controller.center = _standingCenter;
            SetCameraPivotY(_standingCameraPivotLocalY);
        }

        private void SetCameraPivotY(float y)
        {
            if (_cameraPivot == null)
                return;
            Vector3 lp = _cameraPivot.localPosition;
            lp.y = y;
            _cameraPivot.localPosition = lp;
        }

        /// <summary>复活后重置速度、滑铲/梯子状态与站立胶囊。</summary>
        public void ResetStateForRespawn()
        {
            _velocity = Vector3.zero;
            _slideCooldownLeft = 0f;
            _slideTimeLeft = 0f;
            _sprintGraceTimer = 0f;
            _groundedTimer = 0f;
            _mode = MotorMode.Normal;
            _activeLadder = null;
            _ladderInRange = null;
            ApplyCapsuleStanding();
        }
    }
}
