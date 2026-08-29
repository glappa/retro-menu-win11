using System;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using RetroMenu.Services;

namespace RetroMenu.Views
{
    public partial class SettingsWindow : Window
    {
        private bool _loading = true;

        public SettingsWindow()
        {
            InitializeComponent();
            Load();
            _loading = false;
        }

        private void Load()
        {
            var settings = AppSettings.Instance;

            Title = Lang.T("SettingsTitle");
            HeaderText.Text = Lang.T("SettingsTitle");
            AppearanceLabel.Text = Lang.T("Appearance");
            ThemeLabel.Text = Lang.T("Theme");
            FollowRetroBarBox.Content = Lang.T("FollowRetroBar");
            LanguageLabel.Text = Lang.T("Language");
            BehaviourLabel.Text = Lang.T("Behaviour");
            WinKeyLabel.Text = Lang.T("WinKey");
            FrequentLabel.Text = Lang.T("FrequentCount");
            ScaleLabel.Text = Lang.T("MenuScale");
            TilesToggle.Content = Lang.T("ShowTiles");
            TilesHint.Text = Lang.T("ShowTilesHint");
            RecentToggle.Content = Lang.T("ShowRecent");
            RecentHint.Text = Lang.T("ShowRecentHint");
            KeepTaskbarToggle.Content = Lang.T("KeepTaskbar");
            SearchBoxToggle.Content = Lang.T("ShowSearchBox");
            StoreAppsToggle.Content = Lang.T("ShowStoreApps");
            AutoStartToggle.Content = Lang.T("AutoStart");
            CloseButton.Content = Lang.T("Close");
            VersionText.Text = "Retro Menu " +
                (System.Reflection.Assembly.GetEntryAssembly()?.GetName().Version?.ToString(3) ?? "0.1.0");

            ThemeBox.ItemsSource = ThemeManager.Names.ToList();
            ThemeBox.SelectedItem = ThemeManager.Names.Contains(settings.Theme)
                ? settings.Theme : ThemeManager.Names.First();
            ThemeBox.IsEnabled = !settings.FollowRetroBarTheme;

            FollowRetroBarBox.IsChecked = settings.FollowRetroBarTheme;
            UpdateRetroBarStatus();

            LanguageBox.ItemsSource = new[] { "auto", "Deutsch", "English" };
            LanguageBox.SelectedIndex = settings.Language switch
            {
                "de" => 1,
                "en" => 2,
                _ => 0
            };

            WinKeyBox.ItemsSource = new[]
            {
                Lang.T("WinKeyNeutralize"),
                Lang.T("WinKeySwallow"),
                Lang.T("WinKeyOff")
            };
            WinKeyBox.SelectedIndex = settings.WinKeyMode switch
            {
                "Swallow" => 1,
                "Off" => 2,
                _ => 0
            };

            FrequentBox.ItemsSource = Enumerable.Range(0, 13).ToList();
            FrequentBox.SelectedItem = Math.Max(0, Math.Min(12, settings.FrequentCount));

            ScaleBox.ItemsSource = ScaleChoices;
            ScaleBox.SelectedItem = ScaleChoices
                .OrderBy(v => Math.Abs(v - settings.MenuScale * 100))
                .First();

            TilesToggle.IsChecked = settings.ShowTilePanel;
            RecentToggle.IsChecked = settings.ShowRecentPrograms;
            KeepTaskbarToggle.IsChecked = settings.KeepTaskbarVisible;
            SearchBoxToggle.IsChecked = settings.ShowSearchBox;
            StoreAppsToggle.IsChecked = settings.ShowStoreApps;
            AutoStartToggle.IsChecked = settings.AutoStart;
        }

        private void UpdateRetroBarStatus()
        {
            var bridge = App.Me.RetroBar;
            if (bridge != null && bridge.IsPresent)
            {
                RetroBarStatus.Text = Lang.Current == "de"
                    ? $"RetroBar gefunden – Design „{bridge.Theme}“ → „{ThemeManager.MapFromRetroBar(bridge.Theme)}“."
                    : $"RetroBar found – theme \"{bridge.Theme}\" maps to \"{ThemeManager.MapFromRetroBar(bridge.Theme)}\".";
            }
            else
            {
                RetroBarStatus.Text = Lang.Current == "de"
                    ? "RetroBar wurde nicht gefunden."
                    : "RetroBar was not found.";
            }
        }

        private void OnThemeChanged(object sender, SelectionChangedEventArgs e)
        {
            if (_loading || ThemeBox.SelectedItem is not string name) return;
            AppSettings.Instance.Theme = name;
            AppSettings.Instance.Save();
            App.Me.ApplySettings();
        }

        private void OnFollowChanged(object sender, RoutedEventArgs e)
        {
            if (_loading) return;
            AppSettings.Instance.FollowRetroBarTheme = FollowRetroBarBox.IsChecked == true;
            AppSettings.Instance.Save();
            ThemeBox.IsEnabled = !AppSettings.Instance.FollowRetroBarTheme;
            App.Me.ApplySettings();
        }

        private void OnLanguageChanged(object sender, SelectionChangedEventArgs e)
        {
            if (_loading) return;
            AppSettings.Instance.Language = LanguageBox.SelectedIndex switch
            {
                1 => "de",
                2 => "en",
                _ => "auto"
            };
            AppSettings.Instance.Save();
            App.Me.ApplySettings();

            _loading = true;
            Load();
            _loading = false;
        }

        private void OnWinKeyChanged(object sender, SelectionChangedEventArgs e)
        {
            if (_loading) return;
            AppSettings.Instance.WinKeyMode = WinKeyBox.SelectedIndex switch
            {
                1 => "Swallow",
                2 => "Off",
                _ => "Neutralize"
            };
            AppSettings.Instance.Save();
            App.Me.ApplySettings();
        }

        private void OnFrequentChanged(object sender, SelectionChangedEventArgs e)
        {
            if (_loading || FrequentBox.SelectedItem is not int count) return;
            AppSettings.Instance.FrequentCount = count;
            AppSettings.Instance.Save();
            App.Me.ApplySettings();
        }

        private static readonly int[] ScaleChoices = { 100, 125, 150, 175, 200, 250 };

        private void OnScaleChanged(object sender, SelectionChangedEventArgs e)
        {
            if (_loading || ScaleBox.SelectedItem is not int percent) return;
            AppSettings.Instance.MenuScale = percent / 100.0;
            AppSettings.Instance.Save();
            App.Me.ApplySettings();
        }

        private void OnToggleChanged(object sender, RoutedEventArgs e)
        {
            if (_loading) return;
            var settings = AppSettings.Instance;
            settings.ShowSearchBox = SearchBoxToggle.IsChecked == true;
            settings.KeepTaskbarVisible = KeepTaskbarToggle.IsChecked == true;
            settings.ShowRecentPrograms = RecentToggle.IsChecked == true;
            settings.ShowTilePanel = TilesToggle.IsChecked == true;

            bool storeApps = StoreAppsToggle.IsChecked == true;
            bool storeChanged = storeApps != settings.ShowStoreApps;
            settings.ShowStoreApps = storeApps;

            settings.Save();
            App.Me.ApplySettings();
            if (storeChanged) App.Me.Catalog.RefreshAsync();
        }

        private void OnAutoStartChanged(object sender, RoutedEventArgs e)
        {
            if (_loading) return;
            try { AppSettings.Instance.AutoStart = AutoStartToggle.IsChecked == true; }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message, "Retro Menu", MessageBoxButton.OK, MessageBoxImage.Warning);
            }
        }

        private void OnClose(object sender, RoutedEventArgs e) => Close();
    }
}
