using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Input;
using RetroMenu.Services;

namespace RetroMenu.Views
{
    /// <summary>
    /// The ways out of this machine that Windows can go on its own. Everything here
    /// has a client in the box: OpenSSH for the first two, the Telnet client for the
    /// third (an optional feature), and the Explorer for the last two.
    /// </summary>
    public enum ConnectKind
    {
        Ssh,
        Sftp,
        Telnet,
        Ftp,

        /// <summary>A share on another machine, opened as a folder.</summary>
        Folder
    }

    /// <summary>What the connection dialog came back with.</summary>
    public sealed class ConnectRequest
    {
        public ConnectKind Kind { get; set; }

        /// <summary>host, user@host, or \\server\share.</summary>
        public string Target { get; set; }

        /// <summary>Empty for the port the client would have used anyway.</summary>
        public string Port { get; set; }

        /// <summary>The private key to hand ssh with -i, or null to let it choose.</summary>
        public string KeyFile { get; set; }
    }

    /// <summary>
    /// Where to, on which port, and with which key. None of these clients brings a
    /// window of its own, so this is it — in the same Luna dress as the setup
    /// wizard, and with the keys lying in the user's .ssh folder offered rather
    /// than typed out.
    /// </summary>
    public partial class ConnectDialog : Window
    {
        /// <summary>Files in .ssh that are never a private key.</summary>
        private static readonly string[] NotKeys =
        {
            "known_hosts", "known_hosts.old", "config", "authorized_keys", "environment",
        };

        private readonly ConnectKind _kind;
        private ConnectRequest _result;

        public ConnectDialog(ConnectKind kind)
        {
            InitializeComponent();
            _kind = kind;

            // The menu entry ends in an ellipsis because it opens this window;
            // the window itself has arrived and says so without one.
            string title = Lang.T(TitleKey(kind)).TrimEnd('…', '.', ' ');
            Title = title;
            TitleText.Text = title;
            Subtitle.Text = kind == ConnectKind.Ssh ? Lang.T("SshSubtitle") : Lang.T("ConnectSubtitle");
            TargetLabel.Text = Lang.T(PromptKey(kind));
            PortLabel.Text = Lang.T("ConnectPort");
            KeyLabel.Text = Lang.T("SshKey");
            KeyHint.Text = Lang.T("SshKeyHint");
            ConnectButton.Content = Lang.T("SshConnectButton");
            CancelButton.Content = Lang.T("Cancel");
            BrowseButton.ToolTip = Lang.T("SshBrowse");

            // A key belongs to the two SSH-borne protocols, and a share is named by
            // its path rather than by a host and a port.
            bool keys = kind == ConnectKind.Ssh || kind == ConnectKind.Sftp;
            KeyRow.Visibility = keys ? Visibility.Visible : Visibility.Collapsed;
            PortRow.Visibility = kind == ConnectKind.Folder ? Visibility.Collapsed : Visibility.Visible;

            if (keys) FillKeys();
            Loaded += (_, __) => TargetBox.Focus();
        }

        private static string TitleKey(ConnectKind kind) => kind switch
        {
            ConnectKind.Sftp => "SftpConnection",
            ConnectKind.Telnet => "TelnetConnection",
            ConnectKind.Ftp => "FtpConnection",
            ConnectKind.Folder => "NetworkFolder",
            _ => "SshTitle"
        };

        private static string PromptKey(ConnectKind kind) => kind switch
        {
            ConnectKind.Ssh => "SshPrompt",
            ConnectKind.Sftp => "SshPrompt",
            ConnectKind.Folder => "FolderPrompt",
            _ => "HostPrompt"
        };

        /// <summary>Shows the dialog; null when it was cancelled.</summary>
        public static ConnectRequest Ask(Window owner, ConnectKind kind)
        {
            var dialog = new ConnectDialog(kind);
            if (owner != null && owner.IsVisible)
            {
                dialog.Owner = owner;
                dialog.WindowStartupLocation = WindowStartupLocation.CenterOwner;
            }

            return dialog.ShowDialog() == true ? dialog._result : null;
        }

        /// <summary>
        /// The keys of the current user. A private key sits next to its .pub, which
        /// is the surest sign in a folder that also holds known_hosts and config.
        /// </summary>
        private void FillKeys()
        {
            var entries = new List<KeyChoice> { new KeyChoice(Lang.T("SshNoKey"), null) };

            try
            {
                string folder = Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".ssh");

                if (Directory.Exists(folder))
                {
                    foreach (string file in Directory.EnumerateFiles(folder).OrderBy(f => f))
                    {
                        string name = Path.GetFileName(file);
                        if (name.EndsWith(".pub", StringComparison.OrdinalIgnoreCase)) continue;
                        if (NotKeys.Contains(name, StringComparer.OrdinalIgnoreCase)) continue;

                        bool looksLikeKey =
                            File.Exists(file + ".pub") ||
                            name.StartsWith("id_", StringComparison.OrdinalIgnoreCase) ||
                            name.EndsWith(".pem", StringComparison.OrdinalIgnoreCase) ||
                            name.EndsWith(".key", StringComparison.OrdinalIgnoreCase);

                        if (looksLikeKey) entries.Add(new KeyChoice(name, file));
                    }
                }
            }
            catch { }

            KeyBox.ItemsSource = entries;
            KeyBox.SelectedIndex = 0;
        }

        private sealed class KeyChoice
        {
            public KeyChoice(string label, string path)
            {
                Label = label;
                Path = path;
            }

            public string Label { get; }
            public string Path { get; }
            public override string ToString() => Label;
        }

        private void OnBrowse(object sender, RoutedEventArgs e)
        {
            var picker = new Microsoft.Win32.OpenFileDialog
            {
                Title = Lang.T("SshKey"),
                CheckFileExists = true
            };

            try
            {
                string folder = Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".ssh");
                if (Directory.Exists(folder)) picker.InitialDirectory = folder;
            }
            catch { }

            if (picker.ShowDialog(this) != true) return;

            var chosen = new KeyChoice(Path.GetFileName(picker.FileName), picker.FileName);
            var entries = ((IEnumerable<KeyChoice>)KeyBox.ItemsSource).ToList();
            entries.Add(chosen);
            KeyBox.ItemsSource = entries;
            KeyBox.SelectedItem = chosen;
        }

        private void OnConnect(object sender, RoutedEventArgs e)
        {
            string target = (TargetBox.Text ?? string.Empty).Trim();
            if (target.Length == 0) { TargetBox.Focus(); return; }

            // Only digits reach a command line as a port; anything else is a typo
            // and is better ignored than passed on.
            string port = new string((PortBox.Text ?? string.Empty).Trim()
                .TakeWhile(char.IsDigit).ToArray());

            _result = new ConnectRequest
            {
                Kind = _kind,
                Target = target,
                Port = port,
                KeyFile = KeyRow.Visibility == Visibility.Visible
                    ? (KeyBox.SelectedItem as KeyChoice)?.Path
                    : null
            };
            DialogResult = true;
        }

        private void OnCancel(object sender, RoutedEventArgs e) => DialogResult = false;

        /// <summary>The title bar has no frame of its own to drag, so it drags itself.</summary>
        private void OnDragTitle(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
            if (e.ChangedButton == MouseButton.Left) DragMove();
        }

        private void OnKeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Escape) DialogResult = false;
        }
    }
}
