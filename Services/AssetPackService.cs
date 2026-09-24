using System.Text.Json;
using System.Windows.Media.Imaging;
using DeskLofi.Models;

namespace DeskLofi.Services;

/// <summary>Discovers pack metadata and keeps each referenced PNG decoded once.</summary>
public sealed class AssetPackService
{
    private readonly Dictionary<string, CharacterPack> _characters = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, PetPack> _pets = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, RoomPack> _rooms = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, BitmapSource> _images = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, BitmapSource[]> _animations = new(StringComparer.OrdinalIgnoreCase);
    private static readonly JsonSerializerOptions JsonOptions = new() { PropertyNameCaseInsensitive = true };

    private readonly CharacterPack? _activeCharacter;
    private readonly PetPack? _activePet;

    public AssetPackService(AppSettings settings, string? assetRoot = null)
    {
        var root = assetRoot ?? Path.Combine(AppContext.BaseDirectory, "Assets");
        Discover(Path.Combine(root, "Characters"), _characters);
        Discover(Path.Combine(root, "Pets"), _pets);
        Discover(Path.Combine(root, "Rooms"), _rooms);
        _activeCharacter = ChooseActive(_characters, settings.ActiveCharacterPackId, "girl_default", "Character");
        _activePet = ChooseActive(_pets, settings.ActivePetPackId, "orange_cat", "Pet");
    }

    public IReadOnlyList<CharacterPack> GetCharacterPacks() => _characters.Values.ToArray();
    public IReadOnlyList<PetPack> GetPetPacks() => _pets.Values.ToArray();
    public IReadOnlyList<RoomPack> GetRoomPacks() => _rooms.Values.ToArray();
    public CharacterPack? GetActiveCharacter() => _activeCharacter;
    public PetPack? GetActivePet() => _activePet;
    public BitmapSource[] GetCharacterAnimation(string name) => GetAnimation(_activeCharacter, name);
    public BitmapSource[] GetPetAnimation(string name) => GetAnimation(_activePet, name);

    public string? ResolveFramePath(AssetPack pack, string relativePath)
    {
        if (string.IsNullOrWhiteSpace(relativePath) || Path.IsPathRooted(relativePath)) return null;
        try
        {
            var root = Path.GetFullPath(pack.DirectoryPath);
            var path = Path.GetFullPath(Path.Combine(root, relativePath.Replace('/', Path.DirectorySeparatorChar).Replace('\\', Path.DirectorySeparatorChar)));
            if (!path.StartsWith(root + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase) ||
                !string.Equals(Path.GetExtension(path), ".png", StringComparison.OrdinalIgnoreCase) || !File.Exists(path)) return null;
            return path;
        }
        catch { return null; }
    }

    private void Discover<T>(string root, Dictionary<string, T> found) where T : AssetPack
    {
        if (!Directory.Exists(root)) return;
        foreach (var directory in Directory.EnumerateDirectories(root).OrderBy(x => x, StringComparer.OrdinalIgnoreCase))
        {
            var manifest = Path.Combine(directory, "pack.json");
            if (!File.Exists(manifest)) continue;
            try
            {
                var pack = JsonSerializer.Deserialize<T>(File.ReadAllText(manifest), JsonOptions);
                if (pack is null || string.IsNullOrWhiteSpace(pack.Id))
                {
                    LoggerService.Warn($"Skipping pack with missing id: {manifest}");
                    continue;
                }
                pack.DirectoryPath = Path.GetFullPath(directory);
                if (pack is AnimationPack animations && !ValidateAnimations(animations, manifest)) continue;
                if (pack is RoomPack room) ValidateRoomLayers(room, manifest);
                if (!found.TryAdd(pack.Id, pack))
                {
                    LoggerService.Warn($"Skipping duplicate pack id '{pack.Id}': {manifest}");
                    continue;
                }
                LoggerService.Info($"Asset pack registered: {pack.Id}");
            }
            catch (Exception error)
            {
                LoggerService.Warn($"Skipping invalid pack JSON: {manifest} ({error.GetType().Name}: {error.Message})");
            }
        }
    }

    private bool ValidateAnimations(AnimationPack pack, string manifest)
    {
        var valid = new Dictionary<string, string[]>(StringComparer.OrdinalIgnoreCase);
        foreach (var (name, frames) in pack.Animations ?? new Dictionary<string, string[]>())
        {
            if (string.IsNullOrWhiteSpace(name) || frames is null || frames.Length == 0 ||
                frames.Any(frame => ResolveFramePath(pack, frame) is null))
            {
                LoggerService.Warn($"Skipping missing or invalid animation '{name}' in {manifest}");
                continue;
            }
            valid[name] = frames;
        }
        pack.Animations = valid;
        if (valid.TryGetValue("idle", out var idle) && idle.Length > 0) return true;
        LoggerService.Warn($"Skipping pack without a valid idle/master image: {manifest}");
        return false;
    }

    private void ValidateRoomLayers(RoomPack room, string manifest)
    {
        var valid = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var (name, path) in room.Layers ?? new Dictionary<string, string>())
        {
            if (ResolveFramePath(room, path) is { }) valid[name] = path;
            else LoggerService.Warn($"Skipping missing room layer '{name}' in {manifest}");
        }
        room.Layers = valid;
    }

    private BitmapSource[] GetAnimation(AnimationPack? pack, string name)
    {
        if (pack is null || !pack.Animations.TryGetValue(name, out var frames)) return [];
        var animationKey = $"{pack.DirectoryPath}|{name}";
        if (_animations.TryGetValue(animationKey, out var cached)) return cached;
        var loaded = new List<BitmapSource>(frames.Length);
        foreach (var frame in frames)
        {
            var path = ResolveFramePath(pack, frame);
            if (path is null) return _animations[animationKey] = [];
            if (!_images.TryGetValue(path, out var image))
            {
                try
                {
                    var bitmap = new BitmapImage();
                    bitmap.BeginInit();
                    bitmap.UriSource = new Uri(path);
                    bitmap.CacheOption = BitmapCacheOption.OnLoad;
                    bitmap.EndInit();
                    bitmap.Freeze();
                    image = bitmap;
                    _images[path] = image;
                }
                catch (Exception error)
                {
                    LoggerService.Warn($"Skipping animation '{name}' from pack '{pack.Id}': {error.Message}");
                    return _animations[animationKey] = [];
                }
            }
            loaded.Add(image);
        }
        return _animations[animationKey] = loaded.ToArray();
    }

    private static T? ChooseActive<T>(Dictionary<string, T> packs, string requested, string fallback, string kind) where T : AssetPack
    {
        if (!string.IsNullOrWhiteSpace(requested) && packs.TryGetValue(requested, out var selected)) return selected;
        if (!string.IsNullOrWhiteSpace(requested)) LoggerService.Warn($"{kind} pack '{requested}' is unavailable; using fallback.");
        if (packs.TryGetValue(fallback, out var builtIn)) return builtIn;
        return packs.Values.FirstOrDefault();
    }
}
