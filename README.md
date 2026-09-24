# DeskLofi

Windows pixel-room companion built with C# .NET 8 and WPF.

## Run

```powershell
dotnet restore
dotnet build
dotnet run
```

Run the smoke checks with:

```powershell
dotnet run --project Tests\DeskLofi.SmokeTests\DeskLofi.SmokeTests.csproj
```

## Phase 2 systems

- `TimeService` reads local Windows time and refreshes every 30 seconds. Clock/date visibility is in Settings.
- `DayNightService` transitions sky and lighting layers over 3 seconds. Time boundaries can be changed through `TimeService.Configure`.
- `SceneDefinitionService` loads `Data/scenes.json`; each scene declares `BaseBackground`, per-period `SkyAssets` and `LightingAssets`, window bounds, and character/clock positions.
- `WeatherService` uses Open-Meteo, reads only manually configured city/coordinates, refreshes at the selected interval (15 minutes by default), and keeps its cache at `Data/weather-cache.json`.
- `WeatherEffectController` reuses pixel drops within `WindowBounds`; visual effects stop when disabled. Thunder flashes are spaced randomly.
- `AmbienceService` is an audio channel separate from music. Missing ambience files are skipped. Weather ambience fades over a few seconds.
- `MusicService` stores metadata and original file paths in `Data/music-library.json`; it never copies audio files.

## Test Night/Rain

Open Settings → Weather, check **Developer mode: simulate time/weather**, choose **Night** and **Rain**, then Save. The clock also uses the selected debug period in Debug builds. Set Weather Effects off to test the still room, or set Weather Ambience on after adding an ambience file. Turn off Developer mode to return to local time and live weather.

## Replace scene and weather assets

Bedroom sky/lighting layers are optional transparent PNGs at:

```text
Assets/Scenes/Bedroom/background.png
Assets/Scenes/Bedroom/Sky/Morning.png
Assets/Scenes/Bedroom/Sky/Day.png
Assets/Scenes/Bedroom/Sky/Evening.png
Assets/Scenes/Bedroom/Sky/Night.png
Assets/Scenes/Bedroom/Lighting/Morning.png
Assets/Scenes/Bedroom/Lighting/Day.png
Assets/Scenes/Bedroom/Lighting/Evening.png
Assets/Scenes/Bedroom/Lighting/Night.png
```

Each window layer is scaled with nearest-neighbor rendering. Rain/cloud/fog/snow and the tiny status label are drawn as pixel placeholders; replace those in `Services/WeatherEffectController.cs` and the `WeatherText` scene element when you add icon art.

## Add music and ambience

Use the radio popup or Settings → Music to add `.mp3`/`.wav` files or folders. Original files stay where they are. Missing paths are marked and can be removed from Music Library. Playlist and favorite changes are saved only when the library changes.

Put ambience files here (WAV/MP3/WMA):

```text
Assets/Audio/Ambience/rain_light.wav
Assets/Audio/Ambience/rain_medium.wav
Assets/Audio/Ambience/rain_heavy.wav
Assets/Audio/Ambience/thunder.wav
```

Settings live in `Data/settings.json`. Rotating privacy-conscious logs go to `Logs/desklifi.log`. Global input hooks report only timestamps/activity and do not retain key codes or typed content.

Choose Tiếng Việt or English in Settings → General → Language. The choice is saved and used for the settings, music popup, playlist window, music library, and tray menu.

## Asset packs

Character packs live in `Assets/Characters`, Pet packs in `Assets/Pets`, and Room pack metadata in `Assets/Rooms`. The built-in packs are `Girl_Default`, `OrangeCat`, and `DefaultRoom`. Each subdirectory has a `pack.json` with `id`, `name`, and relative PNG paths. Character and Pet manifests use an `animations` object: `idle` is required; `typing`, `mouse`, `blink`, `coffee`, `stretch`, `tail`, and `sleep` are optional. Room manifests can list PNG paths under `layers`; room rendering still uses the current scene system.

For example, to add another Character, create `Assets/Characters/Boy_Default/pack.json` and its PNGs:

```json
{
  "id": "boy_default",
  "name": "Boy",
  "animations": {
    "idle": ["MASTER.png"],
    "typing": ["Typing/typing_01.png", "Typing/typing_02.png"]
  }
}
```

For another Pet, create `Assets/Pets/NewPet/pack.json` with `idle` and any available `tail` or `sleep` frame arrays. Set `ActiveCharacterPackId` or `ActivePetPackId` in `Data/settings.json` to the pack ID and restart. A missing selected pack falls back to the built-in pack. Invalid manifests or missing idle images are logged and skipped; unavailable optional animations are skipped without using another pack's artwork. Frames are decoded and cached once. The existing state machines keep all animation timing and input behavior in code.
