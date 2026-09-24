using System.Globalization;
using System.Windows;
using DeskLofi.Models;
using DeskLofi.Services;
using Forms = System.Windows.Forms;
using WpfButton = System.Windows.Controls.Button;
using WpfComboBox = System.Windows.Controls.ComboBox;
using WpfComboBoxItem = System.Windows.Controls.ComboBoxItem;
using MediaColor = System.Windows.Media.Color;
using MediaBrushes = System.Windows.Media.Brushes;
using SolidColorBrush = System.Windows.Media.SolidColorBrush;

namespace DeskLofi.Views;

public partial class SettingsWindow : Window
{
    private readonly SettingsService _service;
    private readonly MusicService? _music;

    public SettingsWindow(SettingsService service, MusicService? music = null)
    {
        InitializeComponent();
        _service = service;
        _music = music;
        ApplySettings(service.Current);
        LanguageChoice.SelectionChanged += (_, _) => ApplyLanguage();
        Refresh.ValueChanged += (_, _) => UpdateRefreshValue();
        ApplyLanguage();
        ShowSection("General");
    }

    private void ApplySettings(AppSettings s)
    {
        LanguageChoice.SelectedIndex = UiText.IsEnglish(s.Language) ? 1 : 0;
        Startup.IsChecked = s.StartWithWindows;
        TopmostEnabled.IsChecked = s.AlwaysOnTop;
        ShowOnTaskbar.IsChecked = s.ShowOnTaskbar;
        Keyboard.IsChecked = s.ReactToKeyboard;
        Mouse.IsChecked = s.ReactToMouse;
        Autoplay.IsChecked = s.AutoPlayMusic;
        Sleep.Value = s.CatSleepTimeoutSeconds;
        Afk.Value = s.AfkTimeoutMinutes;
        Scale.Value = s.Scale;
        ShowClock.IsChecked = s.ShowClock;
        ShowDate.IsChecked = s.ShowDate;
        AutoDayNight.IsChecked = s.AutoDayNight;
        EnableWeather.IsChecked = s.EnableRealWeather;
        City.Text = s.CityName;
        Latitude.Text = s.Latitude.ToString(CultureInfo.InvariantCulture);
        Longitude.Text = s.Longitude.ToString(CultureInfo.InvariantCulture);
        Refresh.Value = Math.Clamp(s.WeatherRefreshMinutes, 5, 120);
        WeatherEffects.IsChecked = s.WeatherEffects;
        WeatherAmbience.IsChecked = s.WeatherAmbienceAuto;
        Lightning.IsChecked = s.EnableLightning;
        WeatherVolume.Value = s.WeatherVolume;
        MusicVolume.Value = s.Volume;
        DebugMode.IsChecked = s.DebugMode;
        Select(DebugTimeChoice, s.DebugTime, 3);
        Select(DebugWeatherChoice, s.DebugWeather, 2);
        Select(SceneChoice, s.Scene, 0);
        ApplyLanguage();
    }

    private void ApplyLanguage()
    {
        if (LanguageChoice.SelectedItem is not WpfComboBoxItem item) return;
        UiText.TranslateTree(this, item.Tag?.ToString());
        UpdateRefreshValue();
    }

    private void UpdateRefreshValue()
    {
        var language = (LanguageChoice.SelectedItem as WpfComboBoxItem)?.Tag?.ToString();
        RefreshValueLabel.Text = UiText.Choose(language, $"{Refresh.Value:0} phút", $"{Refresh.Value:0} min");
    }

    private static void Select(WpfComboBox combo, string value, int defaultIndex)
    {
        foreach (var item in combo.Items.OfType<WpfComboBoxItem>())
        {
            if (!string.Equals(item.Tag?.ToString() ?? item.Content?.ToString(), value, StringComparison.OrdinalIgnoreCase)) continue;
            combo.SelectedItem = item;
            return;
        }
        combo.SelectedIndex = defaultIndex;
    }

    private void NavigateClick(object sender, RoutedEventArgs e)
    {
        if (sender is WpfButton { Tag: string section }) ShowSection(section);
    }

