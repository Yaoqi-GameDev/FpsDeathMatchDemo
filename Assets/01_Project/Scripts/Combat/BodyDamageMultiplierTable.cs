using UnityEngine;

namespace FpsDemo.Combat
{
    /// <summary>
    /// 部位伤害倍率表：武器仅提供基础伤害，最终伤害 = 基础 × 倍率。
    /// 拖到玩家 <see cref="FpsHitscanWeapon"/> 或人机 FpsAiHitscanWeapon；未拖则使用 <see cref="ResolveMultiplier"/> 内建默认（头 2 / 上身 1 / 四肢 0.7）。
    /// </summary>
    [CreateAssetMenu(fileName = "BodyDamageMultiplierTable", menuName = "FpsDemo/Combat/Body Damage Multiplier Table")]
    public sealed class BodyDamageMultiplierTable : ScriptableObject
    {
        [SerializeField] private float _head = 2f;
        [SerializeField] private float _torso = 1f;
        [SerializeField] private float _limb = 0.7f;

        public float Head => _head;
        public float Torso => _torso;
        public float Limb => _limb;

        public float GetMultiplier(DamageBodyRegion region)
        {
            return region switch
            {
                DamageBodyRegion.Head => _head,
                DamageBodyRegion.Torso => _torso,
                DamageBodyRegion.Limb => _limb,
                _ => 1f,
            };
        }

        /// <summary>未配置表时使用与 Inspector 默认一致的内建倍率。</summary>
        public static float ResolveMultiplier(DamageBodyRegion region, BodyDamageMultiplierTable table)
        {
            if (table != null)
                return table.GetMultiplier(region);

            return region switch
            {
                DamageBodyRegion.Head => 2f,
                DamageBodyRegion.Torso => 1f,
                DamageBodyRegion.Limb => 0.7f,
                _ => 1f,
            };
        }
    }
}
