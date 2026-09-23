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
    private readonly SettingsService _settings; private readonly TimeService _time=new(); private readonly DayNightService _dayNight; private readonly SceneDefinition _sceneDefinition; private readonly SceneDefinitionService _sceneDefinitions=new(); private readonly WeatherService _weather; private readonly WeatherEffectController _weatherEffects; private readonly AmbienceService _ambience; private readonly ActivityTracker _activity=new(); private readonly InputMonitor _input; private readonly CompanionStateManager _states; private readonly SpriteLoader _sprites=new(); private readonly SpriteAnimationController _girlAnimation; private readonly AnimationController _animation=new(); private readonly MusicService _music; private readonly DispatcherTimer _timer; private readonly Forms.NotifyIcon _tray; private long _lastAnimationTick=Stopwatch.GetTimestamp(); private int _ticks; private bool? _lastGirlFallback; private bool _isSeeking,_initializingMusicUi,_disposed; private TimePeriod? _assetFrom,_assetTo; private DateTime _lastMoveSave=DateTime.MinValue;
#if DEBUG
    private System.Windows.Controls.Primitives.Popup? _animationDebugPopup;
    private TextBlock? _animationDebugText;
    private string? _debugAnimationOverride;
#endif
    public MainWindow(SettingsService settings)
    {
        InitializeComponent(); _settings=settings; LoggerService.Info("DeskLofi startup."); _time.Updated+=UpdateClock; _sceneDefinition=_sceneDefinitions.Load(settings.Current.Scene); _dayNight=new DayNightService(_time,settings.Current.AutoDayNight); _dayNight.TransitionUpdated+=OnDayNightTransition; _weatherEffects=new WeatherEffectController(WeatherLayer,LightningLayer); _weather=new WeatherService(new LocationSettings{CityName=settings.Current.CityName,Latitude=settings.Current.Latitude,Longitude=settings.Current.Longitude},settings.Current.WeatherRefreshMinutes,settings.Current.EnableRealWeather); _weather.WeatherChanged+=OnWeatherChanged; _weatherEffects.LightningFlashed+=OnLightningFlashed; _weatherEffects.Apply(_weather.Current.State,settings.Current.WeatherEffects,settings.Current.EnableLightning); _states=new(_activity,settings.Current); _girlAnimation=new SpriteAnimationController(SpriteAnimationController.LoadCatalog(Path.Combine(AppContext.BaseDirectory,"Data","girl-animations.json")), name=>[_sprites.GetFrame("Girl",name,0)]); _girlAnimation.Play("Idle"); _input=new(); _input.KeyboardActivity+=OnKeyboard; _input.MouseActivity+=OnMouse; _states.StateChanged+=(_,_)=>UpdateGirlAnimation();
        _music=new(settings); _ambience=new(settings.Current.WeatherVolume); _initializingMusicUi=true;VolumeSlider.Value=settings.Current.Volume;ShuffleButton.Opacity=_music.Shuffle?1:.55;RepeatButton.Opacity=_music.Repeat?1:.55;PopulatePlaylists();_initializingMusicUi=false;_music.TrackChanged+=OnTrackChanged;_music.PlaybackChanged+=OnPlaybackChanged;
        _timer=new(){Interval=TimeSpan.FromMilliseconds(50)};_timer.Tick+=(_,_)=>{var now=Stopwatch.GetTimestamp();_girlAnimation.Advance(Stopwatch.GetElapsedTime(_lastAnimationTick,now));_lastAnimationTick=now;_animation.Advance();if(++_ticks%2==0)_states.Tick();if(_ticks%20==0)UpdateProgress();Render();};_timer.Start();
        _tray=new Forms.NotifyIcon{Text="DeskLofi",Icon=System.Drawing.SystemIcons.Application,Visible=true};_tray.ContextMenuStrip=new Forms.ContextMenuStrip();_tray.ContextMenuStrip.Items.Add("Show DeskLofi",null,(_,_)=>Show());_tray.ContextMenuStrip.Items.Add("Play / Pause",null,(_,_)=>ToggleMusic());_tray.ContextMenuStrip.Items.Add("Next Track",null,(_,_)=>_music.Next());_tray.ContextMenuStrip.Items.Add("Settings",null,(_,_)=>OpenSettings());_tray.ContextMenuStrip.Items.Add("Exit",null,(_,_)=>ExitApp());_tray.DoubleClick+=(_,_)=>Show();
        Topmost=settings.Current.AlwaysOnTop; Width=300*settings.Current.Scale;Height=126*settings.Current.Scale;Scene.LayoutTransform=new ScaleTransform(settings.Current.Scale,settings.Current.Scale);
#if DEBUG
        CreateAnimationDebugPanel();
#endif
    }
    private void OnLoaded(object sender,RoutedEventArgs e){PositionOnTaskbar();ApplySceneDefinition();UpdateClock();UpdateDayNight(_dayNight.CurrentPeriod,_dayNight.CurrentPeriod,1);ApplyWeatherVisual(_weather.Current);Render();if(_settings.Current.AutoPlayMusic)_music.Toggle();}
    private void OnSourceInitialized(object? sender,EventArgs e){var hwnd=new WindowInteropHelper(this).Handle;var ex=GetWindowLong(hwnd,-20);SetWindowLong(hwnd,-20,ex|0x08000000|0x00000080);}
    [System.Runtime.InteropServices.DllImport("user32.dll",EntryPoint="GetWindowLongPtrW")] private static extern IntPtr GetWindowLongPtr(IntPtr hWnd,int index);
    [System.Runtime.InteropServices.DllImport("user32.dll",EntryPoint="SetWindowLongPtrW")] private static extern IntPtr SetWindowLongPtr(IntPtr hWnd,int index,IntPtr value);
    private static int GetWindowLong(IntPtr hwnd,int index)=>unchecked((int)GetWindowLongPtr(hwnd,index).ToInt64());
    private static void SetWindowLong(IntPtr hwnd,int index,int value)=>SetWindowLongPtr(hwnd,index,new IntPtr(value));
    private void PositionOnTaskbar(){var wa=Forms.Screen.PrimaryScreen?.WorkingArea;if(wa==null)return;var scale=PresentationSource.FromVisual(this)?.CompositionTarget?.TransformFromDevice.M11??1;Left=_settings.Current.Left>=0?_settings.Current.Left:(wa.Value.Width/2-Width/2)*scale;Top=_settings.Current.Top>=0?_settings.Current.Top:(wa.Value.Bottom*scale-Height);}
    private void OnKeyboard(DateTime at)=>Dispatcher.BeginInvoke(()=>{_activity.Keyboard(at);_states.Tick();Render();},DispatcherPriority.Input);
    private void OnMouse(DateTime at)=>Dispatcher.BeginInvoke(()=>{_activity.Mouse(at);_states.Tick();},DispatcherPriority.Input);
    private void UpdateGirlAnimation()
    {
        var animation = _states.Girl.ToString();
#if DEBUG
        animation = _debugAnimationOverride ?? animation;
#endif
        _girlAnimation.Play(animation);
        Render();
    }
    private void Render(){if(!IsLoaded)return;var source=_girlAnimation.CurrentFrame;if(!ReferenceEquals(GirlSprite.Source,source))GirlSprite.Source=source;var fallback=_girlAnimation.UsingFallback;if(_lastGirlFallback!=fallback){GirlSprite.Width=fallback?64:112;GirlSprite.Height=fallback?64:106;Canvas.SetTop(GirlSprite,fallback?42:0);MonitorArtwork.Visibility=fallback?Visibility.Visible:Visibility.Collapsed;_lastGirlFallback=fallback;}var cat=_sprites.GetFrame("Cat",_states.Cat.ToString(),_animation.Frame/10);if(!ReferenceEquals(CatSprite.Source,cat))CatSprite.Source=cat;
#if DEBUG
        if(_animationDebugPopup?.IsOpen==true&&_animationDebugText!=null)_animationDebugText.Text=$"Girl: {_states.Girl}  Animation: {_girlAnimation.CurrentAnimation}  FPS: {_girlAnimation.CurrentFps:0.#}  Keys/s: {_activity.KeyboardEventsPerSecond(DateTime.UtcNow,TimeSpan.FromMilliseconds(Math.Max(100,_settings.Current.FastTypingWindowMs))):0.0}";
#endif
    }
