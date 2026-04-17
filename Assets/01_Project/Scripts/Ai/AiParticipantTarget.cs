using FpsDemo.Combat;
using FpsDemo.Match;
using UnityEngine;

namespace FpsDemo.Ai
{
    /// <summary>
    /// 人机共用：在 <see cref="MatchParticipant.ActiveParticipants"/> 中选距离最近、且存活（有 <see cref="Health"/> 则未死）的<strong>他人</strong>根 <see cref="Transform"/>。
    /// </summary>
    public static class AiParticipantTarget
    {
        public static Transform FindNearestAliveOther(Transform self, MatchParticipant selfParticipant)
        {
            if (self == null)
                return null;

            GameObject selfRoot = selfParticipant != null ? selfParticipant.Root : self.gameObject;
            Vector3 selfPos = self.position;
            float bestSq = float.PositiveInfinity;
            Transform best = null;

            var list = MatchParticipant.ActiveParticipants;
            for (int i = 0; i < list.Count; i++)
            {
                var mp = list[i];
                if (mp == null)
                    continue;
                if (mp.gameObject == selfRoot)
                    continue;

                var h = mp.GetComponent<Health>();
                if (h != null && h.IsDead)
                    continue;

                Vector3 p = mp.transform.position;
                float dx = p.x - selfPos.x;
                float dy = p.y - selfPos.y;
                float dz = p.z - selfPos.z;
                float sq = dx * dx + dy * dy + dz * dz;
                if (sq < bestSq)
                {
                    bestSq = sq;
                    best = mp.transform;
                }
            }

            return best;
        }
    }
}
