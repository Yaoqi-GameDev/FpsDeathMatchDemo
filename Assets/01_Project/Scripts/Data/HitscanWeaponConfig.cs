using UnityEngine;

namespace FpsDemo.Data
{
    /// <summary>
    /// <b>Hitscan（即时射线）武器</b>的数值配置：伤害、射速、弹药、射程等。
    /// 一种「武器类型」对应一类配置形状；具体枪种用<strong>不同资产文件</strong>区分（命名建议：<c>Rifle_xxx</c>、<c>SMG_xxx</c>）。
    /// 新建：<b>Create → FpsDemo → Data → Hitscan Weapon Config</b>。运行时只读，勿在玩法中改写磁盘 asset。
    /// </summary>
    [CreateAssetMenu(fileName = "HitscanWeaponConfig", menuName = "FpsDemo/Data/Hitscan Weapon Config", order = 0)]
    public sealed class HitscanWeaponConfig : ScriptableObject
    {
        [Header("伤害 / 射速")]
        [SerializeField] private float _damagePerShot = 10f;
        [SerializeField] private float _fireRatePerSecond = 8f;

        [Header("弹药")]
        [SerializeField] private int _magazineSize = 30;
        [Tooltip("开局备弹（运行时 Reserve 从此初始化）。")]
        [SerializeField] private int _startingReserveAmmo = 90;
        [SerializeField] private float _reloadDurationSeconds = 1.6f;

        [Header("射线")]
        [SerializeField] private float _maxRange = 100f;

        [Header("后座（由 FpsRecoilController 读取）")]
        [Tooltip("每发增加的俯仰后座（度），沿本地 X 表现为上抬。")]
        [SerializeField] private float _recoilVerticalPerShot = 0.35f;
        [Tooltip("每发随机偏航后座，均匀分布于 [-half, +half]（度）。")]
        [SerializeField] private float _recoilYawRandomHalf = 0.12f;
        [Tooltip("后座角向 0 恢复的速度（度/秒）。")]
        [SerializeField] private float _recoilRecoveryDegreesPerSecond = 9f;

        public float DamagePerShot => _damagePerShot;
        public float FireRatePerSecond => _fireRatePerSecond;
        public int MagazineSize => _magazineSize;
        public int StartingReserveAmmo => _startingReserveAmmo;
        public float ReloadDurationSeconds => _reloadDurationSeconds;
        public float MaxRange => _maxRange;

        public float RecoilVerticalPerShot => _recoilVerticalPerShot;
        public float RecoilYawRandomHalf => _recoilYawRandomHalf;
        public float RecoilRecoveryDegreesPerSecond => _recoilRecoveryDegreesPerSecond;
    }
}
