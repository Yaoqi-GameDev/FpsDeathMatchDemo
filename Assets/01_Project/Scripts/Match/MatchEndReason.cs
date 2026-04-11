namespace FpsDemo.Match
{
    public enum MatchEndReason
    {
        /// <summary>有人达到目标击杀数（可能多人同帧达标，按规则取并列）。</summary>
        TargetKillsReached,

        /// <summary>倒计时归零，按击杀数比高低，允许平局。</summary>
        TimeExpired,
    }
}
