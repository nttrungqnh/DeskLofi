using System.Windows;
using System.Windows.Controls;
using DeskLofi.Services;

namespace DeskLofi.Views;
public partial class PlaylistWindow : Window
{
    private readonly MusicService _music;

    public PlaylistWindow(MusicService music)
    {
        InitializeComponent();
        _music = music;
        Refresh();
        RefreshLanguage();
    }

    public void RefreshLanguage() => UiText.TranslateTree(this, _music.Language);

    private void Refresh()
    {
        Items.ItemsSource = null;
        Items.ItemsSource = _music.Playlists.ToList();
        Items.SelectedItem = _music.SelectedPlaylist;
    }

    private void SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (Items.SelectedItem is not string name) return;
        NameInput.Text = name;
        _music.SelectPlaylist(name);
    }

    private void CreateClick(object sender, RoutedEventArgs e)
    {
        var name = NameInput.Text.Trim();
        _music.CreatePlaylist(name);
        _music.SelectPlaylist(name);
        Refresh();
    }

    private void RenameClick(object sender, RoutedEventArgs e)
    {
        if (Items.SelectedItem is not string oldName) return;
        _music.RenamePlaylist(oldName, NameInput.Text);
        Refresh();
    }

    private void DeleteClick(object sender, RoutedEventArgs e)
    {
        if (Items.SelectedItem is not string name) return;
        _music.DeletePlaylist(name);
        Refresh();
    }
}
