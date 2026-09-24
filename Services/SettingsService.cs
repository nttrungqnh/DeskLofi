using System.Text.Json;
using DeskLofi.Models;

namespace DeskLofi.Services;
public sealed class SettingsService
{
    private const int PreviousDefaultTypingIdleDelayMs = 1200;
    private const double PreviousDefaultCatScale = 0.07;
    private const double PreviousDefaultCatOffsetX = 112;
    private const double PreviousDefaultCatOffsetY = 32;
    private const int PreviousDefaultCatSleepTimeoutSeconds = 45;
    private readonly string _path = Path.Combine(AppContext.BaseDirectory, "Data", "settings.json");
    private static readonly JsonSerializerOptions Options = new() { WriteIndented = true, PropertyNameCaseInsensitive = true };
    public AppSettings Current { get; private set; }
    public SettingsService() { Directory.CreateDirectory(Path.GetDirectoryName(_path)!); Current = Load(); }
    private AppSettings Load()
    {
        try
        {
            if (!File.Exists(_path)) return new();
            var settings = JsonSerializer.Deserialize<AppSettings>(File.ReadAllText(_path), Options) ?? new();
            // Move the previous built-in delay to the new default while preserving custom values.
            if (settings.TypingIdleDelayMs == PreviousDefaultTypingIdleDelayMs)
                settings.TypingIdleDelayMs = AppSettings.DefaultTypingIdleDelayMs;
            if (Math.Abs(settings.CatScale - PreviousDefaultCatScale) < 0.000001)
                settings.CatScale = AppSettings.DefaultCatScale;
            if (Math.Abs(settings.CatOffsetX - PreviousDefaultCatOffsetX) < 0.000001 &&
                Math.Abs(settings.CatOffsetY - PreviousDefaultCatOffsetY) < 0.000001)
            {
                settings.CatOffsetX = AppSettings.DefaultCatOffsetX;
                settings.CatOffsetY = AppSettings.DefaultCatOffsetY;
            }
            if (settings.CatSleepTimeoutSeconds == PreviousDefaultCatSleepTimeoutSeconds)
                settings.CatSleepTimeoutSeconds = AppSettings.DefaultCatSleepTimeoutSeconds;
            return settings;
        }
        catch { return new(); }
    }
    public void Save() { try { File.WriteAllText(_path, JsonSerializer.Serialize(Current, Options)); } catch { } }
}
