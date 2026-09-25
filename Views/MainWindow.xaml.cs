using System.ComponentModel;
using System.Diagnostics;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Interop;
using System.Windows.Threading;
using DeskLofi.Animation;
using DeskLofi.Models;
using DeskLofi.Services;
using Forms = System.Windows.Forms;
using MediaColor = System.Windows.Media.Color;

namespace DeskLofi.Views;
public partial class MainWindow : Window, IDisposable
{
    private readonly Forms.ToolStripMenuItem _topmostTrayItem;
    private readonly Forms.ToolStripMenuItem _settingsTrayItem;
    private readonly Forms.ToolStripMenuItem _exitTrayItem;
    private const double GirlSceneWidth = 112, GirlSceneHeight = 106;
    private enum GirlAnimationPhase { Idle, Typing, MouseEntering, MouseActive, MouseLeaving, IdleSpecialEntering, IdleSpecialHolding, IdleSpecialLeaving }
    private const double CoffeeFrameSeconds = .16, CoffeeHoldSeconds = .7, StretchFrameSeconds = .18, StretchHoldSeconds = .8;
    private readonly SettingsService _settings; private readonly TimeService _time=new(); private readonly DayNightService _dayNight; private readonly SceneDefinition _sceneDefinition; private readonly SceneDefinitionService _sceneDefinitions=new(); private readonly WeatherService _weather; private readonly WeatherEffectController _weatherEffects; private readonly AmbienceService _ambience; private readonly ActivityTracker _activity=new(); private readonly InputMonitor _input; private readonly CompanionStateManager _states; private readonly SpriteLoader _sprites=new(); private readonly AssetPackService _packs; private readonly GirlImageCache _girlImages; private readonly CatImageCache _catImages; private readonly CatStateMachine _catStates; private readonly BlinkController _blink; private readonly MusicService _music; private readonly DispatcherTimer _timer; private readonly Forms.NotifyIcon _tray; private long _lastAnimationTick=Stopwatch.GetTimestamp(); private int _ticks; private int _typingFrameIndex; private double _typingFrameElapsed; private bool _typingActive; private GirlAnimationPhase _girlAnimationPhase=GirlAnimationPhase.Idle; private int _mouseFrameIndex; private double _mouseFrameElapsed; private bool _typingAfterMouseLeave; private bool _typingAfterMouseMaster; private bool _isSeeking,_disposed; private bool _debugBlinkPreview = false; private TimePeriod? _assetFrom,_assetTo; private DateTime _lastMoveSave=DateTime.MinValue;
    private GirlAction _activeIdleSpecial = GirlAction.None; private int _idleSpecialFrameIndex; private double _idleSpecialFrameElapsed;
#if DEBUG
    private System.Windows.Controls.Primitives.Popup? _animationDebugPopup;
    private TextBlock? _animationDebugText;
    private string? _debugGirlFrame;
#endif
    public MainWindow(SettingsService settings, Func<GirlState>? chooseIdleSpecial = null)
    {
        InitializeComponent();
        IsVisibleChanged += OnAnimationVisibilityChanged;
        StateChanged += OnAnimationWindowStateChanged;
        _settings = settings;
        LoggerService.Info("DeskLofi startup.");
        _time.Updated += UpdateClock;
        _sceneDefinition = _sceneDefinitions.Load(settings.Current.Scene);
        _dayNight = new DayNightService(_time, settings.Current.AutoDayNight);
        _dayNight.TransitionUpdated += OnDayNightTransition;
        _weatherEffects = new WeatherEffectController(WeatherLayer, LightningLayer);
        _weather = new WeatherService(new LocationSettings
        {
            CityName = settings.Current.CityName,
            Latitude = settings.Current.Latitude,
            Longitude = settings.Current.Longitude
        }, settings.Current.WeatherRefreshMinutes, settings.Current.EnableRealWeather);
        _weather.WeatherChanged += OnWeatherChanged;
        _weatherEffects.LightningFlashed += OnLightningFlashed;
        _weatherEffects.Apply(_weather.Current.State, settings.Current.WeatherEffects, settings.Current.EnableLightning);
        _packs = new AssetPackService(settings.Current);
        _girlImages = new GirlImageCache(_packs, () => _sprites.GetFrame("Girl", "Idle", 0));
        _catImages = new CatImageCache(_packs);
        _catStates = new CatStateMachine(sleepTimeoutSeconds: settings.Current.CatSleepTimeoutSeconds,
            tailFrameCount: _catImages.TailFrames.Length, sleepFrameCount: _catImages.SleepFrames.Length);
        _states = new CompanionStateManager(_activity, settings.Current, chooseIdleSpecial, HasGirlAnimation);
        CatSprite.Source = _catImages.Master;
        ApplyCharacterLayout(settings.Current.Scale);
        _blink = new BlinkController(settings.Current);
        _input = new InputMonitor(settings.Current.MouseMoveThrottleMs);
        _input.KeyboardActivity += OnKeyboard;
        _input.MouseActivity += OnMouse;
        _states.StateChanged += _ => UpdateGirlAnimation();
        _music=new(settings); _ambience=new(settings.Current.WeatherVolume);ShuffleButton.Opacity=_music.Shuffle?1:.55;RepeatButton.Opacity=_music.Repeat?1:.55;PopulatePlaylists();_music.TrackChanged+=OnTrackChanged;_music.PlaybackChanged+=OnPlaybackChanged;_music.LibraryChanged+=OnMusicLibraryChanged;
        _timer=new(){Interval=TimeSpan.FromMilliseconds(25)};_timer.Tick+=(_,_)=>{var now=Stopwatch.GetTimestamp();var elapsed=Stopwatch.GetElapsedTime(_lastAnimationTick,now);_blink.Advance(elapsed);_lastAnimationTick=now;AdvanceGirlAnimation(elapsed);_catStates.Advance(elapsed);if(++_ticks%4==0){_states.Tick();UpdateBlinkEligibility();}if(_ticks%40==0)UpdateProgress();Render();};_timer.Start();
        _tray=new Forms.NotifyIcon{Text="DeskLofi",Icon=System.Drawing.SystemIcons.Application,Visible=true};_tray.ContextMenuStrip=new Forms.ContextMenuStrip();_tray.ContextMenuStrip.Items.Add("Show DeskLofi",null,(_,_)=>Show());_tray.ContextMenuStrip.Items.Add("Play / Pause",null,(_,_)=>ToggleMusic());_tray.ContextMenuStrip.Items.Add("Next Track",null,(_,_)=>_music.Next());_topmostTrayItem=new Forms.ToolStripMenuItem { CheckOnClick=true, Checked=settings.Current.AlwaysOnTop };_topmostTrayItem.Click+=(_,_)=>{_settings.Current.AlwaysOnTop=_topmostTrayItem.Checked;_settings.Save();ApplyWindowLayering();};_tray.ContextMenuStrip.Items.Add(_topmostTrayItem);_settingsTrayItem=new Forms.ToolStripMenuItem("Settings",null,(_,_)=>OpenSettings());_tray.ContextMenuStrip.Items.Add(_settingsTrayItem);_exitTrayItem=new Forms.ToolStripMenuItem("Exit",null,(_,_)=>ExitApp());_tray.ContextMenuStrip.Items.Add(_exitTrayItem);_tray.DoubleClick+=(_,_)=>Show();
        ApplyLanguage();
        Topmost=settings.Current.AlwaysOnTop;ShowInTaskbar=settings.Current.ShowOnTaskbar;
#if DEBUG
        CreateAnimationDebugPanel();
#endif
    }
    private string T(string vietnamese,string english)=>UiText.Choose(_settings.Current.Language,vietnamese,english);
    private bool HasGirlAnimation(GirlState state) => state switch
    {
        GirlState.Typing or GirlState.TypingFast => _girlImages.TypingFrames.Length > 0,
        GirlState.Mouse => _girlImages.MouseFrames.Length > 0,
        GirlState.Coffee => _girlImages.CoffeeFrames.Length > 0,
        GirlState.Stretch => _girlImages.StretchFrames.Length > 0,
        _ => true
    };
    private void ApplyLanguage()
    {
        UiText.TranslateTree(this,_settings.Current.Language);
        if(MusicPopup.Child is { } popup)UiText.TranslateTree(popup,_settings.Current.Language);
        var trayItems=_tray.ContextMenuStrip?.Items;
        if(trayItems is {Count:>=5})
        {
            trayItems[0].Text=T("Hiện DeskLofi","Show DeskLofi");
            trayItems[1].Text=T("Phát / Tạm dừng","Play / Pause");
            trayItems[2].Text=T("Bài tiếp","Next Track");
        }
        _topmostTrayItem.Text=T("Hiển thị trên các ứng dụng khác","Show over other apps");
        _settingsTrayItem.Text=T("Cài đặt","Settings");
        _exitTrayItem.Text=T("Thoát","Exit");
        PopulatePlaylists();
        UpdateTrack();
    }
    private void OnLoaded(object sender,RoutedEventArgs e){ApplyCharacterLayout(_settings.Current.Scale);PositionOnTaskbar();ApplySceneDefinition();UpdateClock();UpdateDayNight(_dayNight.CurrentPeriod,_dayNight.CurrentPeriod,1);ApplyWeatherVisual(_weather.Current);UpdateBlinkEligibility();Render();ApplyWindowLayering();if(_settings.Current.AutoPlayMusic)_music.Toggle();}
    private void OnSourceInitialized(object? sender,EventArgs e){ApplyTaskbarVisibility();ApplyWindowLayering();}
    private void ApplyTaskbarVisibility()
    {
        ShowInTaskbar=_settings.Current.ShowOnTaskbar;
        var hwnd=new WindowInteropHelper(this).Handle;
        if(hwnd==IntPtr.Zero)return;
        var ex=GetWindowLong(hwnd,-20);
        ex=_settings.Current.ShowOnTaskbar
            ? (ex&~(0x08000000|0x00000080))|0x00040000
            : (ex|0x08000000|0x00000080)&~0x00040000;
        SetWindowLong(hwnd,-20,ex);
        SetWindowPos(hwnd,IntPtr.Zero,0,0,0,0,0x0020|0x0002|0x0001|0x0004);
    }
    private void ApplyWindowLayering()
    {
        _topmostTrayItem.Checked=_settings.Current.AlwaysOnTop;
        Topmost=_settings.Current.AlwaysOnTop;
        var hwnd=new WindowInteropHelper(this).Handle;
        if(hwnd==IntPtr.Zero)return;
        SetWindowPos(hwnd,_settings.Current.AlwaysOnTop?new IntPtr(-1):new IntPtr(-2),0,0,0,0,0x0002|0x0001|0x0010);
    }
    [System.Runtime.InteropServices.DllImport("user32.dll",EntryPoint="GetWindowLongPtrW")] private static extern IntPtr GetWindowLongPtr(IntPtr hWnd,int index);
    [System.Runtime.InteropServices.DllImport("user32.dll",EntryPoint="SetWindowLongPtrW")] private static extern IntPtr SetWindowLongPtr(IntPtr hWnd,int index,IntPtr value);
    [System.Runtime.InteropServices.DllImport("user32.dll")] private static extern bool SetWindowPos(IntPtr hWnd,IntPtr hWndInsertAfter,int x,int y,int cx,int cy,uint flags);
    private static int GetWindowLong(IntPtr hwnd,int index)=>unchecked((int)GetWindowLongPtr(hwnd,index).ToInt64());
    private static void SetWindowLong(IntPtr hwnd,int index,int value)=>SetWindowLongPtr(hwnd,index,new IntPtr(value));
    private void PositionOnTaskbar()
    {
        var wa=Forms.Screen.PrimaryScreen?.WorkingArea;
        if(wa==null)return;
        var scale=PresentationSource.FromVisual(this)?.CompositionTarget?.TransformFromDevice.M11??1;
        var minLeft=wa.Value.Left*scale;
        var maxLeft=Math.Max(minLeft,wa.Value.Right*scale-Width);
        var desiredLeft=_settings.Current.ShowOnTaskbar
            ? minLeft+_settings.Current.TaskbarOffsetX*scale
            : _settings.Current.Left>=0?_settings.Current.Left:(minLeft+wa.Value.Width*scale/2-Width/2);
        Left=Math.Clamp(desiredLeft,minLeft,maxLeft);
        Top=wa.Value.Bottom*scale-Height;
    }
    private void OnKeyboard(DateTime at)=>Dispatcher.BeginInvoke(()=>{_activity.Keyboard(at);_catStates.NotifyActivity(true,at);_blink.SetCanBlink(false);_states.Tick();UpdateBlinkEligibility();Render();},DispatcherPriority.Input);
    private void OnMouse(DateTime at)=>Dispatcher.BeginInvoke(()=>{_activity.Mouse(at);_catStates.NotifyActivity(false,at);_blink.SetCanBlink(false);_states.Tick();UpdateBlinkEligibility();Render();},DispatcherPriority.Input);
    private void OnAnimationVisibilityChanged(object sender,DependencyPropertyChangedEventArgs e)=>UpdateAnimationActivity();
    private void OnAnimationWindowStateChanged(object? sender,EventArgs e)=>UpdateAnimationActivity();
    private void UpdateAnimationActivity()
    {
        var active=IsVisible&&WindowState!=WindowState.Minimized;
        _blink.SetCanBlink(active && _girlImages.HasBlink && _girlAnimationPhase==GirlAnimationPhase.Idle && (_states.Girl==GirlState.Idle || _debugBlinkPreview));
        if(active){_lastAnimationTick=Stopwatch.GetTimestamp();_timer.Start();}
        else _timer.Stop();
    }
    private void UpdateBlinkEligibility()
    {
        var lastKey = _activity.LastKeyboardActivity;
        var lastMouse = _activity.LastMouseActivity;
        var lastActivityIsKeyboard = lastKey >= lastMouse;
        var lastActivity = lastActivityIsKeyboard ? lastKey : lastMouse;
        var idleDelay = lastActivityIsKeyboard ? _settings.Current.TypingIdleDelayMs : _settings.Current.MouseIdleDelayMs;
        var activitySettled = lastActivity == DateTime.MinValue || DateTime.UtcNow - lastActivity >= TimeSpan.FromMilliseconds(Math.Max(0, idleDelay));
        var idlePhase = _girlAnimationPhase == GirlAnimationPhase.Idle;
        _blink.SetCanBlink(IsVisible && WindowState != WindowState.Minimized && _girlImages.HasBlink && idlePhase && !_typingAfterMouseMaster && activitySettled && (_states.Girl == GirlState.Idle || _debugBlinkPreview));
    }
    private void UpdateGirlAnimation()
    {
        var typing = _states.Girl is GirlState.Typing or GirlState.TypingFast;
        if (_states.Action is GirlAction.Coffee or GirlAction.Stretch)
        {
            if (_girlAnimationPhase is not (GirlAnimationPhase.IdleSpecialEntering or GirlAnimationPhase.IdleSpecialHolding or GirlAnimationPhase.IdleSpecialLeaving))
            {
                _activeIdleSpecial = _states.Action;
                var frames = _states.Action == GirlAction.Stretch ? _girlImages.StretchFrames : _girlImages.CoffeeFrames;
                _girlAnimationPhase = frames.Length <= 1 ? GirlAnimationPhase.IdleSpecialHolding : GirlAnimationPhase.IdleSpecialEntering;
                _idleSpecialFrameIndex = 0;
                _idleSpecialFrameElapsed = 0;
                _typingActive = false;
            }
            UpdateBlinkEligibility();
            Render();
            return;
        }
        if (_states.Girl == GirlState.Mouse)
        {
            if (_girlAnimationPhase is not (GirlAnimationPhase.MouseEntering or GirlAnimationPhase.MouseActive))
            {
                _girlAnimationPhase = GirlAnimationPhase.MouseEntering;
                _mouseFrameIndex = 0;
                _mouseFrameElapsed = 0;
                _typingAfterMouseLeave = false;
            }
        }
        else if (_girlAnimationPhase is GirlAnimationPhase.MouseEntering or GirlAnimationPhase.MouseActive)
        {
            _girlAnimationPhase = GirlAnimationPhase.MouseLeaving;
            _mouseFrameIndex = Math.Min(1, _girlImages.MouseFrames.Length - 1);
            _mouseFrameElapsed = 0;
            _typingAfterMouseLeave = typing;
        }
        else if (_girlAnimationPhase == GirlAnimationPhase.MouseLeaving)
        {
            _typingAfterMouseLeave = typing;
        }
        else if (typing)
        {
            if (_girlAnimationPhase != GirlAnimationPhase.Typing)
            {
                _girlAnimationPhase = GirlAnimationPhase.Typing;
                _typingFrameIndex = 0;
                _typingFrameElapsed = 0;
            }
        }
        else
        {
            _girlAnimationPhase = GirlAnimationPhase.Idle;
            _typingFrameElapsed = 0;
        }
        _typingActive = _girlAnimationPhase == GirlAnimationPhase.Typing;
        UpdateBlinkEligibility();
        Render();
    }
    private void AdvanceGirlAnimation(TimeSpan elapsed)
    {
        var delta = Math.Max(0, elapsed.TotalSeconds);
        if (_girlAnimationPhase == GirlAnimationPhase.Idle && _typingAfterMouseMaster)
        {
            _typingAfterMouseMaster = false;
            if (_states.Girl is GirlState.Typing or GirlState.TypingFast)
            {
                _girlAnimationPhase = GirlAnimationPhase.Typing;
                _typingActive = true;
                _typingFrameIndex = 0;
                _typingFrameElapsed = 0;
            }
        }
        if (_girlAnimationPhase == GirlAnimationPhase.Typing && _typingActive && _girlImages.TypingFrames.Length > 0)
        {
            _typingFrameElapsed += delta;
            var frameDuration = 1d / (_states.Girl == GirlState.TypingFast ? 10d : 6d);
            var steps = (int)(_typingFrameElapsed / frameDuration);
            if (steps > 0) { _typingFrameElapsed -= steps * frameDuration; _typingFrameIndex = (_typingFrameIndex + steps) % _girlImages.TypingFrames.Length; }
            return;
        }
        if (_girlAnimationPhase is GirlAnimationPhase.IdleSpecialEntering or GirlAnimationPhase.IdleSpecialHolding or GirlAnimationPhase.IdleSpecialLeaving)
        {
            var frames = _activeIdleSpecial == GirlAction.Stretch ? _girlImages.StretchFrames : _girlImages.CoffeeFrames;
            var normalFrameSeconds = _activeIdleSpecial == GirlAction.Stretch ? StretchFrameSeconds : CoffeeFrameSeconds;
            var holdSeconds = _activeIdleSpecial == GirlAction.Stretch ? StretchHoldSeconds : CoffeeHoldSeconds;
            _idleSpecialFrameElapsed += delta;
            while (_girlAnimationPhase is GirlAnimationPhase.IdleSpecialEntering or GirlAnimationPhase.IdleSpecialHolding or GirlAnimationPhase.IdleSpecialLeaving)
            {
                var duration = _girlAnimationPhase == GirlAnimationPhase.IdleSpecialHolding ? holdSeconds : normalFrameSeconds;
                if (_idleSpecialFrameElapsed < duration) break;
                _idleSpecialFrameElapsed -= duration;
                if (_girlAnimationPhase == GirlAnimationPhase.IdleSpecialEntering)
                {
                    _idleSpecialFrameIndex++;
                    if (_idleSpecialFrameIndex == frames.Length - 1) _girlAnimationPhase = GirlAnimationPhase.IdleSpecialHolding;
                }
                else if (_girlAnimationPhase == GirlAnimationPhase.IdleSpecialHolding)
                {
                    _girlAnimationPhase = GirlAnimationPhase.IdleSpecialLeaving;
                    _idleSpecialFrameIndex = Math.Max(0, _idleSpecialFrameIndex - 1);
                }
                else if (_idleSpecialFrameIndex > 0) _idleSpecialFrameIndex--;
                else
                {
                    _girlAnimationPhase = GirlAnimationPhase.Idle;
                    _activeIdleSpecial = GirlAction.None;
                    _idleSpecialFrameElapsed = 0;
                    _states.CompleteAction();
                    UpdateBlinkEligibility();
                    break;
                }
            }
            return;
        }
        if (_girlAnimationPhase is not (GirlAnimationPhase.MouseEntering or GirlAnimationPhase.MouseActive or GirlAnimationPhase.MouseLeaving)) return;
        if (_girlImages.MouseFrames.Length == 0) { _girlAnimationPhase = GirlAnimationPhase.Idle; return; }
        var activeIndex = Math.Max(0, _girlImages.MouseFrames.Length - 2);
        var lastIndex = _girlImages.MouseFrames.Length - 1;
        _mouseFrameElapsed += delta;
        var frameDurationSeconds = _girlAnimationPhase == GirlAnimationPhase.MouseActive ? .18 : .12;
        while (_mouseFrameElapsed >= frameDurationSeconds)
        {
            _mouseFrameElapsed -= frameDurationSeconds;
            if (_girlAnimationPhase == GirlAnimationPhase.MouseEntering)
            {
                if (_mouseFrameIndex < activeIndex) _mouseFrameIndex++;
                else { _mouseFrameIndex = activeIndex; _girlAnimationPhase = GirlAnimationPhase.MouseActive; frameDurationSeconds = .18; }
            }
            else if (_girlAnimationPhase == GirlAnimationPhase.MouseActive)
            {
                _mouseFrameIndex = _mouseFrameIndex == activeIndex ? lastIndex : activeIndex;
            }
            else if (_mouseFrameIndex > 0) _mouseFrameIndex--;
            else
            {
                var resumeTyping = _typingAfterMouseLeave && (_states.Girl is GirlState.Typing or GirlState.TypingFast);
                _girlAnimationPhase = GirlAnimationPhase.Idle;
                _typingAfterMouseLeave = false;
                _typingAfterMouseMaster = resumeTyping;
                _typingActive = false;
                _mouseFrameElapsed = 0;
                UpdateBlinkEligibility();
                break;
            }
            frameDurationSeconds = _girlAnimationPhase == GirlAnimationPhase.MouseActive ? .18 : .12;
        }
    }
    private void Render()
    {
        if (!IsLoaded) return;
#if DEBUG
        var debugFrame = _debugGirlFrame;
#else
        string? debugFrame = null;
#endif
        var source = debugFrame switch
        {
            "HALF" => _girlImages.BlinkHalf,
            "CLOSE" => _girlImages.BlinkClose,
            "MASTER" => _girlImages.Master,
            _ when _girlAnimationPhase is GirlAnimationPhase.IdleSpecialEntering or GirlAnimationPhase.IdleSpecialHolding or GirlAnimationPhase.IdleSpecialLeaving =>
                (_activeIdleSpecial == GirlAction.Stretch ? _girlImages.StretchFrames : _girlImages.CoffeeFrames).ElementAtOrDefault(_idleSpecialFrameIndex) ?? _girlImages.Master,
            _ when _girlAnimationPhase is GirlAnimationPhase.MouseEntering or GirlAnimationPhase.MouseActive or GirlAnimationPhase.MouseLeaving => _girlImages.MouseFrames.ElementAtOrDefault(_mouseFrameIndex) ?? _girlImages.Master,
            _ when _girlAnimationPhase == GirlAnimationPhase.Typing => _girlImages.TypingFrames.ElementAtOrDefault(_typingFrameIndex) ?? _girlImages.Master,
            _ when _girlAnimationPhase == GirlAnimationPhase.Idle && (_debugBlinkPreview || _states.Girl == GirlState.Idle) => _blink.Frame switch
            {
                BlinkFrame.Half => _girlImages.BlinkHalf,
                BlinkFrame.Closed => _girlImages.BlinkClose,
                _ => _girlImages.Master
            },
            _ => _girlImages.Master
        };
        if (!ReferenceEquals(GirlSprite.Source, source)) GirlSprite.Source = source;
        var cat = _catStates.State switch
        {
            CatState.TailWag => _catImages.TailFrames.ElementAtOrDefault(_catStates.TailFrameIndex) ?? _catImages.Master,
            CatState.GoingToSleep or CatState.Sleeping or CatState.WakingUp => _catImages.SleepFrames.ElementAtOrDefault(_catStates.SleepFrameIndex) ?? _catImages.Master,
            _ => _catImages.Master
        };
        if (!ReferenceEquals(CatSprite.Source, cat)) CatSprite.Source = cat;
#if DEBUG
        if (_animationDebugPopup?.IsOpen == true && _animationDebugText != null)
        {
            var blink = _blink.NextBlinkInSeconds is double next ? $"{next:0.0}s" : "—";
            _animationDebugText.Text = $"User: {_states.Activity}  Girl Action: {_states.Action}\nCat State: {_catStates.State}\nBlink: {_blink.Frame}  Next: {blink}";
        }
#endif
    }
#if DEBUG
    private void CreateAnimationDebugPanel()
    {
        var panel = new StackPanel();
        _animationDebugText = new TextBlock { Foreground = System.Windows.Media.Brushes.White, Margin = new Thickness(4), FontSize = 11 };
        panel.Children.Add(_animationDebugText);
        var previews = new WrapPanel();
        foreach (var (label, frame) in new[] { ("MASTER", "MASTER"), ("HALF", "HALF"), ("CLOSE", "CLOSE") })
        {
            var imageFrame = frame;
            var button = new System.Windows.Controls.Button { Content = label, Margin = new Thickness(2) };
            button.Click += (_, _) => { _debugBlinkPreview = false; _debugGirlFrame = imageFrame; UpdateBlinkEligibility(); Render(); };
            previews.Children.Add(button);
        }
        var play = new System.Windows.Controls.Button { Content = "Play Blink", Margin = new Thickness(2) };
        play.Click += (_, _) => { _debugGirlFrame = null; _debugBlinkPreview = true; UpdateBlinkEligibility(); _blink.BlinkNow(); Render(); };
        previews.Children.Add(play);
        var auto = new System.Windows.Controls.Button { Content = "Auto", Margin = new Thickness(2) };
        auto.Click += (_, _) => { _debugGirlFrame = null; _debugBlinkPreview = false; UpdateBlinkEligibility(); Render(); };
        previews.Children.Add(auto);
        foreach (var action in new[] { GirlAction.Coffee, GirlAction.Stretch })
        {
            var force = new System.Windows.Controls.Button { Content = $"Force {action}", Margin = new Thickness(2) };
            force.Click += (_, _) => { _debugGirlFrame = null; _debugBlinkPreview = false; _states.ForceAction(action); Render(); };
            previews.Children.Add(force);
        }
        panel.Children.Add(previews);
        _animationDebugPopup = new System.Windows.Controls.Primitives.Popup
        {
            PlacementTarget = this, Placement = System.Windows.Controls.Primitives.PlacementMode.Top,
            StaysOpen = true, AllowsTransparency = true,
            Child = new Border { Background = new SolidColorBrush(MediaColor.FromArgb(240, 30, 29, 45)),
                BorderBrush = System.Windows.Media.Brushes.LightGray, BorderThickness = new Thickness(1), Padding = new Thickness(5), Child = panel }
        };
        _tray.ContextMenuStrip!.Items.Insert(4, new Forms.ToolStripMenuItem("Blink debug", null, (_, _) => _animationDebugPopup.IsOpen = !_animationDebugPopup.IsOpen));
        _tray.ContextMenuStrip.Items.Insert(5, new Forms.ToolStripMenuItem("Force Cat Tail Wag", null, (_, _) => { _catStates.ForceTailWag(); Render(); }));
        _tray.ContextMenuStrip.Items.Insert(6, new Forms.ToolStripMenuItem("Force Cat Sleep", null, (_, _) => { _catStates.ForceSleep(); Render(); }));
        _tray.ContextMenuStrip.Items.Insert(7, new Forms.ToolStripMenuItem("Force Cat Wake", null, (_, _) => { _catStates.ForceWake(); Render(); }));
        _tray.ContextMenuStrip.Items.Insert(8, new Forms.ToolStripMenuItem("Force Coffee", null, (_, _) => { _debugGirlFrame = null; _debugBlinkPreview = false; _states.ForceAction(GirlAction.Coffee); Render(); }));
        _tray.ContextMenuStrip.Items.Insert(9, new Forms.ToolStripMenuItem("Force Stretch", null, (_, _) => { _debugGirlFrame = null; _debugBlinkPreview = false; _states.ForceAction(GirlAction.Stretch); Render(); }));
    }
#endif
    private void OnDayNightTransition(TimePeriod from,TimePeriod to,double amount)=>UpdateDayNight(from,to,amount);
    private void UpdateDayNight(TimePeriod from,TimePeriod to,double amount){SkyLayer.Fill=BlendLayerColor(_sceneDefinition.SkyColors,from,to,amount,"#FF25365D");LightingLayer.Fill=BlendLayerColor(_sceneDefinition.LightingColors,from,to,amount,"#00000000");LampGlow.Opacity=LampAmount(from)+(LampAmount(to)-LampAmount(from))*amount;if(_assetFrom!=from||_assetTo!=to){_assetFrom=from;_assetTo=to;SkyImageFrom.Source=SceneDefinitionService.LoadImage(_sceneDefinition,_sceneDefinition.SkyAssets.GetValueOrDefault(from.ToString()));SkyImageTo.Source=SceneDefinitionService.LoadImage(_sceneDefinition,_sceneDefinition.SkyAssets.GetValueOrDefault(to.ToString()));LightingImageFrom.Source=SceneDefinitionService.LoadImage(_sceneDefinition,_sceneDefinition.LightingAssets.GetValueOrDefault(from.ToString()));LightingImageTo.Source=SceneDefinitionService.LoadImage(_sceneDefinition,_sceneDefinition.LightingAssets.GetValueOrDefault(to.ToString()));}SkyImageFrom.Opacity=SkyImageFrom.Source is null?0:1-amount;SkyImageTo.Opacity=SkyImageTo.Source is null?0:amount;LightingImageFrom.Opacity=LightingImageFrom.Source is null?0:1-amount;LightingImageTo.Opacity=LightingImageTo.Source is null?0:amount;}
    private void ApplySceneDefinition(){var w=_sceneDefinition.WindowBounds;Canvas.SetLeft(SkyLayer,w.X);Canvas.SetTop(SkyLayer,w.Y);SkyLayer.Width=w.Width;SkyLayer.Height=w.Height;foreach(var image in new[]{SkyImageFrom,SkyImageTo}){Canvas.SetLeft(image,w.X);Canvas.SetTop(image,w.Y);image.Width=w.Width;image.Height=w.Height;}Canvas.SetLeft(WeatherLayer,w.X);Canvas.SetTop(WeatherLayer,w.Y);Canvas.SetLeft(LightningLayer,w.X);Canvas.SetTop(LightningLayer,w.Y);LightningLayer.Width=w.Width;LightningLayer.Height=w.Height;Canvas.SetLeft(WindowFrame,w.X-3);Canvas.SetTop(WindowFrame,w.Y-3);WindowFrame.Width=w.Width+6;WindowFrame.Height=w.Height+6;Canvas.SetLeft(ClockObject,_sceneDefinition.ClockPosition.X);Canvas.SetTop(ClockObject,_sceneDefinition.ClockPosition.Y);_weatherEffects.SetViewport(w.Width,w.Height);BaseBackgroundLayer.Source=SceneDefinitionService.LoadImage(_sceneDefinition,_sceneDefinition.BaseBackground);BaseBackgroundLayer.Visibility=BaseBackgroundLayer.Source is null?Visibility.Collapsed:Visibility.Visible;}
    private void ApplyCharacterLayout(double scale)
    {
        scale = Math.Clamp(scale, 0.75, 2.0);
        var catScale = double.IsFinite(_settings.Current.CatScale) ? Math.Clamp(_settings.Current.CatScale, 0.02, 0.2) : AppSettings.DefaultCatScale;
        var catX = double.IsFinite(_settings.Current.CatOffsetX) ? _settings.Current.CatOffsetX : AppSettings.DefaultCatOffsetX;
        var catY = double.IsFinite(_settings.Current.CatOffsetY) ? _settings.Current.CatOffsetY : AppSettings.DefaultCatOffsetY;
        CatSprite.Width = _catImages.Master.PixelWidth * catScale;
        CatSprite.Height = _catImages.Master.PixelHeight * catScale;
        Canvas.SetLeft(CatSprite, catX);
        Canvas.SetTop(CatSprite, catY);
        Scene.Width = Math.Max(GirlSceneWidth, catX + CatSprite.Width);
        Scene.Height = Math.Max(GirlSceneHeight, catY + CatSprite.Height);
        CatLayer.Width = Scene.Width;
        CatLayer.Height = Scene.Height;
        Width = Scene.Width * scale;
        Height = Scene.Height * scale;
        Scene.LayoutTransform = new ScaleTransform(scale, scale);
    }
    private static double LampAmount(TimePeriod p)=>p is TimePeriod.Evening or TimePeriod.Night?0.85:0;
    private static System.Windows.Media.Brush BlendLayerColor(Dictionary<string,string> colors,TimePeriod from,TimePeriod to,double amount,string fallback){try{var ca=(MediaColor)System.Windows.Media.ColorConverter.ConvertFromString(colors.GetValueOrDefault(from.ToString(),fallback));var cb=(MediaColor)System.Windows.Media.ColorConverter.ConvertFromString(colors.GetValueOrDefault(to.ToString(),fallback));return new SolidColorBrush(MediaColor.FromArgb((byte)(ca.A+(cb.A-ca.A)*amount),(byte)(ca.R+(cb.R-ca.R)*amount),(byte)(ca.G+(cb.G-ca.G)*amount),(byte)(ca.B+(cb.B-ca.B)*amount)));}catch{return System.Windows.Media.Brushes.Transparent;}}
    private void UpdateClock(){if(!Dispatcher.CheckAccess()){Dispatcher.BeginInvoke(UpdateClock);return;}var display=_time.CurrentTime;
#if DEBUG
        if(_settings.Current.DebugMode&&Enum.TryParse<TimePeriod>(_settings.Current.DebugTime,true,out var debugPeriod))display=display.Date.AddHours(debugPeriod switch{TimePeriod.Morning=>6,TimePeriod.Day=>12,TimePeriod.Evening=>18,_=>23}).AddMinutes(30);
#endif
        ClockText.Text=display.ToString("HH:mm");DateText.Text=DateOnly.FromDateTime(display).ToString("dd/MM");ClockPanel.Visibility=_settings.Current.ShowClock?Visibility.Visible:Visibility.Collapsed;DateText.Visibility=_settings.Current.ShowDate?Visibility.Visible:Visibility.Collapsed;}
    private void OnWeatherChanged(WeatherInfo info){if(!Dispatcher.CheckAccess()){Dispatcher.BeginInvoke(()=>OnWeatherChanged(info));return;}ApplyWeatherVisual(info);}
    private void ApplyWeatherVisual(WeatherInfo info){var state=info.State;
        if(_settings.Current.DebugMode){if(Enum.TryParse<TimePeriod>(_settings.Current.DebugTime,true,out var p))_dayNight.SetDebugPeriod(p);if(Enum.TryParse<WeatherState>(_settings.Current.DebugWeather,true,out var w))state=w;}else _dayNight.SetDebugPeriod(null);
        _weatherEffects.Apply(state,_settings.Current.WeatherEffects,_settings.Current.EnableLightning);
        _ambience.Apply(state,_settings.Current.WeatherAmbienceAuto);
        var icon=state switch{WeatherState.Clear=>"SUN",WeatherState.PartlyCloudy=>"P.CL",WeatherState.Cloudy=>"CLD",WeatherState.Fog=>"FOG",WeatherState.Drizzle=>"DRZ",WeatherState.Rain=>"RAIN",WeatherState.HeavyRain=>"H.RN",WeatherState.Thunderstorm=>"THN",WeatherState.Snow=>"SNOW",_=>"--"};
        WeatherText.Text=info.Temperature is { } t?$"{icon} {t:0}°":icon;WeatherText.ToolTip=$"{T("Thời tiết từ Open-Meteo","Weather by Open-Meteo")} · {_weather.CityName} · {state} · {info.Temperature:0.#}°C · {T("Cập nhật","Updated")} {info.LastUpdated?.ToString("HH:mm")??T("chưa có","never")}";
    }
    private void OnLightningFlashed()=>_ambience.PlayThunder();
    private void OnDragStart(object s,MouseButtonEventArgs e)
    {
        if(MusicPopup.IsMouseOver||e.ChangedButton!=MouseButton.Left||e.ClickCount!=1)return;
        try
        {
            DragMove();
            if(_settings.Current.ShowOnTaskbar)
            {
                var screen=Forms.Screen.PrimaryScreen?.WorkingArea;
                var scale=PresentationSource.FromVisual(this)?.CompositionTarget?.TransformFromDevice.M11??1;
                if(screen is not null)_settings.Current.TaskbarOffsetX=(Left-screen.Value.Left*scale)/scale;
            }
            else _settings.Current.Left=Left;
            _settings.Current.Top=Top;
            _settings.Save();
            _lastMoveSave=DateTime.UtcNow;
        }
        catch { }
    }
    private void OnRightClick(object s,MouseButtonEventArgs e){var menu=new ContextMenu();void Add(string text,RoutedEventHandler action){var item=new MenuItem{Header=text};item.Click+=action;menu.Items.Add(item);}Add(_music.IsPlaying?T("Tạm dừng","Pause"):T("Phát","Play"),(_,_)=>ToggleMusic());Add(T("Bài tiếp","Next track"),(_,_)=>_music.Next());Add(T("Điều khiển nhạc","Music controls"),(_,_)=>OpenMusicPopup());menu.Items.Add(new Separator());Add(T("Cài đặt","Settings"),(_,_)=>OpenSettings());Add(T("Thoát","Exit"),(_,_)=>ExitApp());ContextMenu=menu;menu.IsOpen=true;}
    private void OnCatMouseDown(object s,MouseButtonEventArgs e){if(e.ClickCount>1){_catStates.ForceTailWag();Render();e.Handled=true;}}
    private void OnGirlClick(object s,MouseButtonEventArgs e){e.Handled=true;}
    private void OnCatClick(object s,MouseButtonEventArgs e){_catStates.ForceTailWag();Render();}
    private void OnRadioClick(object s,MouseButtonEventArgs e){OpenMusicPopup();e.Handled=true;}
    private void OpenMusicPopup(){MusicPopup.PlacementTarget=this;MusicPopup.Placement=System.Windows.Controls.Primitives.PlacementMode.Top;MusicPopup.IsOpen=true;UpdateTrack();}
    private void OnPrevious(object s,RoutedEventArgs e)=>_music.Previous(); private void OnNext(object s,RoutedEventArgs e)=>_music.Next(); private void OnPlayPause(object s,RoutedEventArgs e)=>ToggleMusic();
    private void OnShuffle(object s,RoutedEventArgs e){_music.ToggleShuffle();ShuffleButton.Opacity=_music.Shuffle?1:.55;} private void OnRepeat(object s,RoutedEventArgs e){_music.ToggleRepeat();RepeatButton.Opacity=_music.Repeat?1:.55;}
    private void OnFavorite(object s,RoutedEventArgs e){_music.ToggleFavorite();UpdateTrack();}
    private void ToggleMusic()=>_music.Toggle();
    private void UpdateTrack(){var track=_music.Current;TrackLabel.Text=track?.Title??T("Chưa có bài hát","No tracks yet");ArtistLabel.Text=track is null?T("Thêm nhạc để bắt đầu","Add music to get started"):$"{(string.IsNullOrWhiteSpace(track.Artist)?T("Nghệ sĩ chưa rõ","Unknown artist"):track.Artist)}{(track.IsMissing?T(" · Thiếu tệp"," · Missing file"):"")}";PlayButton.Content=_music.IsPlaying?"Ⅱ":"▶";FavoriteButton.Content=track?.IsFavorite==true?"♥":"♡";CurrentCover.Background=CoverBrush(track is null?0:_music.Tracks.ToList().FindIndex(t=>t.Id==track.Id));RefreshPlaylistTracks();UpdateProgress();}
    private void OnTrackChanged(Track? track){if(!Dispatcher.CheckAccess()){Dispatcher.BeginInvoke(()=>OnTrackChanged(track));return;}UpdateTrack();}
    private void OnPlaybackChanged(bool playing)=>Dispatcher.BeginInvoke(UpdateTrack);
    private void UpdateProgress(){if(!IsLoaded)return;var duration=_music.DurationSeconds;var position=_music.PositionSeconds;if(!_isSeeking){ProgressSlider.Maximum=Math.Max(duration,1);ProgressSlider.Value=Math.Clamp(position,0,ProgressSlider.Maximum);}ProgressLabel.Text=$"{FormatTime(position)} / {FormatTime(duration)}";}
    private static string FormatTime(double seconds)=>TimeSpan.FromSeconds(Math.Max(0,seconds)).ToString(seconds>=3600?@"h\:mm\:ss":@"mm\:ss");
    private void OnProgressChanged(object s,RoutedPropertyChangedEventArgs<double> e)=>UpdateProgress();
    private void OnSeekStart(object s,MouseButtonEventArgs e)=>_isSeeking=true;
    private void OnSeekEnd(object s,MouseButtonEventArgs e){_music.Seek(ProgressSlider.Value);_isSeeking=false;}
    private void PopulatePlaylists(){PlaylistCaption.Text=$"{T("Danh sách phát","Playlist")}  ·  {_music.SelectedPlaylist}";RefreshPlaylistTracks();}
    private void RefreshPlaylistTracks()
    {
        var currentId=_music.Current?.Id;
        var rows=_music.GetPlaylistTracks().Select((track,index)=>new MusicTrackRow(track.Id,track.Title,string.IsNullOrWhiteSpace(track.Artist)?T("Nghệ sĩ chưa rõ","Unknown artist"):track.Artist,FormatTime(track.Duration),track.Id==currentId,CoverBrush(index),T("Bấm để phát bài hát","Click to play track"))).ToList();
        PlaylistTracks.ItemsSource=rows;
        EmptyPlaylistHint.Visibility=rows.Count==0?Visibility.Visible:Visibility.Collapsed;
    }
    private sealed record MusicTrackRow(string TrackId,string Title,string Artist,string Duration,bool IsCurrent,System.Windows.Media.Brush CoverBrush,string PlayTooltip);
    private static System.Windows.Media.Brush CoverBrush(int index)
    {
        var colors=new (MediaColor Start,MediaColor End)[]
        {
            (MediaColor.FromRgb(113,99,164),MediaColor.FromRgb(240,145,113)),
            (MediaColor.FromRgb(79,123,174),MediaColor.FromRgb(226,166,151)),
            (MediaColor.FromRgb(166,87,63),MediaColor.FromRgb(240,169,104)),
            (MediaColor.FromRgb(42,75,139),MediaColor.FromRgb(204,114,141)),
            (MediaColor.FromRgb(131,91,133),MediaColor.FromRgb(244,168,121))
        };
        var pair=colors[Math.Abs(index)%colors.Length];
        return new LinearGradientBrush(pair.Start,pair.End,45);
    }
    private void OnMusicLibraryChanged(){if(!Dispatcher.CheckAccess()){Dispatcher.BeginInvoke(OnMusicLibraryChanged);return;}PopulatePlaylists();}
    private void OnPlaylistTrackClick(object sender,RoutedEventArgs e){if(sender is System.Windows.Controls.Button {Tag:string id})_music.PlayTrack(id);}
    private void OnPlaylistPickerClick(object sender,RoutedEventArgs e)
    {
        var menu=new ContextMenu{PlacementTarget=PlaylistPickerButton,Placement=System.Windows.Controls.Primitives.PlacementMode.Top};
        foreach(var name in _music.Playlists)
        {
            var item=new MenuItem{Header=name,IsCheckable=true,IsChecked=name==_music.SelectedPlaylist};
            item.Click+=(_,_)=>{_music.SelectPlaylist(name);PopulatePlaylists();};
            menu.Items.Add(item);
        }
        menu.IsOpen=true;
    }
    private void OnMusicOptionsClick(object sender,RoutedEventArgs e)
    {
        var menu=new ContextMenu{PlacementTarget=MusicOptionsButton,Placement=System.Windows.Controls.Primitives.PlacementMode.Top};
        void Add(string label,RoutedEventHandler handler){var item=new MenuItem{Header=label};item.Click+=handler;menu.Items.Add(item);}
        Add(T("Thêm tệp nhạc","Add music files"),OnAddFiles);
        Add(T("Thêm thư mục nhạc","Add music folder"),OnAddFolder);
        Add(T("Mở thư viện nhạc","Open music library"),OnOpenLibrary);
        menu.Items.Add(new Separator());
        var volume=new Slider{Minimum=0,Maximum=1,Value=_music.Volume,Width=145,Margin=new Thickness(10,3,10,8)};
        volume.ValueChanged+=OnVolumeChanged;
        menu.Items.Add(new MenuItem{Header=T("Âm lượng","Volume"),StaysOpenOnClick=true});
        menu.Items.Add(volume);
        menu.IsOpen=true;
    }
    private void OnAddFiles(object s,RoutedEventArgs e){var dialog=new Microsoft.Win32.OpenFileDialog{Multiselect=true,Filter="Audio files (*.mp3;*.wav)|*.mp3;*.wav|MP3 (*.mp3)|*.mp3|WAV (*.wav)|*.wav"};if(dialog.ShowDialog(this)==true)ShowAddFeedback(_music.AddFiles(dialog.FileNames));}
    private void OnAddFolder(object s,RoutedEventArgs e){using var dialog=new Forms.FolderBrowserDialog();if(dialog.ShowDialog()==Forms.DialogResult.OK)ShowAddFeedback(_music.AddFolder(dialog.SelectedPath));}
    private void OnRemoveMissing(object s,RoutedEventArgs e){_music.RemoveMissingTracks();UpdateTrack();}
    private void OnOpenLibrary(object s,RoutedEventArgs e){var w=new MusicLibraryWindow(_music){Owner=this};w.Show();}
    private void OnManagePlaylists(object s,RoutedEventArgs e){var w=new PlaylistWindow(_music){Owner=this};w.Closed+=(_,_)=>PopulatePlaylists();w.Show();}
    private void OnDragOver(object s,System.Windows.DragEventArgs e)=>e.Effects=e.Data.GetDataPresent(System.Windows.DataFormats.FileDrop)?System.Windows.DragDropEffects.Copy:System.Windows.DragDropEffects.None;
    private void OnFileDrop(object s,System.Windows.DragEventArgs e){if(e.Data.GetData(System.Windows.DataFormats.FileDrop) is string[] paths)ShowAddFeedback(_music.AddFiles(paths));}
    private readonly DispatcherTimer _feedbackTimer=new(){Interval=TimeSpan.FromSeconds(2)};
    private void ShowAddFeedback(int count){DropFeedbackText.Text=count==0?T("Không thêm được bài hát phù hợp","No supported tracks added"):T($"Đã thêm {count} bài hát",$"{count} track{(count==1?"":"s")} added");DropFeedback.Visibility=Visibility.Visible;_feedbackTimer.Stop();_feedbackTimer.Tick-=HideFeedback;_feedbackTimer.Tick+=HideFeedback;_feedbackTimer.Start();}
    private void HideFeedback(object? sender,EventArgs e){DropFeedback.Visibility=Visibility.Collapsed;_feedbackTimer.Stop();}
    private void ApplyCharacterScalePreview(double scale)
    {
        scale = Math.Clamp(scale, 0.75, 2.0);
        ApplyCharacterLayout(scale);
        PositionOnTaskbar();
    }
    private void OnVolumeChanged(object s,RoutedPropertyChangedEventArgs<double> e){if(_music==null)return;_music.Volume=e.NewValue;_settings.Current.Volume=e.NewValue;_settings.Save();}
    private void OpenSettings()
    {
        var window=new SettingsWindow(_settings,_music){Owner=this};
        window.CharacterScalePreviewChanged += ApplyCharacterScalePreview;
        window.Closed+=(_,_)=>
        {
            ApplyTaskbarVisibility();
            UpdateBlinkEligibility();
            ApplyWindowLayering();
            ApplyCharacterLayout(_settings.Current.Scale);
            PositionOnTaskbar();
            _dayNight.RefreshSetting(_settings.Current.AutoDayNight);
            _weather.SetInterval(_settings.Current.WeatherRefreshMinutes);
            _weather.SetLocation(new LocationSettings{CityName=_settings.Current.CityName,Latitude=_settings.Current.Latitude,Longitude=_settings.Current.Longitude});
            _weather.SetEnabled(_settings.Current.EnableRealWeather);
            _ambience.SetVolume(_settings.Current.WeatherVolume);
            _music.Volume=_settings.Current.Volume;
            _settings.Save();
            ApplyLanguage();
            foreach(Window open in System.Windows.Application.Current.Windows)
            {
                if(open is PlaylistWindow playlists)playlists.RefreshLanguage();
                if(open is MusicLibraryWindow library)library.RefreshLanguage();
            }
            ApplyWeatherVisual(_weather.Current);
            UpdateClock();
        };
        window.Show();
    }
    protected override void OnClosing(CancelEventArgs e){if(!_allowExit){e.Cancel=true;Hide();}else base.OnClosing(e);}
    private bool _allowExit;
    private void ExitApp(){_allowExit=true;_settings.Save();System.Windows.Application.Current.Shutdown();}
    public void Dispose(){if(_disposed)return;_disposed=true;_allowExit=true;IsVisibleChanged-=OnAnimationVisibilityChanged;StateChanged-=OnAnimationWindowStateChanged;_timer.Stop();_feedbackTimer.Stop();_music.TrackChanged-=OnTrackChanged;_music.PlaybackChanged-=OnPlaybackChanged;_music.LibraryChanged-=OnMusicLibraryChanged;_weatherEffects.LightningFlashed-=OnLightningFlashed;_ambience.Dispose();_weatherEffects.Dispose();_weather.WeatherChanged-=OnWeatherChanged;_weather.Dispose();_dayNight.TransitionUpdated-=OnDayNightTransition;_dayNight.Dispose();_time.Updated-=UpdateClock;_time.Dispose();_input.KeyboardActivity-=OnKeyboard;_input.MouseActivity-=OnMouse;_input.Dispose();_blink.Dispose();_tray.Visible=false;_tray.Dispose();_music.Dispose();LoggerService.Info("DeskLofi shutdown.");}
}
