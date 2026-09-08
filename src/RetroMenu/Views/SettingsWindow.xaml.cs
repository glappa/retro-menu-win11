using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Navigation;
using RetroMenu.Services;

namespace RetroMenu.Views
{
    /// <summary>
    /// The settings program. It is a program of its own — started as
    /// RetroMenuSettings.exe, or as this executable with --settings — and never
    /// part of a running menu, because the switch that turns the menu off has to
    /// keep working once it is off.
    ///
    /// Every change is written straight away and the menu is told about it. There
    /// is no OK button on purpose: what you see in the menu is what is saved.
    /// </summary>
    public partial class SettingsWindow : Window
    {
        private bool _loading = true;

        /// <summary>Runs alongside the language box: the code behind each entry.</summary>
        private List<string> _languageCodes = new List<string>();

        /// <summary>One checkbox per entry of the right hand column, in its order.</summary>
        private readonly List<CheckBox> _placeBoxes = new List<CheckBox>();

        private static readonly int[] ScaleChoices = { 100, 125, 150, 175, 200, 250 };

        public SettingsWindow()
        {
            InitializeComponent();
            Load();
            _loading = false;
        }

        // ------------------------------------------------------------- filling in

        private void Load()
        {
            var settings = AppSettings.Instance;

            Localize();

            ThemeBox.ItemsSource = ThemeManager.Names.ToList();
            ThemeBox.SelectedItem = ThemeManager.Names.Contains(settings.Theme)
                ? settings.Theme : ThemeManager.Names.First();
            ThemeBox.IsEnabled = !settings.FollowRetroBarTheme;
            FollowRetroBarBox.IsChecked = settings.FollowRetroBarTheme;

            int chosen = _languageCodes.FindIndex(
                code => string.Equals(code, settings.Language, StringComparison.OrdinalIgnoreCase));
            LanguageBox.SelectedIndex = chosen >= 0 ? chosen : 0;

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

            EnabledToggle.IsChecked = settings.Enabled;
            AutoStartToggle.IsChecked = settings.AutoStart;

            UserPictureToggle.IsChecked = settings.ShowUserPicture;
            UserNameBox.Text = settings.UserName ?? "";
            SoundsToggle.IsChecked = settings.PlaySounds;

            FavouritesToggle.IsChecked = settings.ShowFavourites;
            TilesToggle.IsChecked = settings.ShowTilePanel;
            TilesToggle.IsEnabled = settings.ShowFavourites;
            SlotsToggle.IsChecked = settings.ShowDefaultAppSlots;
            RecentToggle.IsChecked = settings.ShowRecentPrograms;
            AllProgramsToggle.IsChecked = settings.ShowAllProgramsButton;

            SearchHotkeyToggle.IsChecked = settings.SearchHotkey;
            SearchBoxToggle.IsChecked = settings.ShowSearchBox;
            SearchFilesToggle.IsChecked = settings.SearchFiles;
            SearchFilesToggle.IsEnabled = settings.ShowSearchBox;
            StoreAppsToggle.IsChecked = settings.ShowStoreApps;

            KeepTaskbarToggle.IsChecked = settings.KeepTaskbarVisible;
            RunAsAdminToggle.IsChecked = settings.ShowRunAsAdmin;
            XpExplorerToggle.IsChecked = settings.UseXpExplorer;

            BuildPlaceList();
            UpdateStatus();
        }

