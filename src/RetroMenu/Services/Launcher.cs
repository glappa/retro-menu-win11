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

        private static void RunCommand(string command)
        {
            if (string.IsNullOrEmpty(command) || command == Separator) return;

            if (command.StartsWith("place:", StringComparison.Ordinal))
            {
                Shell("explorer.exe", command.Substring("place:".Length));
                return;
            }

            if (command.StartsWith("url:", StringComparison.Ordinal))
            {
                Shell(command.Substring("url:".Length), null);
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

        /// <summary>The right hand column of the classic menu.</summary>
        public static List<StartItem> BuildPlaces()
        {
            var places = new List<StartItem>();

            void Add(string key, string parsingName, string command) =>
                places.Add(new StartItem
                {
                    Name = Lang.T(key),
                    Kind = StartItemKind.Place,
                    ParsingName = parsingName,
                    Command = command
                });

            void Line() => places.Add(new StartItem
            {
                Name = "-",
                Kind = StartItemKind.Command,
                Command = Separator
            });

            // Shell names rather than file paths: Environment.GetFolderPath returns an
            // empty string for Pictures/Music on some profiles, and the virtual folders
            // have no path at all.
            Add("Documents", "shell:Personal", "place:shell:Personal");
            Add("Pictures", "shell:My Pictures", "place:shell:My Pictures");
            Add("Music", "shell:My Music", "place:shell:My Music");
            Add("Downloads", "shell:Downloads", "place:shell:Downloads");
            Line();
            // Windows 11 answers every icon API with a plain folder for these two,
            // so take the real ones straight out of the shell resource library.
            Add("Computer", "res:imageres.dll,109", "place:shell:MyComputerFolder");
            Add("Network", "res:imageres.dll,25", "place:shell:NetworkPlacesFolder");
            Line();
            Add("ControlPanel", "shell:ControlPanelFolder", "place:shell:ControlPanelFolder");
            Add("Settings", "shell:ControlPanelFolder", "url:ms-settings:");
            Line();
            Add("SearchPlace", SearchShellItem, "search");
            Add("Run", RunShellItem, "rundialog");

            return places;
        }

        // The shell still exposes "Search" and "Run..." as namespace items, which is
        // where their icons come from.
        private const string SearchShellItem = "shell:::{2559a1f0-21d7-11d4-bdaf-00c04f60b9f0}";
        private const string RunShellItem = "shell:::{2559a1f3-21d7-11d4-bdaf-00c04f60b9f0}";
    }
}
