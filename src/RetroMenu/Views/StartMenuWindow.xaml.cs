using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Threading;
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

        public StartMenuWindow()
        {
            InitializeComponent();
            Deactivated += OnDeactivated;
            PreviewKeyDown += OnPreviewKeyDown;

            // XP opened the "My Recent Documents" and "Connect To" flyouts on hover.
            _hoverTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(320) };
            _hoverTimer.Tick += (_, __) => { _hoverTimer.Stop(); OpenPlaceSubmenu(); };
        }

        public bool IsOpen => IsVisible;

        public void EnsureHandle()
        {
            _handle = new WindowInteropHelper(this).EnsureHandle();
        }

        // ---------------------------------------------------------------- show / hide

        public void ShowMenu()
        {
            var bar = TaskbarLocator.Locate();
            double scale = DpiScale();

            ApplyMenuScale();
            MaxHeight = AvailableHeight(bar, scale);
            Rebuild();

            // Lay out at full size first, then place it: the menu grows with its
            // content, so its height is only known after a measure pass.
            Opacity = 0;
            Show();
            UpdateLayout();
            Position(bar, scale);
            Opacity = 1;

            Activate();
            if (_handle == IntPtr.Zero) EnsureHandle();
            NativeMethods.ForceForeground(_handle);
            AnnounceToTaskbar();

            if (SearchHost.Visibility == Visibility.Visible)
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
            _hoverItem = null;
            SearchBox.Clear();
            ShowSearchResults(false);
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
            _taskbarPresence.Dispose();
            base.OnClosed(e);
        }

        private void OnDeactivated(object sender, EventArgs e)
        {
            // A cascading submenu takes the focus for a moment; that must not close us.
            if (_popupOpen) return;
            HideMenu();
        }

        private void OnPreviewKeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key != Key.Escape) return;

            if (!string.IsNullOrEmpty(SearchBox.Text))
            {
                SearchBox.Clear();
                e.Handled = true;
                return;
            }
            HideMenu();
            e.Handled = true;
        }

        private void ApplyMenuScale()
        {
            double scale = AppSettings.Instance.MenuScale;
            if (double.IsNaN(scale) || double.IsInfinity(scale)) scale = 1.0;
            scale = Math.Max(0.75, Math.Min(3.0, scale));
            RootScale.ScaleX = scale;
            RootScale.ScaleY = scale;
        }

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
            AllProgramsLabel.Text = Lang.T("AllPrograms");
            LogOffLabel.Text = Lang.T("LogOff");
            ShutDownLabel.Text = Lang.T("ShutDown");
            SearchHint.Text = Lang.T("SearchHint");
            NoResults.Text = Lang.T("NoResults");
            UserName.Text = UserInfo.DisplayName();

            if (UserPicture.Source == null)
                UserPicture.Source = UserInfo.Picture();

            SearchHost.Visibility = AppSettings.Instance.ShowSearchBox
                ? Visibility.Visible : Visibility.Collapsed;

            ApplyFontSmoothing();
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
            if (item.Command == Launcher.Separator) return;

            HideMenu();
            Launcher.Launch(item);
        }

        private void OnItemRightClick(object sender, MouseButtonEventArgs e)
        {
            if ((e.OriginalSource as FrameworkElement)?.DataContext is not StartItem item) return;
            if (item.Kind == StartItemKind.Place || item.Kind == StartItemKind.Command) return;

            var settings = AppSettings.Instance;
            var menu = new ContextMenu();

            menu.Items.Add(Command(Lang.T("Open"), () => { HideMenu(); Launcher.Launch(item); }));

            if (settings.ShowRunAsAdmin && item.Kind == StartItemKind.Shortcut)
                menu.Items.Add(Command(Lang.T("RunAsAdmin"), () => { HideMenu(); Launcher.LaunchAsAdmin(item); }));

            menu.Items.Add(new Separator());

            if (settings.IsPinned(item.Id))
                menu.Items.Add(Command(Lang.T("Unpin"), () => { settings.Unpin(item.Id); BuildLeftColumn(); }));
            else
                menu.Items.Add(Command(Lang.T("Pin"), () => { settings.Pin(item.Id); BuildLeftColumn(); }));

            menu.Items.Add(Command(Lang.T("RemoveFromList"),
                () => { settings.ForgetLaunch(item.Id); BuildLeftColumn(); }));

            if (item.Kind == StartItemKind.Shortcut)
            {
                menu.Items.Add(new Separator());
                menu.Items.Add(Command(Lang.T("OpenFileLocation"),
                    () => { HideMenu(); Launcher.OpenFileLocation(item); }));
            }

            menu.PlacementTarget = e.OriginalSource as UIElement;
            OpenPopup(menu);
            e.Handled = true;
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
            var item = _hoverItem;
            var anchor = _hoverAnchor;
            if (item == null || anchor == null || string.IsNullOrEmpty(item.SubmenuSource)) return;
            if (!anchor.IsMouseOver) return;

            var menu = new ContextMenu
            {
                PlacementTarget = anchor,
                Placement = System.Windows.Controls.Primitives.PlacementMode.Right,
                HorizontalOffset = -4,
                VerticalOffset = -3
            };

            var entries = SubmenuEntries(item.SubmenuSource);
            if (entries.Count == 0)
                menu.Items.Add(new MenuItem { Header = Lang.T("Empty"), IsEnabled = false });
            else
                Populate(menu.Items, entries);

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

        private void OnAllProgramsClick(object sender, RoutedEventArgs e)
        {
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

        private void OnSearchTextChanged(object sender, TextChangedEventArgs e)
        {
            string query = SearchBox.Text;
            SearchHint.Visibility = string.IsNullOrEmpty(query) ? Visibility.Visible : Visibility.Collapsed;

            if (string.IsNullOrWhiteSpace(query))
            {
                ShowSearchResults(false);
                return;
            }

            var hits = App.Me.Catalog.Search(query, 40);
            SearchResults.ItemsSource = hits;
            NoResults.Visibility = hits.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
            ShowSearchResults(true);
        }

        private void ShowSearchResults(bool show)
        {
            SearchScroll.Visibility = show ? Visibility.Visible : Visibility.Collapsed;
            NormalScroll.Visibility = show ? Visibility.Collapsed : Visibility.Visible;
            if (show) return;

            SearchResults.ItemsSource = null;
            NoResults.Visibility = Visibility.Collapsed;
            SearchHint.Visibility = string.IsNullOrEmpty(SearchBox.Text)
                ? Visibility.Visible : Visibility.Collapsed;
        }

        private void OnSearchKeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter)
            {
                var first = (SearchResults.ItemsSource as IEnumerable<StartItem>)?.FirstOrDefault();
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
