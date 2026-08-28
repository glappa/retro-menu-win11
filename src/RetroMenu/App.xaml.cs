using System;
using System.IO;
using System.Linq;
using System.Threading;
using System.Windows;
using System.Windows.Threading;
using RetroMenu.Interop;
using RetroMenu.Services;
using RetroMenu.Views;

namespace RetroMenu
{
    public partial class App : Application
    {
        public static App Me => (App)Current;

        public ProgramCatalog Catalog { get; } = new ProgramCatalog();
        public RetroBarBridge RetroBar { get; private set; }

        private Mutex _singleInstance;
        private KeyboardHook _hook;
        private StartMenuWindow _menu;
        private TrayIconService _tray;
        private FileSystemWatcher[] _programWatchers = Array.Empty<FileSystemWatcher>();
        private DispatcherTimer _rescanDebounce;

        protected override void OnStartup(StartupEventArgs e)
        {
            base.OnStartup(e);

            _singleInstance = new Mutex(true, "RetroMenuWin11.SingleInstance", out bool created);
            if (!created)
            {
                Shutdown();
                return;
            }

            DispatcherUnhandledException += (_, args) =>
            {
                MessageBox.Show(args.Exception.ToString(), "Retro Menu",
                    MessageBoxButton.OK, MessageBoxImage.Error);
                args.Handled = true;
            };

            AppSettings.Load();

            RetroBar = new RetroBarBridge();
            RetroBar.Changed += OnRetroBarChanged;
            RetroBar.Watch();

            Lang.Apply(AppSettings.Instance.Language, RetroBar.Language);
            ThemeManager.Apply(ActiveThemeName());

            _menu = new StartMenuWindow();
            _menu.EnsureHandle();

            _tray = new TrayIconService();
            _tray.OpenRequested += () => ToggleMenu(true);
            _tray.SettingsRequested += ShowSettings;
            _tray.RefreshRequested += () => Catalog.RefreshAsync();
            _tray.ExitRequested += Quit;
            _tray.Show();

            _hook = new KeyboardHook { Mode = ParseWinKeyMode(AppSettings.Instance.WinKeyMode) };
            _hook.StartMenuRequested += OnStartMenuRequested;
            bool hooked = _hook.Install();
            Log.Write($"startup: hook={hooked} mode={_hook.Mode} theme={ThemeManager.Current} " +
                      $"retrobar={RetroBar.IsPresent}/{RetroBar.Theme}");
            if (!hooked && _hook.Mode != WinKeyMode.Off)
            {
                MessageBox.Show(
                    "Der Tastatur-Hook konnte nicht gesetzt werden. Die Windows-Taste öffnet weiter das Windows-11-Menü.",
                    "Retro Menu", MessageBoxButton.OK, MessageBoxImage.Warning);
            }

            Catalog.Refreshed += OnCatalogRefreshed;
            Catalog.RefreshAsync();
            WatchProgramFolders();

            if (e.Args.Any(a => string.Equals(a, "--show", StringComparison.OrdinalIgnoreCase)))
            {
                Dispatcher.BeginInvoke(new Action(() => ToggleMenu(true)), DispatcherPriority.ApplicationIdle);
            }
        }

        public static string ActiveThemeName()
        {
            var settings = AppSettings.Instance;
            if (settings.FollowRetroBarTheme && Me?.RetroBar != null && Me.RetroBar.IsPresent)
                return ThemeManager.MapFromRetroBar(Me.RetroBar.Theme);
            return settings.Theme;
        }

        public static WinKeyMode ParseWinKeyMode(string value) =>
            Enum.TryParse<WinKeyMode>(value, true, out var mode) ? mode : WinKeyMode.Neutralize;

        public void ApplySettings()
        {
            Lang.Apply(AppSettings.Instance.Language, RetroBar?.Language);
            ThemeManager.Apply(ActiveThemeName());
            if (_hook != null) _hook.Mode = ParseWinKeyMode(AppSettings.Instance.WinKeyMode);
            _tray?.Localize();
            _menu?.Rebuild();
        }

        private void OnRetroBarChanged()
        {
            Dispatcher.BeginInvoke(new Action(() =>
            {
                if (AppSettings.Instance.FollowRetroBarTheme)
                    ThemeManager.Apply(ActiveThemeName());
                if (AppSettings.Instance.Language == "auto")
                {
                    Lang.Apply("auto", RetroBar.Language);
                    _tray?.Localize();
                    _menu?.Rebuild();
                }
            }));
        }

        private void OnCatalogRefreshed()
        {
            Dispatcher.BeginInvoke(new Action(() => _menu?.Rebuild()));
        }

        private void OnStartMenuRequested()
        {
            // Called on the hook thread: hand over and get out of the input queue.
            Log.Write("hook: start menu requested");
            Dispatcher.BeginInvoke(new Action(() => ToggleMenu(false)));
        }

        public void ToggleMenu(bool forceOpen)
        {
            if (_menu == null) return;

            try
            {
                if (_menu.IsOpen && !forceOpen)
                {
                    Log.Write("toggle: hiding");
                    _menu.HideMenu();
                }
                else
                {
                    _menu.ShowMenu();
                    Log.Write($"toggle: shown at {_menu.Left},{_menu.Top} visible={_menu.IsVisible}");
                }
            }
            catch (Exception ex)
            {
                Log.Write("toggle failed: " + ex);
            }
        }

        public void ShowSettings()
        {
            _menu?.HideMenu();
            var existing = Windows.OfType<SettingsWindow>().FirstOrDefault();
            if (existing != null) { existing.Activate(); return; }

            var window = new SettingsWindow();
            window.Show();
            window.Activate();
        }

        private void WatchProgramFolders()
        {
            string[] roots =
            {
                Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.CommonStartMenu), "Programs"),
                Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.StartMenu), "Programs")
            };

            _rescanDebounce = new DispatcherTimer { Interval = TimeSpan.FromSeconds(3) };
            _rescanDebounce.Tick += (_, __) =>
            {
                _rescanDebounce.Stop();
                Catalog.RefreshAsync();
            };

            _programWatchers = roots.Where(Directory.Exists).Select(root =>
            {
                var watcher = new FileSystemWatcher(root)
                {
                    IncludeSubdirectories = true,
                    NotifyFilter = NotifyFilters.FileName | NotifyFilters.DirectoryName,
                    EnableRaisingEvents = true
                };
                FileSystemEventHandler bump = (_, __) =>
                    Dispatcher.BeginInvoke(new Action(() => { _rescanDebounce.Stop(); _rescanDebounce.Start(); }));
                watcher.Created += bump;
                watcher.Deleted += bump;
                watcher.Renamed += (_, __) =>
                    Dispatcher.BeginInvoke(new Action(() => { _rescanDebounce.Stop(); _rescanDebounce.Start(); }));
                return watcher;
            }).ToArray();
        }

        public void Quit()
        {
            _hook?.Dispose();
            _tray?.Dispose();
            RetroBar?.Dispose();
            foreach (var watcher in _programWatchers) watcher.Dispose();
            Shutdown();
        }

        protected override void OnExit(ExitEventArgs e)
        {
            _hook?.Dispose();
            _tray?.Dispose();
            _singleInstance?.Dispose();
            base.OnExit(e);
        }
    }
}
