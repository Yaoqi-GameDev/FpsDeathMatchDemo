using System.Collections.Generic;
using FpsDemo.Combat;
using UnityEngine;

namespace FpsDemo.Match
{
    /// <summary>
    /// 局内统一复活点：场景中放<strong>一个</strong>该组件，将若干贴地空物体拖入列表；
    /// <see cref="PlayerDeathRespawn"/>、人机/假人复活等引用此处，避免与玩家各维护一套点。
    /// 选取时：全体参赛者共用冷却；并排除「存活单位」在 XZ 上距该点过近的点；若仍无可用则逐级放宽（仍应配置足够点数）。
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class MatchSpawnPoints : MonoBehaviour
    {
        [Header("复活点")]
        [Tooltip("复活点（空物体 Transform，建议 Y 与地面对齐）。")]
        [SerializeField] private Transform[] _spawnPoints;

        [Header("选取规则")]
        [Tooltip("任意单位（含假人）在该点复活后，该点在若干秒内不再作为候选；用 Unscaled 时间。")]
        [SerializeField] private float _respawnCooldownSeconds = 3f;

        [Tooltip("XZ 平面：若存活参赛者距该点小于此距离，则该点不选（排除自身）；死亡单位不占位。")]
        [SerializeField] private float _minClearRadiusFromParticipant = 2.5f;

        /// <summary>与 <see cref="_spawnPoints"/> 下标对齐；记录上次在该点被分配复活的 Unscaled 时间。</summary>
        private float[] _lastSpawnUsedUnscaled;

        /// <summary>是否存在至少一个有效复活点。</summary>
        public bool HasAnyValidPoint
        {
            get
            {
                if (_spawnPoints == null)
                    return false;
                for (int i = 0; i < _spawnPoints.Length; i++)
                {
                    if (_spawnPoints[i] != null)
                        return true;
                }

                return false;
            }
        }

        private void Awake()
        {
            EnsureCooldownBuffer();
        }

#if UNITY_EDITOR
        private void OnValidate()
        {
            _respawnCooldownSeconds = Mathf.Max(0f, _respawnCooldownSeconds);
            _minClearRadiusFromParticipant = Mathf.Max(0f, _minClearRadiusFromParticipant);
        }
#endif

        /// <summary>
        /// 为一次复活选取一个点：冷却 + 与存活参赛者距离；成功时登记该点已用时间。
        /// <paramref name="excludeOccupancyFromThisRoot"/>：复活中的单位根物体，不参与「近身」判定（避免误伤自己）。
        /// </summary>
        public bool TryPickSpawnPointForRespawn(GameObject excludeOccupancyFromThisRoot, out Transform spawnPoint)
        {
            spawnPoint = null;
            EnsureCooldownBuffer();

            if (_spawnPoints == null || _spawnPoints.Length == 0)
                return false;

            var indices = new List<int>(8);
            for (int i = 0; i < _spawnPoints.Length; i++)
            {
                if (_spawnPoints[i] != null)
                    indices.Add(i);
            }

            if (indices.Count == 0)
                return false;

            // 1) 冷却 + 近身空闲
            var pool = FilterIndices(indices, excludeOccupancyFromThisRoot, requireCooldown: true, requireProximityClear: true);
            if (TryPickRandomFromPool(pool, out spawnPoint))
            {
                RegisterSpawnPointUsed(spawnPoint);
                return true;
            }

            // 2) 仅冷却，忽略近身（仍避免刚用过）
            pool = FilterIndices(indices, excludeOccupancyFromThisRoot, requireCooldown: true, requireProximityClear: false);
            if (TryPickRandomFromPool(pool, out spawnPoint))
            {
                RegisterSpawnPointUsed(spawnPoint);
                return true;
            }

            // 3) 最后手段：任意有效点（仍登记冷却，避免连续随机到同一点）
            if (TryPickRandomFromPool(indices, out spawnPoint))
            {
                Debug.LogWarning(
                    "MatchSpawnPoints: 复活点均在冷却或近身被占满，已放宽规则随机选点；请增加复活点或调大半径/缩短冷却。",
                    this);
                RegisterSpawnPointUsed(spawnPoint);
                return true;
            }

            return false;
        }

        private void EnsureCooldownBuffer()
        {
            int n = _spawnPoints != null ? _spawnPoints.Length : 0;
            if (_lastSpawnUsedUnscaled != null && _lastSpawnUsedUnscaled.Length == n)
                return;

            var next = new float[n];
            for (int i = 0; i < n; i++)
                next[i] = float.NegativeInfinity;

            if (_lastSpawnUsedUnscaled != null)
            {
                int copy = Mathf.Min(_lastSpawnUsedUnscaled.Length, n);
                for (int i = 0; i < copy; i++)
                    next[i] = _lastSpawnUsedUnscaled[i];
            }

            _lastSpawnUsedUnscaled = next;
        }

        private List<int> FilterIndices(
            List<int> source,
            GameObject excludeOccupancyFromThisRoot,
            bool requireCooldown,
            bool requireProximityClear)
        {
            var outList = new List<int>(source.Count);
            float now = Time.unscaledTime;
            for (int i = 0; i < source.Count; i++)
            {
                int idx = source[i];
                if (requireCooldown && !IsCooldownElapsed(idx, now))
                    continue;
                if (requireProximityClear && !IsSpawnClearFromLivingParticipants(idx, excludeOccupancyFromThisRoot))
                    continue;
                outList.Add(idx);
            }

            return outList;
        }

        private bool IsCooldownElapsed(int index, float nowUnscaled)
        {
            if (_lastSpawnUsedUnscaled == null || index < 0 || index >= _lastSpawnUsedUnscaled.Length)
                return true;
            return nowUnscaled - _lastSpawnUsedUnscaled[index] >= _respawnCooldownSeconds;
        }

        private bool IsSpawnClearFromLivingParticipants(int spawnIndex, GameObject excludeOccupancyRoot)
        {
            var t = _spawnPoints[spawnIndex];
            if (t == null)
                return false;

            Vector3 p = t.position;
            float r = _minClearRadiusFromParticipant;
            float rSq = r * r;

            var participants = MatchParticipant.ActiveParticipants;
            for (int i = 0; i < participants.Count; i++)
            {
                var mp = participants[i];
                if (mp == null)
                    continue;
                if (excludeOccupancyRoot != null && mp.gameObject == excludeOccupancyRoot)
                    continue;

                var h = mp.GetComponent<Health>();
                if (h != null && h.IsDead)
                    continue;

                Vector3 q = mp.transform.position;
                float dx = p.x - q.x;
                float dz = p.z - q.z;
                if (dx * dx + dz * dz <= rSq)
                    return false;
            }

            return true;
        }

        private bool TryPickRandomFromPool(List<int> pool, out Transform spawnPoint)
        {
            spawnPoint = null;
            if (pool == null || pool.Count == 0)
                return false;

            int pick = pool[Random.Range(0, pool.Count)];
            return TryGetSpawnTransform(pick, out spawnPoint);
        }

        private bool TryGetSpawnTransform(int index, out Transform spawnPoint)
        {
            spawnPoint = null;
            if (_spawnPoints == null || index < 0 || index >= _spawnPoints.Length)
                return false;
            spawnPoint = _spawnPoints[index];
            return spawnPoint != null;
        }

        private void RegisterSpawnPointUsed(Transform usedSpawnPoint)
        {
            if (usedSpawnPoint == null || _spawnPoints == null || _lastSpawnUsedUnscaled == null)
                return;
            for (int i = 0; i < _spawnPoints.Length; i++)
            {
                if (_spawnPoints[i] == usedSpawnPoint)
                {
                    _lastSpawnUsedUnscaled[i] = Time.unscaledTime;
                    return;
                }
            }
        }
    }
}
