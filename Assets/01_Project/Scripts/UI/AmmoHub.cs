using FpsDemo.Combat;
using TMPro;
using UnityEngine;

namespace FpsDemo.UI
{
    /// <summary>
    /// 仅负责把 <see cref="FpsHitscanWeapon"/> 的弹药数据写到 UI Text（不修改武器数据）。
    /// 准星等静态 UI 可挂在同 Canvas 下，不必挂在本脚本上。
    /// </summary>
    public sealed class AmmoHub : MonoBehaviour
    {
        [SerializeField] private FpsHitscanWeapon _weapon;
        [SerializeField] private TMP_Text _ammoText;

        [Tooltip("例如：{0}=弹匣内，{1}=备弹")]
        [SerializeField] private string _format = "{0} / {1}";

        private void LateUpdate()
        {
            if (_weapon == null || _ammoText == null)
                return;

            _ammoText.text = string.Format(_format, _weapon.AmmoInMagazine, _weapon.ReserveAmmo);
        }
    }
}