    private void ShowSection(string section)
    {
        GeneralPage.Visibility = section == "General" ? Visibility.Visible : Visibility.Collapsed;
        WeatherPage.Visibility = section == "Weather" ? Visibility.Visible : Visibility.Collapsed;
        MusicPage.Visibility = section == "Music" ? Visibility.Visible : Visibility.Collapsed;
        AppearancePage.Visibility = section == "Appearance" ? Visibility.Visible : Visibility.Collapsed;
        OtherPage.Visibility = section == "Other" ? Visibility.Visible : Visibility.Collapsed;
        foreach (var button in new[] { GeneralNav, WeatherNav, MusicNav, AppearanceNav, OtherNav })
            button.Background = button.Tag?.ToString() == section
                ? new SolidColorBrush(MediaColor.FromRgb(226, 237, 255))
                : MediaBrushes.Transparent;
    }

    private void SaveClick(object sender, RoutedEventArgs e)
    {
        SaveSettings();
        StartupService.SetEnabled(_service.Current.StartWithWindows);
        Close();
    }

    public void SaveSettings()
    {
        var s = _service.Current;
        s.Language = (LanguageChoice.SelectedItem as WpfComboBoxItem)?.Tag?.ToString() == "en" ? "en" : "vi";
        s.StartWithWindows = Startup.IsChecked == true;
        s.AlwaysOnTop = TopmostEnabled.IsChecked == true;
        s.ShowOnTaskbar = ShowOnTaskbar.IsChecked == true;
        s.ReactToKeyboard = Keyboard.IsChecked == true;
        s.ReactToMouse = Mouse.IsChecked == true;
        s.AutoPlayMusic = Autoplay.IsChecked == true;
        s.CatSleepTimeoutSeconds = (int)Sleep.Value;
        s.AfkTimeoutMinutes = (int)Afk.Value;
        s.Scale = Scale.Value;
        s.ShowClock = ShowClock.IsChecked == true;
        s.ShowDate = ShowDate.IsChecked == true;
        s.AutoDayNight = AutoDayNight.IsChecked == true;
        s.EnableRealWeather = EnableWeather.IsChecked == true;
        s.CityName = City.Text.Trim();
        s.Latitude = ParseCoordinate(Latitude.Text);
        s.Longitude = ParseCoordinate(Longitude.Text);
        s.WeatherRefreshMinutes = (int)Refresh.Value;
        s.WeatherEffects = WeatherEffects.IsChecked == true;
        s.WeatherAmbienceAuto = WeatherAmbience.IsChecked == true;
        s.EnableLightning = Lightning.IsChecked == true;
        s.WeatherVolume = WeatherVolume.Value;
        s.Volume = MusicVolume.Value;
        s.DebugMode = DebugMode.IsChecked == true;
        s.DebugTime = (DebugTimeChoice.SelectedItem as WpfComboBoxItem)?.Tag?.ToString() ?? "";
        s.DebugWeather = (DebugWeatherChoice.SelectedItem as WpfComboBoxItem)?.Tag?.ToString() ?? "";
        s.Scene = (SceneChoice.SelectedItem as WpfComboBoxItem)?.Tag?.ToString() ?? "Bedroom";
        _service.Save();
    }

    private static double ParseCoordinate(string value) =>
        double.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out var parsed) ? parsed : 0;

    private void ResetClick(object sender, RoutedEventArgs e) => ApplySettings(new AppSettings());
    private void CancelClick(object sender, RoutedEventArgs e) => Close();

    private void AddFilesClick(object sender, RoutedEventArgs e)
    {
        if (_music is null) return;
        var dialog = new Microsoft.Win32.OpenFileDialog { Multiselect = true, Filter = "Audio files (*.mp3;*.wav)|*.mp3;*.wav" };
        if (dialog.ShowDialog(this) == true) _music.AddFiles(dialog.FileNames);
    }

    private void AddFolderClick(object sender, RoutedEventArgs e)
    {
        if (_music is null) return;
        using var dialog = new Forms.FolderBrowserDialog();
        if (dialog.ShowDialog() == Forms.DialogResult.OK) _music.AddFolder(dialog.SelectedPath);
    }

    private void OpenLibraryClick(object sender, RoutedEventArgs e)
    {
        if (_music is not null) new MusicLibraryWindow(_music) { Owner = this }.Show();
    }
}
