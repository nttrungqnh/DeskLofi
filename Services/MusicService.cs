using System.Text;
using System.Text.Json;
using System.Windows.Media;
using DeskLofi.Models;

namespace DeskLofi.Services;

/// <summary>Personal local library. Audio files are referenced by path and never copied.</summary>
public sealed class MusicService : IDisposable
{
    private readonly MediaPlayer _player = new();
    private readonly string _root = Path.Combine(AppContext.BaseDirectory, "Music");
    private readonly string _libraryPath = Path.Combine(AppContext.BaseDirectory, "Data", "music-library.json");
    private readonly AppSettings _settings;
    private readonly JsonSerializerOptions _json = new() { PropertyNameCaseInsensitive = true, WriteIndented = true };
    private MusicLibraryDocument _library = new();
    private int _index = -1;
    private readonly Random _random = new();
    public IReadOnlyList<Track> Tracks => _library.Tracks;
    public IReadOnlyList<string> Playlists => _library.Playlists;
    public string SelectedPlaylist { get; private set; }
    public bool IsPlaying { get; private set; }
    public double Volume { get => _player.Volume; set { _player.Volume = Math.Clamp(value, 0, 1); _settings.Volume = _player.Volume; } }
    public bool Shuffle { get => _settings.MusicShuffle; private set => _settings.MusicShuffle = value; }
    public bool Repeat { get => _settings.MusicRepeat; private set => _settings.MusicRepeat = value; }
    public Track? Current => _index >= 0 && _index < _library.Tracks.Count ? _library.Tracks[_index] : null;
    public double PositionSeconds => _player.Position.TotalSeconds;
    public double DurationSeconds => _player.NaturalDuration.HasTimeSpan ? _player.NaturalDuration.TimeSpan.TotalSeconds : Current?.Duration ?? 0;
    public event Action<Track?>? TrackChanged;
    public event Action<bool>? PlaybackChanged;
    public event Action? LibraryChanged;

    public MusicService(double volume) : this(new AppSettings { Volume = volume }) { }
    public MusicService(SettingsService settings) : this(settings.Current) { _saveSettings = settings.Save; }
    private Action? _saveSettings;
    private bool _disposed;
    private MusicService(AppSettings settings)
    {
        _settings = settings; SelectedPlaylist = settings.MusicLibraryLastPlaylist;
        Directory.CreateDirectory(_root); Directory.CreateDirectory(Path.GetDirectoryName(_libraryPath)!); _player.Volume = Math.Clamp(settings.Volume, 0, 1);
        LoadLibrary(); _player.MediaEnded += (_, _) => { if (Repeat) RestartCurrent(); else Next(); };
        _player.MediaOpened += (_, _) => { if (Current is { } track && _player.NaturalDuration.HasTimeSpan && Math.Abs(track.Duration-_player.NaturalDuration.TimeSpan.TotalSeconds)>.5) { track.Duration = _player.NaturalDuration.TimeSpan.TotalSeconds; SaveLibrary(); } };
        _player.MediaFailed += (_, e) => { if(Current is { } track){track.IsMissing=!File.Exists(track.FilePath);LoggerService.Error($"Music playback failed for track {track.Id}: {e.ErrorException?.Message??"unknown media error"}");Pause();TrackChanged?.Invoke(track);} };
        if (!string.IsNullOrWhiteSpace(settings.MusicLibraryLastTrackId))
        { _index = _library.Tracks.FindIndex(t => t.Id == settings.MusicLibraryLastTrackId && !t.IsMissing); if (_index >= 0) OpenCurrent(false); }
    }