        /// <summary>All the text in one place, so a language change can repeat it.</summary>
        private void Localize()
        {
            Title = Lang.T("SettingsTitle");
            HeaderText.Text = Lang.T("SettingsTitle");
            HeaderSubtext.Text = Lang.T("SettingsSubtitle");

            int page = Math.Max(0, PageList.SelectedIndex);
            PageList.ItemsSource = new[]
            {
                Lang.T("PageGeneral"),
                Lang.T("PageAppearance"),
                Lang.T("PageMenu"),
                Lang.T("PagePlaces"),
                Lang.T("PageSearch"),
                Lang.T("PageAdvanced")
            };
            PageList.SelectedIndex = page;

            MasterHeading.Text = Lang.T("PageGeneral");
            EnabledToggle.Content = Lang.T("UseRetroMenu");
            EnabledHint.Text = Lang.T("UseRetroMenuHint");
            StartMenuButton.Content = Lang.T("StartMenuNow");
            StartHeading.Text = Lang.T("StartHeading");
            AutoStartToggle.Content = Lang.T("AutoStart");
            WinKeyLabel.Text = Lang.T("WinKey");
            WinKeyHint.Text = Lang.T("WinKeyHint");
            LanguageLabel.Text = Lang.T("Language");

            ThemeHeading.Text = Lang.T("Appearance");
            ThemeLabel.Text = Lang.T("Theme");
            FollowRetroBarBox.Content = Lang.T("FollowRetroBar");
            ScaleLabel.Text = Lang.T("MenuScale");
            HeaderHeading.Text = Lang.T("HeaderHeading");
            UserPictureToggle.Content = Lang.T("ShowUserPicture");
            UserNameLabel.Text = Lang.T("UserNameSetting");
            UserNameHint.Text = Lang.T("UserNameHint");
            SoundHeading.Text = Lang.T("SoundHeading");
            SoundsToggle.Content = Lang.T("PlaySounds");
            SoundsHint.Text = Lang.T("PlaySoundsHint");

            PinnedHeading.Text = Lang.T("PinnedHeading");
            FavouritesToggle.Content = Lang.T("ShowFavourites");
            FavouritesHint.Text = Lang.T("ShowFavouritesHint");
            TilesToggle.Content = Lang.T("ShowTiles");
            TilesHint.Text = Lang.T("ShowTilesHint");
            ColumnHeading.Text = Lang.T("ColumnHeading");
            SlotsToggle.Content = Lang.T("ShowDefaultSlots");
            SlotsHint.Text = Lang.T("ShowDefaultSlotsHint");
            FrequentLabel.Text = Lang.T("FrequentCount");
            RecentToggle.Content = Lang.T("ShowRecent");
            RecentHint.Text = Lang.T("ShowRecentHint");
            AllProgramsToggle.Content = Lang.T("ShowAllProgramsButton");
            ForgetButton.Content = Lang.T("ForgetAll");

            PlacesHeading.Text = Lang.T("PlacesHeading");
            PlacesHint.Text = Lang.T("PlacesHint");
            PlacesAllButton.Content = Lang.T("SelectAll");
            PlacesNoneButton.Content = Lang.T("SelectNone");

            SearchHeading.Text = Lang.T("SearchHeading");
            SearchBoxToggle.Content = Lang.T("ShowSearchBox");
            SearchBoxHint.Text = Lang.T("ShowSearchBoxHint");
            SearchFilesToggle.Content = Lang.T("SearchFilesSetting");
            SearchFilesHint.Text = Lang.T("SearchFilesHint");
            ProgramsHeading.Text = Lang.T("ProgramsHeading");
            StoreAppsToggle.Content = Lang.T("ShowStoreApps");
            StoreAppsHint.Text = Lang.T("ShowStoreAppsHint");

            HotkeyHeading.Text = Lang.T("HotkeyHeading");
            SearchHotkeyToggle.Content = Lang.T("SearchHotkey");
            SearchHotkeyHint.Text = Lang.T("SearchHotkeyHint");
            DesktopHeading.Text = Lang.T("DesktopHeading");
            KeepTaskbarToggle.Content = Lang.T("KeepTaskbar");
            KeepTaskbarHint.Text = Lang.T("KeepTaskbarHint");
            RunAsAdminToggle.Content = Lang.T("ShowRunAsAdmin");
            XpExplorerToggle.Content = Lang.T("UseXpExplorer");

            // Three states, not two: there, missing, or there but unusable — the
            // remains of an installation that would only throw a crash box.
            bool ready = XpExplorerBridge.Path != null;
            XpExplorerHint.Text = ready
                ? Lang.T("UseXpExplorerHint")
                : Lang.T(XpExplorerBridge.IsBroken ? "UseXpExplorerBroken" : "UseXpExplorerMissing");
            XpExplorerToggle.IsEnabled = ready;

            FilesHeading.Text = Lang.T("FilesHeading");
            FolderPath.Text = AppSettings.Folder;
            OpenFolderButton.Content = Lang.T("OpenSettingsFolder");
            OpenLogButton.Content = Lang.T("OpenLog");
            ExportButton.Content = Lang.T("ExportSettings");
            ImportButton.Content = Lang.T("ImportSettings");
            ResetButton.Content = Lang.T("ResetSettings");

            AboutHeading.Text = Lang.T("AboutHeading");
            AboutText.Text = Lang.T("AboutText");

            CloseButton.Content = Lang.T("Close");
            VersionText.Text = "Retro Menu " + Version;

            // The entries of the two boxes are words as well, so they are renamed
            // along with the rest — keeping whatever was chosen.
            Quietly(() =>
            {
                int winKey = Math.Max(0, WinKeyBox.SelectedIndex);
                WinKeyBox.ItemsSource = new[]
                {
                    Lang.T("WinKeyNeutralize"),
                    Lang.T("WinKeySwallow"),
                    Lang.T("WinKeyOff")
                };
                WinKeyBox.SelectedIndex = winKey;

                // Two automatic entries first, then every language by its own name.
                int language = Math.Max(0, LanguageBox.SelectedIndex);
                _languageCodes = new List<string> { Lang.AutoWindows, Lang.AutoRetroBar };
                var names = new List<string> { Lang.T("LangAutoWindows"), Lang.T("LangAutoRetroBar") };
                foreach (var entry in Lang.Languages)
                {
                    _languageCodes.Add(entry.Code);
                    names.Add(entry.Native);
                }

                LanguageBox.ItemsSource = names;
                LanguageBox.SelectedIndex = language;
            });

            UpdateLanguageHint();
            UpdateRetroBarStatus();
            foreach (var box in _placeBoxes) box.Content = Lang.T((string)box.Tag);
        }

