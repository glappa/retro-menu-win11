using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Threading.Tasks;
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
    /// as "Documents", or an internal command such as "Run...".
    /// </summary>
    public sealed class StartItem : INotifyPropertyChanged
    {
        private BitmapSource _smallIcon;
        private BitmapSource _largeIcon;
        private bool _smallRequested;
        private bool _largeRequested;

        public string Name { get; set; }

        /// <summary>Something the shell can parse: a file path or shell:AppsFolder\{aumid}.</summary>
        public string ParsingName { get; set; }

        /// <summary>Where the shortcut points, when we know it. Used for tooltips only.</summary>
        public string Target { get; set; }

        public StartItemKind Kind { get; set; } = StartItemKind.Shortcut;

        /// <summary>Set for <see cref="StartItemKind.Command"/> entries.</summary>
        public string Command { get; set; }

        public List<StartItem> Children { get; } = new List<StartItem>();

        public bool IsFolder => Kind == StartItemKind.Folder;

        /// <summary>Stable key for pin lists and launch counters.</summary>
        public string Id => ParsingName ?? Command ?? Name;

        public string Tooltip => Kind == StartItemKind.Shortcut && !string.IsNullOrEmpty(Target)
            ? Name + "\n" + Target
            : Name;

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
