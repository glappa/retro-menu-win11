using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Threading;
using Microsoft.Win32;
using RetroMenu.Interop;
using RetroMenu.Model;
using RetroMenu.Services;

namespace RetroMenu.Views
{
    public partial class StartMenuWindow : Window
    {
        private bool _popupOpen;
        private IntPtr _handle;

        private readonly DispatcherTimer _hoverTimer;
        private readonly TaskbarPresence _taskbarPresence = new TaskbarPresence();
        private StartItem _hoverItem;
        private Button _hoverAnchor;

        private readonly DispatcherTimer _allProgramsTimer;
        private readonly DispatcherTimer _fileSearchTimer;
        private ShellContextMenu _shellMenu;
        private int _searchToken;
        private List<StartItem> _programHits = new List<StartItem>();
        private List<StartItem> _settingHits = new List<StartItem>();

        private TaskbarInfo _bar;
        private double _scale = 1.0;

        public StartMenuWindow()
        {
            InitializeComponent();
            Deactivated += OnDeactivated;
            PreviewKeyDown += OnPreviewKeyDown;

            // XP opened the "My Recent Documents" and "Connect To" flyouts on hover.
            _hoverTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(320) };
            _hoverTimer.Tick += (_, __) => { _hoverTimer.Stop(); OpenPlaceSubmenu(); };

            // XP also opened All Programs by resting on it, not only by clicking.
            _allProgramsTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(400) };
            _allProgramsTimer.Tick += (_, __) =>
            {
                _allProgramsTimer.Stop();
                if (!_popupOpen && AllProgramsButton.IsMouseOver) OpenAllPrograms();
            };