#if DEBUG
    private void CreateAnimationDebugPanel()
    {
        var panel = new StackPanel();
        _animationDebugText = new TextBlock { Foreground = System.Windows.Media.Brushes.White, Margin = new Thickness(4), FontSize = 11 };
        panel.Children.Add(_animationDebugText);
        var animations = new WrapPanel();
        foreach (var name in new[] { "Idle", "Typing", "TypingFast" })
        {
            var animation = name;
            var button = new System.Windows.Controls.Button { Content = animation, Margin = new Thickness(2) };
            button.Click += (_, _) => { _debugAnimationOverride = animation; UpdateGirlAnimation(); };
            animations.Children.Add(button);
        }
        var auto = new System.Windows.Controls.Button { Content = "Auto", Margin = new Thickness(2) };
        auto.Click += (_, _) => { _debugAnimationOverride = null; _girlAnimation.SetFpsOverride(null); UpdateGirlAnimation(); };
        animations.Children.Add(auto); panel.Children.Add(animations);
        var speeds = new WrapPanel();
        foreach (var fps in new[] { 2, 4, 6, 8, 10, 12 })
        {
            var speed = fps;
            var button = new System.Windows.Controls.Button { Content = $"{fps} FPS", Margin = new Thickness(2) };
            button.Click += (_, _) => { _girlAnimation.SetFpsOverride(speed); Render(); };
            speeds.Children.Add(button);
        }
        panel.Children.Add(speeds);
        _animationDebugPopup = new System.Windows.Controls.Primitives.Popup { PlacementTarget = this, Placement = System.Windows.Controls.Primitives.PlacementMode.Top,
            StaysOpen = true, AllowsTransparency = true,
            Child = new Border { Background = new SolidColorBrush(MediaColor.FromArgb(240, 30, 29, 45)),
                BorderBrush = System.Windows.Media.Brushes.LightGray, BorderThickness = new Thickness(1), Padding = new Thickness(5), Child = panel } };
        _tray.ContextMenuStrip!.Items.Insert(4, new Forms.ToolStripMenuItem("Animation debug", null, (_, _) => _animationDebugPopup.IsOpen = !_animationDebugPopup.IsOpen));
    }
