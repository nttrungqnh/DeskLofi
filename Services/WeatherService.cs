using System.Net.Http;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Windows.Threading;
using DeskLofi.Models;

namespace DeskLofi.Services;

/// <summary>Current weather from Open-Meteo. Failed refreshes preserve the last cached result.</summary>
public sealed class WeatherService : IDisposable
{
    private readonly HttpClient _http = new() { Timeout = TimeSpan.FromSeconds(12) };
    private readonly DispatcherTimer _timer = new();
    private readonly string _cachePath = Path.Combine(AppContext.BaseDirectory, "Data", "weather-cache.json");
    private readonly JsonSerializerOptions _json = new() { PropertyNameCaseInsensitive = true, WriteIndented = true };
    private LocationSettings _location;
    private int _refreshing;
    public WeatherInfo Current { get; private set; }
    public string CityName => _location.CityName;
    public event Action<WeatherInfo>? WeatherChanged;
    public event Action<Exception>? RefreshFailed;

    public WeatherService(LocationSettings location, int refreshMinutes = 15, bool enabled = true, HttpMessageHandler? handler = null)
    {
        _location = location; Current = LoadCache(); Enabled = enabled;
        if(handler is not null){_http.Dispose();_http=new HttpClient(handler){Timeout=TimeSpan.FromSeconds(12)};}
        SetInterval(refreshMinutes); _timer.Tick += async (_, _) => await RefreshAsync();
        if (enabled) { _timer.Start(); if (IsValidLocation(location)) _ = RefreshAsync(); }
    }
    public bool Enabled { get; private set; }
    public void SetEnabled(bool enabled) { Enabled = enabled; if (enabled) { _timer.Start(); if (IsValidLocation(_location)) _ = RefreshAsync(); } else _timer.Stop(); }
    public void SetInterval(int minutes) => _timer.Interval = TimeSpan.FromMinutes(Math.Clamp(minutes, 1, 120));
    public void SetLocation(LocationSettings location)
    {
        _location = location;
        if (IsValidLocation(location)) _ = RefreshAsync();
    }
    public async Task RefreshAsync(CancellationToken cancellationToken = default)
    {
        if (!Enabled || !IsValidLocation(_location) || Interlocked.Exchange(ref _refreshing, 1) != 0) return;
        try
        {
            var url = $"https://api.open-meteo.com/v1/forecast?latitude={_location.Latitude.ToString(System.Globalization.CultureInfo.InvariantCulture)}&longitude={_location.Longitude.ToString(System.Globalization.CultureInfo.InvariantCulture)}&current=temperature_2m,apparent_temperature,relative_humidity_2m,wind_speed_10m,weather_code&timezone=auto";
            using var response = await _http.GetAsync(url, cancellationToken).ConfigureAwait(false);
            response.EnsureSuccessStatusCode();
            await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken).ConfigureAwait(false);
            var payload = await JsonSerializer.DeserializeAsync<OpenMeteoResponse>(stream, _json, cancellationToken).ConfigureAwait(false);
            if (payload?.Current is null) throw new JsonException("The weather response did not contain current conditions.");
            Current = new WeatherInfo { WeatherCode = payload.Current.WeatherCode, State = MapCode(payload.Current.WeatherCode), Temperature = payload.Current.Temperature, FeelsLike = payload.Current.ApparentTemperature, Humidity = payload.Current.Humidity, WindSpeed = payload.Current.WindSpeed, LastUpdated = DateTime.Now };
            SaveCache(Current); LoggerService.Info($"Weather updated: {Current.State}, code {Current.WeatherCode}.");
            WeatherChanged?.Invoke(Current);
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException or JsonException or IOException or ObjectDisposedException)
        {
            LoggerService.Error("Weather update failed", ex); RefreshFailed?.Invoke(ex);
            // Preserve the prior cache/result. Unknown is only used before the first successful refresh.
        }
        finally { Interlocked.Exchange(ref _refreshing, 0); }
    }
    public static WeatherState MapCode(int code) => code switch
    {
        0 => WeatherState.Clear,
        1 or 2 => WeatherState.PartlyCloudy,
        3 => WeatherState.Cloudy,
        45 or 48 => WeatherState.Fog,
        51 or 53 or 55 or 56 or 57 => WeatherState.Drizzle,
        61 or 63 or 66 or 80 or 81 => WeatherState.Rain,
        65 or 67 or 82 => WeatherState.HeavyRain,
        95 or 96 or 99 => WeatherState.Thunderstorm,
        71 or 73 or 75 or 77 or 85 or 86 => WeatherState.Snow,
        _ => WeatherState.Unknown
    };
    private static bool IsValidLocation(LocationSettings x) => double.IsFinite(x.Latitude) && double.IsFinite(x.Longitude) && x.Latitude is >= -90 and <= 90 && x.Longitude is >= -180 and <= 180 && (x.Latitude != 0 || x.Longitude != 0);
    private WeatherInfo LoadCache() { try { return File.Exists(_cachePath) ? JsonSerializer.Deserialize<WeatherInfo>(File.ReadAllText(_cachePath), _json) ?? new() : new(); } catch (Exception e) { LoggerService.Error("Weather cache could not be loaded", e); return new(); } }
    private void SaveCache(WeatherInfo info) { try { Directory.CreateDirectory(Path.GetDirectoryName(_cachePath)!); File.WriteAllText(_cachePath, JsonSerializer.Serialize(info, _json)); } catch (Exception e) { LoggerService.Error("Weather cache could not be saved", e); } }
    public void Dispose() { _timer.Stop(); _http.Dispose(); }

    private sealed class OpenMeteoResponse { public CurrentConditions? Current { get; set; } }
    private sealed class CurrentConditions
    {
        [JsonPropertyName("temperature_2m")] public double? Temperature { get; set; }
        [JsonPropertyName("apparent_temperature")] public double? ApparentTemperature { get; set; }
        [JsonPropertyName("relative_humidity_2m")] public double? Humidity { get; set; }
        [JsonPropertyName("wind_speed_10m")] public double? WindSpeed { get; set; }
        [JsonPropertyName("weather_code")] public int WeatherCode { get; set; }
    }
}
