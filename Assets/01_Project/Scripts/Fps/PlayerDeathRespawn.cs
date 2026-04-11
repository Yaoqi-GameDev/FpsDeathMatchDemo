using System.Collections;
using FpsDemo.Combat;
using FpsDemo.Match;
using UnityEngine;
using UnityEngine.UI;

namespace FpsDemo.Fps
{
    /// <summary>
    /// 玩家：死亡时灰幕、<see cref="FpsInput.GameplayInputEnabled"/> 关闭（仍可转视角）、关闭电机与武器；延迟后复活。
    /// 复活位置<strong>推荐</strong>用场景里预放的 <see cref="_spawnPoints"/>（随机其一），与常见 FPS 一致；未配置时再走可选的矩形内随机 + 向下射线。
    /// </summary>
    [RequireComponent(typeof(Health))]
    [RequireComponent(typeof(CharacterController))]
    public sealed class PlayerDeathRespawn : MonoBehaviour
    {
        [Header("引用（可空则同物体 GetComponent）")]
        [SerializeField] private FpsInput _input;
        [SerializeField] private FpsPlayerMotor _motor;
        [SerializeField] private FpsHitscanWeapon _weapon;
        [SerializeField] private FpsPlayerLook _look;

        [Header("死亡表现")]
        [SerializeField] private float _respawnDelaySeconds = 3f;
        [Tooltip("Screen Space Overlay 半透明罩层；死亡时偏灰。")]
        [SerializeField] private Color _deathTint = new Color(0.22f, 0.22f, 0.25f, 0.62f);

        [Header("复活位置（推荐）")]
        [Tooltip("在场景里放若干空物体，贴地放在安全复活处；死亡时随机选一个。留空则使用下方「后备」。")]
        [SerializeField] private Transform[] _spawnPoints;
        [Tooltip("使用复活点时，是否把身体朝向对齐到该点的旋转（仅 Y），并把俯仰归零。")]
        [SerializeField] private bool _snapViewToSpawnYaw = true;

        [Header("无复活点时的后备（可选）")]
        [Tooltip("在水平矩形内随机 XZ，从上方向下射线找地；试几次后仍失败则用矩形中心高度附近。")]
        [SerializeField] private Transform _fallbackAreaCenter;
        [SerializeField] private Vector2 _fallbackHalfExtentsXZ = new Vector2(80f, 80f);
        [SerializeField] private float _raycastDownFrom = 120f;
        [SerializeField] private float _groundSnapYOffset = 0.08f;
        [SerializeField] private int _fallbackMaxTries = 12;
        [SerializeField] private LayerMask _groundMask;

        private Health _health;
        private CharacterController _controller;
        private GameObject _deathOverlayRoot;
        private Coroutine _respawnRoutine;

        private void Awake()
        {
            _health = GetComponent<Health>();
            _controller = GetComponent<CharacterController>();
            if (_input == null)
                _input = GetComponent<FpsInput>();
            if (_motor == null)
                _motor = GetComponent<FpsPlayerMotor>();
            if (_weapon == null)
                _weapon = GetComponent<FpsHitscanWeapon>();
            if (_look == null)
                _look = GetComponent<FpsPlayerLook>();

            _health.SetDestroyOnDeath(false);

            if (_groundMask.value == 0)
                _groundMask = Physics.DefaultRaycastLayers;

            BuildDeathOverlay();
        }

        private void OnDestroy()
        {
            if (_deathOverlayRoot != null)
                Destroy(_deathOverlayRoot);
        }

        private void OnEnable()
        {
            _health.Died += OnDied;
        }

        private void OnDisable()
        {
            _health.Died -= OnDied;
        }

        private void OnDied(KillReport _)
        {
            if (_respawnRoutine != null)
                return;

            if (MatchManager.Instance != null && MatchManager.Instance.IsMatchOver)
                return;

            _respawnRoutine = StartCoroutine(DeathAndRespawnRoutine());
        }

