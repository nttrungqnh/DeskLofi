using System.Windows.Media;
using System.Windows.Media.Imaging;
using DeskLofi.Models;
using PixelColor = System.Windows.Media.Color;

namespace DeskLofi.Animation;
public sealed class SpriteLoader
{
    private readonly Dictionary<string, BitmapSource[]> _cache = new(StringComparer.OrdinalIgnoreCase);
    public BitmapSource GetFrame(string group, string state, int frame)
    {
        var key = $"{group}/{state}"; if (!_cache.TryGetValue(key, out var frames)) _cache[key] = frames = Load(key, group == "Girl");
        return frames[Math.Abs(frame) % frames.Length];
    }
    private static BitmapSource[] Load(string key, bool girl)
    {
        var folder = Path.Combine(AppContext.BaseDirectory, "Assets", "Scenes", "Bedroom", key); var files = Directory.Exists(folder) ? Directory.GetFiles(folder, "*.png").OrderBy(x => x, StringComparer.OrdinalIgnoreCase).ToArray() : [];
        var result = new List<BitmapSource>(); foreach (var file in files) try { var b = new BitmapImage(); b.BeginInit(); b.UriSource = new Uri(file); b.CacheOption = BitmapCacheOption.OnLoad; b.EndInit(); b.Freeze(); result.Add(b); } catch { }
        if (result.Count == 0) result.Add(Placeholder(girl)); return result.ToArray();
    }
    private static BitmapSource Placeholder(bool girl)
    {
        const int s = 64; var pixels = new byte[s*s*4]; void Put(int x,int y,PixelColor c) { if (x<0||y<0||x>=s||y>=s)return; int i=(y*s+x)*4; pixels[i]=c.B;pixels[i+1]=c.G;pixels[i+2]=c.R;pixels[i+3]=c.A; }
        void Rect(int x,int y,int w,int h,PixelColor c) { for(int yy=y;yy<y+h;yy++)for(int xx=x;xx<x+w;xx++)Put(xx,yy,c); }
        var outline=PixelColor.FromRgb(40,35,48); var skin=PixelColor.FromRgb(245,190,150); var hair=PixelColor.FromRgb(78,48,54); var cloth=PixelColor.FromRgb(133,115,186);
        if (girl) { Rect(22,7,20,5,hair);Rect(18,12,28,18,hair);Rect(22,15,20,15,skin);Rect(18,16,5,18,hair);Rect(41,16,5,16,hair);Rect(25,22,3,3,outline);Rect(36,22,3,3,outline);Rect(25,33,16,18,cloth);Rect(18,48,30,5,outline);Rect(8,55,48,4,PixelColor.FromRgb(130,82,62));Rect(24,53,10,2,skin);Rect(36,53,8,2,skin); }
        else { Rect(18,25,28,22,outline);Rect(15,17,8,13,outline);Rect(41,17,8,13,outline);Rect(20,27,24,17,PixelColor.FromRgb(232,224,208));Rect(24,33,4,4,outline);Rect(36,33,4,4,outline);Rect(29,39,7,3,PixelColor.FromRgb(230,145,144));Rect(25,46,5,5,outline);Rect(37,46,5,5,outline); }
        var bmp=BitmapSource.Create(s,s,96,96,PixelFormats.Bgra32,null,pixels,s*4);bmp.Freeze();return bmp;
    }
}
