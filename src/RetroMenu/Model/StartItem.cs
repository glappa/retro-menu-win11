using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Windows;
using System.Windows.Media.Imaging;
using RetroMenu.Interop;

namespace RetroMenu.Model
{
    public enum StartItemKind
    {
        Folder,
        Shortcut,
        StoreApp,
        Place,
        Command
    }

    /// <summary>
    /// One entry in the menu: a program, a folder of programs, a shell place such
    /// as "My Documents", or an internal command such as "Run...".
    /// </summary>
    public sealed class StartItem : INotifyPropertyChanged
    {
        private BitmapSource _smallIcon;
        private BitmapSource _largeIcon;
        private bool _smallRequested;
        private bool _largeRequested;

        public string Name { get; set; }

        /// <summary>
        /// The grey second line under the name. Windows XP used it for the two
        /// special slots at the top of the left column: "Internet / Firefox".
        /// </summary>
        public string Subtext { get; set; }

        /// <summary>Something the shell can parse: a file path or shell:AppsFolder\{aumid}.</summary>
        public string ParsingName { get; set; }

        /// <summary>Where the shortcut points, when we know it. Used for tooltips only.</summary>
        public string Target { get; set; }

        public StartItemKind Kind { get; set; } = StartItemKind.Shortcut;

        /// <summary>Set for <see cref="StartItemKind.Command"/> entries.</summary>
        public string Command { get; set; }

        /// <summary>XP printed the first two left entries and the top right group bold.</summary>
        public bool Bold { get; set; }

        /// <summary>
        /// Picks a special row template, for the two entries at the foot of the
        /// classic menu that carry a drawn icon rather than a shell one.
        /// </summary>
        public string TemplateKey { get; set; }

        /// <summary>
        /// Installed since the last time the menu looked. XP highlighted such entries
        /// in All Programs until they had been opened once.
        /// </summary>
        public bool IsNew { get; set; }

        /// <summary>
        /// A shell folder to fill a cascading submenu from, e.g. the Recent
        /// Documents folder behind "My Recent Documents".
        /// </summary>
        public string SubmenuSource { get; set; }

        public List<StartItem> Children { get; } = new List<StartItem>();

        public bool IsFolder => Kind == StartItemKind.Folder;

        /// <summary>Stable key for pin lists and launch counters.</summary>
        public string Id => ParsingName ?? Command ?? Name;

        public string Tooltip => Kind == StartItemKind.Shortcut && !string.IsNullOrEmpty(Target)
            ? Name + "\n" + Target
            : Name;

        public FontWeight TitleWeight => Bold ? FontWeights.Bold : FontWeights.Normal;

        public Visibility SubtextVisibility =>
            string.IsNullOrEmpty(Subtext) ? Visibility.Collapsed : Visibility.Visible;

        public Visibility ArrowVisibility =>
            string.IsNullOrEmpty(SubmenuSource) ? Visibility.Collapsed : Visibility.Visible;

        public BitmapSource SmallIcon
        {
            get
            {
                if (!_smallRequested)
                {
                    _smallRequested = true;
                    LoadAsync(16);
                }
                return _smallIcon;
            }
            private set { _smallIcon = value; OnPropertyChanged(nameof(SmallIcon)); }
        }

        public BitmapSource LargeIcon
        {
            get
            {
                if (!_largeRequested)
                {
                    _largeRequested = true;
                    LoadAsync(32);
                }
                return _largeIcon;
            }
            private set { _largeIcon = value; OnPropertyChanged(nameof(LargeIcon)); }
        }

        private void LoadAsync(int size)
        {
            string parsing = ParsingName;
            if (string.IsNullOrEmpty(parsing)) return;

            IconLoader.Enqueue(() =>
            {
                var image = ShellIcon.Get(parsing, size);
                if (image == null) return;

                var app = System.Windows.Application.Current;
                if (app == null) return;

                app.Dispatcher.BeginInvoke(new Action(() =>
                {
                    if (size <= 16) SmallIcon = image;
                    else LargeIcon = image;
                }));
            });
        }

        public event PropertyChangedEventHandler PropertyChanged;

        private void OnPropertyChanged(string name) =>
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));

        public override string ToString() => Name;
    }
}
