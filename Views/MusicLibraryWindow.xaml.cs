using System.Windows;
using System.Windows.Input;
using DeskLofi.Models;
using DeskLofi.Services;
using Forms = System.Windows.Forms;

namespace DeskLofi.Views;
public partial class MusicLibraryWindow : Window
{
    private readonly MusicService _music;
    public MusicLibraryWindow(MusicService music){InitializeComponent();_music=music;_music.LibraryChanged+=Refresh;RefreshLanguage();Refresh();}
    public void RefreshLanguage(){UiText.TranslateTree(this,_music.Language);CountLabel.Text=UiText.Choose(_music.Language,$"{_music.Tracks.Count} bài hát · tệp vẫn ở thư mục gốc",$"{_music.Tracks.Count} tracks · files remain in their original locations");}
    private void Refresh(){if(!Dispatcher.CheckAccess()){Dispatcher.BeginInvoke(Refresh);return;}TrackList.ItemsSource=null;TrackList.ItemsSource=_music.Tracks;RefreshLanguage();}
    private void TrackDoubleClick(object sender,MouseButtonEventArgs e){if(TrackList.SelectedItem is Track track&&!track.IsMissing)_music.PlayTrack(track.Id);}
    private void AddFilesClick(object sender,RoutedEventArgs e){var dialog=new Microsoft.Win32.OpenFileDialog{Multiselect=true,Filter="Audio files (*.mp3;*.wav)|*.mp3;*.wav"};if(dialog.ShowDialog(this)==true)_music.AddFiles(dialog.FileNames);}
    private void AddFolderClick(object sender,RoutedEventArgs e){using var dialog=new Forms.FolderBrowserDialog();if(dialog.ShowDialog()==Forms.DialogResult.OK)_music.AddFolder(dialog.SelectedPath);}
    private void FavoriteClick(object sender,RoutedEventArgs e){if(TrackList.SelectedItem is Track track)_music.ToggleFavorite(track.Id);}
    private void RemoveMissingClick(object sender,RoutedEventArgs e)=>_music.RemoveMissingTracks();
    protected override void OnClosed(EventArgs e){_music.LibraryChanged-=Refresh;base.OnClosed(e);}
}