#endif
    private void OnDayNightTransition(TimePeriod from,TimePeriod to,double amount)=>UpdateDayNight(from,to,amount);
    private void UpdateDayNight(TimePeriod from,TimePeriod to,double amount){SkyLayer.Fill=BlendLayerColor(_sceneDefinition.SkyColors,from,to,amount,"#FF25365D");LightingLayer.Fill=BlendLayerColor(_sceneDefinition.LightingColors,from,to,amount,"#00000000");LampGlow.Opacity=LampAmount(from)+(LampAmount(to)-LampAmount(from))*amount;if(_assetFrom!=from||_assetTo!=to){_assetFrom=from;_assetTo=to;SkyImageFrom.Source=SceneDefinitionService.LoadImage(_sceneDefinition,_sceneDefinition.SkyAssets.GetValueOrDefault(from.ToString()));SkyImageTo.Source=SceneDefinitionService.LoadImage(_sceneDefinition,_sceneDefinition.SkyAssets.GetValueOrDefault(to.ToString()));LightingImageFrom.Source=SceneDefinitionService.LoadImage(_sceneDefinition,_sceneDefinition.LightingAssets.GetValueOrDefault(from.ToString()));LightingImageTo.Source=SceneDefinitionService.LoadImage(_sceneDefinition,_sceneDefinition.LightingAssets.GetValueOrDefault(to.ToString()));}SkyImageFrom.Opacity=SkyImageFrom.Source is null?0:1-amount;SkyImageTo.Opacity=SkyImageTo.Source is null?0:amount;LightingImageFrom.Opacity=LightingImageFrom.Source is null?0:1-amount;LightingImageTo.Opacity=LightingImageTo.Source is null?0:amount;}
    private void ApplySceneDefinition(){var w=_sceneDefinition.WindowBounds;Canvas.SetLeft(SkyLayer,w.X);Canvas.SetTop(SkyLayer,w.Y);SkyLayer.Width=w.Width;SkyLayer.Height=w.Height;foreach(var image in new[]{SkyImageFrom,SkyImageTo}){Canvas.SetLeft(image,w.X);Canvas.SetTop(image,w.Y);image.Width=w.Width;image.Height=w.Height;}Canvas.SetLeft(WeatherLayer,w.X);Canvas.SetTop(WeatherLayer,w.Y);Canvas.SetLeft(LightningLayer,w.X);Canvas.SetTop(LightningLayer,w.Y);LightningLayer.Width=w.Width;LightningLayer.Height=w.Height;Canvas.SetLeft(WindowFrame,w.X-3);Canvas.SetTop(WindowFrame,w.Y-3);WindowFrame.Width=w.Width+6;WindowFrame.Height=w.Height+6;Canvas.SetLeft(GirlContainer,_sceneDefinition.GirlPosition.X);Canvas.SetTop(GirlContainer,_sceneDefinition.GirlPosition.Y);Canvas.SetLeft(CatSprite,_sceneDefinition.CatPosition.X);Canvas.SetTop(CatSprite,_sceneDefinition.CatPosition.Y);Canvas.SetLeft(ClockObject,_sceneDefinition.ClockPosition.X);Canvas.SetTop(ClockObject,_sceneDefinition.ClockPosition.Y);_weatherEffects.SetViewport(w.Width,w.Height);BaseBackgroundLayer.Source=SceneDefinitionService.LoadImage(_sceneDefinition,_sceneDefinition.BaseBackground);BaseBackgroundLayer.Visibility=BaseBackgroundLayer.Source is null?Visibility.Collapsed:Visibility.Visible;}
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
        WeatherText.Text=info.Temperature is { } t?$"{icon} {t:0}°":icon;WeatherText.ToolTip=$"Weather by Open-Meteo · {_weather.CityName} · {state} · {info.Temperature:0.#}°C · Updated {info.LastUpdated?.ToString("HH:mm")??"never"}";
    }
    private void OnLightningFlashed()=>_ambience.PlayThunder();
    private void OnDragStart(object s,MouseButtonEventArgs e){if(GirlSprite.IsMouseOver||CatSprite.IsMouseOver||Radio.IsMouseOver||MusicPopup.IsMouseOver)return;if(e.ChangedButton==MouseButton.Left&&e.ClickCount==1){try{DragMove();_settings.Current.Left=Left;_settings.Current.Top=Top;_settings.Save();_lastMoveSave=DateTime.UtcNow;}catch{}}}
    private void OnRightClick(object s,MouseButtonEventArgs e){var menu=new ContextMenu();void Add(string text,RoutedEventHandler action){var item=new MenuItem{Header=text};item.Click+=action;menu.Items.Add(item);}Add(_music.IsPlaying?"Pause":"Play",(_,_)=>ToggleMusic());Add("Next Track",(_,_)=>_music.Next());menu.Items.Add(new Separator());var scenes=new MenuItem{Header="Scene"};scenes.Items.Add(new MenuItem{Header="Bedroom",IsChecked=true});menu.Items.Add(scenes);menu.Items.Add(new Separator());Add("Hide Companion",(_,_)=>Hide());Add("Settings",(_,_)=>OpenSettings());Add("Exit",(_,_)=>ExitApp());ContextMenu=menu;menu.IsOpen=true;}
    private void OnCatMouseDown(object s,MouseButtonEventArgs e){if(e.ClickCount>1){_states.ReactToCat(true);e.Handled=true;}}
    private void OnGirlClick(object s,MouseButtonEventArgs e){_states.ReactToGirl();}
    private void OnCatClick(object s,MouseButtonEventArgs e){_states.ReactToCat();}
    private void OnRadioClick(object s,MouseButtonEventArgs e){MusicPopup.IsOpen=!MusicPopup.IsOpen;UpdateTrack();e.Handled=true;}
    private void OnPrevious(object s,RoutedEventArgs e)=>_music.Previous(); private void OnNext(object s,RoutedEventArgs e)=>_music.Next(); private void OnPlayPause(object s,RoutedEventArgs e)=>ToggleMusic();
    private void OnShuffle(object s,RoutedEventArgs e){_music.ToggleShuffle();ShuffleButton.Opacity=_music.Shuffle?1:.55;} private void OnRepeat(object s,RoutedEventArgs e){_music.ToggleRepeat();RepeatButton.Opacity=_music.Repeat?1:.55;}
    private void OnFavorite(object s,RoutedEventArgs e){_music.ToggleFavorite();UpdateTrack();}
    private void ToggleMusic()=>_music.Toggle();
    private void UpdateTrack(){var track=_music.Current;TrackLabel.Text=track?.Title??"Add music files";ArtistLabel.Text=track is null?"":$"{track.Artist}{(string.IsNullOrWhiteSpace(track.Album)?"":$" · {track.Album}")}{(track.IsMissing?" · MISSING":"")}";PlayButton.Content=_music.IsPlaying?"Ⅱ":"▶";FavoriteButton.Content=track?.IsFavorite==true?"♥":"♡";UpdateProgress();}
    private void OnTrackChanged(Track? track){if(!Dispatcher.CheckAccess()){Dispatcher.BeginInvoke(()=>OnTrackChanged(track));return;}UpdateTrack();}
    private void OnPlaybackChanged(bool playing)=>Dispatcher.BeginInvoke(UpdateTrack);
    private void UpdateProgress(){if(!IsLoaded)return;var duration=_music.DurationSeconds;var position=_music.PositionSeconds;if(!_isSeeking){ProgressSlider.Maximum=Math.Max(duration,1);ProgressSlider.Value=Math.Clamp(position,0,ProgressSlider.Maximum);}ProgressLabel.Text=$"{FormatTime(position)} / {FormatTime(duration)}";}
    private static string FormatTime(double seconds)=>TimeSpan.FromSeconds(Math.Max(0,seconds)).ToString(seconds>=3600?@"h\:mm\:ss":@"mm\:ss");
    private void OnProgressChanged(object s,RoutedPropertyChangedEventArgs<double> e)=>UpdateProgress();
    private void OnSeekStart(object s,MouseButtonEventArgs e)=>_isSeeking=true;
    private void OnSeekEnd(object s,MouseButtonEventArgs e){_music.Seek(ProgressSlider.Value);_isSeeking=false;}
    private void PopulatePlaylists(){_initializingMusicUi=true;PlaylistSelector.Items.Clear();foreach(var item in _music.Playlists)PlaylistSelector.Items.Add(item);PlaylistSelector.SelectedItem=_music.SelectedPlaylist;_initializingMusicUi=false;}
    private void OnPlaylistChanged(object s,SelectionChangedEventArgs e){if(!_initializingMusicUi&&PlaylistSelector.SelectedItem is string name)_music.SelectPlaylist(name);}
    private void OnAddFiles(object s,RoutedEventArgs e){var dialog=new Microsoft.Win32.OpenFileDialog{Multiselect=true,Filter="Audio files (*.mp3;*.wav)|*.mp3;*.wav|MP3 (*.mp3)|*.mp3|WAV (*.wav)|*.wav"};if(dialog.ShowDialog(this)==true)ShowAddFeedback(_music.AddFiles(dialog.FileNames));}
    private void OnAddFolder(object s,RoutedEventArgs e){using var dialog=new Forms.FolderBrowserDialog();if(dialog.ShowDialog()==Forms.DialogResult.OK)ShowAddFeedback(_music.AddFolder(dialog.SelectedPath));}
    private void OnRemoveMissing(object s,RoutedEventArgs e){_music.RemoveMissingTracks();UpdateTrack();}
    private void OnOpenLibrary(object s,RoutedEventArgs e){var w=new MusicLibraryWindow(_music){Owner=this};w.Show();}
    private void OnManagePlaylists(object s,RoutedEventArgs e){var w=new PlaylistWindow(_music){Owner=this};w.Closed+=(_,_)=>PopulatePlaylists();w.Show();}
    private void OnDragOver(object s,System.Windows.DragEventArgs e)=>e.Effects=e.Data.GetDataPresent(System.Windows.DataFormats.FileDrop)?System.Windows.DragDropEffects.Copy:System.Windows.DragDropEffects.None;
    private void OnFileDrop(object s,System.Windows.DragEventArgs e){if(e.Data.GetData(System.Windows.DataFormats.FileDrop) is string[] paths)ShowAddFeedback(_music.AddFiles(paths));}
    private readonly DispatcherTimer _feedbackTimer=new(){Interval=TimeSpan.FromSeconds(2)};
    private void ShowAddFeedback(int count){DropFeedbackText.Text=count==0?"No supported tracks added":$"{count} track{(count==1?"":"s")} added";DropFeedback.Visibility=Visibility.Visible;_feedbackTimer.Stop();_feedbackTimer.Tick-=HideFeedback;_feedbackTimer.Tick+=HideFeedback;_feedbackTimer.Start();}
    private void HideFeedback(object? sender,EventArgs e){DropFeedback.Visibility=Visibility.Collapsed;_feedbackTimer.Stop();}
    private void OnVolumeChanged(object s,RoutedPropertyChangedEventArgs<double> e){if(_music==null)return;_music.Volume=e.NewValue;_settings.Current.Volume=e.NewValue;_settings.Save();}
    private void OpenSettings(){var w=new SettingsWindow(_settings,_music){Owner=this};w.Closed+=(_,_)=>{Topmost=_settings.Current.AlwaysOnTop;Width=300*_settings.Current.Scale;Height=126*_settings.Current.Scale;Scene.LayoutTransform=new ScaleTransform(_settings.Current.Scale,_settings.Current.Scale);_dayNight.RefreshSetting(_settings.Current.AutoDayNight);_weather.SetInterval(_settings.Current.WeatherRefreshMinutes);_weather.SetLocation(new LocationSettings{CityName=_settings.Current.CityName,Latitude=_settings.Current.Latitude,Longitude=_settings.Current.Longitude});_weather.SetEnabled(_settings.Current.EnableRealWeather);_ambience.SetVolume(_settings.Current.WeatherVolume);_music.Volume=_settings.Current.Volume;_settings.Save();ApplyWeatherVisual(_weather.Current);UpdateClock();};w.Show();}
    protected override void OnClosing(CancelEventArgs e){if(!_allowExit){e.Cancel=true;Hide();}else base.OnClosing(e);}
    private bool _allowExit;
    private void ExitApp(){_allowExit=true;_settings.Save();System.Windows.Application.Current.Shutdown();}
    public void Dispose(){if(_disposed)return;_disposed=true;_allowExit=true;_timer.Stop();_feedbackTimer.Stop();_music.TrackChanged-=OnTrackChanged;_music.PlaybackChanged-=OnPlaybackChanged;_weatherEffects.LightningFlashed-=OnLightningFlashed;_ambience.Dispose();_weatherEffects.Dispose();_weather.WeatherChanged-=OnWeatherChanged;_weather.Dispose();_dayNight.TransitionUpdated-=OnDayNightTransition;_dayNight.Dispose();_time.Updated-=UpdateClock;_time.Dispose();_input.KeyboardActivity-=OnKeyboard;_input.MouseActivity-=OnMouse;_input.Dispose();_tray.Visible=false;_tray.Dispose();_music.Dispose();LoggerService.Info("DeskLofi shutdown.");}
}
