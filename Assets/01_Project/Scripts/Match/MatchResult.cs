using System.Collections.Generic;

namespace FpsDemo.Match
{
    /// <summary>一局结束时的快照，供占位结算 UI 或后续 HUD 使用。</summary>
    public readonly struct MatchResult
    {
        public readonly MatchEndReason Reason;
        /// <summary>并列优胜者（时间到时可能多人同击杀）。</summary>
        public readonly IReadOnlyList<MatchParticipant> Winners;
        public readonly int WinningKillCount;

        public MatchResult(MatchEndReason reason, IReadOnlyList<MatchParticipant> winners, int winningKillCount)
        {
            Reason = reason;
            Winners = winners;
            WinningKillCount = winningKillCount;
        }
    }
}
