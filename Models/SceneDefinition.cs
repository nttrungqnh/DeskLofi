using System.Text.Json.Serialization;

namespace DeskLofi.Models;

public sealed class SceneDefinition
{
    public string Name { get; set; } = "Bedroom";
    public string BaseBackground { get; set; } = "";
    public Dictionary<string, string> SkyColors { get; set; } = new(StringComparer.OrdinalIgnoreCase);
    public Dictionary<string, string> LightingColors { get; set; } = new(StringComparer.OrdinalIgnoreCase);
    public Dictionary<string, string> SkyAssets { get; set; } = new(StringComparer.OrdinalIgnoreCase);
    public Dictionary<string, string> LightingAssets { get; set; } = new(StringComparer.OrdinalIgnoreCase);
    public SceneRect WindowBounds { get; set; } = new(48, 8, 42, 48);
    public ScenePoint GirlPosition { get; set; } = new(95, 6);
    public ScenePoint CatPosition { get; set; } = new(217, 49);
    public ScenePoint ClockPosition { get; set; } = new(8, 5);
    [JsonIgnore] public string AssetRoot { get; set; } = "";
}

public sealed record ScenePoint(double X, double Y);
public sealed record SceneRect(double X, double Y, double Width, double Height);