        private IEnumerator DeathAndRespawnRoutine()
        {
            if (_input != null)
                _input.GameplayInputEnabled = false;
            if (_motor != null)
                _motor.enabled = false;
            if (_weapon != null)
                _weapon.enabled = false;

            if (_deathOverlayRoot != null)
                _deathOverlayRoot.SetActive(true);

            yield return new WaitForSecondsRealtime(Mathf.Max(0f, _respawnDelaySeconds));

            if (MatchManager.Instance != null && MatchManager.Instance.IsMatchOver)
            {
                _respawnRoutine = null;
                yield break;
            }

            PickSpawnPosition(out Transform spawnTf, out Vector3 fallbackPos);

            _controller.enabled = false;
            transform.position = spawnTf != null ? spawnTf.position : fallbackPos;

            if (spawnTf != null && _snapViewToSpawnYaw)
            {
                if (_look != null)
                    _look.SnapToWorldYaw(spawnTf.eulerAngles.y);
                else
                {
                    Vector3 e = transform.eulerAngles;
                    e.y = spawnTf.eulerAngles.y;
                    transform.eulerAngles = e;
                }
            }

            _controller.enabled = true;

            _health.ReviveFull();

            if (_weapon != null)
                _weapon.ResetAmmoToConfigDefaults();

            if (_motor != null)
            {
                _motor.ResetStateForRespawn();
                _motor.enabled = true;
            }

            if (_weapon != null)
                _weapon.enabled = true;

            if (_input != null)
                _input.GameplayInputEnabled = true;

            if (_deathOverlayRoot != null)
                _deathOverlayRoot.SetActive(false);

            _respawnRoutine = null;
        }

        /// <summary>
        /// 优先从 <see cref="_spawnPoints"/> 随机选一个；若未配置则 <paramref name="spawnTf"/> 为 null，<paramref name="fallbackPos"/> 为矩形内射线结果。
        /// </summary>
        private void PickSpawnPosition(out Transform spawnTf, out Vector3 fallbackPos)
        {
            spawnTf = null;
            fallbackPos = transform.position;

            if (_spawnPoints == null || _spawnPoints.Length == 0)
            {
                fallbackPos = ResolveFallbackPosition();
                return;
            }

            int valid = 0;
            for (int i = 0; i < _spawnPoints.Length; i++)
            {
                if (_spawnPoints[i] != null)
                    valid++;
            }

            if (valid == 0)
            {
                fallbackPos = ResolveFallbackPosition();
                return;
            }

            int pick = Random.Range(0, valid);
            for (int i = 0; i < _spawnPoints.Length; i++)
            {
                if (_spawnPoints[i] == null)
                    continue;
                if (pick == 0)
                {
                    spawnTf = _spawnPoints[i];
                    return;
                }

                pick--;
            }

            fallbackPos = ResolveFallbackPosition();
        }

        private Vector3 ResolveFallbackPosition()
        {
            Vector3 center = _fallbackAreaCenter != null ? _fallbackAreaCenter.position : transform.position;

            for (int i = 0; i < _fallbackMaxTries; i++)
            {
                float x = Random.Range(center.x - _fallbackHalfExtentsXZ.x, center.x + _fallbackHalfExtentsXZ.x);
                float z = Random.Range(center.z - _fallbackHalfExtentsXZ.y, center.z + _fallbackHalfExtentsXZ.y);
                Vector3 origin = new Vector3(x, center.y + _raycastDownFrom, z);
                if (Physics.Raycast(origin, Vector3.down, out RaycastHit hit, _raycastDownFrom + 400f, _groundMask, QueryTriggerInteraction.Ignore))
                    return hit.point + Vector3.up * _groundSnapYOffset;
            }

            Debug.LogWarning("PlayerDeathRespawn: 后备射线未命中地面，使用矩形中心上方一点；请在场景里添加 Spawn Points 或检查 Layer / 碰撞体。", this);
            return new Vector3(center.x, center.y + 2f, center.z);
        }

        private void BuildDeathOverlay()
        {
            _deathOverlayRoot = new GameObject("PlayerDeathTint");
            var c = _deathOverlayRoot.AddComponent<Canvas>();
            c.renderMode = RenderMode.ScreenSpaceOverlay;
            c.sortingOrder = 320;
            _deathOverlayRoot.AddComponent<GraphicRaycaster>();
            var scaler = _deathOverlayRoot.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);

            var imgGo = new GameObject("Gray");
            imgGo.transform.SetParent(_deathOverlayRoot.transform, false);
            var rt = imgGo.AddComponent<RectTransform>();
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;

            var img = imgGo.AddComponent<Image>();
            img.color = _deathTint;
            img.raycastTarget = false;

            _deathOverlayRoot.SetActive(false);
        }
    }
}
