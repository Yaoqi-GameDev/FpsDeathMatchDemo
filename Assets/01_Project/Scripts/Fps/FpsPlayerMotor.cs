using UnityEngine;

namespace FpsDemo.Fps
{
    /// <summary>
    /// 第一人称移动：走路、蹲、疾跑、滑铲、梯子、跳跃与连跳惩罚。
    /// 蹲伏移速倍率仅贴地生效，空中不按蹲把水平目标速度压低。
    /// 挂在与 <see cref="CharacterController"/> 同一物体上。
    /// </summary>
    [RequireComponent(typeof(CharacterController))]
    public sealed class FpsPlayerMotor : MonoBehaviour
    {
        private enum MotorMode
        {
            Normal,
            Sliding,
            Ladder
        }

        [SerializeField] private FpsInput _input;
        [SerializeField] private Transform _cameraPivot;

        [Header("走路 / 疾跑")]
        [SerializeField] private float _walkSpeed = 4.5f;
        [SerializeField] private float _sprintSpeed = 8f;
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

        private void Awake()
        {
            _controller = GetComponent<CharacterController>();
        }

        private void Start()
        {
            ApplyCapsuleStanding();
        }

        private void Update()
        {
            if (_input == null)
                return;

            _slideCooldownLeft = Mathf.Max(0f, _slideCooldownLeft - Time.deltaTime);

            if (_mode == MotorMode.Sliding)
            {
                UpdateSliding();
                return;
            }

            if (_mode == MotorMode.Ladder)
            {
                UpdateLadder();
                return;
            }

            UpdateNormal();
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

        private void UpdateNormal()
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

            Vector2 axes = _input.MoveAxes;
            bool sprinting = _input.SprintHeld;

            if (sprinting)
                _sprintGraceTimer = _sprintSlideGraceSeconds;
            else
                _sprintGraceTimer = Mathf.Max(0f, _sprintGraceTimer - Time.deltaTime);

            bool slideEligible = sprinting || _sprintGraceTimer > 0f;

            if (_ladderInRange != null && _input.InteractPressedThisFrame)
            {
                EnterLadder();
                return;
            }

            bool trySlide = _input.CrouchPressedThisFrame
                && grounded
                && _slideCooldownLeft <= 0f
                && slideEligible;

            if (trySlide)
            {
                StartSlide();
                return;
            }

            bool crouch = _input.CrouchHeld && !slideEligible;
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

            if (_input.JumpPressedThisFrame && grounded)
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

        private void UpdateSliding()
        {
            if (_input.JumpPressedThisFrame)
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

        private void UpdateLadder()
        {
            if (_activeLadder == null)
            {
                _mode = MotorMode.Normal;
                ApplyCapsuleStanding();
                return;
            }

            if (_input.InteractPressedThisFrame)
            {
                ExitLadder(jumpOff: false);
                return;
            }

            if (_input.JumpPressedThisFrame)
            {
                ExitLadder(jumpOff: true);
                _controller.Move(_velocity * Time.deltaTime);
                return;
            }

            Vector2 axes = _input.MoveAxes;
            Vector3 up = _activeLadder.WorldUp;
            Vector3 right = _activeLadder.WorldRight;

            // W/S 沿梯 up；A/D 沿梯 right（微调）。朝向由玩家自己转相机/身体即可。
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
