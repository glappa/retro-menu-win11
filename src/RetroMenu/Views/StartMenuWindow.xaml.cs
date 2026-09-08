using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
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
        private int _popupGeneration;

        /// <summary>
        /// Set by the entries of our own that leave the menu standing — pinning,
        /// folders, forgetting a program. Without it the flyout closing counts as
        /// a click away from the menu, because the pointer was over the flyout.
        /// </summary>
        private bool _popupKeepsMenu;
        private IntPtr _handle;

        private readonly DispatcherTimer _hoverTimer;
        private readonly TaskbarPresence _taskbarPresence = new TaskbarPresence();
        private StartItem _hoverItem;
        private Button _hoverAnchor;

        private readonly DispatcherTimer _fileSearchTimer;
        private ShellContextMenu _shellMenu;
        private int _searchToken;
        private List<StartItem> _programHits = new List<StartItem>();
        private List<StartItem> _settingHits = new List<StartItem>();

        /// <summary>True while the search has the whole menu to itself.</summary>
        private bool _searchView;

        /// <summary>True while All Programs has the whole menu to itself.</summary>
        private bool _programsView;

        /// <summary>The group list on the left of that view; the first line is all of them.</summary>
        private List<StartItem> _groups = new List<StartItem>();
        private StartItem _allGroup;
        private StartItem _activeGroup;
        private int _programRows;
        private int _advancedToken;
        private bool _fillingFilters;
        private readonly DispatcherTimer _advancedFileTimer;
        private List<StartItem> _indexFiles = new List<StartItem>();
        private List<StartItem> _diskFiles = new List<StartItem>();
        private CancellationTokenSource _diskCancel;
        private bool _diskRunning;

        /// <summary>
        /// The rows on show. It is a live collection rather than a list handed over
        /// again and again: the walk reports every second or so, and replacing the
        /// whole list each time threw the scroll position away mid-scroll.
        /// </summary>
        private readonly ObservableCollection<StartItem> _advancedList =
            new ObservableCollection<StartItem>();

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

            // Programs appear as you type; the file index is asked once typing pauses.
            _fileSearchTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(350) };
            _fileSearchTimer.Tick += (_, __) => { _fileSearchTimer.Stop(); SearchFiles(); };

            _advancedFileTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(350) };
            _advancedFileTimer.Tick += (_, __) => { _advancedFileTimer.Stop(); SearchFilesAdvanced(); };

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

        /// <summary>
        /// Windows+S: the menu, with the cursor already in its search box. Where
        /// there is no box — it is switched off, or a 9x theme never had one — the
        /// search that theme did have opens instead.
        /// </summary>
        public void ShowSearch()
        {
            if (IsClassic)
            {
                // Windows 95 through 2000 kept their Find in a window of its own,
                // and so does this.
                HideMenu();
                Launcher.Run("search");
                return;
            }

            if (!IsOpen) ShowMenu();
            else NativeMethods.ForceForeground(_handle);

            if (SearchHost.Visibility != Visibility.Visible)
            {
                if (!_searchView) EnterSearchView(string.Empty);
                return;
            }

            SearchBox.Focus();
            SearchBox.SelectAll();
        }

        public void HideMenu()
        {
            _taskbarPresence.Hide();
            if (!IsVisible) return;
            _popupOpen = false;
            _popupGeneration++; // let any flyout still winding down keep its hands off
            _hoverTimer.Stop();
            _hoverItem = null;
            SearchBox.Clear();
            ShowSearchResults(false);
            LeaveSearchView();
            LeaveProgramsView();

            SearchHost.Visibility = AppSettings.Instance.ShowSearchBox
                ? Visibility.Visible : Visibility.Collapsed;

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

            // Neither may dragging a pinned entry about, which runs its own loop.
            if (_dragging) return;

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
                // Escape steps back one layer at a time: out of a filter or a
                // layout of its own, then out of a typed query, and only then out
                // of the menu itself.
                if (_programsView)
                {
                    if (!string.IsNullOrEmpty(ProgramsFilterBox.Text)) ProgramsFilterBox.Clear();
                    else LeaveProgramsView();
                    e.Handled = true;
                    return;
                }

                if (_searchView)
                {
                    LeaveSearchView();
                    e.Handled = true;
                    return;
                }

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

            if (_programsView)
            {
                var groups = ButtonsIn(ProgramGroups);
                groups.Add(ProgramsBackButton);
                return groups;
            }

            var list = ButtonsIn(SearchScroll.Visibility == Visibility.Visible ? SearchScroll : NormalScroll);
            list.Add(AllProgramsButton);
            return list;
        }

        private List<Button> RightButtons()
        {
            if (IsClassic) return new List<Button>();
            if (_programsView) return ButtonsIn(ProgramsScroll);

            var list = ButtonsIn(PlaceItems);
            if (TilePanel.Visibility == Visibility.Visible) list.AddRange(ButtonsIn(TileItems));
            return list;
        }

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
            // A theme or language change rebuilds everything; a layout of its own
            // is put away rather than half redrawn. All Programs comes back
            // afterwards, because a rescan lands here too — a program installing
            // itself in the background must not throw the user out of the list.
            bool programs = _programsView;
            LeaveSearchView();
            LeaveProgramsView();

            bool classic = IsClassic;
            Root.Visibility = classic ? Visibility.Collapsed : Visibility.Visible;
            ClassicRoot.Visibility = classic ? Visibility.Visible : Visibility.Collapsed;

            UserName.Text = UserInfo.DisplayName();
            ApplyFontSmoothing();

            if (classic)
            {
                TilePanel.Visibility = Visibility.Collapsed;
                ClassicItems.ItemsSource = Launcher.BuildClassicRows();
                return;
            }

            AllProgramsLabel.Text = Lang.T("AllPrograms");
            AllProgramsButton.Visibility = AppSettings.Instance.ShowAllProgramsButton
                ? Visibility.Visible : Visibility.Collapsed;
            SleepLabel.Text = Lang.T("Standby");
            LogOffLabel.Text = Lang.T("LogOff");
            ShutDownLabel.Text = Lang.T("ShutDown");
            SearchHint.Text = Lang.T("SearchHint");
            NoResults.Text = Lang.T("NoResults");

            if (UserPicture.Source == null)
                UserPicture.Source = UserInfo.Picture();

            UserTile.Visibility = AppSettings.Instance.ShowUserPicture
                ? Visibility.Visible : Visibility.Collapsed;

            SearchHost.Visibility = AppSettings.Instance.ShowSearchBox
                ? Visibility.Visible : Visibility.Collapsed;
            FilesToggle.Content = Lang.T("SearchFiles");
            FilesToggle.IsChecked = AppSettings.Instance.SearchFiles;

            BuildLeftColumn();
            PlaceItems.ItemsSource = Launcher.BuildPlaces();

            if (programs) EnterProgramsView();
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
                bool demoTiles = AppSettings.Instance.ShowTilePanel
                                 && AppSettings.Instance.ShowFavourites;
                var demoTop = AppSettings.Instance.ShowDefaultAppSlots
                    ? Launcher.BuildDefaultAppSlots() : new List<StartItem>();
                var demoFavourites = AppSettings.Instance.ShowFavourites
                    ? Demo.Pinned() : new List<StartItem>();
                if (!demoTiles) demoTop.AddRange(demoFavourites);

                TopItems.ItemsSource = demoTop;
                FrequentItems.ItemsSource = Demo.Frequent();
                TopSeparator.Visibility = Visibility.Visible;

                TilePanel.Visibility = demoTiles ? Visibility.Visible : Visibility.Collapsed;
                TileHeader.Text = Lang.T("TilesHeader");
                TileItems.ItemsSource = demoTiles ? demoFavourites : null;
                return;
            }

            var settings = AppSettings.Instance;
            SeedPinsOnce();

            // XP's top group: the Internet and E-mail slots, then the favourites.
            // A favourite may be a folder, which opens as a cascade like everything
            // else in this menu.
            var top = settings.ShowDefaultAppSlots
                ? Launcher.BuildDefaultAppSlots()
                : new List<StartItem>();
            var taken = new HashSet<string>(top.Select(i => i.Id), StringComparer.OrdinalIgnoreCase);

            // Switched off, the pinned programs are not merely hidden: they stay
            // out of "taken" as well, so a program that is both pinned and often
            // used still turns up in the list below rather than vanishing.
            var favourites = settings.ShowFavourites
                ? BuildFavourites(settings, taken)
                : new List<StartItem>();

            // With the tile panel showing them, listing the favourites in the column
            // as well would just say everything twice.
            bool tiles = settings.ShowTilePanel && settings.ShowFavourites;
            if (!tiles) top.AddRange(favourites);

            TilePanel.Visibility = tiles ? Visibility.Visible : Visibility.Collapsed;
            TileHeader.Text = Lang.T("TilesHeader");
            TileItems.ItemsSource = tiles ? favourites : null;

            // Either the ones started most often, as XP had it, or the ones started
            // most recently.
            var candidates = settings.ShowRecentPrograms
                ? settings.MostRecent(settings.FrequentCount * 3)
                : settings.MostUsed(settings.FrequentCount * 3);

            var frequent = candidates
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

        /// <summary>
        /// The favourites as menu entries. A folder becomes one entry that opens as
        /// a cascade; everything it holds is marked as spoken for so the list below
        /// does not repeat it.
        /// </summary>
        private static List<StartItem> BuildFavourites(AppSettings settings, HashSet<string> taken)
        {
            var built = new List<StartItem>();

            foreach (var favourite in settings.Favourites)
            {
                if (favourite.IsFolder)
                {
                    foreach (var id in favourite.Items) taken.Add(id);

                    var folder = new StartItem
                    {
                        Name = favourite.Folder,
                        Kind = StartItemKind.Folder,
                        ParsingName = "res:imageres.dll,18",
                        SubmenuSource = Launcher.FavouriteFolderPrefix + favourite.Folder
                    };

                    // Its tile draws these four rather than an empty folder.
                    folder.Preview.AddRange(favourite.Items.Take(4)
                        .Select(Resolve).Where(i => i != null));

                    built.Add(folder);
                    continue;
                }

                var resolved = Resolve(favourite.Id);
                if (resolved == null || !taken.Add(resolved.Id)) continue;
                built.Add(resolved);
            }

            return built;
        }

        private void SeedPinsOnce()
        {
            var settings = AppSettings.Instance;
            if (settings.Seeded) return;

            settings.Seeded = true;

            var quick = App.Me.RetroBar?.QuickLaunchOrder ?? new List<string>();
            foreach (var path in quick.Where(File.Exists).Take(4))
            {
                if (!settings.IsFavourite(path))
                    settings.Favourites.Add(new FavouriteEntry { Id = path });
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
            if (item.Command == "sleepmenu") { OnSleepClick(sender, e); return; }
            if (item.Command == "logoffmenu") { OnLogOffClick(sender, e); return; }
            if (item.Command == "powermenu") { OnShutDownClick(sender, e); return; }

            // The magnifier searches here rather than handing the job to Explorer.
            if (item.Command == Launcher.SearchInMenu) { BeginMenuSearch(); return; }

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

            // A favourites folder is ours alone; the shell knows nothing about it.
            if (item.IsFolder && item.SubmenuSource != null &&
                item.SubmenuSource.StartsWith(Launcher.FavouriteFolderPrefix, StringComparison.Ordinal))
            {
                string folderName = item.SubmenuSource.Substring(Launcher.FavouriteFolderPrefix.Length);

                menu.Items.Add(Command(Lang.T("RenameFolder"), () =>
                {
                    string name = InputDialog.Ask(this, Lang.T("FolderNamePrompt"), folderName);
                    if (name != null) settings.RenameFolder(folderName, name);
                    BuildLeftColumn();
                }));
                menu.Items.Add(Command(Lang.T("DissolveFolder"),
                    () => { settings.DissolveFolder(folderName); BuildLeftColumn(); }));

                menu.PlacementTarget = e.OriginalSource as UIElement;
                OpenPopup(menu);
                e.Handled = true;
                return;
            }

            // Our own entries first, where XP kept its pinning commands.
            bool favourite = settings.IsFavourite(item.Id);

            if (favourite)
                menu.Items.Add(Command(Lang.T("Unpin"),
                    () => { settings.RemoveFavourite(item.Id); BuildLeftColumn(); }));
            else
                menu.Items.Add(Command(Lang.T("Pin"),
                    () => { settings.AddFavourite(item.Id); BuildLeftColumn(); }));

            if (favourite)
            {
                menu.Items.Add(BuildFolderMenu(item, settings));

                if (settings.FolderOf(item.Id) != null)
                {
                    menu.Items.Add(Command(Lang.T("OutOfFolder"),
                        () => { settings.MoveOutOfFolder(item.Id); BuildLeftColumn(); }));
                }
            }

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

        /// <summary>"Move to folder" with the existing folders and a way to add one.</summary>
        private MenuItem BuildFolderMenu(StartItem item, AppSettings settings)
        {
            var parent = new MenuItem { Header = Lang.T("MoveToFolder") };
            string current = settings.FolderOf(item.Id);

            foreach (var name in settings.FolderNames.ToList())
            {
                if (string.Equals(name, current, StringComparison.OrdinalIgnoreCase)) continue;
                string target = name;
                parent.Items.Add(Command(target,
                    () => { settings.MoveToFolder(item.Id, target); BuildLeftColumn(); }));
            }

            if (parent.Items.Count > 0) parent.Items.Add(new Separator());

            parent.Items.Add(Command(Lang.T("NewFolder"), () =>
            {
                string name = InputDialog.Ask(this, Lang.T("FolderNamePrompt"));
                if (name != null) settings.MoveToFolder(item.Id, name);
                BuildLeftColumn();
            }));

            return parent;
        }

        /// <summary>
        /// One of our own entries in a flyout. What they do — pinning, folders,
        /// forgetting a program — happens to the menu itself, so the menu has to
        /// still be there afterwards. The ones that should close it say so by
        /// calling <see cref="HideMenu"/> themselves.
        /// </summary>
        private MenuItem Command(string header, Action action)
        {
            var entry = new MenuItem { Header = header };
            entry.Click += (_, __) => { _popupKeepsMenu = true; action(); };
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
            _popupKeepsMenu = false;

            // Right-clicking a second entry is a single gesture that both dismisses
            // the open flyout and asks for a new one, and the two arrive in either
            // order. Only the newest flyout may decide what happens when one closes;
            // without that the old one's farewell finds the new one covering the
            // pointer, concludes the user has clicked away, and shuts the menu.
            int generation = ++_popupGeneration;

            menu.Closed += (_, __) =>
            {
                if (generation != _popupGeneration) return;
                _popupOpen = false;
                bool keep = _popupKeepsMenu;

                Dispatcher.BeginInvoke(new Action(() =>
                {
                    if (generation != _popupGeneration || !IsVisible) return;

                    // Dismissed with the pointer still on the menu, or after an
                    // entry that only rearranged the menu: keep it open and take
                    // the focus back. Dismissed by a click elsewhere: close.
                    if (keep || IsMouseOver) NativeMethods.ForceForeground(_handle);
                    else HideMenu();
                }), DispatcherPriority.Input);
            };

            menu.IsOpen = true;
        }

        // ---------------------------------------------------------------- dragging pins about

        /// <summary>
        /// What travels in a drag: a program by its id, a folder by its name behind
        /// the same prefix its submenu uses, so a folder called like a path cannot
        /// be mistaken for one.
        /// </summary>
        private const string PinFormat = "RetroMenu.Favourite";

        /// <summary>Where a dragged entry would land.</summary>
        private enum DropPlace { None, Into, Before, After, End }

        private Point _dragFrom;
        private string _dragKey;
        private bool _dragging;
        private DropMark _dropMark;

        /// <summary>The key an entry travels under, or null when it cannot be dragged.</summary>
        private static string DragKeyOf(StartItem item)
        {
            if (item == null) return null;

            if (item.IsFolder)
            {
                return item.SubmenuSource != null &&
                       item.SubmenuSource.StartsWith(Launcher.FavouriteFolderPrefix, StringComparison.Ordinal)
                    ? item.SubmenuSource
                    : null;
            }

            // Only what is pinned can be rearranged. The frequently used list
            // underneath keeps its own order, which the machine decides.
            return AppSettings.Instance.IndexOfFavourite(item.Id) >= 0 ? item.Id : null;
        }

        private static bool IsFolderKey(string key) =>
            key != null && key.StartsWith(Launcher.FavouriteFolderPrefix, StringComparison.Ordinal);

        private static string FolderNameOf(string key) =>
            key.Substring(Launcher.FavouriteFolderPrefix.Length);

        private void OnPinDragStart(object sender, MouseButtonEventArgs e)
        {
            _dragKey = null;

            // The favourites of the demo run are made up; rearranging them would
            // write nonsense into the settings of whoever took the screenshots.
            if (Demo.IsActive) return;

            var item = (e.OriginalSource as FrameworkElement)?.DataContext as StartItem;
            string key = DragKeyOf(item);
            if (key == null) return;

            _dragFrom = e.GetPosition(this);
            _dragKey = key;
        }

        private void OnPinDragMove(object sender, MouseEventArgs e)
        {
            if (_dragKey == null || _dragging) return;

            if (e.LeftButton != MouseButtonState.Pressed)
            {
                _dragKey = null;
                return;
            }

            // Below the threshold this is still a click, and a click starts a program.
            var now = e.GetPosition(this);
            if (Math.Abs(now.X - _dragFrom.X) < SystemParameters.MinimumHorizontalDragDistance &&
                Math.Abs(now.Y - _dragFrom.Y) < SystemParameters.MinimumVerticalDragDistance) return;

            _dragging = true;
            try
            {
                DragDrop.DoDragDrop((DependencyObject)sender,
                                    new DataObject(PinFormat, _dragKey),
                                    DragDropEffects.Move);
            }
            finally
            {
                _dragging = false;
                _dragKey = null;
                ClearDropMark();
            }
        }

        private void OnPinDragOver(object sender, DragEventArgs e)
        {
            bool fits = PlanDrop(sender, e, out var target, out var place, out bool sideways);
            e.Effects = fits ? DragDropEffects.Move : DragDropEffects.None;
            e.Handled = true;
            ShowDropMark(target, place, sideways);
        }

        private void OnPinDragLeave(object sender, DragEventArgs e) => ClearDropMark();

        private void OnPinDrop(object sender, DragEventArgs e)
        {
            ClearDropMark();
            if (!PlanDrop(sender, e, out var target, out var place, out _)) return;

            ApplyDrop(e.Data.GetData(PinFormat) as string, target, place);
            e.Handled = true;
        }

        /// <summary>
        /// Works out what the drop would mean: dropped on the middle of an entry it
        /// makes a folder of the two, the way the Windows 11 menu groups its pinned
        /// apps; dropped nearer one edge it slots in beside it; dropped past the
        /// last entry it goes to the end.
        /// </summary>
        private bool PlanDrop(object sender, DragEventArgs e, out Button target,
                              out DropPlace place, out bool sideways)
        {
            target = null;
            place = DropPlace.None;

            // The tiles run across the panel, the entries in the left column down it.
            sideways = ReferenceEquals(sender, TilePanel);

            if (sender is not UIElement host) return false;
            if (e.Data?.GetDataPresent(PinFormat) != true) return false;

            string dragged = e.Data.GetData(PinFormat) as string;
            if (string.IsNullOrEmpty(dragged)) return false;

            var over = FindButton(host.InputHitTest(e.GetPosition(host)) as DependencyObject);
            string key = DragKeyOf(over?.DataContext as StartItem);

            if (key == null)
            {
                // Empty space, or one of the entries that do not belong to the
                // favourites at all: the end of the row is the only sensible place.
                place = DropPlace.End;
                return true;
            }

            if (string.Equals(key, dragged, StringComparison.OrdinalIgnoreCase)) return false;

            target = over;
            var point = e.GetPosition(over);
            double across = sideways
                ? point.X / Math.Max(1, over.ActualWidth)
                : point.Y / Math.Max(1, over.ActualHeight);

            // A folder cannot go into a folder: there is one level of them, as in
            // the Windows 11 menu, and that is the level the settings file keeps.
            if (!IsFolderKey(dragged) && across > 0.3 && across < 0.7) place = DropPlace.Into;
            else place = across < 0.5 ? DropPlace.Before : DropPlace.After;

            return true;
        }

        private void ApplyDrop(string dragged, Button target, DropPlace place)
        {
            if (string.IsNullOrEmpty(dragged) || place == DropPlace.None) return;

            var settings = AppSettings.Instance;
            bool folder = IsFolderKey(dragged);

            if (place == DropPlace.End)
            {
                Reorder(dragged, settings.Favourites.Count);
                BuildLeftColumn();
                return;
            }

            string key = DragKeyOf(target?.DataContext as StartItem);
            if (key == null) return;

            if (place == DropPlace.Into)
            {
                if (IsFolderKey(key)) settings.MoveToFolder(dragged, FolderNameOf(key));
                else settings.GroupInto(key, dragged, Lang.T("FolderDefaultName"));
            }
            else
            {
                int at = IsFolderKey(key)
                    ? settings.IndexOfFolder(FolderNameOf(key))
                    : settings.IndexOfFavourite(key);

                if (at < 0) return;
                Reorder(dragged, place == DropPlace.After ? at + 1 : at);
            }

            BuildLeftColumn();
        }

        private static void Reorder(string key, int to)
        {
            if (IsFolderKey(key)) AppSettings.Instance.ReorderFolder(FolderNameOf(key), to);
            else AppSettings.Instance.ReorderFavourite(key, to);
        }

        private void ShowDropMark(Button target, DropPlace place, bool sideways)
        {
            ClearDropMark();
            if (target == null || place == DropPlace.None || place == DropPlace.End) return;

            var layer = AdornerLayer.GetAdornerLayer(target);
            if (layer == null) return;

            var kind = place switch
            {
                DropPlace.Into => DropMarkKind.Frame,
                DropPlace.Before => DropMarkKind.Before,
                _ => DropMarkKind.After
            };

            _dropMark = new DropMark(target, kind, sideways,
                                     TryFindResource("ItemHighlightBackground") as Brush ?? Brushes.Black);
            layer.Add(_dropMark);
        }

        private void ClearDropMark()
        {
            if (_dropMark == null) return;
            AdornerLayer.GetAdornerLayer(_dropMark.AdornedElement)?.Remove(_dropMark);
            _dropMark = null;
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
            else if (item.SubmenuSource.StartsWith(Launcher.FavouriteFolderPrefix, StringComparison.Ordinal))
            {
                var contents = FavouriteFolderContents(
                    item.SubmenuSource.Substring(Launcher.FavouriteFolderPrefix.Length));

                if (contents.Count == 0)
                    menu.Items.Add(new MenuItem { Header = Lang.T("Empty"), IsEnabled = false });
                else
                    Populate(menu.Items, contents);
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

        private static List<StartItem> FavouriteFolderContents(string name)
        {
            if (Demo.IsActive) return Demo.FolderContents();

            var folder = AppSettings.Instance.Favourites.FirstOrDefault(
                f => f.IsFolder && string.Equals(f.Folder, name, StringComparison.OrdinalIgnoreCase));

            if (folder == null) return new List<StartItem>();

            return folder.Items.Select(Resolve).Where(i => i != null).ToList();
        }

        private static List<StartItem> SubmenuEntries(string source)
        {
            var result = new List<StartItem>();

            // XP listed the connections themselves here, with "Show all connections"
            // underneath. The shell gives them all the same parsing name, so they
            // carry their own command instead of a path.
            if (string.Equals(source, Launcher.ConnectionsSubmenu, StringComparison.OrdinalIgnoreCase))
            {
                // Reaching another machine belongs under this heading as much as the
                // adapters do, and Windows brings both clients along itself: Remote
                // Desktop, and the OpenSSH one behind it. Whatever is not installed
                // is simply left out.
                if (Launcher.RemoteDesktopPath != null)
                    result.Add(new StartItem
                    {
                        Name = Lang.T("RemoteDesktop"),
                        ParsingName = Launcher.RemoteDesktopPath,
                        Kind = StartItemKind.Command,
                        Command = Launcher.RemoteDesktop
                    });

                if (Launcher.SshPath != null)
                    result.Add(new StartItem
                    {
                        Name = Lang.T("SshConnection"),
                        ParsingName = Launcher.SshPath,
                        Kind = StartItemKind.Command,
                        Command = Launcher.SshConnect
                    });

                if (Launcher.SftpPath != null)
                    result.Add(new StartItem
                    {
                        Name = Lang.T("SftpConnection"),
                        ParsingName = Launcher.SftpPath,
                        Kind = StartItemKind.Command,
                        Command = Launcher.SftpConnect
                    });

                // Telnet went from a program every Windows had to an optional
                // feature; where it has not been switched on, the entry stays away.
                if (Launcher.TelnetPath != null)
                    result.Add(new StartItem
                    {
                        Name = Lang.T("TelnetConnection"),
                        ParsingName = Launcher.TelnetPath,
                        Kind = StartItemKind.Command,
                        Command = Launcher.TelnetConnect
                    });

                result.Add(new StartItem
                {
                    Name = Lang.T("FtpConnection"),
                    ParsingName = "res:imageres.dll,143",
                    Kind = StartItemKind.Command,
                    Command = Launcher.FtpConnect
                });

                result.Add(new StartItem
                {
                    Name = Lang.T("NetworkFolder"),
                    ParsingName = "res:imageres.dll,25",
                    Kind = StartItemKind.Command,
                    Command = Launcher.NetworkFolder
                });

                // The Linux systems, as a cascade of their own when there are any.
                var systems = WslDistros.Names();
                if (Launcher.WslPath != null && systems.Count > 0)
                {
                    var wsl = new StartItem
                    {
                        Name = Lang.T("WslShell"),
                        ParsingName = Launcher.WslPath,
                        Kind = StartItemKind.Folder,
                        SubmenuSource = Launcher.WslSubmenu
                    };

                    foreach (string system in systems)
                    {
                        wsl.Children.Add(new StartItem
                        {
                            Name = system,
                            ParsingName = Launcher.WslPath,
                            Kind = StartItemKind.Command,
                            Command = Launcher.WslPrefix + system
                        });
                    }

                    result.Add(wsl);
                }

                result.Add(new StartItem
                {
                    Name = Lang.T("MapDrive"),
                    ParsingName = "res:imageres.dll,120",
                    Kind = StartItemKind.Command,
                    Command = Launcher.MapDrive
                });

                result.Add(new StartItem
                {
                    Name = "-",
                    Kind = StartItemKind.Command,
                    Command = Launcher.Separator
                });

                int beforeAdapters = result.Count;
                foreach (string name in NetworkConnections.Names())
                {
                    result.Add(new StartItem
                    {
                        Name = name,
                        ParsingName = "res:netshell.dll,0",
                        Kind = StartItemKind.Command,
                        Command = Launcher.ConnectionPrefix + name
                    });
                }

                // Only separate the adapters from what follows if there were any.
                if (result.Count > beforeAdapters)
                {
                    result.Add(new StartItem
                    {
                        Name = "-",
                        Kind = StartItemKind.Command,
                        Command = Launcher.Separator
                    });
                }

                result.Add(new StartItem
                {
                    Name = Lang.T("AllConnections"),
                    ParsingName = "res:imageres.dll,25",
                    Kind = StartItemKind.Command,
                    Command = Launcher.Connections
                });

                return result;
            }

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

        /// <summary>
        /// "All Programs" takes the menu over rather than cascading out of it, the
        /// way the search does: the groups on the left, the programs themselves in
        /// as many columns as the screen has room for. A cascade is one entry wide
        /// however many programs there are, and on a machine with a few hundred of
        /// them that meant walking a wall of submenus for every one.
        /// </summary>
        private void OnAllProgramsClick(object sender, RoutedEventArgs e) => OpenAllPrograms();

        private void OpenAllPrograms()
        {
            if (_popupOpen || IsClassic) return;
            EnterProgramsView();
        }

        /// <summary>A 26 pixel row and its gap; the width takes a long name.</summary>
        private const double ProgramRow = 28;
        private const double ProgramColumn = 226;

        private void EnterProgramsView()
        {
            if (_searchView) LeaveSearchView();
            _programsView = true;

            // Take over the height the columns had, as the search layout does, and
            // spread out sideways as far as the screen allows.
            double height = ColumnsHost.ActualHeight;
            if (height > 100) ProgramsViewGrid.Height = height;
            ProgramsViewGrid.Width = ProgramsWidth(App.Me.Catalog.Flat.Count);

            ColumnsHost.Visibility = Visibility.Collapsed;
            ProgramsViewHost.Visibility = Visibility.Visible;

            ProgramsTitle.Text = Lang.T("AllPrograms");
            ProgramsFilterLabel.Text = Lang.T("ProgramFilter");
            ProgramsGroupsLabel.Text = Lang.T("ProgramGroups");
            ProgramsBackLabel.Text = Lang.T("Back");
            ProgramsFilterBox.Text = string.Empty;

            _activeGroup = null;
            BuildProgramGroups();
            ShowPrograms();
            ProgramsFilterBox.Focus();

            // The menu just changed shape, and the columns can only be counted once
            // the new shape has been through a layout pass.
            Dispatcher.BeginInvoke(new Action(() => { Reposition(); ShowPrograms(); }),
                                   DispatcherPriority.Loaded);
        }

        private void LeaveProgramsView()
        {
            if (!_programsView) return;

            _programsView = false;
            ProgramsViewHost.Visibility = Visibility.Collapsed;
            ColumnsHost.Visibility = Visibility.Visible;
            ProgramsScroll.Content = null;
            ProgramGroups.ItemsSource = null;
            _activeGroup = null;

            Dispatcher.BeginInvoke(new Action(Reposition), DispatcherPriority.Loaded);
        }

        private void OnLeaveProgramsView(object sender, RoutedEventArgs e) => LeaveProgramsView();

        /// <summary>
        /// Wide enough for the programs there are, and no wider: two columns at the
        /// least, four at the most — past four the eye has further to travel than
        /// the scrollbar would have moved — and never wider than the screen. It is
        /// settled once, as the view opens, so that filtering does not make the
        /// menu breathe in and out under the pointer.
        /// </summary>
        private double ProgramsWidth(int programs)
        {
            int rows = Math.Max(1, ProgramRows());
            int columns = Math.Max(2, Math.Min(4, (programs + rows - 1) / rows));

            double scale = Math.Max(0.75, Math.Min(3.0, AppSettings.Instance.MenuScale));
            double screen = _bar == null ? 0 : (_bar.Monitor.Right - _bar.Monitor.Left) / _scale;
            double room = screen / scale - 24;
            double wanted = 208 + columns * ProgramColumn + 12;

            return room < 480 ? wanted : Math.Min(wanted, room);
        }

        private void BuildProgramGroups()
        {
            // The line above the folders stands for the whole catalogue.
            if (_allGroup == null || _allGroup.Name != Lang.T("AllPrograms"))
            {
                _allGroup = new StartItem
                {
                    Name = Lang.T("AllPrograms"),
                    Kind = StartItemKind.Folder,
                    ParsingName = "res:imageres.dll,18"
                };
            }

            var groups = new List<StartItem> { _allGroup };

            foreach (var folder in App.Me.Catalog.Root.Children.Where(c => c.IsFolder))
            {
                // The Store apps are gathered into a group of our own making, which
                // stands for no folder on disk and so has no icon to ask for.
                if (string.IsNullOrEmpty(folder.ParsingName)) folder.ParsingName = "res:imageres.dll,18";
                groups.Add(folder);
            }

            _groups = groups;
            MarkActiveGroup();
            ProgramGroups.ItemsSource = _groups;
        }

        /// <summary>The group being shown is the bold one, as in a list of tabs.</summary>
        private void MarkActiveGroup()
        {
            var active = _activeGroup ?? _allGroup;
            foreach (var group in _groups) group.Bold = ReferenceEquals(group, active);
        }

        private void OnProgramGroupClick(object sender, RoutedEventArgs e)
        {
            if ((e.OriginalSource as FrameworkElement)?.DataContext is not StartItem group) return;

            _activeGroup = ReferenceEquals(group, _allGroup) ? null : group;
            MarkActiveGroup();
            ProgramGroups.Items.Refresh();
            ShowPrograms();
            e.Handled = true;
        }

        /// <summary>
        /// What the right hand side shows: the chosen group, or the whole
        /// catalogue, narrowed by whatever has been typed into the filter box.
        /// Folders are flattened away — the group list on the left is where the
        /// tree is, and repeating it in both halves helps nobody.
        /// </summary>
        private List<StartItem> CurrentPrograms()
        {
            var source = _activeGroup == null ? App.Me.Catalog.Flat : Flatten(_activeGroup);
            string query = ProgramsFilterBox.Text?.Trim() ?? string.Empty;

            if (query.Length == 0)
                return source.OrderBy(i => i.Name, StringComparer.CurrentCultureIgnoreCase).ToList();

            return source
                .Select(item => new { item, rank = SearchRank.Of(item.Name, query) })
                .Where(x => x.rank > 0)
                .OrderByDescending(x => x.rank)
                .ThenBy(x => x.item.Name, StringComparer.CurrentCultureIgnoreCase)
                .Select(x => x.item)
                .ToList();
        }

        private static List<StartItem> Flatten(StartItem folder)
        {
            var found = new List<StartItem>();

            void Walk(StartItem node)
            {
                foreach (var child in node.Children)
                {
                    if (child.IsFolder) Walk(child);
                    else found.Add(child);
                }
            }

            Walk(folder);
            return found;
        }

        /// <summary>
        /// Fills the right hand side. The programs run down a column and on into
        /// the next, the way a newspaper sets its text, so an alphabetical list
        /// stays alphabetical however many columns it takes.
        /// </summary>
        private void ShowPrograms()
        {
            if (!_programsView) return;

            var items = CurrentPrograms();

            ProgramsHeader.Text = _activeGroup?.Name ?? Lang.T("AllPrograms");
            ProgramsCount.Text = items.Count > 0 ? Lang.F("ProgramCount", items.Count)
                : App.Me.Catalog.Flat.Count == 0 ? Lang.T("Loading")
                : Lang.T("NoResults");

            int rows = ProgramRows();
            _programRows = rows;

            var columns = new StackPanel { Orientation = Orientation.Horizontal };
            var template = (DataTemplate)Resources["ProgramRowTemplate"];

            for (int start = 0; start < items.Count; start += rows)
            {
                columns.Children.Add(new ItemsControl
                {
                    Width = ProgramColumn,
                    ItemTemplate = template,
                    ItemsSource = items.GetRange(start, Math.Min(rows, items.Count - start))
                });
            }

            ProgramsScroll.Content = columns;
            ProgramsScroll.ScrollToHorizontalOffset(0);
        }

        private int ProgramRows()
        {
            // Before the first layout pass the view has no height of its own yet;
            // the one it is about to be given, less the caption line, is the best
            // that can be said at that point.
            double height = ProgramsScroll.ActualHeight;
            if (height < 80) height = Math.Max(120, ProgramsViewGrid.Height - 34);

            // The scrollbar along the bottom takes its 17 pixels out of the height
            // as soon as the programs outgrow the view. Leaving room for it from
            // the start keeps the last row of every column whole, and keeps the
            // count from changing the moment the bar appears.
            return Math.Max(1, (int)((height - 18) / ProgramRow));
        }

        private void OnProgramsResized(object sender, SizeChangedEventArgs e)
        {
            // Only when a whole row has been won or lost, or the rebuild would
            // chase its own scrollbar up and down.
            if (!_programsView || !e.HeightChanged) return;
            if (ProgramRows() == _programRows) return;
            ShowPrograms();
        }

        /// <summary>The programs stand in columns, so the wheel moves sideways.</summary>
        private void OnProgramsWheel(object sender, MouseWheelEventArgs e)
        {
            if (e.Delta == 0) return;
            ProgramsScroll.ScrollToHorizontalOffset(
                ProgramsScroll.HorizontalOffset - Math.Sign(e.Delta) * ProgramColumn / 2);
            e.Handled = true;
        }

        private void OnProgramsFilterChanged(object sender, TextChangedEventArgs e) => ShowPrograms();

        private void OnProgramsFilterKeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter)
            {
                var first = CurrentPrograms().FirstOrDefault();
                if (first != null)
                {
                    HideMenu();
                    Launcher.Launch(first);
                }
                e.Handled = true;
            }
            else if (e.Key == Key.Down)
            {
                var first = ButtonsIn(ProgramsScroll).FirstOrDefault();
                if (first == null) return;
                first.Focus();
                e.Handled = true;
            }
        }

        private void Populate(ItemCollection into, IList<StartItem> children)
        {
            foreach (var child in children)
            {
                if (child.Command == Launcher.Separator)
                {
                    into.Add(new System.Windows.Controls.Separator());
                    continue;
                }

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

        /// <summary>
        /// "Search" from the right hand column. The menu searches itself rather than
        /// handing the job to Explorer, and it does it in a layout of its own: the
        /// columns step aside and the whole menu becomes the search, with the
        /// criteria on the left and the results on the right, the way XP's search
        /// arranged them. The classic single-column layouts have no room for that
        /// and keep calling the Windows search.
        /// </summary>
        private void BeginMenuSearch()
        {
            if (IsClassic)
            {
                HideMenu();
                Launcher.Run("search");
                return;
            }

            EnterSearchView(SearchBox.Text);
        }

        private void EnterSearchView(string query)
        {
            _searchView = true;

            // Take over the height the columns had. Without it the menu would grow
            // with every hit until it filled the screen instead of scrolling.
            double height = ColumnsHost.ActualHeight;
            if (height > 100) SearchViewGrid.Height = height;

            ColumnsHost.Visibility = Visibility.Collapsed;
            SearchViewHost.Visibility = Visibility.Visible;

            LocalizeSearchView();
            FillSearchFilters();

            _advancedList.Clear();
            AdvancedResults.ItemsSource = _advancedList;

            AdvancedBox.Text = query ?? string.Empty;
            AdvancedBox.CaretIndex = AdvancedBox.Text.Length;
            AdvancedBox.Focus();

            // The menu just changed shape; it has to be put back on the taskbar edge.
            Dispatcher.BeginInvoke(new Action(Reposition), DispatcherPriority.Loaded);
            RunAdvancedSearch();
        }

        private void LeaveSearchView()
        {
            if (!_searchView) return;

            _searchView = false;
            _advancedToken++;
            _advancedFileTimer.Stop();
            CancelDiskSearch();
            _indexFiles = new List<StartItem>();
            _diskFiles = new List<StartItem>();

            SearchViewHost.Visibility = Visibility.Collapsed;
            ColumnsHost.Visibility = Visibility.Visible;
            _advancedList.Clear();
            AdvancedNote.Text = string.Empty;

            Dispatcher.BeginInvoke(new Action(Reposition), DispatcherPriority.Loaded);
        }

        private void OnLeaveSearchView(object sender, RoutedEventArgs e) => LeaveSearchView();

        private void LocalizeSearchView()
        {
            AdvancedTitle.Text = Lang.T("AdvancedSearch");
            SearchForLabel.Text = Lang.T("SearchFor");
            CategoriesLabel.Text = Lang.T("Categories");
            AppsFilter.Content = Lang.T("AppsGroup");
            SettingsFilter.Content = Lang.T("SettingsGroup");
            FilesFilter.Content = Lang.T("FilesGroup");
            FileTypeLabel.Text = Lang.T("FileType");
            LocationLabel.Text = Lang.T("Location");
            ModifiedLabel.Text = Lang.T("Modified");
            BackLabel.Text = Lang.T("Back");
        }

        /// <summary>One line of a filter box: what it says, and what it means.</summary>
        private sealed class Choice
        {
            public Choice(string label, object value)
            {
                Label = label;
                Value = value;
            }

            public string Label { get; }
            public object Value { get; }
            public override string ToString() => Label;
        }

        private void FillSearchFilters()
        {
            _fillingFilters = true;
            try
            {
                AppsFilter.IsChecked = true;
                SettingsFilter.IsChecked = true;
                FilesFilter.IsChecked = true;

                FileTypeBox.ItemsSource = new[]
                {
                    new Choice(Lang.T("AnyType"), null),
                    new Choice(Lang.T("TypeDocuments"), "document"),
                    new Choice(Lang.T("TypePictures"), "picture"),
                    new Choice(Lang.T("TypeMusic"), "music"),
                    new Choice(Lang.T("TypeVideos"), "video"),
                    new Choice(Lang.T("TypeFolders"), "folder"),
                };
                FileTypeBox.SelectedIndex = 0;

                // The folder names come from the shell, so they are already in the
                // language Windows speaks and follow a renamed Downloads folder.
                var places = new List<Choice> { new Choice(Lang.T("AnyLocation"), null) };
                foreach (var scope in SearchScopes.Folders())
                    places.Add(new Choice(scope.Name, scope.Path));
                LocationBox.ItemsSource = places;
                LocationBox.SelectedIndex = 0;

                ModifiedBox.ItemsSource = new[]
                {
                    new Choice(Lang.T("AnyTime"), null),
                    new Choice(Lang.T("Today"), TimeSpan.FromDays(1)),
                    new Choice(Lang.T("ThisWeek"), TimeSpan.FromDays(7)),
                    new Choice(Lang.T("ThisMonth"), TimeSpan.FromDays(31)),
                    new Choice(Lang.T("ThisYear"), TimeSpan.FromDays(365)),
                };
                ModifiedBox.SelectedIndex = 0;
            }
            finally { _fillingFilters = false; }
        }

        private FileFilter CurrentFilter() => new FileFilter
        {
            Kind = (FileTypeBox.SelectedItem as Choice)?.Value as string,
            Folder = (LocationBox.SelectedItem as Choice)?.Value as string,
            Within = (ModifiedBox.SelectedItem as Choice)?.Value as TimeSpan?
        };

        private void OnAdvancedTextChanged(object sender, TextChangedEventArgs e) => RunAdvancedSearch();

        private void OnAdvancedFilterChanged(object sender, RoutedEventArgs e)
        {
            if (_fillingFilters) return;

            bool files = FilesFilter.IsChecked == true;
            FileTypeBox.IsEnabled = files;
            LocationBox.IsEnabled = files;
            ModifiedBox.IsEnabled = files;

            RunAdvancedSearch();
        }

        /// <summary>
        /// Programs and settings answer at once; the file index is asked once typing
        /// pauses, and folds its answer in when it arrives.
        /// </summary>
        private void RunAdvancedSearch()
        {
            if (!_searchView) return;

            string query = AdvancedBox.Text;
            _advancedFileTimer.Stop();
            _advancedToken++;
            CancelDiskSearch();
            _indexFiles = new List<StartItem>();
            _diskFiles = new List<StartItem>();

            if (string.IsNullOrWhiteSpace(query))
            {
                _advancedList.Clear();
                AdvancedNote.Text = Lang.T("SearchHint");
                return;
            }

            _programHits = AppsFilter.IsChecked == true
                ? App.Me.Catalog.Search(query, 20)
                : new List<StartItem>();

            var seen = new HashSet<string>(_programHits.Select(p => p.Name),
                                           StringComparer.CurrentCultureIgnoreCase);

            if (AppsFilter.IsChecked == true)
            {
                foreach (var extra in App.Me.Programs.Search(query, 40))
                {
                    if (_programHits.Count >= 30) break;
                    if (seen.Add(extra.Name)) _programHits.Add(extra);
                }
            }

            _settingHits = SettingsFilter.IsChecked == true
                ? App.Me.Settings.Search(query, 15).Where(entry => seen.Add(entry.Name)).ToList()
                : new List<StartItem>();

            bool files = FilesFilter.IsChecked == true;
            ComposeAdvanced();
            if (files) _advancedFileTimer.Start();
        }

        private void CancelDiskSearch()
        {
            _diskRunning = false;
            try { _diskCancel?.Cancel(); } catch { }
            _diskCancel = null;
        }

        /// <summary>
        /// Two sources answer here. The Windows index is instant but only knows what
        /// it was told to index — essentially the user profile — so the disk is
        /// walked as well, which is what turns up a game in a Steam library or a
        /// program under Program Files.
        /// </summary>
        private void SearchFilesAdvanced()
        {
            string query = AdvancedBox.Text;
            if (!_searchView || string.IsNullOrWhiteSpace(query)) return;

            int token = _advancedToken;
            var filter = CurrentFilter();

            Task.Run(() => FileSearch.Query(query, 60, filter)).ContinueWith(task =>
            {
                var files = task.Status == TaskStatus.RanToCompletion
                    ? task.Result
                    : new List<StartItem>();

                Dispatcher.BeginInvoke(new Action(() =>
                {
                    if (token != _advancedToken || !_searchView) return;

                    // The small search shortens the folder to two segments because it
                    // has 135 pixels for it. Here the whole path fits.
                    foreach (var file in files)
                    {
                        try
                        {
                            string folder = Path.GetDirectoryName(file.ParsingName);
                            if (!string.IsNullOrEmpty(folder)) file.Subtext = folder;
                        }
                        catch { }
                    }

                    _indexFiles = files;
                    ComposeAdvanced();
                }));
            });

            var cancel = new CancellationTokenSource();
            _diskCancel = cancel;
            _diskRunning = true;

            void Partial(List<StartItem> sofar) => Dispatcher.BeginInvoke(new Action(() =>
            {
                if (token != _advancedToken || !_searchView) return;
                _diskFiles = sofar;
                ComposeAdvanced();
            }));

            Task.Run(() => DiskSearch.Find(query, filter, 150, TimeSpan.FromSeconds(10),
                                           cancel.Token, Partial))
                .ContinueWith(task =>
                {
                    var files = task.Status == TaskStatus.RanToCompletion
                        ? task.Result
                        : new List<StartItem>();

                    Dispatcher.BeginInvoke(new Action(() =>
                    {
                        if (token != _advancedToken || !_searchView) return;
                        _diskRunning = false;
                        _diskFiles = files;
                        ComposeAdvanced();
                    }));
                });
        }

        /// <summary>
        /// The same grouping the small search uses, only with more room: more
        /// programs, more settings, and a lot more files.
        /// </summary>
        private void ComposeAdvanced()
        {
            var list = new List<StartItem>();
            var settings = _settingHits;
            int hits = 0;

            // The index answers first and is ranked, so it leads; the walk fills in
            // what it never saw, and the same file must not appear twice.
            var files = new List<StartItem>();
            var paths = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var file in _indexFiles.Concat(_diskFiles))
            {
                if (string.IsNullOrEmpty(file.ParsingName)) continue;
                if (paths.Add(file.ParsingName)) files.Add(file);
            }

            if (_programHits.Count > 0)
            {
                list.Add(Header("BestMatch"));
                list.Add(_programHits[0]);
                hits++;

                if (_programHits.Count > 1)
                {
                    list.Add(Header("AppsGroup"));
                    var rest = _programHits.Skip(1).Take(20).ToList();
                    list.AddRange(rest);
                    hits += rest.Count;
                }
            }
            else if (settings.Count > 0)
            {
                list.Add(Header("BestMatch"));
                list.Add(settings[0]);
                hits++;
                settings = settings.Skip(1).ToList();
            }

            if (settings.Count > 0)
            {
                list.Add(Header("SettingsGroup"));
                var rest = settings.Take(12).ToList();
                list.AddRange(rest);
                hits += rest.Count;
            }

            if (FilesFilter.IsChecked == true && files.Count > 0)
            {
                list.Add(Header("FilesGroup"));
                var rest = files.Take(80).ToList();
                list.AddRange(rest);
                hits += rest.Count;
            }

            SyncResults(list);

            if (_diskRunning && FilesFilter.IsChecked == true)
                AdvancedNote.Text = hits > 0
                    ? Lang.F("Hits", hits) + " · " + Lang.T("Searching")
                    : Lang.T("Searching");
            else if (hits == 0) AdvancedNote.Text = Lang.T("NoResults");
            else AdvancedNote.Text = Lang.F("Hits", hits);
        }

        /// <summary>
        /// Brings the shown rows in line with what the search now has. While the
        /// walk keeps finding more, the head of the list stays the same, so only
        /// the new tail is added and whatever the user was looking at stays where
        /// it was. Only a genuinely different result set starts over at the top.
        /// </summary>
        private void SyncResults(List<StartItem> wanted)
        {
            bool keeps = wanted.Count >= _advancedList.Count;
            if (keeps)
            {
                for (int i = 0; i < _advancedList.Count; i++)
                {
                    if (Same(_advancedList[i], wanted[i])) continue;
                    keeps = false;
                    break;
                }
            }

            if (!keeps)
            {
                _advancedList.Clear();
                AdvancedScroll?.ScrollToTop();
            }

            for (int i = _advancedList.Count; i < wanted.Count; i++)
                _advancedList.Add(wanted[i]);
        }

        /// <summary>Same row for our purposes — group captions are rebuilt each time.</summary>
        private static bool Same(StartItem a, StartItem b) =>
            ReferenceEquals(a, b) ||
            (a != null && b != null && a.Command == b.Command && a.Name == b.Name &&
             a.ParsingName == b.ParsingName);

        /// <summary>
        /// The wheel moves three whole rows, the way the list boxes of the era did.
        /// The default is three text lines, which leaves a 34 pixel row cut in half
        /// and makes long lists feel like they are sliding about.
        /// </summary>
        private void OnResultsWheel(object sender, MouseWheelEventArgs e)
        {
            if (sender is not ScrollViewer scroll || e.Delta == 0) return;

            const double row = 38;   // 34 high plus its 4 pixel gap
            scroll.ScrollToVerticalOffset(
                scroll.VerticalOffset - Math.Sign(e.Delta) * row * 3);
            e.Handled = true;
        }

        private void OnAdvancedKeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter)
            {
                var first = _advancedList
                    .FirstOrDefault(item => item.Command != Launcher.GroupHeader);
                if (first != null)
                {
                    HideMenu();
                    Launcher.Launch(first);
                }
                e.Handled = true;
            }
            else if (e.Key == Key.Down)
            {
                AdvancedResults.MoveFocus(new TraversalRequest(FocusNavigationDirection.Next));
                e.Handled = true;
            }
        }

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

        /// <summary>
        /// Standby is one unambiguous thing, so it acts straight away rather than
        /// opening the three-orb dialog for a single choice.
        /// </summary>
        private void OnSleepClick(object sender, RoutedEventArgs e)
        {
            HideMenu();
            Launcher.Power("standby");
        }

        private void ShowPowerDialog((string Key, string Command)[] choices)
        {
            HideMenu();
            var dialog = new PowerDialog(choices);
            dialog.ShowDialog();
        }
    }
}
