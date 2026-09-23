namespace DeskLofi.Animation;

/// <summary>Low-frequency animation clock. Consumers reuse cached sprite frames.</summary>
public sealed class AnimationController
{
    public int Frame { get; private set; }
    public event Action<int>? FrameAdvanced;
    public void Advance() { Frame++; FrameAdvanced?.Invoke(Frame); }
}