            // Programs appear as you type; the file index is asked once typing pauses.
            _fileSearchTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(350) };
            _fileSearchTimer.Tick += (_, __) => { _fileSearchTimer.Stop(); SearchFiles(); };

            // Whatever changes the picture underneath us: a second monitor arriving,
            // the resolution changing, the taskbar moving, the screen locking. All of
            // them either move the menu or mean it should not be on screen at all.
            SizeChanged += (_, __) => Reposition();
            DpiChanged += (_, __) => Reposition();
            SystemEvents.DisplaySettingsChanged += OnDisplayChanged;
            SystemEvents.SessionSwitch += OnSessionSwitch;
            SystemEvents.PowerModeChanged += OnPowerModeChanged;
            SystemParameters.StaticPropertyChanged += OnSystemParameterChanged;
        }

        private void OnDisplayChanged(object sender, EventArgs e) =>
            Dispatcher.BeginInvoke(new Action(Relocate));

        private void OnSystemParameterChanged(object sender, System.ComponentModel.PropertyChangedEventArgs e)
        {
            if (e.PropertyName != nameof(SystemParameters.WorkArea) &&
                e.PropertyName != nameof(SystemParameters.PrimaryScreenHeight)) return;
            Dispatcher.BeginInvoke(new Action(Relocate));
        }

        private void OnSessionSwitch(object sender, SessionSwitchEventArgs e)
        {
            // A locked or handed over session must not keep a menu open behind it.
            if (e.Reason is SessionSwitchReason.SessionLock
                or SessionSwitchReason.ConsoleDisconnect
                or SessionSwitchReason.RemoteDisconnect
                or SessionSwitchReason.SessionLogoff)
            {
                Dispatcher.BeginInvoke(new Action(HideMenu));
            }
        }

        private void OnPowerModeChanged(object sender, PowerModeChangedEventArgs e)
        {
            if (e.Mode == PowerModes.Suspend) Dispatcher.BeginInvoke(new Action(HideMenu));
        }

        /// <summary>Looks the taskbar up again and puts the menu back against it.</summary>
        private void Relocate()
        {
            if (!IsVisible) return;
            _bar = TaskbarLocator.Locate();
            _scale = DpiScale();
            MaxHeight = AvailableHeight(_bar, _scale);
            Position(_bar, _scale);
            AnnounceToTaskbar();
        }

        /// <summary>
        /// Keeps the menu sitting on the taskbar when its own size changes — typing in
        /// the search box used to make it grow and wander off the bottom of the screen.
        /// </summary>
        private void Reposition()
        {
            if (!IsVisible || _bar == null) return;
            Position(_bar, _scale);
        }

        public bool IsOpen => IsVisible;

        public void EnsureHandle()
        {
            _handle = new WindowInteropHelper(this).EnsureHandle();
        }

        // ---------------------------------------------------------------- show / hide

        public void ShowMenu()
        {
            _bar = TaskbarLocator.Locate();
            _scale = DpiScale();

            ApplyMenuScale();
            MaxHeight = AvailableHeight(_bar, _scale);
            ListHost.Height = double.NaN;
            Rebuild();

            // Lay out at full size first, then place it: the menu grows with its
            // content, so its height is only known after a measure pass.
            Opacity = 0;
            Show();
            UpdateLayout();

            // Hold that height for as long as the menu stays open. Search results are
            // longer than the pinned list, and a menu that resizes under the pointer
            // while you type is no fun to aim at.
            if (!IsClassic && ListHost.ActualHeight > 0) ListHost.Height = ListHost.ActualHeight;

            UpdateLayout();
            Position(_bar, _scale);
            Opacity = 1;

            Activate();
            if (_handle == IntPtr.Zero) EnsureHandle();
            NativeMethods.ForceForeground(_handle);
            AnnounceToTaskbar();
            Sounds.MenuPopup();

            if (!IsClassic && SearchHost.Visibility == Visibility.Visible)
            {
                SearchBox.Clear();
                SearchBox.Focus();
            }
        }

        public void HideMenu()
        {
            _taskbarPresence.Hide();
            if (!IsVisible) return;
            _popupOpen = false;
            _hoverTimer.Stop();
            _allProgramsTimer.Stop();
            _hoverItem = null;
            SearchBox.Clear();
            ShowSearchResults(false);
            ListHost.Height = double.NaN;
            Hide();
        }

        /// <summary>
        /// Lets RetroBar know a start menu is up so an auto-hidden bar comes back
        /// out while the menu is showing.
        /// </summary>
        private void AnnounceToTaskbar()
        {
            if (!AppSettings.Instance.KeepTaskbarVisible)
            {
                _taskbarPresence.Hide();
                return;
            }

            if (_handle != IntPtr.Zero && NativeMethods.GetWindowRect(_handle, out var rect))
                _taskbarPresence.Show(rect);
        }

        protected override void OnClosed(EventArgs e)
        {
            SystemEvents.DisplaySettingsChanged -= OnDisplayChanged;
            SystemEvents.SessionSwitch -= OnSessionSwitch;
            SystemEvents.PowerModeChanged -= OnPowerModeChanged;
            SystemParameters.StaticPropertyChanged -= OnSystemParameterChanged;
            _taskbarPresence.Dispose();
            base.OnClosed(e);
        }

        private void OnDeactivated(object sender, EventArgs e)
        {
            // A cascading submenu takes the focus for a moment; that must not close us.
            if (_popupOpen) return;

            // Nor may the gap between one flyout closing and the next one opening:
            // right-clicking a second entry dismisses the first menu, and the focus
            // passes through nobody on the way. As long as the pointer is still on
            // the menu the user is plainly not finished with it.
            if (IsMouseOver)
            {
                Dispatcher.BeginInvoke(new Action(() =>
                {
                    if (IsVisible && IsMouseOver && !IsActive && !_popupOpen)
                        NativeMethods.ForceForeground(_handle);
                }), DispatcherPriority.Input);
                return;
            }

            HideMenu();
        }

        private void OnPreviewKeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Escape)
            {
                if (!string.IsNullOrEmpty(SearchBox.Text))
                {
                    SearchBox.Clear();
                    e.Handled = true;
                    return;
                }
                HideMenu();
                e.Handled = true;
                return;
            }

            if (_popupOpen) return;

            switch (e.Key)
            {
                case Key.Down:
                    e.Handled = Step(+1);
                    return;
                case Key.Up:
                    e.Handled = Step(-1);
                    return;
                case Key.Left:
                    e.Handled = SwitchColumn(toLeft: true);
                    return;
                case Key.Right:
                    if (Equals(Keyboard.FocusedElement, AllProgramsButton))
                    {
                        OpenAllPrograms();
                        e.Handled = true;
                        return;
                    }
                    e.Handled = SwitchColumn(toLeft: false);
                    return;
                case Key.Enter:
                    if (Keyboard.FocusedElement is Button pressed)
                    {
                        pressed.RaiseEvent(new RoutedEventArgs(
                            System.Windows.Controls.Primitives.ButtonBase.ClickEvent));
                        e.Handled = true;
                    }
                    return;
            }

            // Typing a letter jumps to the next entry starting with it, as in XP.
            if (SearchBox.IsKeyboardFocusWithin) return;
            if (e.Key < Key.A || e.Key > Key.Z) return;
            e.Handled = JumpToLetter(e.Key.ToString()[0]);
        }

        // ---------------------------------------------------------------- keyboard walk

        private static List<Button> ButtonsIn(DependencyObject root)
        {
            var found = new List<Button>();
            if (root == null) return found;

            void Walk(DependencyObject node)
            {
                int count = VisualTreeHelper.GetChildrenCount(node);
                for (int i = 0; i < count; i++)
                {
                    var child = VisualTreeHelper.GetChild(node, i);
                    if (child is Button button)
                    {
                        if (button.IsVisible) found.Add(button);
                        continue;
                    }
                    Walk(child);
                }
            }

            Walk(root);
            return found;
        }

        /// <summary>Left column top to bottom, with All Programs last where it sits.</summary>
        private List<Button> LeftButtons()
        {
            if (IsClassic) return ButtonsIn(ClassicItems);

            var list = ButtonsIn(SearchScroll.Visibility == Visibility.Visible ? SearchScroll : NormalScroll);
            list.Add(AllProgramsButton);
            return list;
        }

        private List<Button> RightButtons() => IsClassic ? new List<Button>() : ButtonsIn(PlaceItems);

        private bool Step(int direction)
        {
            var focused = Keyboard.FocusedElement as Button;
            var list = RightButtons().Contains(focused) ? RightButtons() : LeftButtons();
            if (list.Count == 0) return false;

            int index = list.IndexOf(focused);
            if (index < 0) index = direction > 0 ? -1 : 0;

            index = (index + direction + list.Count) % list.Count;
            list[index].Focus();
            return true;
        }

        private bool SwitchColumn(bool toLeft)
        {
            var focused = Keyboard.FocusedElement as Button;
            var from = toLeft ? RightButtons() : LeftButtons();
            var to = toLeft ? LeftButtons() : RightButtons();
            if (to.Count == 0) return false;

            int index = from.IndexOf(focused);
            if (index < 0) index = 0;

            to[Math.Min(index, to.Count - 1)].Focus();
            return true;
        }

        private bool JumpToLetter(char letter)
        {
            var focused = Keyboard.FocusedElement as Button;
            var list = RightButtons().Contains(focused) ? RightButtons() : LeftButtons();
            if (list.Count == 0) return false;

            int start = list.IndexOf(focused) + 1;
            for (int offset = 0; offset < list.Count; offset++)
            {
                var candidate = list[(start + offset) % list.Count];
                string name = (candidate.DataContext as StartItem)?.Name;
                if (string.IsNullOrEmpty(name)) continue;
                if (char.ToUpperInvariant(name[0]) != letter) continue;

                candidate.Focus();
                return true;
            }
            return false;
        }

        // ---------------------------------------------------------------- header

        private void OnHeaderClick(object sender, MouseButtonEventArgs e)
        {
            HideMenu();
            Launcher.Power("useraccounts");
        }

        private void ApplyMenuScale()
        {
            double scale = AppSettings.Instance.MenuScale;
            if (double.IsNaN(scale) || double.IsInfinity(scale)) scale = 1.0;
            scale = Math.Max(0.75, Math.Min(3.0, scale));
            RootScale.ScaleX = scale;
            RootScale.ScaleY = scale;
            ClassicScale.ScaleX = scale;
            ClassicScale.ScaleY = scale;
        }

        /// <summary>True while a 9x era theme is showing its single column.</summary>
        private bool IsClassic => ThemeManager.Layout == MenuLayout.Classic;

        private static double AvailableHeight(TaskbarInfo bar, double scale)
        {
            double height = bar.Edge switch
            {
                TaskbarEdge.Bottom => bar.Bar.Top - bar.Monitor.Top,
                TaskbarEdge.Top => bar.Monitor.Bottom - bar.Bar.Bottom,
                _ => bar.Monitor.Bottom - bar.Monitor.Top
            };
            return Math.Max(240, height / scale);
        }

        private void Position(TaskbarInfo info, double scale)
        {
            double barLeft = info.Bar.Left / scale;
            double barTop = info.Bar.Top / scale;
            double barRight = info.Bar.Right / scale;
            double barBottom = info.Bar.Bottom / scale;
            double monLeft = info.Monitor.Left / scale;
            double monTop = info.Monitor.Top / scale;
            double monRight = info.Monitor.Right / scale;
            double monBottom = info.Monitor.Bottom / scale;

            double width = ActualWidth;
            double height = ActualHeight;

            double left, top;
            switch (info.Edge)
            {
                case TaskbarEdge.Top:
                    left = barLeft;
                    top = barBottom;
                    break;
                case TaskbarEdge.Left:
                    left = barRight;
                    top = monBottom - height;
                    break;
                case TaskbarEdge.Right:
                    left = barLeft - width;
                    top = monBottom - height;
                    break;
                default:
                    left = barLeft;
                    top = barTop - height;
                    break;
            }

            Left = Math.Max(monLeft, Math.Min(left, monRight - width));
            Top = Math.Max(monTop, Math.Min(top, monBottom - height));
        }

        private double DpiScale()
        {
            try
            {
                if (_handle != IntPtr.Zero)
                {
                    uint dpi = NativeMethods.GetDpiForWindow(_handle);
                    if (dpi >= 48) return dpi / 96.0;
                }
            }
            catch { }

            var source = PresentationSource.FromVisual(this);
            if (source?.CompositionTarget != null)
                return source.CompositionTarget.TransformToDevice.M11;

            return 1.0;
        }

        // ---------------------------------------------------------------- content

        public void Rebuild()
        {
            bool classic = IsClassic;
            Root.Visibility = classic ? Visibility.Collapsed : Visibility.Visible;
            ClassicRoot.Visibility = classic ? Visibility.Visible : Visibility.Collapsed;

            UserName.Text = UserInfo.DisplayName();
            ApplyFontSmoothing();

            if (classic)
            {
                ClassicItems.ItemsSource = Launcher.BuildClassicRows();
                return;
            }

            AllProgramsLabel.Text = Lang.T("AllPrograms");
            LogOffLabel.Text = Lang.T("LogOff");
            ShutDownLabel.Text = Lang.T("ShutDown");
            SearchHint.Text = Lang.T("SearchHint");
            NoResults.Text = Lang.T("NoResults");

            if (UserPicture.Source == null)
                UserPicture.Source = UserInfo.Picture();

            SearchHost.Visibility = AppSettings.Instance.ShowSearchBox
                ? Visibility.Visible : Visibility.Collapsed;
            FilesToggle.Content = Lang.T("SearchFiles");
            FilesToggle.IsChecked = AppSettings.Instance.SearchFiles;

            BuildLeftColumn();
            PlaceItems.ItemsSource = Launcher.BuildPlaces();
        }

        private void ApplyFontSmoothing()
        {
            bool smooth = App.Me.RetroBar?.AllowFontSmoothing ?? true;
            TextOptions.SetTextRenderingMode(this,
                smooth ? TextRenderingMode.Auto : TextRenderingMode.Aliased);
        }

        private void BuildLeftColumn()
        {
            if (Demo.IsActive)
            {
                var demoTop = Launcher.BuildDefaultAppSlots();
                demoTop.AddRange(Demo.Pinned());
                TopItems.ItemsSource = demoTop;
                FrequentItems.ItemsSource = Demo.Frequent();
                TopSeparator.Visibility = Visibility.Visible;
                return;
            }

            var settings = AppSettings.Instance;
            SeedPinsOnce();

            // XP's top group: the Internet and E-mail slots, then whatever is pinned.
            var top = Launcher.BuildDefaultAppSlots();
            var taken = new HashSet<string>(top.Select(i => i.Id), StringComparer.OrdinalIgnoreCase);

            top.AddRange(settings.Pinned
                .Where(id => !taken.Contains(id))
                .Select(Resolve)
                .Where(i => i != null));

            foreach (var item in top) taken.Add(item.Id);

            var frequent = settings.MostUsed(settings.FrequentCount * 3)
                .Where(id => !taken.Contains(id))
                .Select(Resolve)
                .Where(i => i != null)
                .Take(Math.Max(0, settings.FrequentCount))
                .ToList();

            TopItems.ItemsSource = top;
            FrequentItems.ItemsSource = frequent;
            TopSeparator.Visibility = top.Count > 0 && frequent.Count > 0
                ? Visibility.Visible : Visibility.Collapsed;
        }

        private void SeedPinsOnce()
        {
            var settings = AppSettings.Instance;
            if (settings.Seeded) return;

            settings.Seeded = true;

            var quick = App.Me.RetroBar?.QuickLaunchOrder ?? new List<string>();
            foreach (var path in quick.Where(File.Exists).Take(4))
            {
                if (!settings.IsPinned(path)) settings.Pinned.Add(path);
            }
            settings.Save();
        }

        /// <summary>
        /// Turns a stored id back into a menu entry: from the catalogue when the
        /// program is still installed, otherwise straight from the path so pins to
        /// things outside the Start Menu (Quick Launch shortcuts) keep working.
        /// </summary>
        private static StartItem Resolve(string id)
        {
            if (string.IsNullOrWhiteSpace(id)) return null;

            var known = App.Me.Catalog.Find(id);
            if (known != null) return known;

            if (id.StartsWith("shell:AppsFolder\\", StringComparison.OrdinalIgnoreCase))
                return null;

            if (!File.Exists(id)) return null;

            return new StartItem
            {
                Name = Path.GetFileNameWithoutExtension(id),
                ParsingName = id,
                Target = id,
                Kind = StartItemKind.Shortcut
            };
        }

        // ---------------------------------------------------------------- items

        private void OnItemClick(object sender, RoutedEventArgs e)
        {
            if ((e.OriginalSource as FrameworkElement)?.DataContext is not StartItem item) return;
            if (item.Command == Launcher.Separator || item.Command == Launcher.GroupHeader) return;

            // The two entries at the foot of the classic menu open the same dialogs
            // the buttons in the XP footer do.
            if (item.Command == "logoffmenu") { OnLogOffClick(sender, e); return; }
            if (item.Command == "powermenu") { OnShutDownClick(sender, e); return; }

            // An entry that only carries a submenu does nothing on its own.
            if (string.IsNullOrEmpty(item.Command) && !string.IsNullOrEmpty(item.SubmenuSource))
            {
                OpenSubmenuFor(item, FindButton(e.OriginalSource as DependencyObject));
                return;
            }

            HideMenu();
            Launcher.Launch(item);
        }

        private void OnItemRightClick(object sender, MouseButtonEventArgs e)
        {
            if ((e.OriginalSource as FrameworkElement)?.DataContext is not StartItem item) return;
            if (item.Kind == StartItemKind.Place || item.Kind == StartItemKind.Command) return;

            var settings = AppSettings.Instance;
            var menu = new ContextMenu();

            // Our own two entries first, where XP kept its pinning commands.
            if (settings.IsPinned(item.Id))
                menu.Items.Add(Command(Lang.T("Unpin"), () => { settings.Unpin(item.Id); BuildLeftColumn(); }));
            else
                menu.Items.Add(Command(Lang.T("Pin"), () => { settings.Pin(item.Id); BuildLeftColumn(); }));

            menu.Items.Add(Command(Lang.T("RemoveFromList"),
                () => { settings.ForgetLaunch(item.Id); BuildLeftColumn(); }));

            // Underneath, the genuine Explorer menu: Open, Run as administrator,
            // Send to, Cut, Copy, Delete, Rename, Properties and any shell extension.
            _shellMenu?.Dispose();
            _shellMenu = new ShellContextMenu();

            bool extended = Keyboard.Modifiers.HasFlag(ModifierKeys.Shift);
            bool haveShellMenu = item.Kind == StartItemKind.Shortcut
                                 && !string.IsNullOrEmpty(item.ParsingName)
                                 && _shellMenu.Open(item.ParsingName, _handle, extended);

            if (haveShellMenu)
            {
                menu.Items.Add(new Separator());
                AddShellEntries(menu.Items, _shellMenu.Entries);
            }
            else
            {
                _shellMenu.Dispose();
                _shellMenu = null;

                // No shell menu for Store apps and for anything the shell declines.
                menu.Items.Insert(0, new Separator());
                menu.Items.Insert(0, Command(Lang.T("Open"), () => { HideMenu(); Launcher.Launch(item); }));

                if (settings.ShowRunAsAdmin && item.Kind == StartItemKind.Shortcut)
                {
                    menu.Items.Add(new Separator());
                    menu.Items.Add(Command(Lang.T("RunAsAdmin"),
                        () => { HideMenu(); Launcher.LaunchAsAdmin(item); }));
                    menu.Items.Add(Command(Lang.T("OpenFileLocation"),
                        () => { HideMenu(); Launcher.OpenFileLocation(item); }));
                }
            }

            menu.Closed += (_, __) =>
            {
                _shellMenu?.Dispose();
                _shellMenu = null;
            };

            menu.PlacementTarget = e.OriginalSource as UIElement;
            OpenPopup(menu);
            e.Handled = true;
        }

        private void AddShellEntries(ItemCollection into, System.Collections.Generic.List<ShellMenuEntry> entries)
        {
            foreach (var entry in entries)
            {
                if (entry.IsSeparator)
                {
                    if (into.Count > 0 && into[into.Count - 1] is not Separator)
                        into.Add(new Separator());
                    continue;
                }

                var element = new MenuItem { Header = entry.Text, IsEnabled = entry.IsEnabled };

                if (entry.HasChildren)
                {
                    AddShellEntries(element.Items, entry.Children);
                }
                else
                {
                    uint id = entry.Id;
                    element.Click += (_, __) =>
                    {
                        // Hand the menu object over before hiding, or the Closed
                        // handler disposes it out from under the command.
                        var shell = _shellMenu;
                        _shellMenu = null;
                        HideMenu();
                        shell?.Invoke(id);
                        shell?.Dispose();
                    };
                }

                into.Add(element);
            }

            // A menu that ends on a separator looks unfinished.
            while (into.Count > 0 && into[into.Count - 1] is Separator)
                into.RemoveAt(into.Count - 1);
        }

        private static MenuItem Command(string header, Action action)
        {
            var entry = new MenuItem { Header = header };
            entry.Click += (_, __) => action();
            return entry;
        }

        /// <summary>
        /// Opens a flyout without letting it close the menu underneath. The popup
        /// takes the activation away from us, and that Deactivated can arrive before
        /// ContextMenu.Opened does — so the guard has to be set up front.
        /// </summary>
        private void OpenPopup(ContextMenu menu)
        {
            _popupOpen = true;
            menu.Closed += (_, __) =>
            {
                _popupOpen = false;
                Dispatcher.BeginInvoke(new Action(() =>
                {
                    if (!IsVisible) return;

                    // Dismissed with the pointer still on the menu: keep it open and
                    // take the focus back. Dismissed by a click elsewhere: close.
                    if (IsMouseOver) NativeMethods.ForceForeground(_handle);
                    else HideMenu();
                }), DispatcherPriority.Input);
            };
            menu.IsOpen = true;
        }

        // ---------------------------------------------------------------- right column flyouts

        private void OnPlaceHover(object sender, MouseEventArgs e)
        {
            if (_popupOpen) return;

            var item = (e.OriginalSource as FrameworkElement)?.DataContext as StartItem;
            if (ReferenceEquals(item, _hoverItem)) return;

            _hoverItem = item;
            _hoverTimer.Stop();

            if (item == null || string.IsNullOrEmpty(item.SubmenuSource)) return;

            _hoverAnchor = FindButton(e.OriginalSource as DependencyObject);
            if (_hoverAnchor != null) _hoverTimer.Start();
        }

        private static Button FindButton(DependencyObject node)
        {
            while (node != null && node is not Button)
                node = VisualTreeHelper.GetParent(node);
            return node as Button;
        }

        private void OpenPlaceSubmenu()
        {
            if (!(_hoverAnchor?.IsMouseOver ?? false)) return;
            OpenSubmenuFor(_hoverItem, _hoverAnchor);
        }

        private void OpenSubmenuFor(StartItem item, Button anchor)
        {
            if (item == null || anchor == null || string.IsNullOrEmpty(item.SubmenuSource)) return;
            if (_popupOpen) return;

            var menu = new ContextMenu
            {
                PlacementTarget = anchor,
                Placement = System.Windows.Controls.Primitives.PlacementMode.Right,
                HorizontalOffset = -4,
                VerticalOffset = -3
            };

            if (item.SubmenuSource == Launcher.CatalogSubmenu)
            {
                Populate(menu.Items, App.Me.Catalog.Root.Children);
                if (menu.Items.Count == 0)
                    menu.Items.Add(new MenuItem { Header = Lang.T("Loading"), IsEnabled = false });
            }
            else
            {
                var entries = SubmenuEntries(item.SubmenuSource);
                if (entries.Count == 0)
                    menu.Items.Add(new MenuItem { Header = Lang.T("Empty"), IsEnabled = false });
                else
                    Populate(menu.Items, entries);
            }

            OpenPopup(menu);
        }

        private static List<StartItem> SubmenuEntries(string source)
        {
            var result = new List<StartItem>();

            if (string.Equals(source, "shell:Recent", StringComparison.OrdinalIgnoreCase))
            {
                // The Recent folder is plain files, and only there do we get the
                // "most recent first" order XP showed.
                try
                {
                    string folder = Path.Combine(
                        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                        @"Microsoft\Windows\Recent");

                    if (Directory.Exists(folder))
                    {
                        result.AddRange(new DirectoryInfo(folder)
                            .EnumerateFiles("*.lnk")
                            .OrderByDescending(f => f.LastWriteTimeUtc)
                            .Take(15)
                            .Select(f => new StartItem
                            {
                                Name = Path.GetFileNameWithoutExtension(f.Name),
                                ParsingName = f.FullName,
                                Target = f.FullName,
                                Kind = StartItemKind.Shortcut
                            }));
                    }
                }
                catch { }
                return result;
            }

            try
            {
                foreach (var entry in ShellFolder.Enumerate(source, 40))
                {
                    result.Add(new StartItem
                    {
                        Name = entry.Name,
                        ParsingName = entry.ParsingName,
                        Kind = StartItemKind.Command,
                        Command = "place:" + entry.ParsingName
                    });
                }
            }
            catch { }

            return result;
        }

        // ---------------------------------------------------------------- all programs

        private void OnAllProgramsClick(object sender, RoutedEventArgs e) => OpenAllPrograms();

        private void OnAllProgramsEnter(object sender, MouseEventArgs e)
        {
            if (_popupOpen) return;
            _allProgramsTimer.Stop();
            _allProgramsTimer.Start();
        }

        private void OnAllProgramsLeave(object sender, MouseEventArgs e) => _allProgramsTimer.Stop();

        private void OpenAllPrograms()
        {
            if (_popupOpen) return;

            var root = App.Me.Catalog.Root;
            var menu = new ContextMenu
            {
                PlacementTarget = AllProgramsButton,
                Placement = System.Windows.Controls.Primitives.PlacementMode.Right,
                HorizontalOffset = -6,
                VerticalOffset = 4
            };

            Populate(menu.Items, root.Children);
            if (menu.Items.Count == 0)
                menu.Items.Add(new MenuItem { Header = Lang.T("Loading"), IsEnabled = false });

            OpenPopup(menu);
        }

        private void Populate(ItemCollection into, IList<StartItem> children)
        {
            foreach (var child in children)
            {
                var entry = new MenuItem
                {
                    Header = child.Name,
                    DataContext = child,
                    Icon = IconFor(child)
                };

                // XP marked programs installed since the last look until opened once.
                if (child.IsNew && TryFindResource("NewItemHighlight") is Brush highlight)
                    entry.Background = highlight;

                if (child.IsFolder)
                {
                    entry.Items.Add(new MenuItem { Header = Lang.T("Loading"), IsEnabled = false });
                    entry.SubmenuOpened += FillSubmenu;
                }
                else
                {
                    entry.Click += (s, _) =>
                    {
                        var item = (StartItem)((MenuItem)s).DataContext;
                        item.IsNew = false;
                        HideMenu();
                        Launcher.Launch(item);
                    };
                }

                into.Add(entry);
            }
        }

        private void FillSubmenu(object sender, RoutedEventArgs e)
        {
            var entry = (MenuItem)sender;
            if (entry.DataContext is not StartItem folder) return;
            if (entry.Tag is string filled && filled == "done") return;

            entry.Tag = "done";
            folder.IsNew = false;
            entry.Background = null;
            entry.Items.Clear();
            Populate(entry.Items, folder.Children);
            e.Handled = true;
        }

        private static Image IconFor(StartItem item)
        {
            var image = new Image { Width = 16, Height = 16 };
            RenderOptions.SetBitmapScalingMode(image, BitmapScalingMode.HighQuality);
            image.SetBinding(Image.SourceProperty,
                new Binding(nameof(StartItem.SmallIcon)) { Source = item });
            return image;
        }

        // ---------------------------------------------------------------- search

        private void OnSearchFilesToggled(object sender, RoutedEventArgs e)
        {
            AppSettings.Instance.SearchFiles = FilesToggle.IsChecked == true;
            AppSettings.Instance.Save();
            RunSearch();
        }

        private void OnSearchTextChanged(object sender, TextChangedEventArgs e) => RunSearch();

        private static StartItem Header(string key) => new StartItem
        {
            Name = Lang.T(key),
            Kind = StartItemKind.Command,
            Command = Launcher.GroupHeader
        };

        /// <summary>
        /// Programs, then Windows settings, then optionally files — grouped under
        /// captions with the likeliest hit on top, the way the Windows 11 search
        /// presents them.
        /// </summary>
        private void RunSearch()
        {
            string query = SearchBox.Text;
            SearchHint.Visibility = string.IsNullOrEmpty(query) ? Visibility.Visible : Visibility.Collapsed;

            _fileSearchTimer.Stop();
            _searchToken++;

            if (string.IsNullOrWhiteSpace(query))
            {
                ShowSearchResults(false);
                return;
            }

            // Start Menu entries first, then anything else installed on the machine
            // that carries a name we have not already shown.
            var programs = App.Me.Catalog.Search(query, 20);
            var seen = new HashSet<string>(programs.Select(p => p.Name), StringComparer.CurrentCultureIgnoreCase);

            foreach (var extra in App.Me.Programs.Search(query, 30))
            {
                if (programs.Count >= 20) break;
                if (seen.Add(extra.Name)) programs.Add(extra);
            }

            _programHits = programs;
            _settingHits = App.Me.Settings.Search(query, 10)
                .Where(entry => seen.Add(entry.Name))
                .ToList();

            Compose(null);
            ShowSearchResults(true);

            if (FilesToggle.IsChecked == true)
            {
                SearchNote.Text = Lang.T("Searching");
                SearchNote.Visibility = Visibility.Visible;
                _fileSearchTimer.Start();
            }
        }

        private void Compose(List<StartItem> files)
        {
            var list = new List<StartItem>();
            var settings = _settingHits;

            if (_programHits.Count > 0)
            {
                list.Add(Header("BestMatch"));
                list.Add(_programHits[0]);

                if (_programHits.Count > 1)
                {
                    list.Add(Header("AppsGroup"));
                    list.AddRange(_programHits.Skip(1).Take(12));
                }
            }
            else if (settings.Count > 0)
            {
                list.Add(Header("BestMatch"));
                list.Add(settings[0]);
                settings = settings.Skip(1).ToList();
            }

            if (settings.Count > 0)
            {
                list.Add(Header("SettingsGroup"));
                list.AddRange(settings.Take(6));
            }

            if (files != null && files.Count > 0)
            {
                list.Add(Header("FilesGroup"));
                list.AddRange(files.Take(15));
            }

            SearchResults.ItemsSource = list;
            NoResults.Visibility = list.Count == 0 && SearchNote.Visibility != Visibility.Visible
                ? Visibility.Visible : Visibility.Collapsed;
        }

        /// <summary>Asks the Windows index, then folds the answer in if it is still wanted.</summary>
        private void SearchFiles()
        {
            string query = SearchBox.Text;
            if (string.IsNullOrWhiteSpace(query)) return;

            int token = _searchToken;

            Task.Run(() => FileSearch.Query(query, 25)).ContinueWith(task =>
            {
                var files = task.Status == TaskStatus.RanToCompletion
                    ? task.Result
                    : new List<StartItem>();

                Dispatcher.BeginInvoke(new Action(() =>
                {
                    if (token != _searchToken) return;

                    if (FileSearch.IsAvailable)
                    {
                        SearchNote.Visibility = Visibility.Collapsed;
                    }
                    else
                    {
                        SearchNote.Text = Lang.T("NoIndex");
                        SearchNote.Visibility = Visibility.Visible;
                    }

                    Compose(files);
                }));
            });
        }

        private void ShowSearchResults(bool show)
        {
            SearchScroll.Visibility = show ? Visibility.Visible : Visibility.Collapsed;
            NormalScroll.Visibility = show ? Visibility.Collapsed : Visibility.Visible;
            if (show) return;

            _programHits = new List<StartItem>();
            _settingHits = new List<StartItem>();
            SearchResults.ItemsSource = null;
            NoResults.Visibility = Visibility.Collapsed;
            SearchNote.Visibility = Visibility.Collapsed;
            SearchHint.Visibility = string.IsNullOrEmpty(SearchBox.Text)
                ? Visibility.Visible : Visibility.Collapsed;
        }

        private void OnSearchKeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter)
            {
                var first = (SearchResults.ItemsSource as IEnumerable<StartItem>)
                    ?.FirstOrDefault(item => item.Command != Launcher.GroupHeader);
                if (first != null)
                {
                    HideMenu();
                    Launcher.Launch(first);
                }
                e.Handled = true;
            }
            else if (e.Key == Key.Down)
            {
                var scroll = SearchScroll.Visibility == Visibility.Visible ? SearchScroll : NormalScroll;
                scroll.MoveFocus(new TraversalRequest(FocusNavigationDirection.Next));
                e.Handled = true;
            }
        }

        // ---------------------------------------------------------------- power

        private void OnLogOffClick(object sender, RoutedEventArgs e)
        {
            ShowPowerDialog(new[]
            {
                ("Lock", "lock"),
                ("LogOff", "logoff")
            });
        }

        private void OnShutDownClick(object sender, RoutedEventArgs e)
        {
            ShowPowerDialog(new[]
            {
                ("Standby", "standby"),
                ("TurnOff", "shutdown"),
                ("Restart", "restart")
            });
        }

        private void ShowPowerDialog((string Key, string Command)[] choices)
        {
            HideMenu();
            var dialog = new PowerDialog(choices);
            dialog.ShowDialog();
        }
    }
}
