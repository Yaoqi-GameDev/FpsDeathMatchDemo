namespace FpsDemo.Combat
{
    /// <summary>
    /// Hitscan 部位倍率分类：与 <see cref="HitboxBodyRegion"/>、<see cref="BodyDamageMultiplierTable"/> 对应。
    /// </summary>
    public enum DamageBodyRegion
    {
        /// <summary>未挂部位组件或旧版 Collider：倍率按 1 处理。</summary>
        Unknown = 0,
        Head = 1,
        Torso = 2,
        /// <summary>四肢（上臂、小臂、大腿、小腿等）。</summary>
        Limb = 3,
    }
}
