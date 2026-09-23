using System.Windows.Controls;
using System.Windows;
using System.Windows.Media;
using System.Windows.Shapes;
using System.Windows.Threading;
using DeskLofi.Models;
using WpfRectangle = System.Windows.Shapes.Rectangle;
using MediaColor = System.Windows.Media.Color;
using MediaBrushes = System.Windows.Media.Brushes;

namespace DeskLofi.Services;

public sealed class WeatherEffectController : IDisposable
{
    private readonly Canvas _layer;
    private readonly WpfRectangle _flashLayer;
    private readonly DispatcherTimer _rainTimer = new() { Interval = TimeSpan.FromMilliseconds(125) };
    private readonly DispatcherTimer _lightningTimer = new();
    private readonly DispatcherTimer _flashFade = new() { Interval = TimeSpan.FromMilliseconds(55) };
    private readonly List<WpfRectangle> _particles = [];
    private readonly List<WpfRectangle> _staticClouds = [];
    private readonly List<WpfRectangle> _fogLayers = [];
    private readonly Random _random = new();
    private WeatherState _state;
    private int _count;
    private double _fallSpeed = 3;
    private bool _enabled = true;
    private bool _lightningEnabled = true;
    private bool _snow;
    private double _flashOpacity;
    private double _viewportWidth=40,_viewportHeight=44;
    public event Action? LightningFlashed;
    public int ParticleCount => _count;
    public bool IsAnimating => _rainTimer.IsEnabled;

    public WeatherEffectController(Canvas layer, WpfRectangle flashLayer)
    {
        _layer = layer; _flashLayer = flashLayer;
        _rainTimer.Tick += AnimateParticles;
        _lightningTimer.Tick += (_, _) => FlashLightning();
        _flashFade.Tick += (_, _) => { _flashOpacity = Math.Max(0, _flashOpacity - .12); _flashLayer.Opacity = _flashOpacity; if (_flashOpacity <= 0) _flashFade.Stop(); };
        for (var i=0;i<30;i++) { var rect=new WpfRectangle{IsHitTestVisible=false};Canvas.SetLeft(rect,_random.Next(0,40));Canvas.SetTop(rect,-_random.Next(0,44));_particles.Add(rect);_layer.Children.Add(rect); }
        for(var i=0;i<4;i++){var f=new WpfRectangle{Width=40,Height=6,Fill=new SolidColorBrush(MediaColor.FromArgb((byte)(70+i*9),220,225,225)),Visibility=Visibility.Collapsed,IsHitTestVisible=false};Canvas.SetLeft(f,0);Canvas.SetTop(f,8+i*9);_layer.Children.Add(f);_fogLayers.Add(f);}
        CreateClouds(); SetLightningInterval(); Apply(WeatherState.Unknown, true, true);
    }
    public void Apply(WeatherState state, bool effectsEnabled, bool lightningEnabled)
    {
        _state=state;_enabled=effectsEnabled;_lightningEnabled=lightningEnabled;
        foreach(var cloud in _staticClouds) cloud.Visibility=Visibility.Collapsed;
        foreach(var fog in _fogLayers)fog.Visibility=Visibility.Collapsed;
        _snow=false;_count=0;_fallSpeed=3;
        if(_enabled) switch(state)
        {
            case WeatherState.Cloudy: foreach(var c in _staticClouds)c.Visibility=Visibility.Visible;break;
            case WeatherState.Fog: foreach(var f in _fogLayers)f.Visibility=Visibility.Visible;break;
            case WeatherState.Drizzle:_count=7;_fallSpeed=2;break;
            case WeatherState.Rain:_count=15;_fallSpeed=3.5;break;
            case WeatherState.HeavyRain:_count=27;_fallSpeed=5;break;
            case WeatherState.Thunderstorm:_count=22;_fallSpeed=4.5;break;
            case WeatherState.Snow:_count=14;_fallSpeed=1.25;_snow=true;break;
        }
        for(var i=0;i<_particles.Count;i++)_particles[i].Visibility=i<_count?Visibility.Visible:Visibility.Collapsed;
        var raining=_enabled&&_count>0;
        if(raining){if(!_rainTimer.IsEnabled)_rainTimer.Start();}
        else _rainTimer.Stop();
        if(_enabled&&_state==WeatherState.Thunderstorm&&_lightningEnabled){if(!_lightningTimer.IsEnabled){SetLightningInterval();_lightningTimer.Start();}}
        else {_lightningTimer.Stop();_flashFade.Stop();_flashOpacity=0;_flashLayer.Opacity=0;}
    }
    public void SetViewport(double width,double height)
    {
        _viewportWidth=Math.Max(1,width);_viewportHeight=Math.Max(1,height);_layer.Width=_viewportWidth;_layer.Height=_viewportHeight;
        foreach(var cloud in _staticClouds)cloud.Width=Math.Min(cloud.Width,_viewportWidth);
        foreach(var fog in _fogLayers)fog.Width=_viewportWidth;
        foreach(var p in _particles){Canvas.SetLeft(p,_random.Next(0,(int)_viewportWidth));Canvas.SetTop(p,-_random.Next(1,(int)_viewportHeight));}
    }
    private void AnimateParticles(object? sender,EventArgs e)
    {
        for(var i=0;i<_count;i++)
        {
            var p=_particles[i];var y=Canvas.GetTop(p)+_fallSpeed+(i%4)*.2;
            if(y>_viewportHeight){y=-_random.Next(3,Math.Max(4,(int)_viewportHeight));Canvas.SetLeft(p,_random.Next(0,(int)_viewportWidth));}
            Canvas.SetTop(p,y);
            if(_snow){p.Width=2;p.Height=2;p.Fill=MediaBrushes.White;}
            else {p.Width=_state==WeatherState.HeavyRain?2:1;p.Height=_state==WeatherState.Drizzle?3:6;p.Fill=_state==WeatherState.Drizzle?MediaBrushes.LightBlue:MediaBrushes.LightCyan;}
            if(Canvas.GetTop(p)==0)Canvas.SetLeft(p,_random.Next(0,(int)_viewportWidth));
        }
    }
    private void CreateClouds()
    {
        foreach(var (x,y,w,h) in new[]{(3d,3d,17d,3d),(14d,1d,16d,4d),(26d,4d,12d,3d)})
        {var c=new WpfRectangle{Width=w,Height=h,Fill=new SolidColorBrush(MediaColor.FromArgb(125,230,236,230)),Visibility=Visibility.Collapsed,IsHitTestVisible=false};Canvas.SetLeft(c,x);Canvas.SetTop(c,y);_layer.Children.Add(c);_staticClouds.Add(c);}
    }
    private void SetLightningInterval()=>_lightningTimer.Interval=TimeSpan.FromSeconds(_random.Next(15,61));
    private void FlashLightning(){if(!_enabled||!_lightningEnabled||_state!=WeatherState.Thunderstorm)return;_flashOpacity=.72;_flashLayer.Opacity=_flashOpacity;_flashFade.Start();LightningFlashed?.Invoke();SetLightningInterval();}
    public void Dispose(){_rainTimer.Stop();_lightningTimer.Stop();_flashFade.Stop();LightningFlashed=null;}
}
