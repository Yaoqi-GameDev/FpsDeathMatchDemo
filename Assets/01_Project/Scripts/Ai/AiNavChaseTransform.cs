using FpsDemo.Combat;
using FpsDemo.Match;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.Serialization;

namespace FpsDemo.Ai
{
    /// <summary>
    /// 人机导航：与 <see cref="FpsAiHitscanShooter.TryGetNavigationEngagement"/> 联动——
    /// 不可交火（超出射程或无 LOS）时<strong>疾跑</strong>追目标；可交火时<strong>停住</strong>站立；
    /// 无目标时在小范围内随机游荡。
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class AiNavChaseTransform : MonoBehaviour
    {
        [SerializeField] private NavMeshAgent _agent;
        [SerializeField] private Health _health;
        [Tooltip("空则同物体获取；用于与射击共用「能否站定交火」判定。")]
        [SerializeField] private FpsAiHitscanShooter _shooter;

        [Header("目标（与射击一致；无 Shooter 时仍用于游荡/追击后备）")]
        [SerializeField] private bool _preferMatchParticipants = true;
        [FormerlySerializedAs("_target")]
        [SerializeField] private Transform _targetOverride;
        [SerializeField] private string _fallbackTargetTag = "Player";
        [SerializeField] private MatchParticipant _selfParticipant;

        [Header("速度")]
        [Tooltip("无敌人游荡。")]
        [SerializeField] private float _wanderMoveSpeed = 3.5f;
        [Tooltip("有敌人但尚不能站定交火时疾跑追目标。")]
        [SerializeField] private float _sprintMoveSpeed = 7f;

        [Header("游荡（无目标）")]
        [SerializeField] private bool _wanderWhenNoTarget = true;
        [SerializeField] private Transform _wanderAnchor;
        [SerializeField] private float _wanderRadius = 6f;
        [SerializeField] private Vector2 _wanderRetargetIntervalSeconds = new Vector2(2.5f, 5f);
        [SerializeField] private float _wanderSampleMaxDistance = 2f;
        [SerializeField] private int _wanderSampleMaxAttempts = 12;

        [Header("避让")]
        [SerializeField] private bool _highQualityObstacleAvoidance = true;
        [SerializeField] private bool _randomizeAvoidancePriority = true;

        private Vector3 _wanderCenterWorld;
        private float _nextWanderPickTime;
        private bool _hadChaseTargetLastFrame;

        private void Awake()
        {
            if (_agent == null)
                _agent = GetComponent<NavMeshAgent>();
            if (_selfParticipant == null)
                _selfParticipant = GetComponent<MatchParticipant>();
            if (_health == null)
                _health = GetComponent<Health>();
            if (_shooter == null)
                _shooter = GetComponent<FpsAiHitscanShooter>();

            _wanderCenterWorld = transform.position;
            _nextWanderPickTime = 0f;

            if (_agent != null)
            {
                if (_highQualityObstacleAvoidance)
                    _agent.obstacleAvoidanceType = ObstacleAvoidanceType.HighQualityObstacleAvoidance;
                if (_randomizeAvoidancePriority)
                    _agent.avoidancePriority = Random.Range(35, 66);
            }
        }

        private void Update()
        {
            if (_agent == null || !_agent.enabled)
                return;
            if (_health != null && _health.IsDead)
                return;
            if (!_agent.isOnNavMesh)
                return;

            if (_shooter != null)
            {
                if (_shooter.TryGetNavigationEngagement(out Transform engageTarget, out bool holdPosition))
                {
                    _hadChaseTargetLastFrame = true;
                    _agent.speed = _sprintMoveSpeed;

                    if (holdPosition)
                    {
                        if (!_agent.isStopped)
                        {
                            _agent.isStopped = true;
                            _agent.ResetPath();
                        }

                        return;
                    }

                    if (_agent.isStopped)
                        _agent.isStopped = false;
                    _agent.SetDestination(engageTarget.position);
                    return;
                }
            }

            Transform chase = ResolveChaseTarget();
            if (chase != null)
            {
                _hadChaseTargetLastFrame = true;
                _agent.speed = _sprintMoveSpeed;
                if (_agent.isStopped)
                    _agent.isStopped = false;
                _agent.SetDestination(chase.position);
                return;
            }

            if (_agent.isStopped)
                _agent.isStopped = false;

            _agent.speed = _wanderMoveSpeed;

            if (!_wanderWhenNoTarget)
            {
                if (!_hadChaseTargetLastFrame)
                    return;
                _hadChaseTargetLastFrame = false;
                _agent.ResetPath();
                return;
            }

            if (_hadChaseTargetLastFrame)
            {
                _hadChaseTargetLastFrame = false;
                _nextWanderPickTime = 0f;
            }

            bool timeToPick = Time.time >= _nextWanderPickTime;
            bool arrived = HasReachedCurrentDestination();
            if (!timeToPick && !arrived)
                return;

            if (TryPickWanderDestination())
                _nextWanderPickTime = Time.time + Random.Range(_wanderRetargetIntervalSeconds.x, _wanderRetargetIntervalSeconds.y);
        }

        private bool HasReachedCurrentDestination()
        {
            if (_agent.pathPending)
                return false;
            if (!_agent.hasPath)
                return false;
            if (_agent.remainingDistance > _agent.stoppingDistance + 0.2f)
                return false;
            return true;
        }

        private bool TryPickWanderDestination()
        {
            Vector3 center = _wanderAnchor != null ? _wanderAnchor.position : _wanderCenterWorld;
            float sampleDist = Mathf.Max(0.5f, _wanderSampleMaxDistance);

            for (int a = 0; a < _wanderSampleMaxAttempts; a++)
            {
                Vector2 rnd = Random.insideUnitCircle * _wanderRadius;
                Vector3 candidate = new Vector3(center.x + rnd.x, center.y, center.z + rnd.y);

                if (NavMesh.SamplePosition(candidate, out NavMeshHit hit, sampleDist, NavMesh.AllAreas))
                {
                    _agent.SetDestination(hit.position);
                    return true;
                }
            }

            return false;
        }

        private Transform ResolveChaseTarget()
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
    }
}
