using FpsDemo.Fps;
using Unity.Netcode;
using UnityEngine;

namespace FpsDemo.Netcode
{
    /// <summary>
    /// 联机玩家显隐：Owner 只见第一人称管线（<see cref="_firstPersonVisualRoot"/>），隐藏第三人称身体与 TP 骨骼上的武器；
    /// 非 Owner 隐藏 FP 子树，显示 <see cref="FpsThirdPersonLocomotionAnimator"/> 下全身与 <see cref="_thirdPersonWeaponRoot"/> 武器网格。
    /// 勿将 <see cref="_thirdPersonRoot"/> 整物体 SetActive(false)，以免停掉全身 <see cref="Animator"/>（日后 <see cref="Unity.Netcode.Components.NetworkAnimator"/>）。
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(NetworkObject))]
    public sealed class FpsNetworkPlayerViewRig : NetworkBehaviour
    {
        [Header("第一人称（仅 Owner 需要激活）")]
        [Tooltip("通常为 CameraPivot/RecoilPivot/FirstPersonView；非 Owner 会 SetActive(false) 整段。")]
        [SerializeField] private Transform _firstPersonVisualRoot;

        [Header("第三人称身体（与 FpsThirdPersonLocomotionAnimator 一致）")]
        [SerializeField] private FpsThirdPersonLocomotionAnimator _thirdPersonLocomotion;

        [Header("第三人称武器（X Bot 手骨下 WeaponRoot）")]
        [Tooltip("Owner 隐藏 Renderer，避免与 FP ViewModel 重叠；非 Owner 显示。")]
        [SerializeField] private Transform _thirdPersonWeaponRoot;

        private void Awake()
        {
            if (_thirdPersonLocomotion == null)
                _thirdPersonLocomotion = GetComponent<FpsThirdPersonLocomotionAnimator>();
        }

        public override void OnNetworkSpawn()
        {
            bool owner = IsOwner;

            if (_firstPersonVisualRoot != null)
                _firstPersonVisualRoot.gameObject.SetActive(owner);

            if (_thirdPersonLocomotion != null)
                _thirdPersonLocomotion.SetVisibleForRemoteCopy(!owner);

            ApplyThirdPersonWeaponRenderers(owner);
        }

        private void ApplyThirdPersonWeaponRenderers(bool owner)
        {
            if (_thirdPersonWeaponRoot == null)
                return;

            foreach (var r in _thirdPersonWeaponRoot.GetComponentsInChildren<Renderer>(true))
            {
                if (r != null)
                    r.enabled = !owner;
            }
        }
    }
}
