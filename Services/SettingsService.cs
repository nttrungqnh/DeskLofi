using System.Text.Json;
using DeskLofi.Models;

namespace DeskLofi.Services;
public sealed class SettingsService
{
    private readonly string _path = Path.Combine(AppContext.BaseDirectory, "Data", "settings.json");
    private static readonly JsonSerializerOptions Options = new() { WriteIndented = true, PropertyNameCaseInsensitive = true };
    public AppSettings Current { get; private set; }
    public SettingsService() { Directory.CreateDirectory(Path.GetDirectoryName(_path)!); Current = Load(); }
    private AppSettings Load() { try { return File.Exists(_path) ? JsonSerializer.Deserialize<AppSettings>(File.ReadAllText(_path), Options) ?? new() : new(); } catch { return new(); } }
    public void Save() { try { File.WriteAllText(_path, JsonSerializer.Serialize(Current, Options)); } catch { } }
}