        private static string Version =>
            System.Reflection.Assembly.GetEntryAssembly()?.GetName().Version?.ToString(3) ?? "0.1.0";

        /// <summary>One checkbox per entry of the right hand column, in menu order.</summary>
        private void BuildPlaceList()
        {
            var hidden = new HashSet<string>(
                AppSettings.Instance.HiddenPlaces ?? new List<string>(),
                StringComparer.OrdinalIgnoreCase);

            PlaceList.Children.Clear();
            _placeBoxes.Clear();

            foreach (var key in Launcher.PlaceKeys)
            {
                var box = new CheckBox
                {
                    Content = Lang.T(key),
                    Tag = key,
                    IsChecked = !hidden.Contains(key),
                    Margin = new Thickness(0, 0, 0, 6),
                    Style = (System.Windows.Style)FindResource("RetroCheckBox")
                };
                box.Checked += OnChanged;
                box.Unchecked += OnChanged;
                PlaceList.Children.Add(box);
                _placeBoxes.Add(box);
            }
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

        /// <summary>
        /// Whether the menu is up, and the offer to start it. Switched off, it says
        /// so instead — a stopped menu is then the expected state, not a fault.
        /// </summary>
        private void UpdateStatus()
        {
            bool on = AppSettings.Instance.Enabled;
            bool running = SettingsBridge.MenuRunning;

            StatusText.Text = !on ? Lang.T("MenuOff")
                : running ? Lang.T("MenuRunning")
                : Lang.T("MenuNotRunning");

            StartMenuButton.Visibility = on && !running && SettingsBridge.CanStartMenu
                ? Visibility.Visible : Visibility.Collapsed;
        }

        // -------------------------------------------------------------- switching

        private void OnPageChanged(object sender, SelectionChangedEventArgs e)
        {
            var pages = new[]
            {
                GeneralPage, AppearancePage, MenuPage, PlacesPage, SearchPage, AdvancedPage
            };

            for (int i = 0; i < pages.Length; i++)
                pages[i].Visibility = i == PageList.SelectedIndex ? Visibility.Visible : Visibility.Collapsed;

            if (PageList.SelectedIndex == 0) UpdateStatus();
        }

        // ------------------------------------------------------------- collecting

        /// <summary>
        /// Reads the whole window back into the settings and writes them. Doing it
        /// wholesale rather than field by field means a new switch only has to be
        /// added in one place.
        /// </summary>
        private void Apply()
        {
            if (_loading) return;
            var settings = AppSettings.Instance;

            settings.Enabled = EnabledToggle.IsChecked == true;

            if (ThemeBox.SelectedItem is string theme) settings.Theme = theme;
            settings.FollowRetroBarTheme = FollowRetroBarBox.IsChecked == true;
            if (ScaleBox.SelectedItem is int percent) settings.MenuScale = percent / 100.0;

            settings.WinKeyMode = WinKeyBox.SelectedIndex switch
            {
                1 => "Swallow",
                2 => "Off",
                _ => "Neutralize"
            };

            settings.ShowUserPicture = UserPictureToggle.IsChecked == true;
            settings.UserName = UserNameBox.Text.Trim();
            settings.PlaySounds = SoundsToggle.IsChecked == true;

            settings.ShowFavourites = FavouritesToggle.IsChecked == true;
            settings.ShowTilePanel = TilesToggle.IsChecked == true;
            settings.ShowDefaultAppSlots = SlotsToggle.IsChecked == true;
            if (FrequentBox.SelectedItem is int count) settings.FrequentCount = count;
            settings.ShowRecentPrograms = RecentToggle.IsChecked == true;
            settings.ShowAllProgramsButton = AllProgramsToggle.IsChecked == true;

            settings.SearchHotkey = SearchHotkeyToggle.IsChecked == true;
            settings.ShowSearchBox = SearchBoxToggle.IsChecked == true;
            settings.SearchFiles = SearchFilesToggle.IsChecked == true;
            settings.ShowStoreApps = StoreAppsToggle.IsChecked == true;

            settings.KeepTaskbarVisible = KeepTaskbarToggle.IsChecked == true;
            settings.ShowRunAsAdmin = RunAsAdminToggle.IsChecked == true;
            settings.UseXpExplorer = XpExplorerToggle.IsChecked == true;

            settings.HiddenPlaces = _placeBoxes
                .Where(b => b.IsChecked != true)
                .Select(b => (string)b.Tag)
                .ToList();

            settings.SaveOptions();
            SettingsBridge.NotifyMenu();

            // The window wears the theme it is setting, so it changes along.
            ThemeManager.Apply(App.ActiveThemeName());

            // A switch that only makes sense while another one is on.
            TilesToggle.IsEnabled = settings.ShowFavourites;
            SearchFilesToggle.IsEnabled = settings.ShowSearchBox;
            ThemeBox.IsEnabled = !settings.FollowRetroBarTheme;

            UpdateStatus();
        }

        private void OnChanged(object sender, RoutedEventArgs e) => Apply();

        private void OnSelectionChanged(object sender, SelectionChangedEventArgs e) => Apply();

        private void OnEnabledChanged(object sender, RoutedEventArgs e)
        {
            if (_loading) return;
            Apply();

            // Switching it back on with nothing running would look like nothing
            // happened, so start it here rather than waiting for the next login.
            if (AppSettings.Instance.Enabled && !SettingsBridge.MenuRunning)
            {
                SettingsBridge.StartMenu();
                UpdateStatusSoon();
            }
        }

        private void OnFavouritesChanged(object sender, RoutedEventArgs e)
        {
            if (_loading) return;

            // The tile panel is the pinned programs in another shape; without them
            // it would show an empty box.
            if (FavouritesToggle.IsChecked != true) Quietly(() => TilesToggle.IsChecked = false);
            Apply();
        }

        /// <summary>
        /// Changes a switch without that change counting as the user's. Ticking a
        /// box raises the same event whoever did it, which is what makes the window
        /// work for the keyboard and for assistive tools — and what makes this
        /// necessary when the window ticks a box itself.
        /// </summary>
        private void Quietly(Action change)
        {
            bool was = _loading;
            _loading = true;
            try { change(); }
            finally { _loading = was; }
        }

        private void OnFollowChanged(object sender, RoutedEventArgs e) => Apply();

        private void OnUserNameChanged(object sender, RoutedEventArgs e) => Apply();

        private void OnLanguageChanged(object sender, SelectionChangedEventArgs e)
        {
            if (_loading) return;

            int index = LanguageBox.SelectedIndex;
            AppSettings.Instance.Language = index >= 0 && index < _languageCodes.Count
                ? _languageCodes[index]
                : Lang.AutoWindows;

            AppSettings.Instance.SaveOptions();
            SettingsBridge.NotifyMenu();

            Lang.Apply(AppSettings.Instance.Language, App.Me.RetroBar?.Language);

            _loading = true;
            Localize();
            _loading = false;
        }

        private void OnAutoStartChanged(object sender, RoutedEventArgs e)
        {
            if (_loading) return;
            try { AppSettings.Instance.AutoStart = AutoStartToggle.IsChecked == true; }
            catch (Exception ex)
            {
                MessageBox.Show(this, ex.Message, "Retro Menu",
                    MessageBoxButton.OK, MessageBoxImage.Warning);
                Quietly(() => AutoStartToggle.IsChecked = AppSettings.Instance.AutoStart);
            }
        }

        /// <summary>The menu needs a moment to take its mutex before it counts as up.</summary>
        private void UpdateStatusSoon()
        {
            var timer = new System.Windows.Threading.DispatcherTimer
            {
                Interval = TimeSpan.FromMilliseconds(700)
            };
            timer.Tick += (_, __) => { timer.Stop(); UpdateStatus(); };
            timer.Start();
        }

        // ---------------------------------------------------------------- buttons

        private void OnStartMenuClick(object sender, RoutedEventArgs e)
        {
            SettingsBridge.StartMenu();
            UpdateStatusSoon();
        }

        private void OnPlacesAll(object sender, RoutedEventArgs e) => SetAllPlaces(true);

        private void OnPlacesNone(object sender, RoutedEventArgs e) => SetAllPlaces(false);

        private void SetAllPlaces(bool on)
        {
            Quietly(() => { foreach (var box in _placeBoxes) box.IsChecked = on; });
            Apply();
        }

        private void OnForgetClick(object sender, RoutedEventArgs e)
        {
            AppSettings.ClearLaunchHistory();
            SettingsBridge.NotifyMenu();
            MessageBox.Show(this, Lang.T("ForgetAllDone"), Lang.T("SettingsTitle"),
                MessageBoxButton.OK, MessageBoxImage.Information);
        }

        private void OnOpenFolder(object sender, RoutedEventArgs e)
        {
            try
            {
                Directory.CreateDirectory(AppSettings.Folder);
                Process.Start(new ProcessStartInfo(AppSettings.Folder) { UseShellExecute = true });
            }
            catch (Exception ex) { Complain(ex); }
        }

        private void OnOpenLog(object sender, RoutedEventArgs e)
        {
            try
            {
                if (!File.Exists(Log.FilePath)) Log.Write("log opened from the settings program");
                Process.Start(new ProcessStartInfo(Log.FilePath) { UseShellExecute = true });
            }
            catch (Exception ex) { Complain(ex); }
        }

        private void OnExport(object sender, RoutedEventArgs e)
        {
            var dialog = new Microsoft.Win32.SaveFileDialog
            {
                FileName = "retromenu-settings.json",
                DefaultExt = ".json",
                Filter = Lang.T("SettingsFileFilter") + " (*.json)|*.json"
            };
            if (dialog.ShowDialog(this) != true) return;

            try
            {
                AppSettings.Instance.SaveOptions();   // make sure the file is current
                File.Copy(AppSettings.FilePath, dialog.FileName, true);
            }
            catch (Exception ex) { Complain(ex); }
        }

        private void OnImport(object sender, RoutedEventArgs e)
        {
            var dialog = new Microsoft.Win32.OpenFileDialog
            {
                DefaultExt = ".json",
                Filter = Lang.T("SettingsFileFilter") + " (*.json)|*.json"
            };
            if (dialog.ShowDialog(this) != true) return;

            try
            {
                // Read it as settings before letting it near the real file: a file
                // picked by mistake must not leave the menu without any settings.
                if (!AppSettings.LooksLikeSettings(dialog.FileName))
                {
                    MessageBox.Show(this, Lang.T("ImportFailed"), Lang.T("SettingsTitle"),
                        MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                Directory.CreateDirectory(AppSettings.Folder);
                File.Copy(dialog.FileName, AppSettings.FilePath, true);
                AppSettings.Reload();
                SettingsBridge.NotifyMenu();
                Reload();
            }
            catch (Exception ex) { Complain(ex); }
        }

        private void OnReset(object sender, RoutedEventArgs e)
        {
            if (MessageBox.Show(this, Lang.T("ResetConfirm"), Lang.T("ResetSettings"),
                    MessageBoxButton.YesNo, MessageBoxImage.Question) != MessageBoxResult.Yes) return;

            // Only the switches go back to how they started. Pinned programs and
            // the launch counts belong to the menu and are left alone.
            new AppSettings().SaveOptions();
            AppSettings.Reload();
            SettingsBridge.NotifyMenu();
            Reload();
        }

        /// <summary>Reads the settings into the window again, after they changed underneath it.</summary>
        private void Reload()
        {
            Lang.Apply(AppSettings.Instance.Language, App.Me.RetroBar?.Language);
            ThemeManager.Apply(App.ActiveThemeName());

            _loading = true;
            Load();
            _loading = false;
        }

        private void Complain(Exception ex) =>
            MessageBox.Show(this, ex.Message, "Retro Menu", MessageBoxButton.OK, MessageBoxImage.Warning);

        private void OnLink(object sender, RequestNavigateEventArgs e)
        {
            try
            {
                Process.Start(new ProcessStartInfo(e.Uri.AbsoluteUri) { UseShellExecute = true });
            }
            catch { }
            e.Handled = true;
        }

        private void OnClose(object sender, RoutedEventArgs e) => Close();
    }
}
