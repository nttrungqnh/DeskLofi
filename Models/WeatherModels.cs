namespace DeskLofi.Models;

public enum WeatherState { Unknown, Clear, PartlyCloudy, Cloudy, Fog, Drizzle, Rain, HeavyRain, Thunderstorm, Snow }

public sealed class WeatherInfo
{
    public WeatherState State { get; set; } = WeatherState.Unknown;
    public double? Temperature { get; set; }
    public double? FeelsLike { get; set; }
    public double? Humidity { get; set; }
    public double? WindSpeed { get; set; }
    public int? WeatherCode { get; set; }
    public DateTime? LastUpdated { get; set; }
}

public sealed class LocationSettings
{
    public string CityName { get; set; } = "";
    public double Latitude { get; set; }
    public double Longitude { get; set; }
}
