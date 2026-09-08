using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using RetroMenu.Interop;
using RetroMenu.Model;

namespace RetroMenu.Services
{
    /// <summary>Everything that actually starts something.</summary>
    public static class Launcher
    {
        public const string Separator = "separator";

        /// <summary>Marks a group caption in the search results.</summary>
        public const string GroupHeader = "header";

        // The shell still exposes "Search" and "Run..." as namespace items, which is
        // where their icons come from.
        private const string SearchShellItem = "shell:::{2559a1f0-21d7-11d4-bdaf-00c04f60b9f0}";
        private const string RunShellItem = "shell:::{2559a1f3-21d7-11d4-bdaf-00c04f60b9f0}";

        /// <summary>
        /// The magnifier searches in the menu itself. The window takes this one
        /// before it gets here, because only it can put the cursor in its own
        /// search box; the Explorer search stays behind it as "search".
        /// </summary>
        public const string SearchInMenu = "searchbox";

        /// <summary>Opens the Network Connections folder.</summary>
        public const string Connections = "connections";

        /// <summary>Remote Desktop, Windows' own client.</summary>
        public const string RemoteDesktop = "rdp";

        /// <summary>The OpenSSH client Windows ships with, after asking where to.</summary>
        public const string SshConnect = "ssh";

        /// <summary>Its file half, for fetching and putting files.</summary>
        public const string SftpConnect = "sftp";

        /// <summary>The Telnet client, which is an optional Windows feature.</summary>
        public const string TelnetConnect = "telnet";

        /// <summary>An FTP server, opened as a folder in the Explorer.</summary>
        public const string FtpConnect = "ftp";

        /// <summary>A share on another machine, likewise as a folder.</summary>
        public const string NetworkFolder = "netfolder";

        /// <summary>One WSL system, named by the rest of the string.</summary>
        public const string WslPrefix = "wsl:";

        /// <summary>The Map Network Drive dialog of the shell.</summary>
        public const string MapDrive = "mapdrive";

        /// <summary>Where Windows keeps its Remote Desktop client.</summary>
        public static string RemoteDesktopPath => InSystem32("mstsc.exe");

        /// <summary>
        /// The OpenSSH client. It has been part of Windows since 1809, but it is an
        /// optional feature and can be taken back out, so the menu checks.
        /// </summary>
        public static string SshPath => InSystem32(@"OpenSSH\ssh.exe");

        public static string SftpPath => InSystem32(@"OpenSSH\sftp.exe");

        /// <summary>
        /// The Telnet client left Windows' default install after XP and has been an
        /// optional feature since; on most machines this is null.
        /// </summary>
        public static string TelnetPath => InSystem32("telnet.exe");

        public static string WslPath => InSystem32("wsl.exe");

