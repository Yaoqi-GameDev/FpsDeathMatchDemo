using FpsDemo.Combat;
using UnityEngine;

namespace FpsDemo.Ai
{
    /// <summary>
    /// 单机 AI：转向目标、距离与视线检测通过后按间隔调用 <see cref="FpsAiHitscanWeapon.TryFireFromAimTransform"/>。
    /// 与 <see cref="FpsDemo.Fps.FpsInput"/> 无关；联机场景可不挂本组件。
    /// </summary>
    public sealed class FpsAiHitscanShooter : MonoBehaviour
    {
        [SerializeField] private FpsAiHitscanWeapon _weapon;
        [SerializeField] private Transform _aimOrigin;
        [Tooltip("空：用 Tag 查找；否则用该物体作为目标根（一般为 Player 根）。")]
        [SerializeField] private Transform _target;
        [SerializeField] private string _targetTag = "Player";

        [Header("转向")]
        [SerializeField] private float _rotateSpeedDegrees = 240f;
        [Tooltip("瞄准点相对目标根的世界 Y 偏移（约胸口高度）。")]
        [SerializeField] private float _aimHeightOffset = 1.2f;

        [Header("射击")]
        [SerializeField] private float _maxAttackRange = 40f;
        [Tooltip("开火间隔（秒），与武器射速独立，便于单独调 AI 强度。")]
        [SerializeField] private float _fireIntervalSeconds = 0.35f;
        [Tooltip("视线检测：首个命中物不是目标根则视为被挡。")]
        [SerializeField] private LayerMask _lineOfSightMask;

        [Header("自身")]
        [SerializeField] private Health _selfHealth;

        private float _nextFireTime;

        private void Awake()
        {
            if (_weapon == null)
                _weapon = GetComponent<FpsAiHitscanWeapon>();

            if (_aimOrigin == null)
                _aimOrigin = transform;

            if (_selfHealth == null)
                _selfHealth = GetComponent<Health>();

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

            Quaternion look = Quaternion.LookRotation(to.normalized, Vector3.up);
            _aimOrigin.rotation = Quaternion.RotateTowards(
                _aimOrigin.rotation,
                look,
                _rotateSpeedDegrees * Time.deltaTime);

            float dist = to.magnitude;
            if (dist > _maxAttackRange)
                return;

            if (!HasLineOfSight(aimPoint, target.root))
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
            if (_target != null)
                return _target;

            if (!string.IsNullOrEmpty(_targetTag))
            {
                var go = GameObject.FindGameObjectWithTag(_targetTag);
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
    }
}
