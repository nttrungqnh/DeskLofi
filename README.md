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

## Girl character

The app opens as a transparent 112×106 character window with the room demo hidden. Idle blink frames are in `Assets/Characters/Girl/Idle/`; Typing and TypingFast share four cached PNGs in `Assets/Characters/Girl/Typing/` at 6 and 10 FPS. The five separate Mouse PNGs are in `Assets/Characters/Girl/Mouse/` and play at 4 FPS in PingPong order. All animations use the same transparent 517×491 canvas. Mouse frames have a common 1.05× nearest-neighbor scale and bottom-right anchor so the character is close to Idle height; the original PNGs remain unchanged. The girl starts in Idle, blinks at randomized 3–7 second intervals, switches to Typing on keyboard activity, and switches to Mouse when mouse activity is newer than the 650 ms keyboard priority window. Mouse returns to Idle after 900 ms without mouse activity. Right-click the character for music controls and settings.

Edit `Data/girl-animations.json` to add new animations. Separate PNGs can use an ordered `Frames` list or `FolderPath`; `MultiFrameAnimationSource` decodes and freezes them once, then places them on the fixed canvas. `SourceScale` applies one nearest-neighbor scale to every frame in an animation; `HorizontalAnchor`, `VerticalAnchor`, `OffsetX`, and `OffsetY` align varying source sizes. `Mode: "PingPong"` reverses the frame index without duplicating decoded images. Event-driven blinking uses `Mode: "IdleWithRandomBlink"` and configurable blink delays and frame durations; its four frames are eyes open, half closed, closed, and open. Increase `CanvasWidth` and `CanvasHeight` if a later animation needs more space. Sprite sheets still support `AssetPath`, `FrameWidth`, `FrameHeight`, `StartX`, `StartY`, `FrameGap`, and `FrameCount`. The same animation controller can be configured for Cat later.

Typing and mouse activity reuse the existing global input hooks and store only timestamps. `TypingIdleDelayMs` (1200), `MouseIdleDelayMs` (900), `MouseMoveThrottleMs` (100), `KeyboardPriorityWindowMs` (650), `FastTypingWindowMs` (2000), and `FastTypingThresholdPerSecond` (7) can be set in `Data/settings.json` in the output folder. In Debug builds, open the tray menu → **Animation debug** to play Idle/Typing/TypingFast/Mouse, trigger **Blink Now**, or preview 4, 5, 6, 7, 8, or 10 FPS. **Auto** returns to live activity and configured FPS. The panel is absent from Release builds.