        private static string InSystem32(string relative)
        {
            try
            {
                string path = System.IO.Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.System), relative);
                return File.Exists(path) ? path : null;
            }
            catch { return null; }
        }

        /// <summary>Stands for "fill this submenu with the network connections".</summary>
        public const string ConnectionsSubmenu = "connections";

        /// <summary>One connection, named by the rest of the string.</summary>
        public const string ConnectionPrefix = "connection:";

        /// <summary>Stands for "fill this submenu with the WSL systems".</summary>
        public const string WslSubmenu = "wsl";

        public static void Launch(StartItem item)
        {
            if (item == null) return;

            try
            {
                switch (item.Kind)
                {
                    case StartItemKind.StoreApp:
                        Shell("explorer.exe", item.ParsingName);
                        break;

                    case StartItemKind.Place:
                    case StartItemKind.Command:
                        RunCommand(item.Command);
                        break;

                    default:
                        Shell(item.ParsingName, null);
                        break;
                }

                AppSettings.Instance.RegisterLaunch(item.Id);
            }
            catch (Exception ex)
            {
                Report(item?.Name, ex);
            }
        }

        public static void LaunchAsAdmin(StartItem item)
        {
            if (item == null || string.IsNullOrEmpty(item.ParsingName)) return;
            try
            {
                Process.Start(new ProcessStartInfo
                {
                    FileName = item.ParsingName,
                    UseShellExecute = true,
                    Verb = "runas"
                });
                AppSettings.Instance.RegisterLaunch(item.Id);
            }
            catch (System.ComponentModel.Win32Exception)
            {
                // User dismissed the UAC prompt; nothing to report.
            }
            catch (Exception ex)
            {
                Report(item.Name, ex);
            }
        }

        public static void OpenFileLocation(StartItem item)
        {
            if (item == null || string.IsNullOrEmpty(item.ParsingName)) return;
            if (!File.Exists(item.ParsingName) && !Directory.Exists(item.ParsingName)) return;
            try { Process.Start("explorer.exe", "/select,\"" + item.ParsingName + "\""); }
            catch (Exception ex) { Report(item.Name, ex); }
        }

        /// <summary>
        /// Shows a folder. The companion file window gets first refusal, so the menu
        /// and what it opens look like they belong together; it hands anything it
        /// cannot show - the Control Panel, say - straight back to the shell.
        /// </summary>
        private static void OpenPlace(string place)
        {
            // The companion window only gets folders it can actually show, and only
            // while it is installed in one piece; everything else — and everything
            // that goes wrong — lands in the Windows Explorer.
            if (XpExplorerBridge.Available && XpExplorerBridge.TryOpen(place)) return;

            Shell("explorer.exe", place);
        }

        private static void RunCommand(string command)
        {
            if (string.IsNullOrEmpty(command) || command == Separator) return;

            if (command.StartsWith("place:", StringComparison.Ordinal))
            {
                OpenPlace(command.Substring("place:".Length));
                return;
            }

            if (command.StartsWith("url:", StringComparison.Ordinal))
            {
                Shell(command.Substring("url:".Length), null);
                return;
            }

            if (command.StartsWith(ConnectionPrefix, StringComparison.Ordinal))
            {
                NetworkConnections.Open(command.Substring(ConnectionPrefix.Length));
                return;
            }

            if (command.StartsWith(WslPrefix, StringComparison.Ordinal))
            {
                StartWsl(command.Substring(WslPrefix.Length));
                return;
            }

            if (command.StartsWith("exec:", StringComparison.Ordinal))
            {
                string rest = command.Substring("exec:".Length);
                int space = rest.IndexOf(' ');
                if (space < 0) Shell(rest, null);
                else Shell(rest.Substring(0, space), rest.Substring(space + 1));
                return;
            }

            switch (command)
            {
                case "rundialog":
                    Process.Start("rundll32.exe", "shell32.dll,#61");
                    break;
                case "search":
                    // Only reached when the menu has no search box of its own,
                    // i.e. in the classic single-column layouts.
                    Shell("search-ms:", null);
                    break;
                case Connections:
                    NetworkConnections.OpenFolder();
                    break;
                case RemoteDesktop:
                    Shell("mstsc.exe", null);
                    break;
                case MapDrive:
                    // The shell's own "Map network drive" dialog, the one Explorer
                    // shows; it has lived at this entry point since Windows 95.
                    Process.Start(Silent("rundll32.exe",
                        "shell32.dll,SHHelpShortcuts_RunDLL Connect"));
                    break;
                case SshConnect:
                    StartConnection(Views.ConnectKind.Ssh);
                    break;
                case SftpConnect:
                    StartConnection(Views.ConnectKind.Sftp);
                    break;
                case TelnetConnect:
                    StartConnection(Views.ConnectKind.Telnet);
                    break;
                case FtpConnect:
                    StartConnection(Views.ConnectKind.Ftp);
                    break;
                case NetworkFolder:
                    StartConnection(Views.ConnectKind.Folder);
                    break;
                case "help":
                    Shell("https://support.microsoft.com/windows", null);
                    break;
                case "useraccounts":
                    // Clicking the picture in XP's header opened User Accounts.
                    Process.Start(Silent("control.exe", "/name Microsoft.UserAccounts"));
                    break;
                case "lock":
                    NativeMethods.LockWorkStation();
                    break;
                case "logoff":
                    Process.Start("shutdown.exe", "/l");
                    break;
                case "shutdown":
                    Process.Start(Silent("shutdown.exe", "/s /t 0"));
                    break;
                case "restart":
                    Process.Start(Silent("shutdown.exe", "/r /t 0"));
                    break;
                case "standby":
                    NativeMethods.SetSuspendState(false, false, false);
                    break;
                case "hibernate":
                    NativeMethods.SetSuspendState(true, false, false);
                    break;
            }
        }

        /// <summary>
        /// Asks where to and hands the rest to the client Windows brought along.
        /// The two console clients run through cmd /k so the window stays put when
        /// the connection is refused — otherwise the console would close on the
        /// error message before it could be read. The two folder-shaped ones go to
        /// the Explorer, which is where a remote folder belongs.
        /// </summary>
        private static void StartConnection(Views.ConnectKind kind)
        {
            var request = Views.ConnectDialog.Ask(null, kind);
            if (request == null) return;

            string where = Safe(request.Target);
            if (where.Length == 0) return;

            string port = Safe(request.Port);
            string key = !string.IsNullOrWhiteSpace(request.KeyFile) && File.Exists(request.KeyFile)
                ? "-i \"" + request.KeyFile + "\" "
                : string.Empty;

            switch (kind)
            {
                case Views.ConnectKind.Ssh:
                    if (SshPath == null) return;
                    RunInConsole("ssh " + key + (port.Length > 0 ? "-p " + port + " " : "") + where);
                    break;

                case Views.ConnectKind.Sftp:
                    // sftp spells the same switch with a capital P.
                    if (SftpPath == null) return;
                    RunInConsole("sftp " + key + (port.Length > 0 ? "-P " + port + " " : "") + where);
                    break;

                case Views.ConnectKind.Telnet:
                    if (TelnetPath == null) return;
                    RunInConsole("telnet " + where + (port.Length > 0 ? " " + port : ""));
                    break;

                case Views.ConnectKind.Ftp:
                    // The Explorer shows an FTP server as a folder, which is what
                    // this menu can do with it; a browser only offers to download
                    // the address these days.
                    OpenInExplorer("ftp://" + where + (port.Length > 0 ? ":" + port : "") + "/");
                    break;

                case Views.ConnectKind.Folder:
                    string share = where.Replace('/', '\\').TrimStart('\\');
                    if (share.Length == 0) return;
                    OpenInExplorer("\\\\" + share);
                    break;
            }
        }

        /// <summary>A console window that stays open after the client has finished.</summary>
        private static void RunInConsole(string commandLine)
        {
            Process.Start(new ProcessStartInfo("cmd.exe", "/k " + commandLine)
            {
                UseShellExecute = true
            });
        }

        /// <summary>
        /// Explorer by name rather than by association: ftp:// and \\server belong
        /// to the file window, and letting the shell pick would hand the first one
        /// to whatever browser is installed.
        /// </summary>
        private static void OpenInExplorer(string place)
        {
            Process.Start(new ProcessStartInfo("explorer.exe", "\"" + place + "\"")
            {
                UseShellExecute = true
            });
        }

        /// <summary>
        /// Opens a shell in one of the WSL systems.
        ///
        /// Two things had to be found out the hard way here. wsl.exe started
        /// straight from a window has no console to talk through and is gone again
        /// before anything appears, so it goes through cmd like the other console
        /// clients. And it does not take the name of a system in quotes — with them
        /// it answers WSL_E_DISTRO_NOT_FOUND — so the name is passed bare.
        /// </summary>
        private static void StartWsl(string distribution)
        {
            if (WslPath == null || string.IsNullOrWhiteSpace(distribution)) return;

            string name = Safe(distribution).Trim();
            if (name.Length == 0) return;

            RunInConsole("wsl -d " + name);
        }

        /// <summary>
        /// What the user typed lands on a command line, so the characters that could
        /// chain a second command onto it are dropped. The key file is not filtered:
        /// it comes from a file picker, and quoting it is enough.
        /// </summary>
        private static string Safe(string text)
        {
            if (string.IsNullOrWhiteSpace(text)) return string.Empty;

            var clean = new System.Text.StringBuilder(text.Length);
            foreach (char c in text.Trim())
                if ("\"'&|<>^%()".IndexOf(c) < 0) clean.Append(c);

            return clean.ToString().Trim();
        }

        public static void Power(string command) => RunCommand(command);

        /// <summary>Runs one of the named commands from outside the item list.</summary>
        public static void Run(string command) => RunCommand(command);

        private static ProcessStartInfo Silent(string file, string args) => new ProcessStartInfo
        {
            FileName = file,
            Arguments = args,
            UseShellExecute = false,
            CreateNoWindow = true
        };

        private static void Shell(string file, string args)
        {
            var psi = new ProcessStartInfo
            {
                FileName = file,
                UseShellExecute = true
            };
            if (!string.IsNullOrEmpty(args)) psi.Arguments = args;
            Process.Start(psi);
        }

        private static void Report(string name, Exception ex)
        {
            System.Windows.MessageBox.Show(
                (name ?? "?") + "\n\n" + ex.Message,
                "Retro Menu",
                System.Windows.MessageBoxButton.OK,
                System.Windows.MessageBoxImage.Warning);
        }

        /// <summary>
        /// The right hand column, in the order Windows XP had it. The first five
        /// entries are the bold group; "My Recent Documents" and "Connect To" carry
        /// a submenu arrow.
        /// </summary>
        /// <summary>
        /// The right hand column, in the order XP had it. The settings program
        /// shows this list so single entries can be switched off, which is what
        /// XP's "Customize Start Menu" did.
        /// </summary>
        public static readonly string[] PlaceKeys =
        {
            "Documents", "RecentDocuments", "Pictures", "Music", "Computer",
            "ControlPanel", "SetProgramAccess", "Connections", "PrintersAndFaxes",
            "Help", "SearchPlace", "Run"
        };

        public static List<StartItem> BuildPlaces()
        {
            var places = new List<StartItem>();
            var hidden = new HashSet<string>(
                AppSettings.Instance.HiddenPlaces ?? new List<string>(),
                StringComparer.OrdinalIgnoreCase);

            void Add(string key, string parsingName, string command,
                     bool bold = false, string submenu = null)
            {
                if (hidden.Contains(key)) return;
                places.Add(new StartItem
                {
                    Name = Lang.T(key),
                    Kind = StartItemKind.Place,
                    ParsingName = parsingName,
                    Command = command,
                    Bold = bold,
                    SubmenuSource = submenu
                });
            }

            void Line() => places.Add(new StartItem
            {
                Name = "-",
                Kind = StartItemKind.Command,
                Command = Separator
            });

            Add("Documents", "shell:Personal", "place:shell:Personal", bold: true);
            Add("RecentDocuments", "shell:Recent", "place:shell:Recent", bold: true, submenu: "shell:Recent");
            Add("Pictures", "shell:My Pictures", "place:shell:My Pictures", bold: true);
            Add("Music", "shell:My Music", "place:shell:My Music", bold: true);
            // Windows 11 answers every icon API with a plain folder for My Computer
            // and Network, so take the real ones out of the shell resource library.
            Add("Computer", "res:imageres.dll,109", "place:shell:MyComputerFolder", bold: true);
            Line();
            Add("ControlPanel", "shell:ControlPanelFolder", "place:shell:ControlPanelFolder");
            Add("SetProgramAccess", "res:imageres.dll,27", "exec:computerdefaults.exe");
            Add("Connections", "res:imageres.dll,25", Connections, submenu: ConnectionsSubmenu);
            Add("PrintersAndFaxes", "shell:PrintersFolder", "place:shell:PrintersFolder");
            Line();
            Add("Help", "res:imageres.dll,104", "help");
            Add("SearchPlace", SearchShellItem, SearchInMenu);
            Add("Run", RunShellItem, "rundialog");

            TrimSeparators(places);
            return places;
        }

        /// <summary>
        /// Switching a whole group off would otherwise leave its dividing lines
        /// standing: two in a row, or one against the top or bottom edge.
        /// </summary>
        private static void TrimSeparators(List<StartItem> rows)
        {
            bool IsLine(StartItem row) => row.Command == Separator;

            for (int i = rows.Count - 1; i >= 0; i--)
            {
                if (!IsLine(rows[i])) continue;
                if (i == 0 || i == rows.Count - 1 || IsLine(rows[i - 1])) rows.RemoveAt(i);
            }
        }

        /// <summary>
        /// The single column Windows 95 through 2000 showed, in their order. The
        /// entries with an arrow open a cascading submenu; "Programme" is fed from
        /// the program catalogue, the others from a shell folder.
        /// </summary>
        public static List<StartItem> BuildClassicRows()
        {
            var rows = new List<StartItem>();

            void Add(string key, string parsingName, string command,
                     string submenu = null, string templateKey = null) =>
                rows.Add(new StartItem
                {
                    Name = Lang.T(key),
                    Kind = StartItemKind.Place,
                    ParsingName = parsingName,
                    Command = command,
                    SubmenuSource = submenu,
                    TemplateKey = templateKey
                });

            void Line() => rows.Add(new StartItem
            {
                Name = "-",
                Kind = StartItemKind.Command,
                Command = Separator
            });

            Add("WindowsUpdate", "res:imageres.dll,106", "url:ms-settings:windowsupdate");
            Line();
            Add("Programs", "res:imageres.dll,18", null, submenu: CatalogSubmenu);
            Add("Favorites", "shell:Favorites", "place:shell:Favorites", submenu: "shell:Favorites");
            Add("RecentDocuments", "shell:Recent", "place:shell:Recent", submenu: "shell:Recent");
            Add("SettingsGroup", "shell:ControlPanelFolder", "place:shell:ControlPanelFolder",
                submenu: "shell:ControlPanelFolder");
            Add("SearchPlace", SearchShellItem, "search");
            Add("Help", "res:imageres.dll,104", "help");
            Add("Run", RunShellItem, "rundialog");
            Line();
            Add("Standby", null, "sleepmenu", templateKey: "sleep");
            Add("LogOffClassic", null, "logoffmenu", templateKey: "logoff");
            Add("ShutDownClassic", null, "powermenu", templateKey: "shutdown");

            return rows;
        }

        /// <summary>Stands for "fill this submenu from the program catalogue".</summary>
        public const string CatalogSubmenu = "catalog";

        /// <summary>Prefix for "fill this submenu from the favourites folder named ...".</summary>
        public const string FavouriteFolderPrefix = "favourites:";

        /// <summary>
        /// The two slots XP kept at the very top of the left column, filled from the
        /// current default browser and mail client.
        /// </summary>
        public static List<StartItem> BuildDefaultAppSlots()
        {
            if (Demo.IsActive) return Demo.DefaultAppSlots();

            var slots = new List<StartItem>();

            var browser = DefaultApps.Browser();
            if (browser.IsUsable)
            {
                slots.Add(new StartItem
                {
                    Name = Lang.T("Internet"),
                    Subtext = browser.FriendlyName,
                    ParsingName = browser.ExecutablePath,
                    Target = browser.ExecutablePath,
                    Kind = StartItemKind.Shortcut,
                    Bold = true
                });
            }

            var mail = DefaultApps.Mail();
            if (mail.IsUsable &&
                !string.Equals(mail.ExecutablePath, browser.ExecutablePath, StringComparison.OrdinalIgnoreCase))
            {
                slots.Add(new StartItem
                {
                    Name = Lang.T("Email"),
                    Subtext = mail.FriendlyName,
                    ParsingName = mail.ExecutablePath,
                    Target = mail.ExecutablePath,
                    Kind = StartItemKind.Shortcut,
                    Bold = true
                });
            }

            return slots;
        }
    }
}
