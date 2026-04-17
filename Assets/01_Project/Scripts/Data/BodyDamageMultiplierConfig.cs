using FpsDemo.Combat;
using UnityEngine;

namespace FpsDemo.Data
{
    /// <summary>
    /// 部位伤害倍率配置：武器仅提供基础伤害，最终伤害 = 基础 × 倍率。
    /// 拖到 <see cref="FpsDemo.Combat.FpsHitscanWeapon"/> 或 <see cref="FpsDemo.Ai.FpsAiHitscanWeapon"/>；未拖则使用 <see cref="ResolveMultiplier"/> 内建默认（头 2 / 上身 1 / 四肢 0.7）。
    /// 新建：<b>Create → FpsDemo → Data → Body Damage Multiplier Config</b>。
    /// </summary>
    [CreateAssetMenu(fileName = "BodyDamageMultiplierConfig", menuName = "FpsDemo/Data/Body Damage Multiplier Config", order = 1)]
    public sealed class BodyDamageMultiplierConfig : ScriptableObject
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

        /// <summary>未配置资源时使用与 Inspector 默认一致的内建倍率。</summary>
        public static float ResolveMultiplier(DamageBodyRegion region, BodyDamageMultiplierConfig config)
        {
            if (config != null)
                return config.GetMultiplier(region);

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
