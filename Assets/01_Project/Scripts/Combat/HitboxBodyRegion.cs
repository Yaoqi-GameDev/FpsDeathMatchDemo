using UnityEngine;

namespace FpsDemo.Combat
{
    /// <summary>
    /// 挂在带 <see cref="Collider"/> 的 Hitbox 上，标记射线命中时的 <see cref="DamageBodyRegion"/>。
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class HitboxBodyRegion : MonoBehaviour
    {
        [SerializeField] private DamageBodyRegion _region = DamageBodyRegion.Torso;

        public DamageBodyRegion Region => _region;
    }
}
