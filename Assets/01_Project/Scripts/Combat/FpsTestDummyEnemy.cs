using System.Collections;
using FpsDemo.Match;
using UnityEngine;

namespace FpsDemo.Combat
{
    /// <summary>
    /// <b>测试用</b>假人：<strong>血量与击杀归因由同物体上的 <see cref="Health"/> 处理</strong>；游荡与复活。
    /// 配置 <see cref="MatchSpawnPoints"/> 时：开局与复活均在随机复活点；游荡仅以<strong>当前落点</strong>为圆心（半径内置，不在 Inspector 暴露）。
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

        [Header("游荡（仅存活时，复活后延迟一拍再开始）")]
        [SerializeField] private float _wanderSpeed = 1.8f;
        [SerializeField] private Vector2 _wanderIntervalSeconds = new Vector2(1.2f, 3.5f);
        [Tooltip("沿墙滑动时保留的间隙，避免卡在表面。")]
        [SerializeField] private float _collisionSkin = 0.05f;

        [Header("碰撞（游荡）")]
        [Tooltip("用于射线检测；空则用 DefaultRaycastLayers。")]
        [SerializeField] private LayerMask _obstacleMask;

        /// <summary>游荡目标在 XZ 上相对此点随机偏移；每次落在复活点或后备圆内随机后更新。</summary>
        private Vector3 _wanderCenterWorld;

        private const float FallbackSpawnRadius = 6f;
        private const float WanderRadius = 8f;

        private Health _health;
        private bool _hiddenForDeath;
        private Collider _collider;
        private Renderer[] _renderers;
        private Coroutine _respawnRoutine;
        private Vector3 _moveTarget;
        private float _nextPickMoveTime;

        public float CurrentHealth => _health != null ? _health.Current : 0f;
        public bool IsDead => _health != null && _health.IsDead;

        private void Awake()
        {
            _health = GetComponent<Health>();
            _health.SetDestroyOnDeath(false);

            _collider = GetComponent<Collider>();
            _renderers = GetComponentsInChildren<Renderer>(true);

            if (!TryTeleportToRandomMatchSpawnPoint())
            {
                _wanderCenterWorld = _spawnAreaCenter != null ? _spawnAreaCenter.position : transform.position;
            }
            else
                _wanderCenterWorld = transform.position;

            _moveTarget = transform.position;
            _nextPickMoveTime = Time.time + Random.Range(_wanderIntervalSeconds.x, _wanderIntervalSeconds.y);
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

            if (_respawnRoutine != null)
                StopCoroutine(_respawnRoutine);
            _respawnRoutine = StartCoroutine(RespawnAfterDelay());
        }

        private int ObstacleLayers =>
            _obstacleMask.value != 0 ? _obstacleMask : Physics.DefaultRaycastLayers;

        private void Update()
        {
            if (_health == null || _health.IsDead || _hiddenForDeath)
                return;

            if (Time.time >= _nextPickMoveTime)
            {
                PickNewWanderTarget();
                _nextPickMoveTime = Time.time + Random.Range(_wanderIntervalSeconds.x, _wanderIntervalSeconds.y);
            }

            Vector3 p = transform.position;
            Vector3 flatTarget = new Vector3(_moveTarget.x, p.y, _moveTarget.z);
            float step = _wanderSpeed * Time.deltaTime;
            Vector3 desired = Vector3.MoveTowards(p, flatTarget, step);
            Vector3 moved = MoveHorizontalWithCapsuleCast(p, desired);
            transform.position = moved;
        }

        private Vector3 MoveHorizontalWithCapsuleCast(Vector3 from, Vector3 to)
        {
            Vector3 delta = to - from;
            delta.y = 0f;
            float dist = delta.magnitude;
            if (dist < 1e-6f)
                return from;

            Vector3 dir = delta / dist;
            GetCapsuleForCast(from, out Vector3 p1, out Vector3 p2, out float radius);

            bool wasEnabled = _collider != null && _collider.enabled;
            if (_collider != null)
                _collider.enabled = false;

            bool hit = Physics.CapsuleCast(
                p1,
                p2,
                radius,
                dir,
                out RaycastHit rh,
                dist,
                ObstacleLayers,
                QueryTriggerInteraction.Ignore);

            if (_collider != null)
                _collider.enabled = wasEnabled;

            if (!hit)
                return to;

            float safe = Mathf.Max(0f, rh.distance - _collisionSkin);
            Vector3 along = from + dir * safe;
            return new Vector3(along.x, from.y, along.z);
        }

        private void GetCapsuleForCast(Vector3 worldFeetY, out Vector3 point1, out Vector3 point2, out float radius)
        {
            float y = worldFeetY.y;
            if (_collider is CapsuleCollider cap)
            {
                Transform t = cap.transform;
                Vector3 c = t.TransformPoint(cap.center);
                float half = Mathf.Max(0f, cap.height * 0.5f - cap.radius);
                Vector3 axis = cap.direction == 0
                    ? t.right
                    : cap.direction == 1 ? t.up : t.forward;
                point1 = c - axis * half;
                point2 = c + axis * half;
                radius = cap.radius * Mathf.Max(t.lossyScale.x, t.lossyScale.z);
                return;
            }

            if (_collider != null)
            {
                Bounds b = _collider.bounds;
                radius = Mathf.Max(b.extents.x, b.extents.z) * 0.95f;
                float h = b.size.y;
                Vector3 center = b.center;
                point1 = new Vector3(center.x, center.y - h * 0.5f + radius, center.z);
                point2 = new Vector3(center.x, center.y + h * 0.5f - radius, center.z);
                return;
            }

            radius = 0.4f;
            point1 = new Vector3(worldFeetY.x, y + radius, worldFeetY.z);
            point2 = new Vector3(worldFeetY.x, y + 1.6f - radius, worldFeetY.z);
        }

        private void PickNewWanderTarget()
        {
            Vector2 c = Random.insideUnitCircle * WanderRadius;
            Vector3 center = _wanderCenterWorld;
            _moveTarget = new Vector3(center.x + c.x, transform.position.y, center.z + c.y);
        }

        /// <summary>将自身放到随机全局复活点；成功则返回 <c>true</c>。</summary>
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

            return true;
        }

        private IEnumerator RespawnAfterDelay()
        {
            yield return new WaitForSeconds(_respawnDelaySeconds);

            if (TryTeleportToRandomMatchSpawnPoint())
            {
                _wanderCenterWorld = transform.position;
            }
            else
            {
                float y = transform.position.y;
                Vector3 c = _spawnAreaCenter != null ? _spawnAreaCenter.position : _wanderCenterWorld;
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
                _wanderCenterWorld = chosen;
            }

            _health.ReviveFull();
            _hiddenForDeath = false;
            if (_collider != null)
                _collider.enabled = true;
            foreach (var ren in _renderers)
            {
                if (ren != null)
                    ren.enabled = true;
            }

            _moveTarget = transform.position;
            _nextPickMoveTime = Time.time + Random.Range(_wanderIntervalSeconds.x, _wanderIntervalSeconds.y);
            _respawnRoutine = null;
        }
    }
}
