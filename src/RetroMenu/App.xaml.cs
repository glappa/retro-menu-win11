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

        /// <summary>Every program on the machine, for the search box.</summary>
        public ProgramIndex Programs { get; } = new ProgramIndex();

        /// <summary>Windows settings pages, also for the search box.</summary>
        public SettingsIndex Settings { get; } = new SettingsIndex();
        public RetroBarBridge RetroBar { get; private set; }

        private Mutex _singleInstance;
        private KeyboardHook _hook;
        private StartMenuWindow _menu;
        private TrayIconService _tray;
        private FileSystemWatcher[] _programWatchers = Array.Empty<FileSystemWatcher>();
        private DispatcherTimer _rescanDebounce;
        private EventWaitHandle _quitSignal;

        protected override void OnStartup(StartupEventArgs e)
        {
            base.OnStartup(e);

            // Debug aid: --dumpmenu <file> writes the shell context menu it reads for
            // that file to the log and exits. Runs before the single instance guard so
            // it works while the menu is already running.
            int dumpAt = Array.FindIndex(e.Args, a =>
                string.Equals(a, "--dumpmenu", StringComparison.OrdinalIgnoreCase));
            if (dumpAt >= 0 && dumpAt + 1 < e.Args.Length)
            {
                DumpShellMenu(e.Args[dumpAt + 1]);
                Shutdown();
                return;
            }

            // --quit asks a running instance to close properly. Killing the process
            // instead leaves its notification icon behind as a dead square until the
            // taskbar next rebuilds its tray.
            if (e.Args.Any(a => string.Equals(a, "--quit", StringComparison.OrdinalIgnoreCase)))
            {
                try
                {
                    if (EventWaitHandle.TryOpenExisting(QuitSignalName, out var running))
                    {
                        running.Set();
                        running.Dispose();
                    }
                }
                catch { }
                Shutdown();
                return;
            }

            // The very same executable is the installer. Started under its Setup
            // name, or with --setup, it offers to install itself instead of
            // opening a menu.
            bool wantsSetup = e.Args.Any(a => string.Equals(a, "--setup", StringComparison.OrdinalIgnoreCase))
                              || LooksLikeSetupDownload();
            bool wantsUninstall = e.Args.Any(a => string.Equals(a, "--uninstall", StringComparison.OrdinalIgnoreCase));

            if (wantsSetup || wantsUninstall)
            {
                ShowSetup(wantsUninstall);
                return;
            }

            _singleInstance = new Mutex(true, "RetroMenuWin11.SingleInstance", out bool created);
            if (!created)
            {
                Shutdown();
                return;
            }

            ListenForQuitSignal();

            // Last chance to take the notification icon down with us.
            AppDomain.CurrentDomain.ProcessExit += (_, __) => _tray?.Dispose();
            SessionEnding += (_, __) => _tray?.Dispose();

            DispatcherUnhandledException += (_, args) =>
            {
                MessageBox.Show(args.Exception.ToString(), "Retro Menu",
                    MessageBoxButton.OK, MessageBoxImage.Error);
                args.Handled = true;
            };

            // --demo replaces the user and their programs with placeholders, so the
            // screenshots in the README give nothing away.
            Demo.IsActive = e.Args.Any(a => string.Equals(a, "--demo", StringComparison.OrdinalIgnoreCase));

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
            _tray.RefreshRequested += () =>
            {
                Catalog.RefreshAsync();
                Programs.RefreshAsync();
                Settings.RefreshAsync();
            };
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

            // The machine wide scan is slower and only feeds the search box, so it
            // follows behind the Start Menu catalogue.
            Programs.RefreshAsync();
            Settings.RefreshAsync();
            WatchProgramFolders();

            if (e.Args.Any(a => string.Equals(a, "--show", StringComparison.OrdinalIgnoreCase)))
            {
                Dispatcher.BeginInvoke(new Action(() => ToggleMenu(true)), DispatcherPriority.ApplicationIdle);
            }
        }

        /// <summary>
        /// A freshly downloaded release asset carries "setup" in its file name and
        /// sits wherever the browser put it. That combination means the user just
        /// double-clicked the installer.
        /// </summary>
        private static bool LooksLikeSetupDownload()
        {
            try
            {
                string exe = Environment.ProcessPath;
                if (string.IsNullOrEmpty(exe)) return false;
                if (Installer.RunningFromInstallDirectory) return false;

                string name = System.IO.Path.GetFileNameWithoutExtension(exe);
                return name.IndexOf("setup", StringComparison.OrdinalIgnoreCase) >= 0;
            }
            catch { return false; }
        }

        private void ShowSetup(bool uninstall)
        {
            ThemeManager.Apply("Windows XP Blue");
            Lang.Apply("auto", null);

            var window = new SetupWindow(uninstall);
            window.Closed += (_, __) => Shutdown();
            window.Show();
        }

        private const string QuitSignalName = @"Local\RetroMenuWin11.Quit";

        /// <summary>Waits in the background for another instance started with --quit.</summary>
        private void ListenForQuitSignal()
        {
            try
            {
                _quitSignal = new EventWaitHandle(false, EventResetMode.AutoReset, QuitSignalName);
                var waiter = new Thread(() =>
                {
                    _quitSignal.WaitOne();
                    Dispatcher.BeginInvoke(new Action(Quit));
                })
                {
                    IsBackground = true,
                    Name = "RetroMenu quit signal"
                };
                waiter.Start();
            }
            catch { /* without the signal --quit simply does nothing */ }
        }

        private static void DumpShellMenu(string path)
        {
            using var menu = new ShellContextMenu();
            bool ok = menu.Open(path, IntPtr.Zero, false);
            Log.Write($"dumpmenu {path}: opened={ok} entries={menu.Entries.Count}");

            void Print(System.Collections.Generic.List<ShellMenuEntry> entries, string indent)
            {
                foreach (var entry in entries)
                {
                    Log.Write(indent + (entry.IsSeparator
                        ? "---"
                        : $"[{entry.Id}] {entry.Text}{(entry.IsEnabled ? "" : " (disabled)")}"));
                    if (entry.HasChildren) Print(entry.Children, indent + "    ");
                }
            }

            Print(menu.Entries, "  ");
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
            _quitSignal?.Dispose();
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
