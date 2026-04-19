using System.Collections;
using FpsDemo.Combat;
using FpsDemo.Match;
using FpsDemo.Netcode;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.UI;

namespace FpsDemo.Fps
{
    /// <summary>
    /// 玩家：死亡时灰幕、<see cref="FpsInput.GameplayInputEnabled"/> 关闭（仍可转视角）、关闭电机与武器；延迟后复活。
    /// 仅 <see cref="MatchParticipant.IsLocalPlayer"/> 或（无 MatchParticipant 时标签为 Player）会走本流程，避免误挂本脚本的单位死亡时全屏遮罩。
    /// 复活位置使用场景中的 <see cref="MatchSpawnPoints"/>；未在 Inspector 拖引用时会在运行时 <c>FindObjectOfType</c>。
    /// 联机且存在 <see cref="PlayerRespawnNetBridge"/> 并已生成：延迟结束后由服务器选点、传送、满血与弹药同步，本机再通过 <see cref="FinishRespawnPresentationAfterServerAuthority"/> 恢复输入与表现。
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

        [Header("复活位置")]
        [Tooltip("全局统一复活点（场景里一个 MatchSpawnPoints）。")]
        [SerializeField] private MatchSpawnPoints _matchSpawnPoints;
        [Tooltip("当 MatchSpawnPoints 未拖或列表为空时使用该点位置；可空则复活时留在原地并打警告。")]
        [SerializeField] private Transform _fallbackRespawnPoint;
        [Tooltip("使用复活点时，是否把身体朝向对齐到该点旋转（仅 Y），并把俯仰归零。")]
        [SerializeField] private bool _snapViewToSpawnYaw = true;

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

            BuildDeathOverlay();
        }

        private void Start()
        {
            TryResolveMatchSpawnPoints();
        }

        private void TryResolveMatchSpawnPoints()
        {
            if (_matchSpawnPoints != null)
                return;
            _matchSpawnPoints = Object.FindObjectOfType<MatchSpawnPoints>();
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

            if (!IsLocalPlayerDeathFlow())
                return;

            if (MatchManager.Instance != null && MatchManager.Instance.IsMatchOver)
                return;

            _respawnRoutine = StartCoroutine(DeathAndRespawnRoutine());
        }

        /// <summary>
        /// 全屏灰幕挂在独立 Canvas 上，会遮住整局画面。若假人/机器人误挂了本脚本，其死亡不应触发本地玩家的死亡 UI。
        /// </summary>
        private bool IsLocalPlayerDeathFlow()
        {
            if (TryGetComponent<MatchParticipant>(out var mp))
                return mp.IsLocalPlayer;
            return CompareTag("Player");
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

            if (ShouldUseServerAuthorityRespawn())
            {
                _respawnRoutine = null;
                GetComponent<PlayerRespawnNetBridge>().RequestRespawnFromOwner();
                yield break;
            }

            TryResolveMatchSpawnPoints();
            PickSpawnPosition(out Vector3 worldPos, out float? yawDegrees);

            _controller.enabled = false;
            transform.position = worldPos;

            if (_snapViewToSpawnYaw && yawDegrees.HasValue)
            {
                if (_look != null)
                    _look.SnapToWorldYaw(yawDegrees.Value);
                else
                {
                    Vector3 e = transform.eulerAngles;
                    e.y = yawDegrees.Value;
                    transform.eulerAngles = e;
                }
            }

            _controller.enabled = true;

            _health.ReviveFull();

            if (TryGetComponent<NetworkHealthBridge>(out var netHealth))
                netHealth.NotifyLocalReviveAfterDeath();

            if (_weapon != null)
                _weapon.ResetAmmoToConfigDefaults();

            FinishRespawnPresentationAfterServerAuthority();
        }

        private bool ShouldUseServerAuthorityRespawn()
        {
            var no = GetComponent<NetworkObject>();
            var bridge = GetComponent<PlayerRespawnNetBridge>();
            var nm = NetworkManager.Singleton;
            return no != null && no.IsSpawned && bridge != null && nm != null && nm.IsListening;
        }

        /// <summary>联机：服务器完成传送/回血/弹药后由 <see cref="PlayerRespawnNetBridge"/> 的 ClientRpc 调用，仅恢复本地表现与输入。</summary>
        public void FinishRespawnPresentationAfterServerAuthority()
        {
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

        private void PickSpawnPosition(out Vector3 worldPos, out float? yawDegrees)
        {
            worldPos = transform.position;
            yawDegrees = null;

            if (_matchSpawnPoints != null && _matchSpawnPoints.HasAnyValidPoint)
            {
                if (_matchSpawnPoints.TryPickSpawnPointForRespawn(gameObject, out Transform spawnTf) &&
                    spawnTf != null)
                {
                    worldPos = spawnTf.position;
                    yawDegrees = spawnTf.eulerAngles.y;
                    return;
                }
            }

            if (_fallbackRespawnPoint != null)
            {
                worldPos = _fallbackRespawnPoint.position;
                yawDegrees = _fallbackRespawnPoint.eulerAngles.y;
                return;
            }

            Debug.LogWarning(
                "PlayerDeathRespawn: 未配置有效的 MatchSpawnPoints，且 Fallback Respawn Point 为空；复活位置未改变。请在场景中添加 MatchSpawnPoints 并拖入引用。",
                this);
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
