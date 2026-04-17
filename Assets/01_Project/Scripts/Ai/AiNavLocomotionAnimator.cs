using UnityEngine;
using UnityEngine.AI;

namespace FpsDemo.Ai
{
    /// <summary>
    /// 人机：将 <see cref="NavMeshAgent"/> 的移动速度写入全身 <see cref="Animator"/>，
    /// 参数默认与 <see cref="FpsDemo.Fps.FpsThirdPersonLocomotionAnimator"/>（X Bot）一致，避免两套 Blend Tree。
    /// 挂在与 <see cref="NavMeshAgent"/> 同物体（一般为根）；<see cref="_animator"/> 指向 X Bot 子物体上的 Animator。
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class AiNavLocomotionAnimator : MonoBehaviour
    {
        [Header("引用")]
        [SerializeField] private NavMeshAgent _agent;
        [Tooltip("一般为 X Bot 子物体上的 Animator。")]
        [SerializeField] private Animator _animator;

        [Header("Animator 参数名（与第三人称 Locomotion 一致）")]
        [SerializeField] private string _speedParameterName = "speed";
        [SerializeField] private string _crouchParameterName = "IsCrouch";
        [SerializeField] private string _aimingParameterName = "IsAiming";
        [SerializeField] private string _groundedParameterName = "IsGrounded";
        [SerializeField] private string _verticalSpeedParameterName = "VerticalSpeed";

        [Header("跳跃相关")]
        [Tooltip("关闭则不写 IsGrounded / VerticalSpeed（人机通常无跳跃）。")]
        [SerializeField] private bool _syncJumpParameters;

        private int _speedHash;
        private int _crouchHash;
        private int _aimingHash;
        private int _groundedHash;
        private int _verticalSpeedHash;

        private void Awake()
        {
            if (_agent == null)
                _agent = GetComponent<NavMeshAgent>();
            if (_animator == null)
                _animator = GetComponentInChildren<Animator>(true);

            _speedHash = Animator.StringToHash(_speedParameterName);
            _crouchHash = Animator.StringToHash(_crouchParameterName);
            _aimingHash = Animator.StringToHash(_aimingParameterName);
            _groundedHash = Animator.StringToHash(_groundedParameterName);
            _verticalSpeedHash = Animator.StringToHash(_verticalSpeedParameterName);
        }

        private void LateUpdate()
        {
            if (_animator == null || !_animator.isActiveAndEnabled)
                return;

            if (_agent == null || !_agent.isOnNavMesh || !_agent.enabled)
            {
                SetIdleLocomotion();
                return;
            }

            Vector3 v = _agent.velocity;
            float horizontal = new Vector3(v.x, 0f, v.z).magnitude;
            _animator.SetFloat(_speedHash, horizontal);

            _animator.SetBool(_crouchHash, false);
            _animator.SetBool(_aimingHash, false);

            if (_syncJumpParameters)
            {
                _animator.SetBool(_groundedHash, true);
                _animator.SetFloat(_verticalSpeedHash, 0f);
            }
        }

        private void OnDisable()
        {
            if (_animator != null)
            {
                _animator.SetFloat(_speedHash, 0f);
                _animator.SetBool(_crouchHash, false);
                _animator.SetBool(_aimingHash, false);
                if (_syncJumpParameters)
                {
                    _animator.SetBool(_groundedHash, true);
                    _animator.SetFloat(_verticalSpeedHash, 0f);
                }
            }
        }

        private void SetIdleLocomotion()
        {
            _animator.SetFloat(_speedHash, 0f);
            _animator.SetBool(_crouchHash, false);
            _animator.SetBool(_aimingHash, false);
            if (_syncJumpParameters)
            {
                _animator.SetBool(_groundedHash, true);
                _animator.SetFloat(_verticalSpeedHash, 0f);
            }
        }
    }
}
