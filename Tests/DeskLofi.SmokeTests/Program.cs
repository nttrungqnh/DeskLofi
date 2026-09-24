using System.Net;
using System.Net.Http;
using System.IO;
using System.Text;
using System.Text.Json;
using System.Windows.Controls;
using System.Reflection;
using DeskLofi.Animation;
using DeskLofi.Models;
using DeskLofi.Services;

namespace DeskLofi.SmokeTests;

internal static class Program
{
    [STAThread]
    private static int Main()
    {
        var checks = new List<(string Name, Func<Task> Run)>
        {
            ("local time boundaries", TestTime), ("WMO weather mapping", TestWeatherMapping),
            ("network error preserves weather cache", TestWeatherFailure), ("visual effects and empty sprite fallback", TestEffects),
            ("Girl master/blink/typing/mouse/coffee/stretch cache", TestGirlImageCache), ("Cat sprite cache, tail wag, and sleep transitions", TestCatAnimation), ("asset pack discovery and fallback", TestAssetPacks), ("WPF Cat remains independent during Typing", TestWindowCatTyping), ("WPF Cat sleep/wake rendering and input", TestWindowCatSleepRendering), ("idle blink timing and interruption", TestBlinkController), ("WPF window renders blink frames", TestWindowBlinkRendering), ("WPF typing loop and idle return", TestWindowTypingRendering), ("WPF mouse loop and idle return", TestWindowMouseRendering), ("WPF typing interrupts mouse animation", TestTypingInterruptsMouse), ("mouse and keyboard transitions", TestMouseTransitions), ("typing state and debounce", TestTypingStates), ("action scheduler and independent activity", TestCoffeeStates), ("Coffee/Stretch scheduling and missing assets", TestIdleSpecialSelection), ("random action intervals and gap", TestDefaultIdleSpecialSelection), ("WPF coffee sequence and idle return", TestWindowCoffeeRendering), ("WPF Coffee keeps tracking input", TestWindowCoffeeInterruption), ("WPF Stretch sequence and idle return", TestWindowStretchRendering), ("WPF Stretch keeps tracking input", TestWindowStretchInterruption),
            ("phase one activity state regression", TestActivityStates), ("settings save/load", TestSettings), ("empty music library and missing file", TestMusicLibrary),
            ("input hook dispose", TestHooks), ("ambience missing assets", TestAmbience), ("tray and application lifecycle", TestHostLifecycle)
        };
        var failed = 0;
        foreach (var check in checks)
        {
            try { check.Run().GetAwaiter().GetResult(); Console.WriteLine($"PASS {check.Name}"); }
            catch (Exception ex) { failed++; Console.WriteLine($"FAIL {check.Name}: {ex.Message}"); }
        }
        Console.WriteLine($"{checks.Count-failed}/{checks.Count} checks passed.");
        return failed == 0 ? 0 : 1;
    }
    private static Task TestTime()
    {
        using var time = new TimeService();
        foreach (var (hour, period) in new[] { (4,TimePeriod.Night),(5,TimePeriod.Morning),(7,TimePeriod.Morning),(8,TimePeriod.Day),(16,TimePeriod.Day),(17,TimePeriod.Evening),(19,TimePeriod.Evening),(20,TimePeriod.Night),(23,TimePeriod.Night) })
        { time.Refresh(new DateTime(2026,9,24,hour,0,0,DateTimeKind.Local)); Assert(time.CurrentPeriod==period,$"Hour {hour} resolved to {time.CurrentPeriod}"); }
        using var dayNight=new DayNightService(time,true){TransitionDuration=TimeSpan.Zero};dayNight.SetDebugPeriod(TimePeriod.Night);Assert(dayNight.CurrentPeriod==TimePeriod.Night,"debug period override");
        return Task.CompletedTask;
    }
    private static Task TestWeatherMapping()
    {
        Assert(WeatherService.MapCode(0)==WeatherState.Clear,"clear");Assert(WeatherService.MapCode(2)==WeatherState.PartlyCloudy,"partly cloudy");Assert(WeatherService.MapCode(3)==WeatherState.Cloudy,"cloudy");Assert(WeatherService.MapCode(45)==WeatherState.Fog,"fog");Assert(WeatherService.MapCode(53)==WeatherState.Drizzle,"drizzle");Assert(WeatherService.MapCode(63)==WeatherState.Rain,"rain");Assert(WeatherService.MapCode(82)==WeatherState.HeavyRain,"heavy rain");Assert(WeatherService.MapCode(95)==WeatherState.Thunderstorm,"thunderstorm");Assert(WeatherService.MapCode(73)==WeatherState.Snow,"snow");Assert(WeatherService.MapCode(-1)==WeatherState.Unknown,"unknown");return Task.CompletedTask;
    }
    private static async Task TestWeatherFailure()
    {
        var handler=new SequenceHandler(HttpStatusCode.OK, HttpStatusCode.ServiceUnavailable);
        using var weather=new WeatherService(new LocationSettings{CityName="Test",Latitude=20.95,Longitude=107.07},15,false,handler);
        var first=new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);weather.WeatherChanged+=_=>first.TrySetResult();weather.SetEnabled(true);
        await first.Task.WaitAsync(TimeSpan.FromSeconds(5));Assert(weather.Current.State==WeatherState.Rain,"initial cached weather response");
        var failed=new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);weather.RefreshFailed+=_=>failed.TrySetResult();await weather.RefreshAsync().WaitAsync(TimeSpan.FromSeconds(5));await failed.Task.WaitAsync(TimeSpan.FromSeconds(5));Assert(weather.Current.State==WeatherState.Rain,"failed refresh must preserve previous state");
    }
    private static Task TestEffects()
    {
        var sprite=new SpriteLoader().GetFrame("Girl","Typing",0);Assert(sprite.PixelWidth==64&&sprite.PixelHeight==64,"missing sprite uses pixel placeholder");var canvas=new Canvas();var flash=new System.Windows.Shapes.Rectangle();using var effects=new WeatherEffectController(canvas,flash);foreach(var state in Enum.GetValues<WeatherState>())effects.Apply(state,true,true);Assert(effects.ParticleCount>0&&effects.IsAnimating,"rain/snow states enable particles");effects.Apply(WeatherState.HeavyRain,false,false);Assert(effects.ParticleCount==0&&!effects.IsAnimating,"disabled effects stop timer");return Task.CompletedTask;
    }
    private static Task TestActivityStates()
    {
        var cfg=new AppSettings{CatSleepTimeoutSeconds=10,AfkTimeoutMinutes=60};var tracker=new ActivityTracker();var manager=new CompanionStateManager(tracker,cfg);var now=DateTime.UtcNow;
        for(var i=0;i<14;i++)tracker.Keyboard(now.AddMilliseconds(i*80));manager.Tick(now.AddSeconds(1.1));Assert(manager.Girl==GirlState.TypingFast,"fast typing reaction");
        var idle=now.AddSeconds(-15);tracker.Keyboard(idle);tracker.Mouse(idle);manager.Tick();Assert(manager.Girl==GirlState.Idle,"Girl remains idle before IdleSpecial begins");
        var slowTracker=new ActivityTracker();var slowManager=new CompanionStateManager(slowTracker,new AppSettings());slowTracker.Keyboard(DateTime.UtcNow);slowManager.Tick();Assert(slowManager.Girl==GirlState.Typing,"slow typing reaction");
        var mouseTracker=new ActivityTracker();var mouseManager=new CompanionStateManager(mouseTracker,new AppSettings{ReactToKeyboard=false});mouseTracker.Mouse(DateTime.UtcNow);mouseManager.Tick();Assert(mouseManager.Girl==GirlState.Mouse,"mouse activity switches to Mouse");
        var activeTracker=new ActivityTracker();var activeManager=new CompanionStateManager(activeTracker,new AppSettings(),()=>GirlState.Coffee);var activeNow=DateTime.UtcNow;activeTracker.Keyboard(activeNow);activeManager.Tick(activeNow);Assert(activeManager.Activity==UserActivityState.Typing,"keyboard activity is tracked");Assert(activeManager.ForceAction(GirlAction.Coffee)&&activeManager.Action==GirlAction.Coffee,"Coffee overlays Typing");activeTracker.Mouse(activeNow.AddSeconds(1));activeManager.Tick(activeNow.AddSeconds(1));Assert(activeManager.Action==GirlAction.Coffee&&activeManager.Activity==UserActivityState.Mouse,"mouse activity updates while Coffee remains active");activeManager.CompleteAction(activeNow.AddSeconds(1));Assert(activeManager.Girl==GirlState.Mouse,"Coffee resumes the latest activity");return Task.CompletedTask;
    }
    private static Task TestGirlImageCache()
    {
        var root = Path.Combine(AppContext.BaseDirectory, "Assets", "Characters", "Girl_Default");
        var fallback = new SpriteLoader();
        var images = new GirlImageCache(new AssetPackService(new AppSettings()), () => fallback.GetFrame("Girl", "Idle", 0));
        Assert(images.Master.PixelWidth == 1254 && images.Master.PixelHeight == 1254, "MASTER uses fixed 1254x1254 canvas");
        Assert(images.BlinkHalf.PixelWidth == 1254 && images.BlinkHalf.PixelHeight == 1254, "blink_half uses same canvas");
        Assert(images.BlinkClose.PixelWidth == 1254 && images.BlinkClose.PixelHeight == 1254, "blink_close uses same canvas");
        Assert(images.TypingFrames.Length == 4 && images.TypingFrames.All(x => x.PixelWidth == 1254 && x.PixelHeight == 1254), "all four Typing frames use the same canvas");
        Assert(images.MouseFrames.Length == 4 && images.MouseFrames.All(x => x.PixelWidth == 1254 && x.PixelHeight == 1254), "all four Mouse frames use the same canvas");
        Assert(images.CoffeeFrames.Length == 5 && images.CoffeeFrames.All(x => x.PixelWidth == 1254 && x.PixelHeight == 1254), "all five Coffee frames use the same canvas");
        Assert(images.StretchFrames.Length == 4 && images.StretchFrames.All(x => x.PixelWidth == 1254 && x.PixelHeight == 1254), "all four Stretch frames use the same canvas");
        Assert(images.Master.IsFrozen && images.BlinkHalf.IsFrozen && images.BlinkClose.IsFrozen, "all images are frozen");
        Assert(images.TypingFrames.All(x => x.IsFrozen), "all Typing images are frozen");
        Assert(images.MouseFrames.All(x => x.IsFrozen), "all Mouse images are frozen");
        Assert(images.CoffeeFrames.All(x => x.IsFrozen), "all Coffee images are frozen");
        Assert(images.StretchFrames.All(x => x.IsFrozen), "all Stretch images are frozen");
        foreach (var mouse in images.MouseFrames.Skip(1))
        {
            Assert(AlphaAt(mouse, 100, 100) == 0 && AlphaAt(mouse, 1200, 1200) == 0 && AlphaAt(mouse, 220, 250) == 0 && AlphaAt(mouse, 1050, 500) == 0, "opaque checkerboard is transparent around and between Mouse artwork");
            Assert(AlphaAt(mouse, 300, 800) == 255 && AlphaAt(mouse, 1100, 1100) == 255, "Mouse character and laptop remain opaque");
        }
        foreach (var coffee in images.CoffeeFrames.Skip(1))
        {
            Assert(AlphaAt(coffee, 100, 100) == 0 && AlphaAt(coffee, 1200, 1200) == 0, "Coffee checkerboard is transparent outside the artwork");
            Assert(AlphaAt(coffee, 1100, 1100) == 255, "Coffee laptop remains opaque");
        }
        foreach (var stretch in images.StretchFrames)
        {
            Assert(AlphaAt(stretch, 0, 0) == 0 && AlphaAt(stretch, 1253, 1253) == 0, "Stretch background stays transparent at the canvas corners");
            Assert(AlphaAt(stretch, 900, 1000) >= 240, "Stretch laptop remains visible");
        }
        Assert(images.ReconstructedCloseAlpha, "closed image alpha is reconstructed from MASTER in memory");
        var pixels = new byte[images.BlinkClose.PixelWidth * images.BlinkClose.PixelHeight * 4];
        images.BlinkClose.CopyPixels(pixels, images.BlinkClose.PixelWidth * 4, 0);
        Assert(pixels[3] == 0, "closed image is transparent outside the character");
        var paths = Directory.GetFiles(root, "*.png", SearchOption.AllDirectories).Select(x => Path.GetRelativePath(root, x)).ToArray();
        Assert(paths.Length == 20 && paths.Any(x => x.Equals("MASTER.png", StringComparison.OrdinalIgnoreCase)) && paths.Any(x => x.EndsWith("blink_half.png", StringComparison.OrdinalIgnoreCase)) && paths.Any(x => x.EndsWith("blink_close.png", StringComparison.OrdinalIgnoreCase)) && Enumerable.Range(1, 5).All(i => paths.Any(x => x.Equals(Path.Combine("Coffee", $"coffee_{i:00}.png"), StringComparison.OrdinalIgnoreCase))) && Enumerable.Range(1, 4).All(i => paths.Any(x => x.Equals(Path.Combine("Stretch", $"stretch_{i:00}.png"), StringComparison.OrdinalIgnoreCase))), "MASTER, Blink, Typing, Mouse, Coffee, and Stretch image assets are available");
        return Task.CompletedTask;
    }

    private static Task TestAssetPacks()
    {
        var defaults = new AssetPackService(new AppSettings());
        Assert(defaults.GetActiveCharacter()?.Id == "girl_default", "default Character pack is discovered");
        Assert(defaults.GetActivePet()?.Id == "orange_cat", "default Pet pack is discovered");
        Assert(defaults.GetRoomPacks().Any(pack => pack.Id == "default_room"), "default Room pack is discovered");
        Assert(defaults.GetCharacterAnimation("typing").Length == 4 && defaults.GetCharacterAnimation("mouse").Length == 4 &&
               defaults.GetCharacterAnimation("blink").Length == 2 && defaults.GetCharacterAnimation("coffee").Length == 5 &&
               defaults.GetCharacterAnimation("stretch").Length == 4, "all default Character animations load");
        Assert(defaults.GetPetAnimation("tail").Length == 4 && defaults.GetPetAnimation("sleep").Length == 3,
            "all default Pet animations load");
        Assert(ReferenceEquals(defaults.GetCharacterAnimation("typing")[0], defaults.GetCharacterAnimation("typing")[0]),
            "animation images are cached");

        var sourceAssets = Path.Combine(AppContext.BaseDirectory, "Assets");
        var tempRoot = Path.Combine(Path.GetTempPath(), "DeskLofi-pack-test-" + Guid.NewGuid().ToString("N"));
        try
        {
            var characters = Path.Combine(tempRoot, "Characters");
            var girl = Path.Combine(characters, "Girl_Default");
            var boy = Path.Combine(characters, "Boy_Default");
            var invalid = Path.Combine(characters, "BrokenJson");
            var noIdle = Path.Combine(characters, "NoIdle");
            var pet = Path.Combine(tempRoot, "Pets", "SimplePet");
            Directory.CreateDirectory(girl);
            Directory.CreateDirectory(boy);
            Directory.CreateDirectory(invalid);
            Directory.CreateDirectory(noIdle);
            Directory.CreateDirectory(pet);
            File.Copy(Path.Combine(sourceAssets, "Characters", "Girl_Default", "MASTER.png"), Path.Combine(girl, "MASTER.png"));
            File.Copy(Path.Combine(sourceAssets, "Characters", "Girl_Default", "MASTER.png"), Path.Combine(boy, "MASTER.png"));
            File.Copy(Path.Combine(sourceAssets, "Pets", "OrangeCat", "CAT_MASTER.png"), Path.Combine(pet, "CAT_MASTER.png"));
            File.WriteAllText(Path.Combine(girl, "pack.json"), JsonSerializer.Serialize(new
            {
                id = "girl_default", name = "Girl test", animations = new Dictionary<string, string[]>
                {
                    ["idle"] = ["MASTER.png"], ["stretch"] = ["MASTER.png"],
                    ["mouse"] = ["missing.png"]
                }
            }));
            File.WriteAllText(Path.Combine(boy, "pack.json"), JsonSerializer.Serialize(new
            {
                id = "boy_default", name = "Boy test", animations = new Dictionary<string, string[]>
                {
                    ["idle"] = ["MASTER.png"]
                }
            }));
            File.WriteAllText(Path.Combine(pet, "pack.json"), JsonSerializer.Serialize(new
            {
                id = "simple_pet", name = "Simple pet", animations = new Dictionary<string, string[]>
                {
                    ["idle"] = ["CAT_MASTER.png"]
                }
            }));
            File.WriteAllText(Path.Combine(invalid, "pack.json"), "{ invalid json");
            File.WriteAllText(Path.Combine(noIdle, "pack.json"), "{\"id\":\"no_idle\",\"animations\":{\"typing\":[\"missing.png\"]}}");

            var packs = new AssetPackService(new AppSettings
            {
                ActiveCharacterPackId = "deleted_character", ActivePetPackId = "deleted_pet"
            }, tempRoot);
            Assert(packs.GetCharacterPacks().Count == 2 && packs.GetActiveCharacter()?.Id == "girl_default",
                "bad JSON and missing idle are skipped; deleted selection falls back to default Character");
            Assert(packs.GetActivePet()?.Id == "simple_pet", "deleted Pet selection falls back to an available pack");
            Assert(packs.GetCharacterAnimation("coffee").Length == 0 && packs.GetCharacterAnimation("mouse").Length == 0,
                "missing optional animations return empty without borrowing frames");
            Assert(packs.GetPetAnimation("sleep").Length == 0 && packs.GetPetAnimation("tail").Length == 0,
                "missing optional Pet animations return empty");
            var tracker = new ActivityTracker();
            var manager = new CompanionStateManager(tracker, new AppSettings(), () => GirlState.Coffee,
                state => state == GirlState.Stretch && packs.GetCharacterAnimation("stretch").Length > 0);
            manager.Tick(manager.NextActionAllowedAt);
            Assert(manager.Girl == GirlState.Stretch, "idle scheduler skips missing Coffee and selects available Stretch");

            var boyPacks = new AssetPackService(new AppSettings { ActiveCharacterPackId = "boy_default" }, tempRoot);
            Assert(boyPacks.GetActiveCharacter()?.Id == "boy_default" && boyPacks.GetCharacterAnimation("coffee").Length == 0,
                "new Character is discovered by manifest and does not inherit Girl animations");
            var boyStates = new CompanionStateManager(new ActivityTracker(), new AppSettings(), () => GirlState.Coffee,
                state => boyPacks.GetCharacterAnimation(state switch
                {
                    GirlState.Coffee => "coffee", GirlState.Stretch => "stretch", GirlState.Typing => "typing",
                    GirlState.Mouse => "mouse", _ => "idle"
                }).Length > 0);
            boyStates.Tick(boyStates.NextActionAllowedAt);
            Assert(boyStates.Girl == GirlState.Idle, "scheduler stays Idle when the selected Character has no special animation");
        }
        finally
        {
            if (Directory.Exists(tempRoot)) Directory.Delete(tempRoot, recursive: true);
        }
        return Task.CompletedTask;
    }

    private static Task TestCatAnimation()
    {
        var cache = new CatImageCache(new AssetPackService(new AppSettings()));
        Assert(cache.TailFrames.Length == 4, "four Cat tail frames are cached");
        Assert(cache.SleepFrames.Length == 3, "three Cat sleep frames are cached");
        Assert(cache.Master.IsFrozen && cache.TailFrames.All(frame => frame.IsFrozen) && cache.SleepFrames.All(frame => frame.IsFrozen), "Cat sources are frozen and reusable");
        Assert(cache.TailFrames.Concat(cache.SleepFrames).All(frame => frame.PixelWidth == cache.Master.PixelWidth && frame.PixelHeight == cache.Master.PixelHeight), "Cat frames keep the same source canvas");
        Assert(cache.SleepFrames.All(frame => AlphaAt(frame, 0, 0) == 0 && AlphaAt(frame, 1535, 1023) == 0), "checkerboard backgrounds stay transparent across Cat sleep frames");
        Assert(AlphaAt(cache.SleepFrames[1], 768, 512) > 0, "Cat artwork remains visible after sleep-frame background cleanup");

        var machine = new CatStateMachine(DateTime.UtcNow, new Random(7));
        Assert(machine.State == CatState.Idle && machine.TailFrameIndex == -1, "Cat starts on its master image");
        Assert(machine.ForceTailWag() && machine.TailFrameIndex == 0, "debug force starts TailWag at frame 01");
        Assert(!machine.ForceTailWag(), "a force request does not restart an active TailWag");
        var expected = new[] { 0, 1, 2, 3, 2, 1, 0 };
        foreach (var (frame, index) in expected.Select((frame, index) => (frame, index)))
        {
            Assert(machine.State == CatState.TailWag && machine.TailFrameIndex == frame, $"Cat tail sequence frame {index + 1}");
            machine.Advance(TimeSpan.FromMilliseconds(frame == 3 ? 140 : 160));
        }
        Assert(machine.State == CatState.Idle && machine.TailFrameIndex == -1, "Cat returns to master after the reverse frames");

        var scheduled = new CatStateMachine(DateTime.UtcNow, new Random(7));
        for (var second = 0; second < 7; second++) scheduled.Advance(TimeSpan.FromSeconds(1));
        Assert(scheduled.State == CatState.Idle, "Cat does not wag before the minimum idle interval");
        for (var second = 7; second < 20 && scheduled.State == CatState.Idle; second++) scheduled.Advance(TimeSpan.FromSeconds(1));
        Assert(scheduled.State == CatState.TailWag, "Cat schedules a random tail wag within 8–20 seconds");

        var start = DateTime.UtcNow;
        var sleepy = new CatStateMachine(start, new Random(7), 120);
        sleepy.Advance(TimeSpan.FromSeconds(120));
        Assert(sleepy.State == CatState.GoingToSleep && sleepy.SleepFrameIndex == 0, "Cat starts sleep frame 01 after two minutes of inactivity");
        sleepy.Advance(TimeSpan.FromMilliseconds(380));
        Assert(sleepy.State == CatState.GoingToSleep && sleepy.SleepFrameIndex == 1, "Cat advances to sleep frame 02");
        sleepy.Advance(TimeSpan.FromMilliseconds(380));
        Assert(sleepy.State == CatState.GoingToSleep && sleepy.SleepFrameIndex == 2, "Cat advances to sleep frame 03");
        sleepy.Advance(TimeSpan.FromMilliseconds(380));
        sleepy.Advance(TimeSpan.FromSeconds(5));
        Assert(sleepy.State == CatState.Sleeping && sleepy.SleepFrameIndex == 2, "Cat holds sleep frame 03 without looping");
        sleepy.NotifyActivity(true, start.AddSeconds(121));
        Assert(sleepy.State == CatState.WakingUp && sleepy.SleepFrameIndex == 2, "keyboard activity wakes Cat from sleep frame 03");
        sleepy.Advance(TimeSpan.FromMilliseconds(240));
        Assert(sleepy.SleepFrameIndex == 1, "Cat wakes through sleep frame 02");
        sleepy.Advance(TimeSpan.FromMilliseconds(240));
        Assert(sleepy.SleepFrameIndex == 0, "Cat wakes through sleep frame 01");
        sleepy.Advance(TimeSpan.FromMilliseconds(240));
        Assert(sleepy.State == CatState.Idle && sleepy.SleepFrameIndex == -1, "Cat returns to master after waking");

        var interrupted = new CatStateMachine(start, new Random(11), 120);
        Assert(interrupted.ForceSleep(), "debug Force Cat Sleep starts the sleep sequence");
        interrupted.Advance(TimeSpan.FromMilliseconds(380));
        Assert(interrupted.State == CatState.GoingToSleep && interrupted.SleepFrameIndex == 1, "interrupted sleep is currently on frame 02");
        interrupted.NotifyActivity(false, start.AddSeconds(1));
        Assert(interrupted.State == CatState.WakingUp && interrupted.SleepFrameIndex == 1, "mouse activity reverses from the current sleep frame");
        interrupted.Advance(TimeSpan.FromMilliseconds(240));
        Assert(interrupted.SleepFrameIndex == 0, "interrupted sleep reverses through frame 01");
        interrupted.Advance(TimeSpan.FromMilliseconds(240));
        Assert(interrupted.State == CatState.Idle && interrupted.SleepFrameIndex == -1, "interrupted sleep returns safely to master");

        var forced = new CatStateMachine(start, new Random(13), 120);
        forced.ForceSleep();
        forced.Advance(TimeSpan.FromMilliseconds(380));
        forced.Advance(TimeSpan.FromMilliseconds(380));
        forced.Advance(TimeSpan.FromMilliseconds(380));
        Assert(forced.State == CatState.Sleeping && forced.ForceWake(), "debug Force Cat Wake starts from the sleeping pose");
        forced.Advance(TimeSpan.FromMilliseconds(240));
        forced.Advance(TimeSpan.FromMilliseconds(240));
        forced.Advance(TimeSpan.FromMilliseconds(240));
        Assert(forced.State == CatState.Idle && forced.SleepFrameIndex == -1, "debug Force Cat Wake finishes at CAT_MASTER");
        return Task.CompletedTask;
    }

    private static Task TestWindowCatTyping()
    {
        var settings = new SettingsService();
        settings.Current.EnableBlink = false; settings.Current.ShowOnTaskbar = false; settings.Current.AlwaysOnTop = false;
        var host = new DeskLofi.Views.MainWindow(settings) { Opacity = 0 };
        try
        {
            var fields = BindingFlags.NonPublic | BindingFlags.Instance;
            var girl = (System.Windows.Controls.Image)typeof(DeskLofi.Views.MainWindow).GetField("GirlSprite", fields)!.GetValue(host)!;
            var cat = (System.Windows.Controls.Image)typeof(DeskLofi.Views.MainWindow).GetField("CatSprite", fields)!.GetValue(host)!;
            var catImages = (CatImageCache)typeof(DeskLofi.Views.MainWindow).GetField("_catImages", fields)!.GetValue(host)!;
            var catStates = (CatStateMachine)typeof(DeskLofi.Views.MainWindow).GetField("_catStates", fields)!.GetValue(host)!;
            var activity = (ActivityTracker)typeof(DeskLofi.Views.MainWindow).GetField("_activity", fields)!.GetValue(host)!;
            var states = (CompanionStateManager)typeof(DeskLofi.Views.MainWindow).GetField("_states", fields)!.GetValue(host)!;
            var timer = (System.Windows.Threading.DispatcherTimer)typeof(DeskLofi.Views.MainWindow).GetField("_timer", fields)!.GetValue(host)!;
            var advanceGirl = typeof(DeskLofi.Views.MainWindow).GetMethod("AdvanceGirlAnimation", fields)!;
            var render = typeof(DeskLofi.Views.MainWindow).GetMethod("Render", fields | BindingFlags.DeclaredOnly)!;

            host.Show(); timer.Stop();
            catStates.ForceTailWag();
            var now = DateTime.UtcNow;
            activity.Keyboard(now);
            states.Tick(now);
            advanceGirl.Invoke(host, [TimeSpan.FromMilliseconds(20)]);
            catStates.Advance(TimeSpan.FromMilliseconds(160));
            render.Invoke(host, null);
            Assert(states.Girl == GirlState.Typing && ReferenceEquals(girl.Source, ((GirlImageCache)typeof(DeskLofi.Views.MainWindow).GetField("_girlImages", fields)!.GetValue(host)!).TypingFrames[0]), "Girl enters Typing while Cat continues independently");
            Assert(catStates.State == CatState.TailWag && ReferenceEquals(cat.Source, catImages.TailFrames[1]), "Cat advances to tail frame 02 during Girl Typing");
            return Task.CompletedTask;
        }
        finally { host.Dispose(); host.Close(); }
    }

    private static Task TestWindowCatSleepRendering()
    {
        var settings = new SettingsService();
        settings.Current.EnableBlink = false; settings.Current.ShowOnTaskbar = false; settings.Current.AlwaysOnTop = false;
        var host = new DeskLofi.Views.MainWindow(settings) { Opacity = 0 };
        try
        {
            var fields = BindingFlags.NonPublic | BindingFlags.Instance;
            var girl = (System.Windows.Controls.Image)typeof(DeskLofi.Views.MainWindow).GetField("GirlSprite", fields)!.GetValue(host)!;
            var cat = (System.Windows.Controls.Image)typeof(DeskLofi.Views.MainWindow).GetField("CatSprite", fields)!.GetValue(host)!;
            var catImages = (CatImageCache)typeof(DeskLofi.Views.MainWindow).GetField("_catImages", fields)!.GetValue(host)!;
            var catStates = (CatStateMachine)typeof(DeskLofi.Views.MainWindow).GetField("_catStates", fields)!.GetValue(host)!;
            var activity = (ActivityTracker)typeof(DeskLofi.Views.MainWindow).GetField("_activity", fields)!.GetValue(host)!;
            var states = (CompanionStateManager)typeof(DeskLofi.Views.MainWindow).GetField("_states", fields)!.GetValue(host)!;
            var timer = (System.Windows.Threading.DispatcherTimer)typeof(DeskLofi.Views.MainWindow).GetField("_timer", fields)!.GetValue(host)!;
            var render = typeof(DeskLofi.Views.MainWindow).GetMethod("Render", fields | BindingFlags.DeclaredOnly)!;
            var position = new System.Windows.Point(System.Windows.Controls.Canvas.GetLeft(cat), System.Windows.Controls.Canvas.GetTop(cat));
            var size = new System.Windows.Size(cat.Width, cat.Height);
            Assert(System.Windows.Media.RenderOptions.GetBitmapScalingMode(cat) == System.Windows.Media.BitmapScalingMode.NearestNeighbor, "Cat retains pixel-art nearest-neighbor rendering");
            void Step(int milliseconds) { catStates.Advance(TimeSpan.FromMilliseconds(milliseconds)); render.Invoke(host, null); }

            host.Show(); timer.Stop();
            Assert(catStates.ForceSleep(), "debug sleep preview can start from Idle");
            render.Invoke(host, null);
            Assert(ReferenceEquals(cat.Source, catImages.SleepFrames[0]), "sleep entry renders frame 01");
            Step(380);
            Assert(ReferenceEquals(cat.Source, catImages.SleepFrames[1]), "sleep entry renders frame 02");
            Step(380);
            Assert(ReferenceEquals(cat.Source, catImages.SleepFrames[2]), "sleep entry renders frame 03");
            Step(380);
            Assert(catStates.State == CatState.Sleeping && ReferenceEquals(cat.Source, catImages.SleepFrames[2]), "Cat holds sleep frame 03");

            var now = DateTime.UtcNow;
            activity.Keyboard(now);
            catStates.NotifyActivity(true, now);
            states.Tick(now);
            render.Invoke(host, null);
            Assert(states.Girl == GirlState.Typing && catStates.State == CatState.WakingUp, "Girl Typing and Cat WakingUp run independently");
            Assert(ReferenceEquals(cat.Source, catImages.SleepFrames[2]), "wake begins from the displayed sleep frame 03");
            Step(240);
            Assert(ReferenceEquals(cat.Source, catImages.SleepFrames[1]), "wake renders frame 02");
            Step(240);
            Assert(ReferenceEquals(cat.Source, catImages.SleepFrames[0]), "wake renders frame 01");
            Step(240);
            Assert(catStates.State == CatState.Idle && ReferenceEquals(cat.Source, catImages.Master), "wake returns to CAT_MASTER");
            Assert(new System.Windows.Point(System.Windows.Controls.Canvas.GetLeft(cat), System.Windows.Controls.Canvas.GetTop(cat)) == position && new System.Windows.Size(cat.Width, cat.Height) == size, "Cat retains its position and render size across sleep frames");
            return Task.CompletedTask;
        }
        finally { host.Dispose(); host.Close(); }
    }

    private static byte AlphaAt(System.Windows.Media.Imaging.BitmapSource frame, int x, int y)
    {
        var bgra = new System.Windows.Media.Imaging.FormatConvertedBitmap(frame, System.Windows.Media.PixelFormats.Bgra32, null, 0);
        var pixel = new byte[4];
        bgra.CopyPixels(new System.Windows.Int32Rect(x, y, 1, 1), pixel, 4, 0);
        return pixel[3];
    }

    private static Task TestWindowMouseRendering()
    {
        var settings = new SettingsService();
        settings.Current.EnableBlink = true; settings.Current.MinBlinkIntervalSeconds = 30; settings.Current.MaxBlinkIntervalSeconds = 30;
        settings.Current.MouseIdleDelayMs = 900; settings.Current.ShowOnTaskbar = false; settings.Current.AlwaysOnTop = false;
        var host = new DeskLofi.Views.MainWindow(settings) { Opacity = 0 };
        try
        {
            var fields = BindingFlags.NonPublic | BindingFlags.Instance;
            var sprite = (System.Windows.Controls.Image)typeof(DeskLofi.Views.MainWindow).GetField("GirlSprite", fields)!.GetValue(host)!;
            var images = (GirlImageCache)typeof(DeskLofi.Views.MainWindow).GetField("_girlImages", fields)!.GetValue(host)!;
            var onMouse = typeof(DeskLofi.Views.MainWindow).GetMethod("OnMouse", fields)!;
            var observed = new List<System.Windows.Media.ImageSource>(); var started = System.Diagnostics.Stopwatch.StartNew(); var repeated = false;
            var frame = new System.Windows.Threading.DispatcherFrame(); var sampler = new System.Windows.Threading.DispatcherTimer { Interval = TimeSpan.FromMilliseconds(10) };
            sampler.Tick += (_, _) =>
            {
                if (sprite.Source is { } source && (observed.Count == 0 || !ReferenceEquals(observed[^1], source))) observed.Add(source);
                if (!repeated && started.Elapsed >= TimeSpan.FromMilliseconds(400)) { repeated = true; onMouse.Invoke(host, [DateTime.UtcNow]); }
                if (started.Elapsed >= TimeSpan.FromMilliseconds(1800)) { sampler.Stop(); frame.Continue = false; }
            };
            host.Show(); onMouse.Invoke(host, [DateTime.UtcNow]); sampler.Start(); System.Windows.Threading.Dispatcher.PushFrame(frame);
            var sequence = observed.Where(source => images.MouseFrames.Any(mouse => ReferenceEquals(mouse, source))).Select(source => Array.FindIndex(images.MouseFrames, mouse => ReferenceEquals(mouse, source))).ToArray();
            Assert(sequence.Take(3).SequenceEqual([0, 1, 2]), $"Mouse enters through 01-02-03; observed {string.Join(',', sequence)}");
            Assert(sequence.Contains(3) && sequence.Zip(sequence.Skip(1)).Any(pair => pair.First == 3 && pair.Second == 2), $"Mouse active loops 03/04 without restarting on movement; observed {string.Join(',', sequence)}");
            Assert(ReferenceEquals(sprite.Source, images.Master), "Mouse returns to MASTER after the 900 ms idle delay and leaving frames");
            var blink = (BlinkController)typeof(DeskLofi.Views.MainWindow).GetField("_blink", fields)!.GetValue(host)!;
            Assert(blink.NextBlinkInSeconds is not null, "Blink is scheduled again after Mouse ends");
            return Task.CompletedTask;
        }
        finally { host.Dispose(); host.Close(); }
    }

    private static Task TestTypingInterruptsMouse()
    {
        var settings = new SettingsService(); settings.Current.EnableBlink = false; settings.Current.MouseIdleDelayMs = 900; settings.Current.TypingIdleDelayMs = 700;
        settings.Current.ShowOnTaskbar = false; settings.Current.AlwaysOnTop = false;
        var host = new DeskLofi.Views.MainWindow(settings) { Opacity = 0 };
        try
        {
            var fields = BindingFlags.NonPublic | BindingFlags.Instance;
            var sprite = (System.Windows.Controls.Image)typeof(DeskLofi.Views.MainWindow).GetField("GirlSprite", fields)!.GetValue(host)!;
            var images = (GirlImageCache)typeof(DeskLofi.Views.MainWindow).GetField("_girlImages", fields)!.GetValue(host)!;
            var onMouse = typeof(DeskLofi.Views.MainWindow).GetMethod("OnMouse", fields)!; var onKeyboard = typeof(DeskLofi.Views.MainWindow).GetMethod("OnKeyboard", fields)!;
            var sawTyping = false; var started = System.Diagnostics.Stopwatch.StartNew(); var frame = new System.Windows.Threading.DispatcherFrame();
            var sampler = new System.Windows.Threading.DispatcherTimer { Interval = TimeSpan.FromMilliseconds(10) };
            sampler.Tick += (_, _) =>
            {
                if (images.TypingFrames.Any(typing => ReferenceEquals(typing, sprite.Source))) sawTyping = true;
                if (started.Elapsed >= TimeSpan.FromMilliseconds(450) && started.Elapsed < TimeSpan.FromMilliseconds(480)) onKeyboard.Invoke(host, [DateTime.UtcNow]);
                if (started.Elapsed >= TimeSpan.FromMilliseconds(1450)) { sampler.Stop(); frame.Continue = false; }
            };
            host.Show(); onMouse.Invoke(host, [DateTime.UtcNow]); sampler.Start(); System.Windows.Threading.Dispatcher.PushFrame(frame);
            Assert(sawTyping, "keyboard activity takes priority after the MouseLeaving frames");
            return Task.CompletedTask;
        }
        finally { host.Dispose(); host.Close(); }
    }

    private static Task TestBlinkController()
    {
        var settings = new AppSettings { EnableBlink = true, MinBlinkIntervalSeconds = 3, MaxBlinkIntervalSeconds = 7 };
        var blink = new BlinkController(settings, new Random(42));
        Assert(blink.Frame == BlinkFrame.Open && !blink.IsBlinking, "blink starts open");
        blink.SetCanBlink(true);
        Assert(blink.NextBlinkInSeconds is >= 3 and <= 7, "idle blink interval is 3–7 seconds");
        blink.Advance(TimeSpan.FromSeconds(blink.NextBlinkInSeconds!.Value));
        Assert(blink.Frame == BlinkFrame.Half, "scheduled blink starts half closed");
        blink.Advance(TimeSpan.FromMilliseconds(60)); Assert(blink.Frame == BlinkFrame.Closed, "half lasts 60 ms");
        blink.Advance(TimeSpan.FromMilliseconds(80)); Assert(blink.Frame == BlinkFrame.Half, "closed lasts 80 ms");
        blink.Advance(TimeSpan.FromMilliseconds(60)); Assert(blink.Frame == BlinkFrame.Open && !blink.IsBlinking, "blink returns open after 60 ms half frame");
        blink.BlinkNow(); Assert(blink.Frame == BlinkFrame.Half, "debug blink starts immediately");
        blink.SetCanBlink(false); Assert(blink.Frame == BlinkFrame.Open && !blink.IsBlinking && blink.NextBlinkInSeconds is null, "activity cancels and restores open immediately");
        blink.Advance(TimeSpan.FromSeconds(8)); Assert(blink.Frame == BlinkFrame.Open, "blink stays disabled during activity");
        blink.SetCanBlink(true); Assert(blink.Frame == BlinkFrame.Open && blink.NextBlinkInSeconds is >= 3 and <= 7, "idle resume schedules a fresh blink");
        blink.Dispose(); Assert(blink.Frame == BlinkFrame.Open && !blink.BlinkNow(), "dispose stops blink");
        var disabled = new BlinkController(new AppSettings { EnableBlink = false }, new Random(1));
        disabled.SetCanBlink(true); Assert(disabled.NextBlinkInSeconds is null && !disabled.BlinkNow(), "setting disables blink");
        return Task.CompletedTask;
    }
    private static Task TestWindowBlinkRendering()
    {
        var settings = new SettingsService();
        settings.Current.EnableBlink = true;
        settings.Current.MinBlinkIntervalSeconds = 1;
        settings.Current.MaxBlinkIntervalSeconds = 1;
        settings.Current.TypingIdleDelayMs = 0;
        settings.Current.MouseIdleDelayMs = 0;
        settings.Current.ShowOnTaskbar = false;
        settings.Current.AlwaysOnTop = false;
        var host = new DeskLofi.Views.MainWindow(settings) { Opacity = 0 };
        try
        {
            var sprite = (System.Windows.Controls.Image)typeof(DeskLofi.Views.MainWindow).GetField("GirlSprite", BindingFlags.NonPublic | BindingFlags.Instance)!.GetValue(host)!;
            var observed = new HashSet<System.Windows.Media.ImageSource>();
            var started = System.Diagnostics.Stopwatch.StartNew();
            var frame = new System.Windows.Threading.DispatcherFrame();
            var sampler = new System.Windows.Threading.DispatcherTimer { Interval = TimeSpan.FromMilliseconds(10) };
            sampler.Tick += (_, _) =>
            {
                if (sprite.Source is { } source) observed.Add(source);
                if (started.Elapsed >= TimeSpan.FromSeconds(2.5)) { sampler.Stop(); frame.Continue = false; }
            };
            host.Show();
            sampler.Start();
            System.Windows.Threading.Dispatcher.PushFrame(frame);
            Assert(observed.Count >= 3, $"real WPF timer renders MASTER, HALF and CLOSE; observed {observed.Count} distinct image sources");
            return Task.CompletedTask;
        }
        finally { host.Dispose(); host.Close(); }
    }
    private static Task TestWindowTypingRendering()
    {
        var settings = new SettingsService();
        settings.Current.EnableBlink = true;
        settings.Current.MinBlinkIntervalSeconds = 30;
        settings.Current.MaxBlinkIntervalSeconds = 30;
        settings.Current.TypingIdleDelayMs = 700;
        settings.Current.ReactToMouse = false;
        settings.Current.ShowOnTaskbar = false;
        settings.Current.AlwaysOnTop = false;
        var host = new DeskLofi.Views.MainWindow(settings) { Opacity = 0 };
        try
        {
            var fields = BindingFlags.NonPublic | BindingFlags.Instance;
            var sprite = (System.Windows.Controls.Image)typeof(DeskLofi.Views.MainWindow).GetField("GirlSprite", fields)!.GetValue(host)!;
            var images = (GirlImageCache)typeof(DeskLofi.Views.MainWindow).GetField("_girlImages", fields)!.GetValue(host)!;
            var onKeyboard = typeof(DeskLofi.Views.MainWindow).GetMethod("OnKeyboard", fields)!;
            var observed = new List<System.Windows.Media.ImageSource>();
            var started = System.Diagnostics.Stopwatch.StartNew();
            var sentSecondKey = false;
            var frame = new System.Windows.Threading.DispatcherFrame();
            var sampler = new System.Windows.Threading.DispatcherTimer { Interval = TimeSpan.FromMilliseconds(10) };
            sampler.Tick += (_, _) =>
            {
                if (sprite.Source is { } source && (observed.Count == 0 || !ReferenceEquals(observed[^1], source))) observed.Add(source);
                if (!sentSecondKey && started.Elapsed >= TimeSpan.FromMilliseconds(320))
                {
                    sentSecondKey = true;
                    onKeyboard.Invoke(host, [DateTime.UtcNow]);
                }
                if (started.Elapsed >= TimeSpan.FromMilliseconds(1700)) { sampler.Stop(); frame.Continue = false; }
            };
            host.Show();
            onKeyboard.Invoke(host, [DateTime.UtcNow]);
            sampler.Start();
            System.Windows.Threading.Dispatcher.PushFrame(frame);
            var sequence = observed
                .Where(source => images.TypingFrames.Any(typing => ReferenceEquals(typing, source)))
                .Select(source => Array.FindIndex(images.TypingFrames, typing => ReferenceEquals(typing, source)))
                .ToArray();
            Assert(sequence.Take(5).SequenceEqual([0, 1, 2, 3, 0]), $"Typing keeps looping through 01-02-03-04 after another key; observed {string.Join(',', sequence)}");
            Assert(ReferenceEquals(sprite.Source, images.Master), "Typing returns to MASTER after the 700 ms idle timeout");
            var blink = (BlinkController)typeof(DeskLofi.Views.MainWindow).GetField("_blink", fields)!.GetValue(host)!;
            Assert(blink.NextBlinkInSeconds is not null, "Blink is scheduled again after Typing ends");
            return Task.CompletedTask;
        }
        finally { host.Dispose(); host.Close(); }
    }
    private static Task TestCoffeeStates()
    {
        var start = DateTime.UtcNow;
        var activity = new ActivityTracker();
        var states = new CompanionStateManager(activity, new AppSettings(), () => GirlState.Coffee,
            random: new Random(3), startedAt: start);
        var due = states.NextActionAllowedAt;
        Assert(due - start >= TimeSpan.FromSeconds(CompanionStateManager.FirstActionMinSeconds) &&
               due - start <= TimeSpan.FromSeconds(CompanionStateManager.FirstActionMaxSeconds), "first action waits 10–20 seconds");
        states.Tick(due.AddTicks(-1));
        Assert(states.Action == GirlAction.None, "special action does not start early");
        activity.Keyboard(due);
        states.Tick(due);
        Assert(states.Activity == UserActivityState.Typing && states.Action == GirlAction.Coffee,
            "Typing and Coffee coexist");
        Assert(!states.ForceAction(GirlAction.Stretch), "Stretch cannot interrupt Coffee");
        activity.Keyboard(due.AddSeconds(1));
        states.Tick(due.AddSeconds(1));
        Assert(states.Activity == UserActivityState.Typing && states.Action == GirlAction.Coffee,
            "keyboard input keeps tracking Typing without cancelling Coffee");
        states.CompleteAction(due.AddSeconds(1));
        Assert(states.Action == GirlAction.None && states.Girl == GirlState.Typing,
            "Coffee returns to current Typing activity");
        Assert(states.NextCoffeeAt - due.AddSeconds(1) >= TimeSpan.FromSeconds(CompanionStateManager.CoffeeMinSeconds) &&
               states.NextCoffeeAt - due.AddSeconds(1) <= TimeSpan.FromSeconds(CompanionStateManager.CoffeeMaxSeconds),
            "Coffee is rescheduled in 20–40 seconds");
        return Task.CompletedTask;
    }

    private static Task TestIdleSpecialSelection()
    {
        var start = DateTime.UtcNow;
        var missingCoffee = new CompanionStateManager(new ActivityTracker(), new AppSettings(),
            () => GirlState.Coffee, state => state == GirlState.Stretch, new Random(4), start);
        missingCoffee.Tick(missingCoffee.NextActionAllowedAt);
        Assert(missingCoffee.Action == GirlAction.Stretch, "missing Coffee is skipped in favor of available Stretch");
        var noSpecials = new CompanionStateManager(new ActivityTracker(), new AppSettings(),
            () => GirlState.Coffee, state => state is GirlState.Typing or GirlState.Mouse, new Random(4), start);
        noSpecials.Tick(noSpecials.NextActionAllowedAt.AddHours(1));
        Assert(noSpecials.Action == GirlAction.None, "missing Coffee and Stretch remain idle");

        var rapid = new ActivityTracker();
        var delayed = new CompanionStateManager(rapid, new AppSettings(), () => GirlState.Coffee,
            random: new Random(6), startedAt: start);
        var due = delayed.NextActionAllowedAt;
        for (var i = 0; i < 16; i++) rapid.Keyboard(due.AddMilliseconds(i * 30 - 450));
        delayed.Tick(due);
        Assert(delayed.Girl == GirlState.TypingFast && delayed.Action == GirlAction.None,
            "very fast typing postpones a due action briefly");
        var later = due.AddSeconds(9);
        delayed.Tick(later);
        Assert(delayed.Action == GirlAction.Coffee, "fast typing never postpones an action beyond eight seconds");
        return Task.CompletedTask;
    }

    private static Task TestDefaultIdleSpecialSelection()
    {
        var start = DateTime.UtcNow;
        var states = new CompanionStateManager(new ActivityTracker(), new AppSettings(), () => GirlState.Stretch,
            random: new Random(9), startedAt: start);
        var first = states.NextActionAllowedAt;
        states.Tick(first);
        Assert(states.Action == GirlAction.Stretch, "Stretch can be the first selected action");
        states.Tick(first.AddHours(1));
        Assert(states.Action == GirlAction.Stretch, "Coffee cannot enter while Stretch is active");
        states.CompleteAction(first.AddSeconds(3));
        var gap = states.NextActionAllowedAt - first.AddSeconds(3);
        Assert(gap >= TimeSpan.FromSeconds(CompanionStateManager.ActionGapMinSeconds) &&
               gap <= TimeSpan.FromSeconds(CompanionStateManager.ActionGapMaxSeconds), "actions have a 5–10 second gap");
        Assert(states.NextStretchAt - first.AddSeconds(3) >= TimeSpan.FromSeconds(CompanionStateManager.StretchMinSeconds) &&
               states.NextStretchAt - first.AddSeconds(3) <= TimeSpan.FromSeconds(CompanionStateManager.StretchMaxSeconds),
            "Stretch is rescheduled in 30–60 seconds");
        states.Tick(states.NextActionAllowedAt.AddTicks(-1));
        Assert(states.Action == GirlAction.None, "Coffee waits until the shared action gap ends");
        states.Tick(states.NextActionAllowedAt);
        Assert(states.Action == GirlAction.Coffee, "overdue Coffee starts after the gap");
        return Task.CompletedTask;
    }

    private static Task TestWindowCoffeeRendering()
    {
        var settings = new SettingsService();
        settings.Current.EnableBlink = true; settings.Current.MinBlinkIntervalSeconds = 30; settings.Current.MaxBlinkIntervalSeconds = 30;
        settings.Current.ShowOnTaskbar = false; settings.Current.AlwaysOnTop = false;
        var host = new DeskLofi.Views.MainWindow(settings, () => GirlState.Coffee) { Opacity = 0 };
        try
        {
            var fields = BindingFlags.NonPublic | BindingFlags.Instance;
            var sprite = (System.Windows.Controls.Image)typeof(DeskLofi.Views.MainWindow).GetField("GirlSprite", fields)!.GetValue(host)!;
            var images = (GirlImageCache)typeof(DeskLofi.Views.MainWindow).GetField("_girlImages", fields)!.GetValue(host)!;
            var states = (CompanionStateManager)typeof(DeskLofi.Views.MainWindow).GetField("_states", fields)!.GetValue(host)!;
            var timer = (System.Windows.Threading.DispatcherTimer)typeof(DeskLofi.Views.MainWindow).GetField("_timer", fields)!.GetValue(host)!;
            var advance = typeof(DeskLofi.Views.MainWindow).GetMethod("AdvanceGirlAnimation", fields)!;
            var render = typeof(DeskLofi.Views.MainWindow).GetMethod("Render", fields | BindingFlags.DeclaredOnly)!;
            void Step(int milliseconds) { advance.Invoke(host, [TimeSpan.FromMilliseconds(milliseconds)]); render.Invoke(host, null); }
            void Expect(int frame) => Assert(ReferenceEquals(sprite.Source, images.CoffeeFrames[frame]), $"Coffee displays frame {frame + 1:00}");

            host.Show(); timer.Stop();
            Assert(states.ForceAction(GirlAction.Coffee), "Force Coffee uses the scheduled action pipeline");
            Assert(states.Girl == GirlState.Coffee, "WPF host enters Coffee while idle");
            Expect(0);
            for (var frame = 1; frame < images.CoffeeFrames.Length; frame++) { Step(160); Expect(frame); }
            Step(650); Expect(4);
            Step(60); Expect(3);
            for (var frame = 2; frame >= 0; frame--) { Step(160); Expect(frame); }
            Step(160);
            Assert(ReferenceEquals(sprite.Source, images.Master) && states.Girl == GirlState.Idle, "Coffee returns to MASTER and Idle after the reverse frames");
            var blink = (BlinkController)typeof(DeskLofi.Views.MainWindow).GetField("_blink", fields)!.GetValue(host)!;
            Assert(blink.NextBlinkInSeconds is not null, "Blink is scheduled again after Coffee ends");
            return Task.CompletedTask;
        }
        finally { host.Dispose(); host.Close(); }
    }

    private static Task TestWindowCoffeeInterruption()
    {
        VerifyActionOverlay(GirlAction.Coffee, UserActivityState.Typing, UserActivityState.Typing);
        VerifyActionOverlay(GirlAction.Coffee, UserActivityState.Mouse, UserActivityState.Mouse);
        VerifyActionOverlay(GirlAction.Coffee, UserActivityState.Typing, UserActivityState.Mouse);
        VerifyActionOverlay(GirlAction.Coffee, UserActivityState.Idle, UserActivityState.Idle, sleepingCat: true);
        return Task.CompletedTask;
    }

    private static Task TestWindowStretchRendering()
    {
        var settings = new SettingsService();
        settings.Current.EnableBlink = true; settings.Current.MinBlinkIntervalSeconds = 30; settings.Current.MaxBlinkIntervalSeconds = 30;
        settings.Current.ShowOnTaskbar = false; settings.Current.AlwaysOnTop = false;
        var host = new DeskLofi.Views.MainWindow(settings, () => GirlState.Stretch) { Opacity = 0 };
        try
        {
            var fields = BindingFlags.NonPublic | BindingFlags.Instance;
            var sprite = (System.Windows.Controls.Image)typeof(DeskLofi.Views.MainWindow).GetField("GirlSprite", fields)!.GetValue(host)!;
            var images = (GirlImageCache)typeof(DeskLofi.Views.MainWindow).GetField("_girlImages", fields)!.GetValue(host)!;
            var states = (CompanionStateManager)typeof(DeskLofi.Views.MainWindow).GetField("_states", fields)!.GetValue(host)!;
            var timer = (System.Windows.Threading.DispatcherTimer)typeof(DeskLofi.Views.MainWindow).GetField("_timer", fields)!.GetValue(host)!;
            var advance = typeof(DeskLofi.Views.MainWindow).GetMethod("AdvanceGirlAnimation", fields)!;
            var render = typeof(DeskLofi.Views.MainWindow).GetMethod("Render", fields | BindingFlags.DeclaredOnly)!;
            void Step(int milliseconds) { advance.Invoke(host, [TimeSpan.FromMilliseconds(milliseconds)]); render.Invoke(host, null); }
            void Expect(int frame) => Assert(ReferenceEquals(sprite.Source, images.StretchFrames[frame]), $"Stretch displays frame {frame + 1:00}");

            host.Show(); timer.Stop();
            Assert(states.ForceAction(GirlAction.Stretch), "Force Stretch uses the scheduled action pipeline");
            Assert(states.Girl == GirlState.Stretch, "WPF host enters Stretch while idle");
            Expect(0);
            for (var frame = 1; frame < images.StretchFrames.Length; frame++) { Step(180); Expect(frame); }
            Step(760); Expect(3);
            Step(50); Expect(2);
            for (var frame = 1; frame >= 0; frame--) { Step(180); Expect(frame); }
            Step(180);
            Assert(ReferenceEquals(sprite.Source, images.Master) && states.Girl == GirlState.Idle, "Stretch returns to MASTER and Idle after the reverse frames");
            var blink = (BlinkController)typeof(DeskLofi.Views.MainWindow).GetField("_blink", fields)!.GetValue(host)!;
            Assert(blink.NextBlinkInSeconds is not null, "Blink is scheduled again after Stretch ends");
            return Task.CompletedTask;
        }
        finally { host.Dispose(); host.Close(); }
    }

    private static Task TestWindowStretchInterruption()
    {
        VerifyActionOverlay(GirlAction.Stretch, UserActivityState.Typing, UserActivityState.Typing);
        return Task.CompletedTask;
    }

    private static void VerifyActionOverlay(GirlAction action, UserActivityState initial,
        UserActivityState final, bool sleepingCat = false)
    {
        var settings = new SettingsService();
        settings.Current.EnableBlink = false;
        settings.Current.ShowOnTaskbar = false;
        settings.Current.AlwaysOnTop = false;
        settings.Current.KeyboardPriorityWindowMs = 0;
        var host = new DeskLofi.Views.MainWindow(settings) { Opacity = 0 };
        try
        {
            var fields = BindingFlags.NonPublic | BindingFlags.Instance;
            var type = typeof(DeskLofi.Views.MainWindow);
            var sprite = (System.Windows.Controls.Image)type.GetField("GirlSprite", fields)!.GetValue(host)!;
            var images = (GirlImageCache)type.GetField("_girlImages", fields)!.GetValue(host)!;
            var states = (CompanionStateManager)type.GetField("_states", fields)!.GetValue(host)!;
            var tracker = (ActivityTracker)type.GetField("_activity", fields)!.GetValue(host)!;
            var cat = (CatStateMachine)type.GetField("_catStates", fields)!.GetValue(host)!;
            var timer = (System.Windows.Threading.DispatcherTimer)type.GetField("_timer", fields)!.GetValue(host)!;
            var advance = type.GetMethod("AdvanceGirlAnimation", fields)!;
            var render = type.GetMethod("Render", fields | BindingFlags.DeclaredOnly)!;
            void Step(int milliseconds) { advance.Invoke(host, [TimeSpan.FromMilliseconds(milliseconds)]); render.Invoke(host, null); }

            host.Show(); timer.Stop();
            var now = DateTime.UtcNow;
            if (initial == UserActivityState.Typing) tracker.Keyboard(now);
            else if (initial == UserActivityState.Mouse) tracker.Mouse(now);
            states.Tick(now);
            Assert(states.Activity == initial, "initial user activity is tracked");
            if (sleepingCat)
            {
                Assert(cat.ForceSleep(), "Cat starts sleeping independently");
                cat.Advance(TimeSpan.FromSeconds(2));
                Assert(cat.State == CatState.Sleeping, "Cat reaches Sleeping before Girl action");
            }
            Assert(states.ForceAction(action), $"Force {action} enters the normal action pipeline");
            Assert(!states.ForceAction(action == GirlAction.Coffee ? GirlAction.Stretch : GirlAction.Coffee),
                "a second action cannot interrupt the first");
            Step(action == GirlAction.Coffee ? 320 : 360);
            var middleFrame = action == GirlAction.Coffee ? images.CoffeeFrames[2] : images.StretchFrames[2];
            Assert(ReferenceEquals(sprite.Source, middleFrame), "the action continues to its middle frame");

            now = DateTime.UtcNow;
            if (final == UserActivityState.Typing) tracker.Keyboard(now);
            else if (final == UserActivityState.Mouse) tracker.Mouse(now.AddMilliseconds(10));
            states.Tick(final == UserActivityState.Mouse ? now.AddMilliseconds(10) : now);
            Assert(states.Action == action && states.Activity == final && ReferenceEquals(sprite.Source, middleFrame),
                "input updates activity without cancelling the visible action");
            if (sleepingCat) Assert(cat.State == CatState.Sleeping, "Girl Coffee does not wake Cat");

            Step(10000);
            Assert(states.Action == GirlAction.None && states.Activity == final, "the action completes and retains the latest activity");
            var expected = final switch
            {
                UserActivityState.Typing => images.TypingFrames[0],
                UserActivityState.Mouse => images.MouseFrames[0],
                _ => images.Master
            };
            Assert(ReferenceEquals(sprite.Source, expected), $"{action} returns directly to {final}");
            if (sleepingCat) Assert(cat.State == CatState.Sleeping, "Cat remains asleep after Girl Coffee");
        }
        finally { host.Dispose(); host.Close(); }
    }
    private static Task TestMouseTransitions()
    {
        var settings = new AppSettings { CatSleepTimeoutSeconds = 3600, TypingIdleDelayMs = 1200, MouseIdleDelayMs = 900, KeyboardPriorityWindowMs = 650 };
        var activity = new ActivityTracker();
        var states = new CompanionStateManager(activity, settings);
        var at = DateTime.UtcNow;
        states.Tick(at);
        Assert(states.Girl == GirlState.Idle, "starts Idle");
        activity.Mouse(at.AddMilliseconds(100));
        states.Tick(at.AddMilliseconds(100));
        Assert(states.Girl == GirlState.Mouse, "Idle to Mouse immediately after activity");
        activity.Mouse(at.AddMilliseconds(700));
        states.Tick(at.AddMilliseconds(700));
        Assert(states.Girl == GirlState.Mouse, "continued mouse activity stays Mouse");
        states.Tick(at.AddMilliseconds(1601));
        Assert(states.Girl == GirlState.Idle, "Mouse returns to Idle after 900 ms");
        activity.Keyboard(at.AddMilliseconds(1700));
        states.Tick(at.AddMilliseconds(1700));
        Assert(states.Girl == GirlState.Typing, "keyboard interrupts Idle immediately");
        activity.Mouse(at.AddMilliseconds(1800));
        states.Tick(at.AddMilliseconds(1800));
        Assert(states.Girl == GirlState.Typing, "mouse cannot steal state within keyboard priority window");
        activity.Mouse(at.AddMilliseconds(2400));
        states.Tick(at.AddMilliseconds(2400));
        Assert(states.Girl == GirlState.Mouse, "Typing moves directly to Mouse after keyboard priority expires");
        activity.Keyboard(at.AddMilliseconds(2410));
        states.Tick(at.AddMilliseconds(2410));
        Assert(states.Girl == GirlState.Typing, "Mouse to Typing interrupts immediately");
        for (var i = 1; i <= 14; i++) activity.Keyboard(at.AddMilliseconds(2410 + i * 60));
        states.Tick(at.AddMilliseconds(3250));
        Assert(states.Girl == GirlState.TypingFast, "fast typing outranks Mouse");
        activity.Mouse(at.AddMilliseconds(3300));
        states.Tick(at.AddMilliseconds(3300));
        Assert(states.Girl == GirlState.TypingFast, "mouse cannot interrupt recent TypingFast");
        activity.Mouse(at.AddMilliseconds(4000));
        states.Tick(at.AddMilliseconds(4000));
        Assert(states.Girl == GirlState.Mouse, "TypingFast moves directly to Mouse after typing stops");
        states.Tick(at.AddMilliseconds(4901));
        Assert(states.Girl == GirlState.Idle, "Mouse eventually returns to Idle");
        var withoutMouse = new CompanionStateManager(activity, new AppSettings { ReactToMouse = false });
        withoutMouse.Tick(at.AddMilliseconds(4000));
        Assert(withoutMouse.Girl != GirlState.Mouse, "mouse setting disables Mouse state");
        var fastActivity = new ActivityTracker();
        var fastStates = new CompanionStateManager(fastActivity, settings);
        fastActivity.Mouse(at);
        fastStates.Tick(at);
        Assert(fastStates.Girl == GirlState.Mouse, "fast typing scenario starts in Mouse");
        for (var i = 0; i < 14; i++) fastActivity.Keyboard(at.AddMilliseconds(100 + i * 35));
        fastStates.Tick(at.AddMilliseconds(555));
        Assert(fastStates.Girl == GirlState.TypingFast, "keyboard interrupts Mouse into TypingFast");
        return Task.CompletedTask;
    }

    private static Task TestTypingStates()
    {
        var settings=new AppSettings{TypingIdleDelayMs=1200,FastTypingWindowMs=2000,FastTypingThresholdPerSecond=7,ReactToMouse=false};
        var tracker=new ActivityTracker();var states=new CompanionStateManager(tracker,settings);var start=DateTime.UtcNow;
        states.Tick(start);Assert(states.Girl==GirlState.Idle,"starts idle before first key");
        tracker.Keyboard(start);states.Tick(start);Assert(states.Girl==GirlState.Typing,"first key reacts immediately");
        for(var i=1;i<14;i++)tracker.Keyboard(start.AddMilliseconds(i*100));
        states.Tick(start.AddMilliseconds(1300));Assert(states.Girl==GirlState.TypingFast,"14 keys in two seconds reach 7 keys/s");
        tracker.Keyboard(start.AddMilliseconds(3100));states.Tick(start.AddMilliseconds(3100));Assert(states.Girl==GirlState.Typing,"lower rate falls back to typing");
        states.Tick(start.AddMilliseconds(4299));Assert(states.Girl==GirlState.Typing,"debounce keeps typing at 1199 ms");
        states.Tick(start.AddMilliseconds(4301));Assert(states.Girl==GirlState.Idle,"idle after 1200 ms");
        states.Tick(start.AddMilliseconds(4350));Assert(states.Girl==GirlState.Idle,"short inactivity remains Idle");
        tracker.Keyboard(start.AddMilliseconds(4400));states.Tick(start.AddMilliseconds(4400));Assert(states.Girl==GirlState.Typing,"resume reacts immediately");
        var interruptedTracker=new ActivityTracker();var interrupted=new CompanionStateManager(interruptedTracker,settings);
        interruptedTracker.Mouse(DateTime.UtcNow);interrupted.Tick();Assert(interrupted.Girl==GirlState.Idle,"mouse activity does not replace Idle");
        interruptedTracker.Keyboard(DateTime.UtcNow);interrupted.Tick();Assert(interrupted.Girl==GirlState.Typing,"keyboard switches immediately to Typing");
        return Task.CompletedTask;
    }
    private static Task TestSettings()
    {
        var settings=new SettingsService();var original=settings.Current.ShowDate;var oldScale=settings.Current.Scale;var oldCity=settings.Current.CityName;var oldLatitude=settings.Current.Latitude;var oldLongitude=settings.Current.Longitude;var window=new DeskLofi.Views.SettingsWindow(settings);double? previewedScale=null;window.CharacterScalePreviewChanged+=value=>previewedScale=value;var fields=BindingFlags.NonPublic|BindingFlags.Instance;((System.Windows.Controls.CheckBox)typeof(DeskLofi.Views.SettingsWindow).GetField("ShowDate",fields)!.GetValue(window)!).IsChecked=!original;((System.Windows.Controls.TextBox)typeof(DeskLofi.Views.SettingsWindow).GetField("City",fields)!.GetValue(window)!).Text="Smoke City";((System.Windows.Controls.TextBox)typeof(DeskLofi.Views.SettingsWindow).GetField("Latitude",fields)!.GetValue(window)!).Text="20.95";((System.Windows.Controls.ComboBox)typeof(DeskLofi.Views.SettingsWindow).GetField("CharacterSizeChoice",fields)!.GetValue(window)!).SelectedItem=((System.Windows.Controls.ComboBox)typeof(DeskLofi.Views.SettingsWindow).GetField("CharacterSizeChoice",fields)!.GetValue(window)!).Items.OfType<System.Windows.Controls.ComboBoxItem>().Single(x=>x.Tag?.ToString()=="1.25");Assert(previewedScale is 1.25,"choosing a size immediately raises live preview");window.SaveSettings();var loaded=new SettingsService();Assert(Math.Abs(loaded.Current.Scale-1.25)<.001,"character size preset saves to settings");Assert(loaded.Current.ShowDate==!original,"settings window save round trip");Assert(loaded.Current.CityName=="Smoke City"&&Math.Abs(loaded.Current.Latitude-20.95)<.001,"weather location saved");loaded.Current.ShowDate=original;loaded.Current.Scale=oldScale;loaded.Current.CityName=oldCity;loaded.Current.Latitude=oldLatitude;loaded.Current.Longitude=oldLongitude;loaded.Save();window.Close();return Task.CompletedTask;
    }
    private static Task TestMusicLibrary()
    {
        var data=Path.Combine(AppContext.BaseDirectory,"Data");var musicFolder=Path.Combine(AppContext.BaseDirectory,"Music");Directory.CreateDirectory(data);Directory.CreateDirectory(musicFolder);var lib=Path.Combine(data,"music-library.json");var rootTrack=Path.Combine(musicFolder,"scan-test.wav");File.Delete(lib);
        var settings=new SettingsService();using(var empty=new MusicService(settings))Assert(empty.Tracks.Count==0,"initial music library empty");
        File.WriteAllBytes(rootTrack,new byte[]{0});var temp=Path.Combine(Path.GetTempPath(),Guid.NewGuid()+".wav");File.WriteAllBytes(temp,new byte[]{0});
        try{using var music=new MusicService(settings);Assert(music.Tracks.Count==1,"Music folder scanned on first launch");Assert(music.Tracks.Single().FilePath==Path.GetFullPath(rootTrack),"Music folder path saved");Assert(music.AddFiles([temp])==1,"track added");Assert(music.Tracks.Any(t=>t.FilePath==Path.GetFullPath(temp)),"original selected file path retained");Assert(File.Exists(lib),"library JSON persisted");File.Delete(rootTrack);File.Delete(temp);music.RefreshMissing();Assert(music.Tracks.Count(t=>t.IsMissing)==2,"deleted files marked missing");Assert(music.RemoveMissingTracks()==2,"missing tracks removed");}
        finally{if(File.Exists(temp))File.Delete(temp);if(File.Exists(rootTrack))File.Delete(rootTrack);File.Delete(lib);}return Task.CompletedTask;
    }
    private static Task TestHooks()
    {
        using(var hook=new InputMonitor()){Assert(hook.IsKeyboardHookInstalled&&hook.IsMouseHookInstalled,"both global hooks installed");}using(var hookAgain=new InputMonitor()){Assert(hookAgain.IsKeyboardHookInstalled&&hookAgain.IsMouseHookInstalled,"hooks can be installed after dispose");}return Task.CompletedTask;
    }
    private static Task TestAmbience(){using var ambience=new AmbienceService(.22);ambience.Apply(WeatherState.Rain,true);Assert(!ambience.IsPlaying,"missing ambience should be skipped");return Task.CompletedTask;}
    private static Task TestHostLifecycle()
    {
        var host=new DeskLofi.Views.MainWindow(new SettingsService());try
        {
            var tray=(System.Windows.Forms.NotifyIcon)typeof(DeskLofi.Views.MainWindow).GetField("_tray",BindingFlags.NonPublic|BindingFlags.Instance)!.GetValue(host)!;var timer=(System.Windows.Threading.DispatcherTimer)typeof(DeskLofi.Views.MainWindow).GetField("_timer",BindingFlags.NonPublic|BindingFlags.Instance)!.GetValue(host)!;
            var input=(InputMonitor)typeof(DeskLofi.Views.MainWindow).GetField("_input",BindingFlags.NonPublic|BindingFlags.Instance)!.GetValue(host)!;
            var images=(GirlImageCache)typeof(DeskLofi.Views.MainWindow).GetField("_girlImages",BindingFlags.NonPublic|BindingFlags.Instance)!.GetValue(host)!;
            var blink=(BlinkController)typeof(DeskLofi.Views.MainWindow).GetField("_blink",BindingFlags.NonPublic|BindingFlags.Instance)!.GetValue(host)!;
            var demoBack=(System.Windows.Controls.Canvas)typeof(DeskLofi.Views.MainWindow).GetField("RoomDemoBack",BindingFlags.NonPublic|BindingFlags.Instance)!.GetValue(host)!;
            var demoFront=(System.Windows.Controls.Canvas)typeof(DeskLofi.Views.MainWindow).GetField("RoomDemoFront",BindingFlags.NonPublic|BindingFlags.Instance)!.GetValue(host)!;
            var girlContainer=(System.Windows.Controls.Canvas)typeof(DeskLofi.Views.MainWindow).GetField("GirlLayer",BindingFlags.NonPublic|BindingFlags.Instance)!.GetValue(host)!;
            var catLayer=(System.Windows.Controls.Canvas)typeof(DeskLofi.Views.MainWindow).GetField("CatLayer",BindingFlags.NonPublic|BindingFlags.Instance)!.GetValue(host)!;
            var catImage=(CatImageCache)typeof(DeskLofi.Views.MainWindow).GetField("_catImages",BindingFlags.NonPublic|BindingFlags.Instance)!.GetValue(host)!;
            var catSprite=(System.Windows.Controls.Image)typeof(DeskLofi.Views.MainWindow).GetField("CatSprite",BindingFlags.NonPublic|BindingFlags.Instance)!.GetValue(host)!;
            var scene=(System.Windows.Controls.Canvas)typeof(DeskLofi.Views.MainWindow).GetField("Scene",BindingFlags.NonPublic|BindingFlags.Instance)!.GetValue(host)!;
            Assert(images.Master.IsFrozen && blink.Frame == BlinkFrame.Open,"app starts with frozen MASTER and open blink state");Assert(demoBack.Visibility==System.Windows.Visibility.Collapsed&&demoFront.Visibility==System.Windows.Visibility.Collapsed,"demo room is hidden on startup");
            Assert(ReferenceEquals(girlContainer.Parent,scene)&&ReferenceEquals(catLayer.Parent,scene)&&scene.Width>112&&ReferenceEquals(catSprite.Source,catImage.Master),"Girl and Cat use separate visible scene layers with CAT_MASTER");
            typeof(DeskLofi.Views.MainWindow).GetMethod("PositionOnTaskbar",BindingFlags.NonPublic|BindingFlags.Instance)!.Invoke(host,null);
            var workArea=System.Windows.Forms.Screen.PrimaryScreen?.WorkingArea;
            if(workArea!=null){var dpiScale=System.Windows.PresentationSource.FromVisual(host)?.CompositionTarget?.TransformFromDevice.M11??1;Assert(Math.Abs(host.Top-(workArea.Value.Bottom*dpiScale-host.Height))<.1,"startup places the girl on the taskbar edge");}
            Assert(tray.Visible,"tray icon visible on startup");Assert(timer.IsEnabled,"single animation timer active");Assert(input.IsKeyboardHookInstalled&&input.IsMouseHookInstalled,"host owns one keyboard and mouse hook");host.Dispose();Assert(!tray.Visible,"tray icon disposed");Assert(!timer.IsEnabled,"animation timer stopped");Assert(!input.IsKeyboardHookInstalled,"keyboard hook disposed");Assert(blink.Frame==BlinkFrame.Open,"host disposes blink controller");
        }
        finally{host.Dispose();}return Task.CompletedTask;
    }
    private static void Assert(bool test,string message){if(!test)throw new InvalidOperationException(message);}
    private sealed class SequenceHandler(params HttpStatusCode[] responses) : HttpMessageHandler
    {
        private readonly Queue<HttpStatusCode> _responses=new(responses);
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request,CancellationToken cancellationToken)
        {
            var code=_responses.Count>0?_responses.Dequeue():HttpStatusCode.ServiceUnavailable;
            if(code!=HttpStatusCode.OK)return Task.FromResult(new HttpResponseMessage(code));
            var json="{\"current\":{\"temperature_2m\":24.0,\"apparent_temperature\":25.0,\"relative_humidity_2m\":80,\"wind_speed_10m\":5.2,\"weather_code\":63}}";
            return Task.FromResult(new HttpResponseMessage(code){Content=new StringContent(json,Encoding.UTF8,"application/json")});
        }
    }
}
