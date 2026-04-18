namespace FpsDemo.Fps
{
    /// <summary>
    /// 为 <see cref="FpsPlayerMotor"/> 提供 <see cref="PlayerLocomotionInput"/>（每帧一次）。
    /// </summary>
    public interface ILocomotionInputSource
    {
        bool TryGetFrame(out PlayerLocomotionInput frame);
    }
}
