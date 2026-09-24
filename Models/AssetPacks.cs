using System.Text.Json.Serialization;

namespace DeskLofi.Models;

public abstract class AssetPack
{
    public string Id { get; set; } = "";
    public string Name { get; set; } = "";
    [JsonIgnore] public string DirectoryPath { get; internal set; } = "";
}

public abstract class AnimationPack : AssetPack
{
    public Dictionary<string, string[]> Animations { get; set; } = new(StringComparer.OrdinalIgnoreCase);
    public IReadOnlyList<string> GetAnimation(string name) => Animations.TryGetValue(name, out var frames) ? frames : [];
}

public sealed class CharacterPack : AnimationPack { }
public sealed class PetPack : AnimationPack { }

// Layer assets are metadata only for now; scene rendering remains in SceneDefinitionService.
public sealed class RoomPack : AssetPack
{
    public Dictionary<string, string> Layers { get; set; } = new(StringComparer.OrdinalIgnoreCase);
}
