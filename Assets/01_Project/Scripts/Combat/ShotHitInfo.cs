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

        public ShotHitInfo(bool hasWorldHit, Vector3 point, Vector3 normal, bool hitDamageable, Collider hitCollider)
        {
            HasWorldHit = hasWorldHit;
            Point = point;
            Normal = normal;
            HitDamageable = hitDamageable;
            HitCollider = hitCollider;
        }
    }
}
