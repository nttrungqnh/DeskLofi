namespace DeskLofi.Models;

public enum AnimationMode { Loop, PingPong, OneShot, IdleWithRandomBlink }
public enum FrameHorizontalAnchor { Left, Center, Right }
public enum FrameVerticalAnchor { Top, Center, Bottom }

public sealed class SpriteAnimationDefinition
{
    public string Name { get; set; } = "";
    public string AssetPath { get; set; } = "";
    public string FolderPath { get; set; } = "";
    public string[] Frames { get; set; } = [];
    public double SourceScale { get; set; } = 1;
    public FrameHorizontalAnchor HorizontalAnchor { get; set; } = FrameHorizontalAnchor.Center;
    public FrameVerticalAnchor VerticalAnchor { get; set; } = FrameVerticalAnchor.Bottom;
    public int OffsetX { get; set; }
    public int OffsetY { get; set; }
    public int FrameCount { get; set; }
    public int FrameWidth { get; set; }
    public int FrameHeight { get; set; }
    public int StartX { get; set; }
    public int StartY { get; set; }
    public int FrameGap { get; set; }
    public double Fps { get; set; } = 8;
    public bool Loop { get; set; } = true;
    public AnimationMode Mode { get; set; } = AnimationMode.Loop;
    public int BlinkDelayMinMs { get; set; } = 3000;
    public int BlinkDelayMaxMs { get; set; } = 7000;
    public int BlinkFrameDurationMinMs { get; set; } = 70;
    public int BlinkFrameDurationMaxMs { get; set; } = 110;
    public double DoubleBlinkChance { get; set; } = 0.125;
    public int DoubleBlinkDelayMinMs { get; set; } = 120;
    public int DoubleBlinkDelayMaxMs { get; set; } = 200;
    public string? NextAnimation { get; set; }
}

public sealed class SpriteAnimationCatalog
{
    public int CanvasWidth { get; set; } = 517;
    public int CanvasHeight { get; set; } = 491;
    public List<SpriteAnimationDefinition> Animations { get; set; } = [];
}
