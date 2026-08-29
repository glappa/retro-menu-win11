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
            if (XpExplorerBridge.Available)
            {
                try
                {
                    Process.Start(new ProcessStartInfo
                    {
                        FileName = XpExplorerBridge.Path,
                        Arguments = "\"" + place + "\"",
                        UseShellExecute = true
                    });
                    return;
                }
                catch
                {
                    // Not there any more, or refused to start. The shell still works.
                }
            }

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
                    Shell("search-ms:", null);
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

        public static void Power(string command) => RunCommand(command);

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
        public static List<StartItem> BuildPlaces()
        {
            var places = new List<StartItem>();

            void Add(string key, string parsingName, string command,
                     bool bold = false, string submenu = null) =>
                places.Add(new StartItem
                {
                    Name = Lang.T(key),
                    Kind = StartItemKind.Place,
                    ParsingName = parsingName,
                    Command = command,
                    Bold = bold,
                    SubmenuSource = submenu
                });

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
            Add("ConnectTo", "res:imageres.dll,25", "place:shell:ConnectionsFolder",
                submenu: "shell:ConnectionsFolder");
            Add("PrintersAndFaxes", "shell:PrintersFolder", "place:shell:PrintersFolder");
            Line();
            Add("Help", "res:imageres.dll,104", "help");
            Add("SearchPlace", SearchShellItem, "search");
            Add("Run", RunShellItem, "rundialog");

            return places;
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
