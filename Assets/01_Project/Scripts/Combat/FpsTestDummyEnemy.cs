using System.Collections;
using FpsDemo.Match;
using UnityEngine;
using UnityEngine.AI;

namespace FpsDemo.Combat
{
    /// <summary>
    /// <b>测试用</b>假人：血量由 <see cref="Health"/>；本脚本仅处理死亡隐藏与全局复活（无游荡位移）。
    /// 若同物体有 <see cref="NavMeshAgent"/>，开局与复活后会 <see cref="NavMeshAgent.Warp"/> 以贴合 NavMesh。
    /// 需<strong>非 Trigger</strong> 的 <see cref="Collider"/>；开局会将 <see cref="Health.SetDestroyOnDeath"/> 设为 <c>false</c>。
    /// </summary>
    [RequireComponent(typeof(Health))]
    public sealed class FpsTestDummyEnemy : MonoBehaviour
    {
        [Header("复活")]
        [SerializeField] private float _respawnDelaySeconds = 2f;
        [Tooltip("与玩家共用的全局复活点（场景中 MatchSpawnPoints）。若已配置且有点，则不再使用下方圆内随机。")]
        [SerializeField] private MatchSpawnPoints _matchSpawnPoints;
        [Tooltip("使用 MatchSpawnPoints 复活时，是否将朝向 Y 对齐到该点旋转。")]
        [SerializeField] private bool _snapYawToSpawnPoint = true;
        [Tooltip("仅当未使用 MatchSpawnPoints 时：圆内随机复活的圆心；空则用开局时物体所在位置。")]
        [SerializeField] private Transform _spawnAreaCenter;
        [Tooltip("无全局复活点：圆内随机复活时，与圆心连线被墙挡住则重抽；最多尝试次数。")]
        [SerializeField] private int _spawnPositionAttempts = 24;

        private const float FallbackSpawnRadius = 6f;

        private Health _health;
        private bool _hiddenForDeath;
        private Vector3 _spawnCenterWorld;
        private Collider _collider;
        private Renderer[] _renderers;
        private Coroutine _respawnRoutine;
        private NavMeshAgent _navAgent;

        public float CurrentHealth => _health != null ? _health.Current : 0f;
        public bool IsDead => _health != null && _health.IsDead;

        private void Awake()
        {
            _health = GetComponent<Health>();
            _health.SetDestroyOnDeath(false);

            _collider = GetComponent<Collider>();
            _renderers = GetComponentsInChildren<Renderer>(true);
            _navAgent = GetComponent<NavMeshAgent>();

            _spawnCenterWorld = transform.position;

            if (!TryTeleportToRandomMatchSpawnPoint())
                _spawnCenterWorld = _spawnAreaCenter != null ? _spawnAreaCenter.position : transform.position;
            else
                _spawnCenterWorld = transform.position;
        }

        private void OnEnable()
        {
            if (_health != null)
                _health.Died += OnHealthDied;
        }

        private void OnDisable()
        {
            if (_health != null)
                _health.Died -= OnHealthDied;
        }

        private void OnHealthDied(KillReport report)
        {
            if (report.Victim != gameObject)
                return;

            if (_hiddenForDeath)
                return;

            _hiddenForDeath = true;
            if (_collider != null)
                _collider.enabled = false;
            foreach (var r in _renderers)
            {
                if (r != null)
                    r.enabled = false;
            }

            if (_navAgent != null && _navAgent.enabled)
                _navAgent.enabled = false;

            if (_respawnRoutine != null)
                StopCoroutine(_respawnRoutine);
            _respawnRoutine = StartCoroutine(RespawnAfterDelay());
        }

        private int ObstacleLayers =>
            Physics.DefaultRaycastLayers;

        private IEnumerator RespawnAfterDelay()
        {
            yield return new WaitForSeconds(_respawnDelaySeconds);

            if (TryTeleportToRandomMatchSpawnPoint())
            {
                _spawnCenterWorld = transform.position;
            }
            else
            {
                float y = transform.position.y;
                Vector3 c = _spawnAreaCenter != null ? _spawnAreaCenter.position : _spawnCenterWorld;
                Vector3 centerFlat = new Vector3(c.x, y, c.z);
                Vector3 chosen = centerFlat;
                for (int attempt = 0; attempt < _spawnPositionAttempts; attempt++)
                {
                    Vector2 rnd = Random.insideUnitCircle * FallbackSpawnRadius;
                    Vector3 candidate = new Vector3(c.x + rnd.x, y, c.z + rnd.y);
                    if (Physics.Linecast(centerFlat, candidate, ObstacleLayers, QueryTriggerInteraction.Ignore))
                        continue;
                    chosen = candidate;
                    break;
                }

                transform.position = chosen;
                _spawnCenterWorld = chosen;
            }

            WarpNavMeshIfNeeded();

            _health.ReviveFull();
            _hiddenForDeath = false;
            if (_collider != null)
                _collider.enabled = true;
            foreach (var ren in _renderers)
            {
                if (ren != null)
                    ren.enabled = true;
            }

            if (_navAgent != null)
            {
                _navAgent.enabled = true;
                if (_navAgent.isOnNavMesh)
                    _navAgent.ResetPath();
            }

            _respawnRoutine = null;
        }

        private bool TryTeleportToRandomMatchSpawnPoint()
        {
            if (_matchSpawnPoints == null || !_matchSpawnPoints.HasAnyValidPoint)
                return false;
            if (!_matchSpawnPoints.TryPickSpawnPointForRespawn(gameObject, out Transform sp) || sp == null)
                return false;
            transform.position = sp.position;
            if (_snapYawToSpawnPoint)
            {
                Vector3 e = transform.eulerAngles;
                e.y = sp.eulerAngles.y;
                transform.eulerAngles = e;
            }

            WarpNavMeshIfNeeded();
            return true;
        }

        private void WarpNavMeshIfNeeded()
        {
            if (_navAgent == null)
                return;
            _navAgent.Warp(transform.position);
        }
    }
}
