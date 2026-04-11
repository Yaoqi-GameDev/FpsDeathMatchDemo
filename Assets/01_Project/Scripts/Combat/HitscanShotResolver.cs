using System;
using UnityEngine;

namespace FpsDemo.Combat
{
    /// <summary>
    /// Hitscan 射线解析与扣血：<see cref="FpsHitscanWeapon"/> 与 AI 侧 <c>FpsAiHitscanWeapon</c> 共用，避免重复实现。
    /// </summary>
    public static class HitscanShotResolver
    {
        /// <summary>
        /// 沿射线由近到远：墙挡弹；可伤害体与开枪者为<strong>同一 Health</strong> 则跳过（防自伤）。
        /// <paramref name="instigatorWeaponTransform"/> 为挂武器脚本的物体（通常与 <see cref="Health"/> 同层或子级），<strong>勿用</strong> <c>transform.root</c> 判断身份——场景里 Player/Bot 常在同一 <c>--Gameplay--</c> 下，<c>root</c> 会相同导致双方都无法受伤。
        /// </summary>
        public static void Resolve(
            Ray ray,
            float maxRange,
            LayerMask hitLayers,
            Transform instigatorWeaponTransform,
            float damagePerShot,
            out bool hitDamageable,
            out ShotHitInfo shotInfo)
        {
            hitDamageable = false;
            Health sourceHealth = instigatorWeaponTransform.GetComponent<Health>()
                ?? instigatorWeaponTransform.GetComponentInParent<Health>(true);
            GameObject instigator = sourceHealth != null ? sourceHealth.gameObject : instigatorWeaponTransform.gameObject;

            RaycastHit[] hits = Physics.RaycastAll(ray, maxRange, hitLayers, QueryTriggerInteraction.Ignore);
            if (hits.Length > 1)
                Array.Sort(hits, (a, b) => a.distance.CompareTo(b.distance));

            for (int i = 0; i < hits.Length; i++)
            {
                RaycastHit h = hits[i];
                var damageable = FindDamageableForHit(h.collider);

                if (damageable != null)
                {
                    if (IsSameShooterAndTarget(damageable, sourceHealth))
                        continue;

                    hitDamageable = true;
                    damageable.ApplyDamage(damagePerShot, instigator);
                    shotInfo = new ShotHitInfo(true, h.point, h.normal, true, h.collider);
                    return;
                }

                shotInfo = new ShotHitInfo(true, h.point, h.normal, false, h.collider);
                return;
            }

            shotInfo = new ShotHitInfo(false, ray.GetPoint(maxRange), -ray.direction, false, null);
        }

        /// <summary>
        /// 先沿碰撞体向上找 <see cref="IDamageable"/>；若 <see cref="CharacterController"/> 等在根而 <see cref="Health"/> 挂在子物体（如 Body），父链会失败，再在同一角色根下查找。
        /// </summary>
        private static IDamageable FindDamageableForHit(Collider collider)
        {
            var d = collider.GetComponentInParent<IDamageable>();
            if (d != null)
                return d;

            return collider.transform.root.GetComponentInChildren<IDamageable>(true);
        }

        /// <summary>是否打中自己：同一 <see cref="Health"/> 实例才算自伤（与场景父节点 <c>transform.root</c> 无关）。</summary>
        private static bool IsSameShooterAndTarget(IDamageable damageable, Health sourceHealth)
        {
            if (sourceHealth == null)
                return false;

            if (damageable is Health h)
                return h == sourceHealth;

            var c = damageable as Component;
            return c != null && c.GetComponentInParent<Health>(true) == sourceHealth;
        }
    }
}
