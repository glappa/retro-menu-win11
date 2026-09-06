using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using RetroMenu.Services;

namespace RetroMenu.Views
{
    public partial class SettingsWindow : Window
    {
        private bool _loading = true;

        /// <summary>Runs alongside the language box: the code behind each entry.</summary>
        private List<string> _languageCodes = new List<string>();

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
            XpExplorerToggle.Content = Lang.T("UseXpExplorer");
            // Three states, not two: there, missing, or there but unusable — the
            // remains of an installation that would only throw a crash box.
            bool ready = XpExplorerBridge.Path != null;
            XpExplorerHint.Text = ready
                ? Lang.T("UseXpExplorerHint")
                : Lang.T(XpExplorerBridge.IsBroken ? "UseXpExplorerBroken" : "UseXpExplorerMissing");
            XpExplorerToggle.IsEnabled = ready;
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

            // Two automatic entries first, then every language by its own name.
            _languageCodes = new List<string> { Lang.AutoWindows, Lang.AutoRetroBar };
            var names = new List<string> { Lang.T("LangAutoWindows"), Lang.T("LangAutoRetroBar") };
            foreach (var language in Lang.Languages)
            {
                _languageCodes.Add(language.Code);
                names.Add(language.Native);
            }

            LanguageBox.ItemsSource = names;
            int chosen = _languageCodes.FindIndex(
                code => string.Equals(code, settings.Language, StringComparison.OrdinalIgnoreCase));
            LanguageBox.SelectedIndex = chosen >= 0 ? chosen : 0;
            UpdateLanguageHint();

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
            XpExplorerToggle.IsChecked = settings.UseXpExplorer;
            AutoStartToggle.IsChecked = settings.AutoStart;
        }

        private void UpdateRetroBarStatus()
        {
            var bridge = App.Me.RetroBar;
            RetroBarStatus.Text = bridge != null && bridge.IsPresent
                ? Lang.F("RetroBarFound", bridge.Theme, ThemeManager.MapFromRetroBar(bridge.Theme))
                : Lang.T("RetroBarMissing");
        }

        /// <summary>
        /// Says what Windows is set to and what the menu made of it — the line that
        /// shows the automatic choice actually landed somewhere sensible.
        /// </summary>
        private void UpdateLanguageHint()
        {
            string windows = SystemLanguage.DisplayName();
            LanguageHint.Text = Lang.F("LangDetected", windows) + " " +
                (Lang.Source == "fallback"
                    ? Lang.F("LangUntranslated", windows)
                    : Lang.F("LangShowing", Lang.CurrentNative));
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
            int index = LanguageBox.SelectedIndex;
            AppSettings.Instance.Language = index >= 0 && index < _languageCodes.Count
                ? _languageCodes[index]
                : Lang.AutoWindows;
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

            settings.UseXpExplorer = XpExplorerToggle.IsChecked == true;
            XpExplorerBridge.Recheck();

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
