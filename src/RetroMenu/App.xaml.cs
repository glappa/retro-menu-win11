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
        private EventWaitHandle _reloadSignal;

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

            // The same executable is also the settings program. Started under the
            // name RetroMenuSettings.exe, which the installer puts next to it as a
            // second name for the same file, or with --settings, it opens the
            // settings window and nothing else: no keyboard hook, no notification
            // icon, no menu. It has to work that way round, because the switch
            // that turns the menu off and on again lives in there.
            if (e.Args.Any(a => string.Equals(a, "--settings", StringComparison.OrdinalIgnoreCase))
                || LooksLikeSettingsProgram())
            {
                RunSettingsProgram();
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
            ListenForSettingsChanges();

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
            _tray.EnabledToggled += on =>
            {
                AppSettings.Instance.Enabled = on;
                AppSettings.Instance.SaveOptions();
                ApplySettings();
            };
            _tray.RefreshRequested += () =>
            {
                Catalog.RefreshAsync();
                Programs.RefreshAsync();
                Settings.RefreshAsync();
            };
            _tray.ExitRequested += Quit;
            _tray.Show();

            _hook = new KeyboardHook
            {
                Mode = EffectiveWinKeyMode(),
                SearchHotkey = SearchHotkeyWanted()
            };
            _hook.StartMenuRequested += OnStartMenuRequested;
            _hook.SearchRequested += OnSearchRequested;
            bool hooked = _hook.Install();
            Log.Write($"startup: hook={hooked} mode={_hook.Mode} theme={ThemeManager.Current} " +
                      $"retrobar={RetroBar.IsPresent}/{RetroBar.Theme}");
            _tray.SetEnabled(AppSettings.Instance.Enabled);

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

            // The Control Panel entries of the right hand column, if any were added.
            ControlPanelItems.Refreshed += OnControlPanelRefreshed;
            ControlPanelItems.RefreshAsync();

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

        /// <summary>
        /// Recognised by its own file name, the way the installer already is. The
        /// installed folder holds RetroMenu.exe and RetroMenuSettings.exe as two
        /// names for one file, so the name is all there is to go on.
        /// </summary>
        private static bool LooksLikeSettingsProgram()
        {
            try
            {
                string exe = Environment.ProcessPath;
                if (string.IsNullOrEmpty(exe)) return false;
                return Path.GetFileNameWithoutExtension(exe)
                    .IndexOf("settings", StringComparison.OrdinalIgnoreCase) >= 0;
            }
            catch { return false; }
        }

        /// <summary>
        /// The settings program: its own process, its own window, and no claim on
        /// the Windows key. It stands on its own on purpose, because the menu may
        /// be switched off or not installed at all, and this is where it is
        /// switched back on.
        /// </summary>
        private void RunSettingsProgram()
        {
            _singleInstance = new Mutex(true, "RetroMenuWin11.Settings", out bool created);
            if (!created)
            {
                SettingsBridge.NotifyFront();
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

            // Never on this thread: reading that folder loads the Control Panel's own
            // shell extensions into the process, and doing that on the UI thread
            // before the first frame leaves the window blank. The shell worker the
            // icons already use is the place for it; the list fills itself in.
            ControlPanelItems.RefreshAsync();

            // Only so the window can say what "follow RetroBar" currently lands on.
            RetroBar = new RetroBarBridge();
            RetroBar.Watch();

            Lang.Apply(AppSettings.Instance.Language, RetroBar.Language);
            ThemeManager.Apply(ActiveThemeName());

            var window = new SettingsWindow();
            window.Closed += (_, __) => Shutdown();
            window.Show();
            window.Activate();

            ListenForFrontSignal(window);
        }

        /// <summary>A second start of the settings program brings this one forward.</summary>
        private void ListenForFrontSignal(Window window)
        {
            try
            {
                var signal = SettingsBridge.CreateFrontSignal();
                var waiter = new Thread(() =>
                {
                    while (true)
                    {
                        signal.WaitOne();
                        Dispatcher.BeginInvoke(new Action(() =>
                        {
                            if (window.WindowState == WindowState.Minimized)
                                window.WindowState = WindowState.Normal;
                            window.Show();
                            Interop.NativeMethods.ForceForeground(
                                new System.Windows.Interop.WindowInteropHelper(window).Handle);
                        }));
                    }
                })
                {
                    IsBackground = true,
                    Name = "RetroMenu settings front"
                };
                waiter.Start();
            }
            catch { }
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

        /// <summary>
        /// Waits for the settings program to say it has written the file. Reading
        /// it again is cheap; rescanning every program on the machine is not, so
        /// that only happens when the setting it hangs on has actually moved.
        /// </summary>
        private void ListenForSettingsChanges()
        {
            try
            {
                _reloadSignal = SettingsBridge.CreateReloadSignal();
                var waiter = new Thread(() =>
                {
                    while (true)
                    {
                        _reloadSignal.WaitOne();
                        Dispatcher.BeginInvoke(new Action(ReloadSettings));
                    }
                })
                {
                    IsBackground = true,
                    Name = "RetroMenu settings signal"
                };
                waiter.Start();
            }
            catch { /* without the signal the changes arrive at the next start */ }
        }

        private void ReloadSettings()
        {
            bool storeApps = AppSettings.Instance.ShowStoreApps;

            AppSettings.Reload();
            Log.Write("settings reloaded: enabled=" + AppSettings.Instance.Enabled +
                      " theme=" + AppSettings.Instance.Theme);

            ApplySettings();
            if (storeApps != AppSettings.Instance.ShowStoreApps) Catalog.RefreshAsync();
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

        /// <summary>
        /// Switched off, the Windows key is left completely alone, and that on its
        /// own is what brings the Windows 11 menu back — including behind
        /// RetroBar's Start button, which is a simulated Windows key press.
        /// </summary>
        private static WinKeyMode EffectiveWinKeyMode() =>
            AppSettings.Instance.Enabled
                ? ParseWinKeyMode(AppSettings.Instance.WinKeyMode)
                : WinKeyMode.Off;

        /// <summary>
        /// Windows+S belongs to Windows again as soon as the menu is switched off —
        /// there would be nothing to open it into.
        /// </summary>
        private static bool SearchHotkeyWanted() =>
            AppSettings.Instance.Enabled && AppSettings.Instance.SearchHotkey;

        public void ApplySettings()
        {
            // Someone may have changed the Windows display language since we last
            // looked; ask again rather than repeating a stale answer.
            SystemLanguage.Forget();
            Lang.Apply(AppSettings.Instance.Language, RetroBar?.Language);
            ThemeManager.Apply(ActiveThemeName());
            if (_hook != null)
            {
                _hook.Mode = EffectiveWinKeyMode();
                _hook.SearchHotkey = SearchHotkeyWanted();
            }
            _tray?.Localize();
            _tray?.SetEnabled(AppSettings.Instance.Enabled);
            if (!AppSettings.Instance.Enabled) _menu?.HideMenu();
            _menu?.Rebuild();
        }

        private void OnRetroBarChanged()
        {
            Dispatcher.BeginInvoke(new Action(() =>
            {
                if (AppSettings.Instance.FollowRetroBarTheme)
                    ThemeManager.Apply(ActiveThemeName());
                // Only the two automatic settings care what RetroBar just became.
                string language = AppSettings.Instance.Language;
                if (language == Lang.AutoWindows || language == Lang.AutoRetroBar)
                {
                    Lang.Apply(language, RetroBar.Language);
                    _tray?.Localize();
                    _menu?.Rebuild();
                }
            }));
        }

        private void OnControlPanelRefreshed()
        {
            // Raised on the shell worker thread.
            Dispatcher.BeginInvoke(new Action(() => _menu?.Rebuild()));
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

        private void OnSearchRequested()
        {
            Log.Write("hook: search requested");
            Dispatcher.BeginInvoke(new Action(ShowSearch));
        }

        /// <summary>
        /// Windows+S. Unlike the Windows key it does not toggle: pressing it while
        /// the menu is already open puts the cursor back in the search box, which is
        /// what the Windows 11 search does too.
        /// </summary>
        public void ShowSearch()
        {
            if (_menu == null) return;
            if (!AppSettings.Instance.Enabled)
            {
                Log.Write("search: ignored, the retro menu is switched off");
                return;
            }

            try { _menu.ShowSearch(); }
            catch (Exception ex) { Log.Write("search failed: " + ex); }
        }

        public void ToggleMenu(bool forceOpen)
        {
            if (_menu == null) return;
            if (!AppSettings.Instance.Enabled)
            {
                Log.Write("toggle: ignored, the retro menu is switched off");
                return;
            }

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

        /// <summary>
        /// Hands over to the settings program, which is a process of its own. What
        /// it changes finds its way back through <see cref="SettingsBridge"/>.
        /// </summary>
        public void ShowSettings()
        {
            _menu?.HideMenu();
            if (SettingsBridge.OpenSettingsProgram()) return;

            MessageBox.Show(Lang.T("SettingsProgramMissing"), "Retro Menu",
                MessageBoxButton.OK, MessageBoxImage.Warning);
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
            _reloadSignal?.Dispose();
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
