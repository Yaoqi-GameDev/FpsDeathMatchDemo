using UnityEngine;

namespace FpsDemo.Combat
{
    /// <summary>
    /// 可被武器等伤害源调用的统一入口；具体扣血、死亡由实现类（如 <see cref="Health"/>）处理。
    /// 不关心击杀来源时实现 <see cref="ApplyDamage(float)"/>，并在带来源的重载中转调即可。
    /// </summary>
    public interface IDamageable
    {
        void ApplyDamage(float amount);

        /// <param name="instigator">伤害来源根物体（如开枪的 <c>Player</c>）；未知可为 <c>null</c>。</param>
        void ApplyDamage(float amount, GameObject instigator);
    }
}
