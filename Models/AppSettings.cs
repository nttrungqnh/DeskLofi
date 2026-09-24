namespace DeskLofi.Models;
public sealed class AppSettings
{
    public bool StartWithWindows { get; set; }
    public string Language { get; set; } = "vi";
    public bool AlwaysOnTop { get; set; } = true;
    public bool ShowOnTaskbar { get; set; }
    public bool ReactToKeyboard { get; set; } = true;
    public bool ReactToMouse { get; set; } = true;
    public int TypingIdleDelayMs { get; set; } = 1200;
    public int MouseIdleDelayMs { get; set; } = 900;
    public int MouseMoveThrottleMs { get; set; } = 100;
    public int KeyboardPriorityWindowMs { get; set; } = 650;
    public int FastTypingWindowMs { get; set; } = 2000;
    public double FastTypingThresholdPerSecond { get; set; } = 7;
    public bool AutoPlayMusic { get; set; }
    public int CatSleepTimeoutSeconds { get; set; } = 45;
    public int AfkTimeoutMinutes { get; set; } = 5;
    public double Scale { get; set; } = 1;
    public double Volume { get; set; } = 0.35;
    public string Scene { get; set; } = "Bedroom";
    public double Left { get; set; } = -1;
    public double TaskbarOffsetX { get; set; } = 93;
    public double Top { get; set; } = -1;
    public bool ShowClock { get; set; } = true;
    public bool ShowDate { get; set; }
    public bool AutoDayNight { get; set; } = true;
    public bool EnableRealWeather { get; set; }
    public string CityName { get; set; } = "";
    public double Latitude { get; set; }
    public double Longitude { get; set; }
    public int WeatherRefreshMinutes { get; set; } = 15;
    public bool WeatherEffects { get; set; } = true;
    public bool WeatherAmbienceAuto { get; set; }
    public bool EnableLightning { get; set; } = true;
    public double WeatherVolume { get; set; } = 0.22;
    public bool DebugMode { get; set; }
    public string DebugTime { get; set; } = "";
    public string DebugWeather { get; set; } = "";
    public string MusicLibraryLastTrackId { get; set; } = "";
    public string MusicLibraryLastPlaylist { get; set; } = "Focus";
    public bool MusicShuffle { get; set; }
    public bool MusicRepeat { get; set; }
}
public sealed class Track
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N");
    public string Title { get; set; } = "";
    public string Artist { get; set; } = "";
    public string Album { get; set; } = "";
    public string FilePath { get; set; } = "";
    public string Category { get; set; } = "Focus";
    public double Duration { get; set; }
    public bool IsFavorite { get; set; }
    public DateTime DateAdded { get; set; } = DateTime.Now;
    public bool IsMissing { get; set; }
    public List<string> Playlists { get; set; } = [];
}

public sealed class MusicLibraryDocument
{
    public List<Track> Tracks { get; set; } = [];
    public List<string> Playlists { get; set; } = ["Focus", "Chill", "Rain", "Night", "Cafe", "Favorites"];
}
