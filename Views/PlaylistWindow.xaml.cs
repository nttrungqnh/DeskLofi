using System.Windows;
using System.Windows.Controls;
using DeskLofi.Services;

namespace DeskLofi.Views;
public partial class PlaylistWindow : Window
{
    private readonly MusicService _music;
    public PlaylistWindow(MusicService music){InitializeComponent();_music=music;Refresh();}
    private void Refresh(){Items.ItemsSource=null;Items.ItemsSource=_music.Playlists.ToList();}
    private void SelectionChanged(object sender,SelectionChangedEventArgs e){if(Items.SelectedItem is string name)NameInput.Text=name;}
    private void CreateClick(object sender,RoutedEventArgs e){_music.CreatePlaylist(NameInput.Text);Refresh();}
    private void RenameClick(object sender,RoutedEventArgs e){if(Items.SelectedItem is string oldName){_music.RenamePlaylist(oldName,NameInput.Text);Refresh();}}
    private void DeleteClick(object sender,RoutedEventArgs e){if(Items.SelectedItem is string name){_music.DeletePlaylist(name);Refresh();}}
}
