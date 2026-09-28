using System.Text.Json;
using DeskLofi.Models;

namespace DeskLofi.Services;
public sealed class SettingsService
{
    private const int PreviousDefaultTypingIdleDelayMs = 1200;
    private const double PreviousDefaultCatScale = 0.07;
    private const double PreviousDefaultCatOffsetX = 112;
    private const double PreviousDefaultCatOffsetY = 32;
    private const double LegacyDefaultCatOffsetX = 100;
    private const double LegacyDefaultCatOffsetY = 44;
    private const double RecentDefaultCatOffsetX = 225;
    private const double RecentDefaultCatOffsetY = 82;
    private const double PreviousRoomCatOffsetX = 224;
    private const double PreviousRoomCatOffsetY = 72;
    private const double EarlyDefaultCatOffsetX = 218;
    private const double EarlyDefaultCatOffsetY = 91;
    private const double PreviousDefaultGirlOffsetX = 83;
    private const double PreviousDefaultGirlOffsetY = 38;
    private const double RecentDefaultGirlOffsetX = 106;
    private const double RecentDefaultGirlOffsetY = 38;
    private const double PreviousDeskGirlOffsetX = 110;
    private const double PreviousDeskGirlOffsetY = 43;
    private const double CurrentDeskGirlOffsetX = 110;
    private const double CurrentDeskGirlOffsetY = 32;
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
            var previousCatPosition = Math.Abs(settings.CatOffsetX - PreviousDefaultCatOffsetX) < 0.000001 &&
                Math.Abs(settings.CatOffsetY - PreviousDefaultCatOffsetY) < 0.000001;
            var legacyCatPosition = Math.Abs(settings.CatOffsetX - LegacyDefaultCatOffsetX) < 0.000001 &&
                Math.Abs(settings.CatOffsetY - LegacyDefaultCatOffsetY) < 0.000001;
            var recentCatPosition = Math.Abs(settings.CatOffsetX - RecentDefaultCatOffsetX) < 0.000001 &&
                Math.Abs(settings.CatOffsetY - RecentDefaultCatOffsetY) < 0.000001;
            var previousRoomCatPosition = Math.Abs(settings.CatOffsetX - PreviousRoomCatOffsetX) < 0.000001 &&
                Math.Abs(settings.CatOffsetY - PreviousRoomCatOffsetY) < 0.000001;
            var earlyDefaultCatPosition = Math.Abs(settings.CatOffsetX - EarlyDefaultCatOffsetX) < 0.000001 &&
                Math.Abs(settings.CatOffsetY - EarlyDefaultCatOffsetY) < 0.000001;
            if (previousCatPosition || legacyCatPosition || recentCatPosition || previousRoomCatPosition || earlyDefaultCatPosition)
            {
                settings.CatOffsetX = AppSettings.DefaultCatOffsetX;
                settings.CatOffsetY = AppSettings.DefaultCatOffsetY;
            }
            if (Math.Abs(settings.GirlOffsetX - PreviousDefaultGirlOffsetX) < 0.000001 &&
                Math.Abs(settings.GirlOffsetY - PreviousDefaultGirlOffsetY) < 0.000001)
            {
                settings.GirlOffsetX = AppSettings.DefaultGirlOffsetX;
                settings.GirlOffsetY = AppSettings.DefaultGirlOffsetY;
            }
            else if (Math.Abs(settings.GirlOffsetX - RecentDefaultGirlOffsetX) < 0.000001 &&
                     Math.Abs(settings.GirlOffsetY - RecentDefaultGirlOffsetY) < 0.000001)
            {
                settings.GirlOffsetX = AppSettings.DefaultGirlOffsetX;
                settings.GirlOffsetY = AppSettings.DefaultGirlOffsetY;
            }
            else if (Math.Abs(settings.GirlOffsetX - PreviousDeskGirlOffsetX) < 0.000001 &&
                     Math.Abs(settings.GirlOffsetY - PreviousDeskGirlOffsetY) < 0.000001)
            {
                settings.GirlOffsetX = AppSettings.DefaultGirlOffsetX;
                settings.GirlOffsetY = AppSettings.DefaultGirlOffsetY;
            }
            else if (Math.Abs(settings.GirlOffsetX - CurrentDeskGirlOffsetX) < 0.000001 &&
                     Math.Abs(settings.GirlOffsetY - CurrentDeskGirlOffsetY) < 0.000001)
            {
                settings.GirlOffsetX = AppSettings.DefaultGirlOffsetX;
                settings.GirlOffsetY = AppSettings.DefaultGirlOffsetY;
            }
            if (settings.CatSleepTimeoutSeconds == PreviousDefaultCatSleepTimeoutSeconds)
                settings.CatSleepTimeoutSeconds = AppSettings.DefaultCatSleepTimeoutSeconds;
            return settings;
        }
        catch { return new(); }
    }
    public void Save() { try { File.WriteAllText(_path, JsonSerializer.Serialize(Current, Options)); } catch { } }
}
