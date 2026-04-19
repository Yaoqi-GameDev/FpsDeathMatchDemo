using FpsDemo.Combat;
using FpsDemo.Match;
using TMPro;
using UnityEngine;

namespace FpsDemo.UI
{
    /// <summary>
    /// 仅负责把 <see cref="FpsHitscanWeapon"/> 的弹药数据写到 UI Text（不修改武器数据）。
    /// <see cref="_weapon"/> 可空：未拖引用时会在运行中从 <see cref="MatchParticipant.ActiveParticipants"/> 里找 <see cref="MatchParticipant.IsLocalPlayer"/> 并在其根物体上取 <see cref="FpsHitscanWeapon"/>（与 <see cref="DeathmatchHudView"/> 解析血量一致，适配联机生成后才有本地玩家）。
    /// 准星等静态 UI 可挂在同 Canvas 下，不必挂在本脚本上。
    /// </summary>
    public sealed class AmmoHub : MonoBehaviour
    {
        [Tooltip("可空：空则运行时解析本地参战者的 FpsHitscanWeapon")]
        [SerializeField] private FpsHitscanWeapon _weapon;
        [SerializeField] private TMP_Text _ammoText;

        [Tooltip("例如：{0}=弹匣内，{1}=备弹")]
        [SerializeField] private string _format = "{0} / {1}";

        private void LateUpdate()
        {
            TryResolveWeapon();
            if (_weapon == null || _ammoText == null)
                return;

            _ammoText.text = string.Format(_format, _weapon.AmmoInMagazine, _weapon.ReserveAmmo);
        }

        private void TryResolveWeapon()
        {
            if (_weapon != null)
                return;

            foreach (var p in MatchParticipant.ActiveParticipants)
            {
                if (p != null && p.IsLocalPlayer)
                {
                    _weapon = p.GetComponent<FpsHitscanWeapon>();
                    break;
                }
            }
        }
    }
}
