using System.Net;
using System.Net.Http;
using System.IO;
using System.Text;
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
            ("typing animation cache and FPS", TestTypingAnimation), ("typing state and debounce", TestTypingStates),
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
        var cfg=new AppSettings{CatSleepTimeoutSeconds=45,AfkTimeoutMinutes=60};var tracker=new ActivityTracker();var manager=new CompanionStateManager(tracker,cfg);var now=DateTime.UtcNow;
        for(var i=0;i<14;i++)tracker.Keyboard(now.AddMilliseconds(i*80));manager.Tick(now.AddSeconds(1.1));Assert(manager.Girl==GirlState.TypingFast,"fast typing reaction");
        var idle=now.AddSeconds(-50);tracker.Keyboard(idle);tracker.Mouse(idle);manager.Tick();Assert(manager.Cat==CatState.Sleep&&manager.Girl==GirlState.Idle,"idle cat sleep and girl idle");
        tracker.Keyboard(DateTime.UtcNow);manager.Tick();Assert(manager.Cat==CatState.WakeUp,"cat wakes on activity");manager.Tick();Assert(manager.Cat==CatState.TailWag,"cat returns to awake state");
        var slowTracker=new ActivityTracker();var slowManager=new CompanionStateManager(slowTracker,new AppSettings());slowTracker.Keyboard(DateTime.UtcNow);slowManager.Tick();Assert(slowManager.Girl==GirlState.Typing,"slow typing reaction");
        var mouseTracker=new ActivityTracker();var mouseManager=new CompanionStateManager(mouseTracker,new AppSettings{ReactToKeyboard=false});mouseTracker.Mouse(DateTime.UtcNow);mouseManager.Tick();Assert(mouseManager.Girl==GirlState.Mouse,"mouse reaction");
        var afkTracker=new ActivityTracker();var afkManager=new CompanionStateManager(afkTracker,new AppSettings{AfkTimeoutMinutes=1});var old=DateTime.UtcNow.AddMinutes(-2);afkTracker.Keyboard(old);afkTracker.Mouse(old);afkManager.Tick();Assert(afkManager.Girl is GirlState.Away or GirlState.Coffee or GirlState.Stretch or GirlState.LookWindow,"AFK animation");afkTracker.Keyboard(DateTime.UtcNow);afkManager.Tick();Assert(afkManager.Girl==GirlState.Typing,"typing interrupts AFK animation");return Task.CompletedTask;
    }
    private static Task TestTypingAnimation()
    {
        var file=Path.Combine(AppContext.BaseDirectory,"Data","girl-animations.json");
        var catalog=SpriteAnimationController.LoadCatalog(file);
        var fallback=new SpriteLoader();
        var player=new SpriteAnimationController(catalog,name=>[fallback.GetFrame("Girl",name,0)]);
        var slow=player.FramesFor("Typing");var fast=player.FramesFor("TypingFast");
        Assert(slow.Length==4,"four typing frames loaded");
        Assert(ReferenceEquals(slow,fast),"fast animation reuses identical cached collection");
        Assert(slow.All(x=>x.PixelWidth==517&&x.PixelHeight==488),"all frames have one canvas without resizing");
        player.Play("Typing");Assert(player.CurrentFps==6,"typing defaults to 6 FPS");
        player.Advance(TimeSpan.FromMilliseconds(170));Assert(player.FrameIndex==1,"typing advances after one frame interval");
        player.Play("Typing");Assert(player.FrameIndex==1,"same state does not restart frame");
        player.Play("TypingFast");Assert(player.FrameIndex==1&&player.CurrentFps==10,"fast mode changes only FPS");
        player.Advance(TimeSpan.FromMilliseconds(105));Assert(player.FrameIndex==2,"fast mode runs at 10 FPS");
        player.Play("Typing");Assert(player.FrameIndex==2,"slowing down keeps frame position");
        foreach(var fps in new[]{4,6,8,10}){player.SetFpsOverride(fps);Assert(player.CurrentFps==fps,$"FPS {fps} selectable");player.Advance(TimeSpan.FromSeconds(1d/fps));}
        player.SetFpsOverride(null);player.Pause();var paused=player.FrameIndex;player.Advance(TimeSpan.FromSeconds(1));Assert(player.FrameIndex==paused,"pause holds frame");player.Stop();Assert(player.FrameIndex==0,"stop resets frame");
        var missing=new SpriteAnimationController(new SpriteAnimationCatalog{Animations=[new SpriteAnimationDefinition{Name="Typing",FolderPath="Assets/Characters/Girl/Missing-Smoke-Test",FrameCount=4,Fps=6}]},name=>[fallback.GetFrame("Girl",name,0)]);
        missing.Play("Typing");Assert(missing.UsingFallback&&missing.CurrentFrame.PixelWidth==64,"missing asset falls back without crashing");
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
        tracker.Keyboard(start.AddMilliseconds(4400));states.Tick(start.AddMilliseconds(4400));Assert(states.Girl==GirlState.Typing,"resume reacts immediately");
        var interruptedTracker=new ActivityTracker();var interrupted=new CompanionStateManager(interruptedTracker,settings);
        interrupted.ReactToGirl();interrupted.Tick();Assert(interrupted.Girl==GirlState.LookAtCat,"idle reaction starts");
        interruptedTracker.Keyboard(DateTime.UtcNow);interrupted.Tick();Assert(interrupted.Girl==GirlState.Typing,"keyboard interrupts a lower-priority animation");
        return Task.CompletedTask;
    }
    private static Task TestSettings()
    {
        var settings=new SettingsService();var original=settings.Current.ShowDate;var oldCity=settings.Current.CityName;var oldLatitude=settings.Current.Latitude;var oldLongitude=settings.Current.Longitude;var window=new DeskLofi.Views.SettingsWindow(settings);var fields=BindingFlags.NonPublic|BindingFlags.Instance;((System.Windows.Controls.CheckBox)typeof(DeskLofi.Views.SettingsWindow).GetField("ShowDate",fields)!.GetValue(window)!).IsChecked=!original;((System.Windows.Controls.TextBox)typeof(DeskLofi.Views.SettingsWindow).GetField("City",fields)!.GetValue(window)!).Text="Smoke City";((System.Windows.Controls.TextBox)typeof(DeskLofi.Views.SettingsWindow).GetField("Latitude",fields)!.GetValue(window)!).Text="20.95";window.SaveSettings();var loaded=new SettingsService();Assert(loaded.Current.ShowDate==!original,"settings window save round trip");Assert(loaded.Current.CityName=="Smoke City"&&Math.Abs(loaded.Current.Latitude-20.95)<.001,"weather location saved");loaded.Current.ShowDate=original;loaded.Current.CityName=oldCity;loaded.Current.Latitude=oldLatitude;loaded.Current.Longitude=oldLongitude;loaded.Save();window.Close();return Task.CompletedTask;
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
            Assert(tray.Visible,"tray icon visible on startup");Assert(timer.IsEnabled,"single animation timer active");Assert(input.IsKeyboardHookInstalled&&input.IsMouseHookInstalled,"host owns one keyboard and mouse hook");host.Dispose();Assert(!tray.Visible,"tray icon disposed");Assert(!timer.IsEnabled,"animation timer stopped");Assert(!input.IsKeyboardHookInstalled,"keyboard hook disposed");
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
