using FpsDemo.Combat;
using FpsDemo.Fps;
using FpsDemo.Match;
using Unity.Netcode;
using UnityEngine;

namespace FpsDemo.Netcode
{
    /// <summary>
    /// 挂在 Player 根（与 <see cref="NetworkObject"/> 同物体）。网络生成后仅 <see cref="NetworkBehaviour.IsOwner"/> 启用第一人称相机、听筒、输入与本地玩法组件；
    /// 非 Owner 关闭，避免多客户端抢同一套键鼠。可选在 Inspector 拖入更多仅 Owner 需要的 <see cref="Behaviour"/>。
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(NetworkObject))]
    public sealed class FpsNetworkPlayerOwnerGate : NetworkBehaviour
    {
        [Header("可选：额外仅 Owner 启用的 Behaviour")]
        [SerializeField] private Behaviour[] _ownerOnlyBehaviours;

        public override void OnNetworkSpawn()
        {
            bool owner = IsOwner;

            if (TryGetComponent<MatchParticipant>(out var mp))
                mp.SetLocalPlayerForNetworking(owner);

            foreach (var cam in GetComponentsInChildren<Camera>(true))
                cam.enabled = owner;

            foreach (var listener in GetComponentsInChildren<AudioListener>(true))
                listener.enabled = owner;

            if (TryGetComponent<FpsInput>(out var input))
                input.enabled = owner;

            if (TryGetComponent<ParrelSyncClientStrafeBot>(out var strafeBot))
                strafeBot.enabled = owner;

            if (TryGetComponent<FpsPlayerLook>(out var look))
                look.enabled = owner;

            if (TryGetComponent<FpsPlayerMotor>(out var motor))
                motor.enabled = IsServer || owner;

            if (TryGetComponent<CharacterController>(out var characterController))
            {
                bool clientPredict = TryGetComponent<PlayerLocomotionNetBridge>(out var plb) && plb.ClientPredictionEnabled;
                characterController.enabled = IsServer || (owner && clientPredict);
            }

            if (TryGetComponent<FpsHitscanWeapon>(out var weapon))
                weapon.enabled = owner;

            foreach (var recoil in GetComponentsInChildren<FpsRecoilController>(true))
                recoil.enabled = owner;

            if (_ownerOnlyBehaviours != null)
            {
                foreach (var b in _ownerOnlyBehaviours)
                {
                    if (b != null)
                        b.enabled = owner;
                }
            }
        }
    }
}
