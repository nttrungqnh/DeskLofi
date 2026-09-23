namespace DeskLofi.Models;

public sealed class SpriteAnimationDefinition
{
    public string Name { get; set; } = "";
    public string AssetPath { get; set; } = "";
    public string FolderPath { get; set; } = "";
    public string[] Frames { get; set; } = [];
    public int FrameCount { get; set; }
    public int FrameWidth { get; set; }
    public double Fps { get; set; } = 8;
    public bool Loop { get; set; } = true;
    public string? NextAnimation { get; set; }
}

public sealed class SpriteAnimationCatalog
{
    public int CanvasWidth { get; set; } = 517;
    public int CanvasHeight { get; set; } = 488;
    public List<SpriteAnimationDefinition> Animations { get; set; } = [];
}
