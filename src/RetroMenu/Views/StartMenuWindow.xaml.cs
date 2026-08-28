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
using RetroMenu.Interop;
using RetroMenu.Model;
using RetroMenu.Services;

namespace RetroMenu.Views
{
    public partial class StartMenuWindow : Window
    {
        private bool _popupOpen;
        private IntPtr _handle;

        public StartMenuWindow()
        {
            InitializeComponent();
            Deactivated += OnDeactivated;
            PreviewKeyDown += OnPreviewKeyDown;
        }

        public bool IsOpen => IsVisible;

        public void EnsureHandle()
        {
            _handle = new WindowInteropHelper(this).EnsureHandle();
        }

        // ---------------------------------------------------------------- show / hide

        public void ShowMenu()
        {
            Rebuild();
            Position();

            Show();
            Activate();
            if (_handle == IntPtr.Zero) EnsureHandle();
            NativeMethods.ForceForeground(_handle);

            if (SearchHost.Visibility == Visibility.Visible)
            {
                SearchBox.Clear();
                SearchBox.Focus();
            }
        }

        public void HideMenu()
        {
            if (!IsVisible) return;
            _popupOpen = false;
            SearchBox.Clear();
            ShowSearchResults(false);
            Hide();
        }

        private void OnDeactivated(object sender, EventArgs e)
        {
            // A cascading submenu takes the focus for a moment; that must not close us.
            if (_popupOpen) return;
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
            }
        }

        private void Position()
        {
            var info = TaskbarLocator.Locate();
            double scale = DpiScale();

            double barLeft = info.Bar.Left / scale;
            double barTop = info.Bar.Top / scale;
            double barRight = info.Bar.Right / scale;
            double barBottom = info.Bar.Bottom / scale;
            double monLeft = info.Monitor.Left / scale;
            double monTop = info.Monitor.Top / scale;
            double monRight = info.Monitor.Right / scale;
            double monBottom = info.Monitor.Bottom / scale;

            double left, top;
            switch (info.Edge)
            {
                case TaskbarEdge.Top:
                    left = barLeft;
                    top = barBottom;
                    break;
                case TaskbarEdge.Left:
                    left = barRight;
                    top = monBottom - Height;
                    break;
                case TaskbarEdge.Right:
                    left = barLeft - Width;
                    top = monBottom - Height;
                    break;
                default:
                    left = barLeft;
                    top = barTop - Height;
                    break;
            }

            Left = Math.Max(monLeft, Math.Min(left, monRight - Width));
            Top = Math.Max(monTop, Math.Min(top, monBottom - Height));
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

            var pinned = settings.Pinned.Select(Resolve).Where(i => i != null).ToList();

            var frequent = settings.MostUsed(settings.FrequentCount * 3)
                .Where(id => !settings.IsPinned(id))
                .Select(Resolve)
                .Where(i => i != null)
                .Take(Math.Max(0, settings.FrequentCount))
                .ToList();

            PinnedItems.ItemsSource = pinned;
            FrequentItems.ItemsSource = frequent;
            PinnedSeparator.Visibility = pinned.Count > 0 && frequent.Count > 0
                ? Visibility.Visible : Visibility.Collapsed;
        }

        private void SeedPinsOnce()
        {
            var settings = AppSettings.Instance;
            if (settings.Seeded) return;

            settings.Seeded = true;

            var quick = App.Me.RetroBar?.QuickLaunchOrder ?? new List<string>();
            foreach (var path in quick.Where(File.Exists).Take(6))
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
                }), System.Windows.Threading.DispatcherPriority.Input);
            };
            menu.IsOpen = true;
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
            if (!show)
            {
                SearchResults.ItemsSource = null;
                NoResults.Visibility = Visibility.Collapsed;
                SearchHint.Visibility = string.IsNullOrEmpty(SearchBox.Text)
                    ? Visibility.Visible : Visibility.Collapsed;
            }
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