    private void LoadLibrary()
    {
        try
        {
            if (File.Exists(_libraryPath))
            {
                _library = JsonSerializer.Deserialize<MusicLibraryDocument>(File.ReadAllText(_libraryPath), _json) ?? new();
                if (_library.Tracks.Count == 0)
                {
                    _library.Tracks = Directory.GetFiles(_root).Where(IsSupported).Select(CreateTrack).ToList();
                    if (_library.Tracks.Count > 0) SaveLibrary();
                }
            }
            else
            {
                var oldPath = Path.Combine(AppContext.BaseDirectory, "Data", "playlists.json");
                if (File.Exists(oldPath)) _library.Tracks = JsonSerializer.Deserialize<List<Track>>(File.ReadAllText(oldPath), _json) ?? [];
                if (_library.Tracks.Count == 0)
                    _library.Tracks = Directory.GetFiles(_root).Where(IsSupported).Select(CreateTrack).ToList();
                if (_library.Tracks.Count != 0) SaveLibrary();
            }
            _library.Playlists ??= ["Focus", "Chill", "Rain", "Night", "Cafe", "Favorites"];
            foreach (var t in _library.Tracks) { t.IsMissing = !File.Exists(t.FilePath); if (t.IsMissing) LoggerService.Info($"Missing music track: {t.Id}"); }
            if (!_library.Playlists.Contains(SelectedPlaylist, StringComparer.OrdinalIgnoreCase)) SelectedPlaylist = "Focus";
        }
        catch (Exception ex) { LoggerService.Error("Music library load failed", ex); _library = new(); }
    }
    private static bool IsSupported(string path) => new[] { ".mp3", ".wav" }.Contains(Path.GetExtension(path), StringComparer.OrdinalIgnoreCase);
    public int AddFiles(IEnumerable<string> paths)
    {
        var known = new HashSet<string>(_library.Tracks.Select(t => NormalizePath(t.FilePath)), StringComparer.OrdinalIgnoreCase); var added = 0;
        foreach (var file in paths)
        {
            try
            {
                if (!File.Exists(file) || !IsSupported(file)) continue;
                var full = Path.GetFullPath(file); if (!known.Add(NormalizePath(full))) continue;
                var track = CreateTrack(full); if(SelectedPlaylist.Equals("Favorites",StringComparison.OrdinalIgnoreCase))track.IsFavorite=true;else track.Playlists.Add(SelectedPlaylist); _library.Tracks.Add(track); added++;
            }
            catch (Exception ex) { LoggerService.Error("Music file could not be added", ex); }
        }
        if (added > 0) SaveLibrary(); return added;
    }
    public int AddFolder(string folder)
    {
        try { return AddFiles(Directory.EnumerateFiles(folder, "*.*", SearchOption.AllDirectories).Where(IsSupported)); }
        catch (Exception ex) { LoggerService.Error("Music folder scan failed", ex); return 0; }
    }
    public int RemoveMissingTracks()
    {
        var before = _library.Tracks.Count; _library.Tracks.RemoveAll(t => !File.Exists(t.FilePath)); var count = before - _library.Tracks.Count;
        if (count > 0) SaveLibrary(); if (_index >= _library.Tracks.Count) _index = -1; return count;
    }
    public void RefreshMissing() { var changed=false;foreach (var t in _library.Tracks){var missing=!File.Exists(t.FilePath);if(t.IsMissing!=missing){t.IsMissing=missing;changed=true;}}if(changed)SaveLibrary(); }
    public void CreatePlaylist(string name) { name = name.Trim(); if (name.Length == 0 || _library.Playlists.Contains(name, StringComparer.OrdinalIgnoreCase)) return; _library.Playlists.Add(name); SaveLibrary(); }
    public void RenamePlaylist(string oldName, string newName)
    {
        newName = newName.Trim(); var i = _library.Playlists.FindIndex(x => x.Equals(oldName, StringComparison.OrdinalIgnoreCase));
        if (i < 0 || newName.Length == 0 || _library.Playlists.Contains(newName, StringComparer.OrdinalIgnoreCase)) return;
        _library.Playlists[i] = newName; foreach (var track in _library.Tracks) for (var j = 0; j < track.Playlists.Count; j++) if (track.Playlists[j].Equals(oldName, StringComparison.OrdinalIgnoreCase)) track.Playlists[j] = newName;
        if (SelectedPlaylist == oldName) SelectPlaylist(newName); SaveLibrary();
    }
    public void DeletePlaylist(string name)
    {
        if (name.Equals("Favorites", StringComparison.OrdinalIgnoreCase) || !_library.Playlists.Remove(name)) return;
        foreach (var track in _library.Tracks) track.Playlists.RemoveAll(x => x.Equals(name, StringComparison.OrdinalIgnoreCase));
        if (SelectedPlaylist == name) SelectPlaylist("Focus"); SaveLibrary();
    }
    public IReadOnlyList<Track> GetPlaylistTracks(string? name = null)
    {
        name ??= SelectedPlaylist;
        if (name.Equals("Favorites", StringComparison.OrdinalIgnoreCase)) return _library.Tracks.Where(t => t.IsFavorite && !t.IsMissing).ToList();
        return _library.Tracks.Where(t => !t.IsMissing && (t.Playlists.Contains(name, StringComparer.OrdinalIgnoreCase) || (t.Playlists.Count == 0 && t.Category.Equals(name, StringComparison.OrdinalIgnoreCase)))).ToList();
    }
    public void SelectPlaylist(string name)
    {
        if (!_library.Playlists.Contains(name, StringComparer.OrdinalIgnoreCase)) return;
        SelectedPlaylist = name; _settings.MusicLibraryLastPlaylist = name; _saveSettings?.Invoke();
        TrackChanged?.Invoke(Current);
    }
    public void ToggleFavorite()
    {
        if (Current is not { } track) return; ToggleFavorite(track.Id);
    }
    public void ToggleFavorite(string id)
    {
        var track=_library.Tracks.FirstOrDefault(t=>t.Id==id);if(track is null)return;track.IsFavorite=!track.IsFavorite;
        if (track.IsFavorite && !_library.Playlists.Contains("Favorites")) _library.Playlists.Add("Favorites"); SaveLibrary();
    }
    public void Toggle() { if (IsPlaying) { _player.Pause(); IsPlaying = false; PlaybackChanged?.Invoke(false); } else if (Current is not null && !Current.IsMissing) { _player.Play(); IsPlaying = true; PlaybackChanged?.Invoke(true); } else Next(); }
    public void PlayTrack(string id){var index=_library.Tracks.FindIndex(t=>t.Id==id);if(index<0)return;_index=index;OpenCurrent(true);}
    public void Play() { if (Current is null) { Next(); return; } if (!Current.IsMissing) { _player.Play(); IsPlaying = true; PlaybackChanged?.Invoke(true); } }
    public void Pause() { _player.Pause(); IsPlaying = false; PlaybackChanged?.Invoke(false); }
    public void Next()
    {
        var candidates = GetPlaylistTracks(); if (candidates.Count == 0) { Pause(); return; }
        var next = Shuffle && candidates.Count > 1 ? candidates[_random.Next(candidates.Count)] : candidates.FirstOrDefault(x => _index < 0 || _library.Tracks.IndexOf(x) > _index) ?? candidates[0];
        _index = _library.Tracks.IndexOf(next); OpenCurrent(true);
    }
    public void Previous()
    {
        var candidates = GetPlaylistTracks(); if (candidates.Count == 0) return;
        var previous = candidates.LastOrDefault(x => _index < 0 || _library.Tracks.IndexOf(x) < _index) ?? candidates[^1]; _index = _library.Tracks.IndexOf(previous); OpenCurrent(true);
    }
    public void ToggleShuffle() { Shuffle = !Shuffle; _saveSettings?.Invoke(); }
    public void ToggleRepeat() { Repeat = !Repeat; _saveSettings?.Invoke(); }
    public void Seek(double seconds) { if (DurationSeconds > 0) _player.Position = TimeSpan.FromSeconds(Math.Clamp(seconds, 0, DurationSeconds)); }
    private void RestartCurrent() { if (Current is null) return; _player.Position = TimeSpan.Zero; _player.Play(); }
    private void OpenCurrent(bool play)
    {
        var track = Current; if (track is null || track.IsMissing) { if (track is not null) track.IsMissing = true; Pause(); TrackChanged?.Invoke(track); return; }
        try { _player.Open(new Uri(Path.GetFullPath(track.FilePath))); _settings.MusicLibraryLastTrackId = track.Id; _saveSettings?.Invoke(); if (play) { _player.Play(); IsPlaying = true; } TrackChanged?.Invoke(track); PlaybackChanged?.Invoke(IsPlaying); }
        catch (Exception ex) { track.IsMissing = true; LoggerService.Error("Music track failed to open", ex); Pause(); TrackChanged?.Invoke(track); }
    }
    private void SaveLibrary() { try { Directory.CreateDirectory(Path.GetDirectoryName(_libraryPath)!); File.WriteAllText(_libraryPath, JsonSerializer.Serialize(_library, _json));LibraryChanged?.Invoke(); } catch (Exception ex) { LoggerService.Error("Music library save failed", ex); } }
    private static string NormalizePath(string path) { try { return Path.GetFullPath(path); } catch { return path; } }
    private static Track CreateTrack(string path)
    {
        var tag = ReadId3v1(path); return new Track { FilePath = Path.GetFullPath(path), Title = tag.Title.Length > 0 ? tag.Title : Path.GetFileNameWithoutExtension(path), Artist = tag.Artist, Album = tag.Album, DateAdded = DateTime.Now, Playlists = [] };
    }
    private static (string Title,string Artist,string Album) ReadId3v1(string path)
    {
        try { if (!Path.GetExtension(path).Equals(".mp3", StringComparison.OrdinalIgnoreCase)) return ("","",""); using var s=File.OpenRead(path); if (s.Length<128)return ("","",""); s.Seek(-128,SeekOrigin.End);var b=new byte[128];s.ReadExactly(b);if(Encoding.ASCII.GetString(b,0,3)!="TAG")return ("","","");string Get(int start,int length)=>Encoding.Latin1.GetString(b,start,length).Trim('\0',' ');return(Get(3,30),Get(33,30),Get(63,30)); } catch { return ("","",""); }
    }
    public void Dispose() { if(_disposed)return;_disposed=true;_player.Stop(); _player.Close(); }
}
