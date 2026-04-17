using FpsDemo.Match;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.Serialization;

namespace FpsDemo.Ai
{
    /// <summary>
    /// 每帧将 <see cref="NavMeshAgent"/> 目的地设为当前追击目标的世界坐标。
    /// 目标选取与 <see cref="FpsAiHitscanShooter"/> 一致：优先最近存活 <see cref="MatchParticipant"/>（排除自身），再可选 Tag 后备。
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class AiNavChaseTransform : MonoBehaviour
    {
        [SerializeField] private NavMeshAgent _agent;

        [Header("目标（与 Fps Ai Hitscan Shooter 对齐）")]
        [Tooltip("勾选：在参战者列表中选最近存活他人；无可用时再走 Tag 后备。")]
        [SerializeField] private bool _preferMatchParticipants = true;
        [Tooltip("非空：始终追该 Transform（调试）；不走参战者列表。")]
        [FormerlySerializedAs("_target")]
        [SerializeField] private Transform _targetOverride;
        [Tooltip("后备：仅当未用手动目标且参战者列表无可用目标时，按 Tag 查找。")]
        [SerializeField] private string _fallbackTargetTag = "Player";
        [Tooltip("空则在同物体上取 MatchParticipant，用于排除自己。")]
        [SerializeField] private MatchParticipant _selfParticipant;

        private void Awake()
        {
            if (_agent == null)
                _agent = GetComponent<NavMeshAgent>();
            if (_selfParticipant == null)
                _selfParticipant = GetComponent<MatchParticipant>();
        }

        private void Update()
        {
            if (_agent == null || !_agent.isOnNavMesh || !_agent.enabled)
                return;

            Transform chase = ResolveChaseTarget();
            if (chase == null)
                return;

            _agent.SetDestination(chase.position);
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
