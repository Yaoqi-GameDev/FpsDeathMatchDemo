using UnityEngine;

namespace FpsDemo.Combat
{
    /// <summary>
    /// 单发 Hitscan 解析结果：供 UI、弹孔、粒子等订阅 <see cref="FpsHitscanWeapon.ShotResolved"/> 使用。
    /// </summary>
    public readonly struct ShotHitInfo
    {
        public readonly bool HasWorldHit;
        public readonly Vector3 Point;
        public readonly Vector3 Normal;
        public readonly bool HitDamageable;
        public readonly Collider HitCollider;
        /// <summary>命中可伤害体时有效；否则为 <see cref="DamageBodyRegion.Unknown"/>。</summary>
        public readonly DamageBodyRegion BodyRegion;
        /// <summary>本发实际使用的部位倍率（未命中可伤害体或未挂 Hitbox 时为 1）。</summary>
        public readonly float DamageMultiplierApplied;
        /// <summary>仅当 <see cref="HitDamageable"/> 为 true：武器配置的基础伤害（未乘部位）。</summary>
        public readonly float BaseDamagePerShot;
        /// <summary>仅当 <see cref="HitDamageable"/> 为 true：实际结算伤害（基础 × 倍率）。</summary>
        public readonly float FinalDamageApplied;

        public ShotHitInfo(
            bool hasWorldHit,
            Vector3 point,
            Vector3 normal,
            bool hitDamageable,
            Collider hitCollider,
            DamageBodyRegion bodyRegion = DamageBodyRegion.Unknown,
            float damageMultiplierApplied = 1f,
            float baseDamagePerShot = 0f,
            float finalDamageApplied = 0f)
        {
            HasWorldHit = hasWorldHit;
            Point = point;
            Normal = normal;
            HitDamageable = hitDamageable;
            HitCollider = hitCollider;
            BodyRegion = bodyRegion;
            DamageMultiplierApplied = damageMultiplierApplied;
            BaseDamagePerShot = baseDamagePerShot;
            FinalDamageApplied = finalDamageApplied;
        }
    }
}
