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

## Girl typing sprite

The supplied animation is four separate PNGs in `Assets/Characters/Girl/Typing/`, loaded in filename order at startup. Each frame keeps its original pixels and is padded transparently to the shared 517×488 canvas with a bottom-left anchor. `Typing` and `TypingFast` reuse the same cached frames at 6 and 10 FPS. The fixed scene viewport is 112×106, uses nearest-neighbor scaling, and hides the room monitor while the sprite's laptop is visible. States without an asset keep the existing placeholder.

Edit `Data/girl-animations.json` to add new animations. For Idle, Mouse, Coffee, or Stretch, add a `FolderPath` (such as `Assets/Characters/Girl/Idle`) and optionally `FrameCount`, `Fps`, `Loop`, and `NextAnimation`. Use equally registered transparent frames; increase `CanvasWidth` and `CanvasHeight` if a later animation needs more space. A horizontal sheet can instead use `AssetPath`, `FrameWidth`, and `FrameCount`. The same animation controller can be configured for Cat later.

Typing activity uses the existing global keyboard hook and stores only timestamps. `TypingIdleDelayMs` (1200), `FastTypingWindowMs` (2000), and `FastTypingThresholdPerSecond` (7) can be set in `Data/settings.json` in the output folder. In Debug builds, open the tray menu → **Animation debug** to play Idle/Typing/TypingFast or override the speed to 2, 4, 6, 8, 10, or 12 FPS. **Auto** returns to live activity and configured FPS. The panel is absent from Release builds.
