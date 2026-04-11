using UnityEngine;

namespace FpsDemo.Combat
{
    /// <summary>
    /// 一次致死（或逻辑死亡）的归因快照：<see cref="Victim"/> 为挂 <see cref="Health"/> 的物体；
    /// <see cref="Killer"/> 为最后一击的伤害来源（通常为开枪者根物体），环境伤/未知为 <c>null</c>。
    /// </summary>
    public readonly struct KillReport
    {
        public readonly GameObject Victim;
        public readonly GameObject Killer;

        public KillReport(GameObject victim, GameObject killer)
        {
            Victim = victim;
            Killer = killer;
        }
    }
}
