using FpsDemo.Combat;
using FpsDemo.Match;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.Serialization;

namespace FpsDemo.Ai
{
    /// <summary>
    /// 单机 AI：身体（<see cref="_bodyFacing"/>）水平对准目标后才允许开火；<see cref="NavMeshAgent"/> 若存在则关闭 <c>updateRotation</c>，由本脚本转身体。
    /// 距离、视线、射速仍按原逻辑。与 <see cref="FpsDemo.Fps.FpsInput"/> 无关。
    /// </summary>
    public sealed class FpsAiHitscanShooter : MonoBehaviour
    {
        [SerializeField] private FpsAiHitscanWeapon _weapon;
        [SerializeField] private Transform _aimOrigin;

        [Header("目标")]
        [Tooltip("勾选：在 MatchParticipant.ActiveParticipants 中选最近存活单位（排除自身）；无可用时再走下方后备。")]
        [SerializeField] private bool _preferMatchParticipants = true;
        [Tooltip("优先：固定目标根（调试用）；非空时始终打该 Transform，不走参战者列表。")]
        [FormerlySerializedAs("_target")]
        [SerializeField] private Transform _targetOverride;
        [Tooltip("后备：Tag 查找（仅当未用手动目标且参战者列表无可用目标时）。")]
        [FormerlySerializedAs("_targetTag")]
        [SerializeField] private string _fallbackTargetTag = "Player";

        [Header("身体朝向（方案 A：身体对准后才开火）")]
        [Tooltip("用于判定「身体是否朝向目标」的 Transform，一般为根物体；空则用本物体。")]
        [SerializeField] private Transform _bodyFacing;
        [Tooltip("身体水平转向目标的最大角速度（度/秒）。")]
        [SerializeField] private float _bodyYawRotateSpeedDegrees = 240f;
        [Tooltip("身体 forward 与目标方向在水平面上夹角 ≤ 此值时才允许开火。")]
        [SerializeField] private float _maxBodyFireAngleDegrees = 20f;
        [Tooltip("人机导航：关闭则由本脚本负责身体朝向，避免与「朝速度方向转」冲突。")]
        [SerializeField] private NavMeshAgent _navAgent;
        [Tooltip("瞄准点相对目标根的世界 Y 偏移（约胸口）；射线从 Aim Origin 发出。")]
        [SerializeField] private float _aimHeightOffset = 1.2f;

        [Header("射击")]
        [SerializeField] private float _maxAttackRange = 40f;
        [Tooltip("开火间隔（秒），与武器射速独立，便于单独调 AI 强度。")]
        [SerializeField] private float _fireIntervalSeconds = 0.35f;
        [Tooltip("视线检测：首个命中物不是目标根则视为被挡。")]
        [SerializeField] private LayerMask _lineOfSightMask;

        [Header("自身")]
        [SerializeField] private Health _selfHealth;
        [Tooltip("空则在同物体上取 MatchParticipant，用于排除自己。")]
        [SerializeField] private MatchParticipant _selfParticipant;

        private float _nextFireTime;

        /// <summary>
        /// 供 <see cref="AiNavChaseTransform"/> 使用：有有效目标且已在射程内、有 LOS 时建议停住站立；否则应继续靠近。
        /// </summary>
        public bool TryGetNavigationEngagement(out Transform target, out bool holdPositionOnNavMesh)
        {
            target = null;
            holdPositionOnNavMesh = false;

            if (_weapon == null || _aimOrigin == null)
                return false;

            if (_selfHealth != null && _selfHealth.IsDead)
                return false;

            target = ResolveTarget();
            if (target == null)
                return false;

            Health targetHealth = target.GetComponentInParent<Health>();
            if (targetHealth != null && targetHealth.IsDead)
                return false;

            Vector3 aimPoint = target.position + Vector3.up * _aimHeightOffset;
            Vector3 to = aimPoint - _aimOrigin.position;
            if (to.sqrMagnitude < 0.0001f)
            {
                holdPositionOnNavMesh = true;
                return true;
            }

            float dist = to.magnitude;
            bool inRange = dist <= _maxAttackRange;
            bool los = HasLineOfSight(aimPoint, target.root);
            holdPositionOnNavMesh = inRange && los;
            return true;
        }

