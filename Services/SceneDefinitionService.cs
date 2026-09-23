using System.Text.Json;
using System.Windows.Media.Imaging;
using DeskLofi.Models;

namespace DeskLofi.Services;

public sealed class SceneDefinitionService
{
    private readonly string _file = Path.Combine(AppContext.BaseDirectory, "Data", "scenes.json");
    public SceneDefinition Load(string name)
    {
        try
        {
            var scenes = JsonSerializer.Deserialize<List<SceneDefinition>>(File.ReadAllText(_file), new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
            var result = scenes?.FirstOrDefault(x => x.Name.Equals(name, StringComparison.OrdinalIgnoreCase)) ?? new SceneDefinition();
            result.AssetRoot = Path.Combine(AppContext.BaseDirectory, "Assets", "Scenes", result.Name);
            return result;
        }
        catch { return new SceneDefinition { AssetRoot = Path.Combine(AppContext.BaseDirectory, "Assets", "Scenes", "Bedroom") }; }
    }
    public static BitmapSource? LoadImage(SceneDefinition scene, string? relativePath)
    {
        if (string.IsNullOrWhiteSpace(relativePath)) return null;
        try
        {
            var normalized = relativePath.Replace('/', Path.DirectorySeparatorChar).Replace('\\', Path.DirectorySeparatorChar);
            var path = Path.IsPathRooted(normalized) ? normalized : Path.Combine(scene.AssetRoot, normalized);
            if (!File.Exists(path)) return null;
            var image = new BitmapImage(); image.BeginInit(); image.UriSource = new Uri(Path.GetFullPath(path)); image.CacheOption = BitmapCacheOption.OnLoad; image.EndInit(); image.Freeze(); return image;
        }
        catch (Exception ex) { LoggerService.Error("Scene layer image could not be loaded", ex); return null; }
    }
}
