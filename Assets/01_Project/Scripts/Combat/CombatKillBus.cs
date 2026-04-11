using System;

namespace FpsDemo.Combat
{
    /// <summary>
    /// 全局击杀事件总线：<see cref="Health"/> 在致死时发布，局内规则 / UI / 播报订阅此处，无需直接引用每个 <see cref="Health"/>。
    /// </summary>
    public static class CombatKillBus
    {
        public static event Action<KillReport> KillCommitted;

        internal static void Publish(in KillReport report)
        {
            KillCommitted?.Invoke(report);
        }
    }
}