        private void Awake()
        {
            if (_weapon == null)
                _weapon = GetComponent<FpsAiHitscanWeapon>();

            if (_aimOrigin == null)
                _aimOrigin = transform;
            if (_bodyFacing == null)
                _bodyFacing = transform;

            if (_selfHealth == null)
                _selfHealth = GetComponent<Health>();
            if (_selfParticipant == null)
                _selfParticipant = GetComponent<MatchParticipant>();

            if (_navAgent == null)
                _navAgent = GetComponent<NavMeshAgent>();
            if (_navAgent != null)
                _navAgent.updateRotation = false;

            if (_lineOfSightMask.value == 0)
                _lineOfSightMask = Physics.DefaultRaycastLayers;
        }

        private void Update()
        {
            if (_weapon == null || _aimOrigin == null)
                return;

            if (_selfHealth != null && _selfHealth.IsDead)
                return;

            Transform target = ResolveTarget();
            if (target == null)
                return;

            Health targetHealth = target.GetComponentInParent<Health>();
            if (targetHealth != null && targetHealth.IsDead)
                return;

            Vector3 aimPoint = target.position + Vector3.up * _aimHeightOffset;
            Vector3 to = aimPoint - _aimOrigin.position;
            if (to.sqrMagnitude < 0.0001f)
                return;

            RotateBodyYawTowardWorldDirection(to);

            float dist = to.magnitude;
            if (dist > _maxAttackRange)
                return;

            if (!HasLineOfSight(aimPoint, target.root))
                return;

            if (!IsBodyAlignedForFire(to))
                return;

            if (Time.time < _nextFireTime)
                return;

            if (_weapon.IsReloading)
            {
                _weapon.TryStartReload();
                return;
            }

            if (_weapon.AmmoInMagazine <= 0)
            {
                _weapon.TryStartReload();
                return;
            }

            if (_weapon.TryFireFromAimTransform(_aimOrigin, out _, out _))
                _nextFireTime = Time.time + _fireIntervalSeconds;
        }

        private Transform ResolveTarget()
        {
            if (_targetOverride != null)
                return _targetOverride;

            if (_preferMatchParticipants)
            {
                var t = AiParticipantTarget.FindNearestAliveOther(transform, _selfParticipant);
                if (t != null)
                    return t;
            }

            if (!string.IsNullOrEmpty(_fallbackTargetTag))
            {
                var go = GameObject.FindGameObjectWithTag(_fallbackTargetTag);
                if (go != null)
                    return go.transform;
            }

            return null;
        }

        private bool HasLineOfSight(Vector3 worldPoint, Transform targetRoot)
        {
            if (targetRoot == null)
                return false;

            Vector3 origin = _aimOrigin.position;
            Vector3 to = worldPoint - origin;
            float dist = to.magnitude;
            if (dist < 1e-4f)
                return true;

            Vector3 dir = to / dist;
            if (!Physics.Raycast(origin, dir, out RaycastHit hit, dist, _lineOfSightMask, QueryTriggerInteraction.Ignore))
                return true;

            return hit.collider.transform.root == targetRoot;
        }

        private void RotateBodyYawTowardWorldDirection(Vector3 worldToTarget)
        {
            Vector3 flat = worldToTarget;
            flat.y = 0f;
            if (flat.sqrMagnitude < 1e-6f)
                return;

            Quaternion targetRot = Quaternion.LookRotation(flat.normalized, Vector3.up);
            _bodyFacing.rotation = Quaternion.RotateTowards(
                _bodyFacing.rotation,
                targetRot,
                _bodyYawRotateSpeedDegrees * Time.deltaTime);
        }

        private bool IsBodyAlignedForFire(Vector3 worldToTarget)
        {
            Vector3 f = _bodyFacing.forward;
            f.y = 0f;
            Vector3 t = worldToTarget;
            t.y = 0f;
            if (f.sqrMagnitude < 1e-8f || t.sqrMagnitude < 1e-8f)
                return true;

            float angle = Vector3.Angle(f.normalized, t.normalized);
            return angle <= _maxBodyFireAngleDegrees;
        }
    }
}
