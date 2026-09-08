using System;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using Microsoft.Win32;

namespace RetroMenu.Services
{
    public sealed class InstallOptions
    {
        public bool StartMenuShortcut = true;
        public bool DesktopShortcut;
        public bool AutoStart = true;
        public bool InstallRetroBar;
    }

    /// <summary>
    /// Puts the program into the user's own program folder — no administrator, no
    /// system-wide changes, and everything it writes is listed in Uninstall so
    /// Windows shows it under installed apps.
    /// </summary>
    public static class Installer
    {
        private const string UninstallKey =
            @"Software\Microsoft\Windows\CurrentVersion\Uninstall\RetroMenuWin11";

        public static string InstallDirectory => Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "Programs", "RetroMenu");

        public static string InstalledExecutable => Path.Combine(InstallDirectory, "RetroMenu.exe");

        /// <summary>
        /// The settings program. It is the same executable under a second name:
        /// started as RetroMenuSettings.exe it opens the settings window instead
        /// of a menu, and it runs whether or not the menu itself is up.
        /// </summary>
        public static string SettingsExecutable =>
            Path.Combine(InstallDirectory, "RetroMenuSettings.exe");

        public static bool IsInstalled => File.Exists(InstalledExecutable);

        public static string CurrentExecutable => Environment.ProcessPath;

        public static bool RunningFromInstallDirectory =>
            string.Equals(Path.GetDirectoryName(CurrentExecutable ?? ""), InstallDirectory,
                StringComparison.OrdinalIgnoreCase);

        private static string StartMenuShortcutPath => Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.StartMenu),
            "Programs", "Retro Menu.lnk");

        private static string DesktopShortcutPath => Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory),
            "Retro Menu.lnk");

        private static string SettingsShortcutPath => Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.StartMenu),
            "Programs", Lang.T("SettingsShortcut") + ".lnk");

        public static void Install(InstallOptions options, Action<string> log)
        {
            log($"Ziel: {InstallDirectory}");
            Directory.CreateDirectory(InstallDirectory);

            // A running copy of ourselves would hold the file open.
            StopRunningCopy(log);

            string source = CurrentExecutable;
            if (!string.IsNullOrEmpty(source) &&
                !string.Equals(source, InstalledExecutable, StringComparison.OrdinalIgnoreCase))
            {
                File.Copy(source, InstalledExecutable, true);
                log("Programmdatei kopiert.");
            }

            LinkSettingsProgram(log);

            if (options.StartMenuShortcut)
            {
                CreateShortcut(StartMenuShortcutPath, InstalledExecutable, "Startmenü im Retro-Stil");
                CreateShortcut(SettingsShortcutPath, SettingsExecutable, "Retro Menu einstellen");
                log("Verknüpfungen im Startmenü angelegt.");
            }

            if (options.DesktopShortcut)
            {
                CreateShortcut(DesktopShortcutPath, InstalledExecutable, "Startmenü im Retro-Stil");
                log("Verknüpfung auf dem Desktop angelegt.");
            }

            SetAutoStart(options.AutoStart);
            log(options.AutoStart ? "Startet künftig mit Windows." : "Kein automatischer Start.");

            WriteUninstallEntry();
            log("In der Programmliste von Windows eingetragen.");
        }

        public static void Uninstall(Action<string> log)
        {
            StopRunningCopy(log);
            SetAutoStart(false);

            RemoveOurShortcuts(log);

            try { Registry.CurrentUser.DeleteSubKeyTree(UninstallKey, false); }
            catch { }

            log("Einstellungen bleiben erhalten in " + AppSettings.Folder);

            // The folder cannot delete itself while we are running out of it.
            ScheduleFolderRemoval(InstallDirectory);
        }

        [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true,
                   EntryPoint = "CreateHardLinkW")]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool CreateHardLink(string linkName, string existingFileName,
                                                  IntPtr securityAttributes);

        /// <summary>
        /// Puts the second name next to the first. A hard link is used rather than
        /// a copy because both names would otherwise carry their own 66 MB of .NET
        /// runtime for what is byte for byte the same program; the link costs a
        /// directory entry. Where hard links are not to be had — a FAT stick, a
        /// stray sharing lock — a plain copy does the same job.
        /// </summary>
        private static void LinkSettingsProgram(Action<string> log)
        {
            try
            {
                // Its own window would hold the file open.
                foreach (var running in Process.GetProcessesByName("RetroMenuSettings"))
                {
                    try
                    {
                        running.CloseMainWindow();
                        running.WaitForExit(2000);
                        if (!running.HasExited) running.Kill();
                    }
                    catch { }
                }

                if (File.Exists(SettingsExecutable)) File.Delete(SettingsExecutable);

                if (CreateHardLink(SettingsExecutable, InstalledExecutable, IntPtr.Zero))
                {
                    log("Einstellungsprogramm angelegt.");
                    return;
                }

                File.Copy(InstalledExecutable, SettingsExecutable, true);
                log("Einstellungsprogramm kopiert.");
            }
            catch (Exception ex)
            {
                log("Einstellungsprogramm konnte nicht angelegt werden: " + ex.Message);
            }
        }

        /// <summary>
        /// Takes back every shortcut that points into our own folder, wherever it
        /// was put and whatever it is called. The names are localised at install
        /// time, and the display language may well have changed since, so looking
        /// at what a shortcut points at is the only reliable way to recognise it.
        /// </summary>
        private static void RemoveOurShortcuts(Action<string> log)
        {
            string[] folders =
            {
                Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.StartMenu), "Programs"),
                Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory)
            };

            var type = Type.GetTypeFromProgID("WScript.Shell");
            dynamic shell = type == null ? null : Activator.CreateInstance(type);

            foreach (var folder in folders)
            {
                if (!Directory.Exists(folder)) continue;

                foreach (var file in Directory.EnumerateFiles(folder, "*.lnk"))
                {
                    try
                    {
                        string target = null;
                        if (shell != null) target = (string)shell.CreateShortcut(file).TargetPath;

                        if (string.IsNullOrEmpty(target)) continue;
                        if (!string.Equals(Path.GetDirectoryName(target), InstallDirectory,
                                           StringComparison.OrdinalIgnoreCase)) continue;

                        File.Delete(file);
                        log("Verknüpfung entfernt: " + Path.GetFileNameWithoutExtension(file));
                    }
                    catch { }
                }
            }
        }

        private static void StopRunningCopy(Action<string> log)
        {
            try
            {
                foreach (var process in Process.GetProcessesByName("RetroMenu"))
                {
                    if (process.Id == Environment.ProcessId) continue;
                    Process.Start(new ProcessStartInfo(InstalledExecutable, "--quit")
                    {
                        UseShellExecute = false,
                        CreateNoWindow = true
                    })?.WaitForExit(4000);
                    process.WaitForExit(4000);
                    if (!process.HasExited) process.Kill();
                    log("Laufende Ausführung beendet.");
                    break;
                }
            }
            catch { }
        }

        private static void SetAutoStart(bool on)
        {
            try
            {
                using var key = Registry.CurrentUser.CreateSubKey(
                    @"Software\Microsoft\Windows\CurrentVersion\Run");
                if (key == null) return;
                if (on) key.SetValue("RetroMenu", "\"" + InstalledExecutable + "\"");
                else key.DeleteValue("RetroMenu", false);
            }
            catch { }
        }

        private static void WriteUninstallEntry()
        {
            try
            {
                using var key = Registry.CurrentUser.CreateSubKey(UninstallKey);
                if (key == null) return;

                string version = System.Reflection.Assembly.GetEntryAssembly()
                    ?.GetName().Version?.ToString(3) ?? "0.1.0";

                key.SetValue("DisplayName", "Retro Menu");
                key.SetValue("DisplayVersion", version);
                key.SetValue("Publisher", "glappa");
                key.SetValue("DisplayIcon", InstalledExecutable);
                key.SetValue("InstallLocation", InstallDirectory);
                key.SetValue("UninstallString", "\"" + InstalledExecutable + "\" --uninstall");
                key.SetValue("URLInfoAbout", "https://github.com/glappa/retro-menu-win11");
                key.SetValue("NoModify", 1, RegistryValueKind.DWord);
                key.SetValue("NoRepair", 1, RegistryValueKind.DWord);

                try
                {
                    var info = new FileInfo(InstalledExecutable);
                    if (info.Exists)
                        key.SetValue("EstimatedSize", (int)(info.Length / 1024), RegistryValueKind.DWord);
                }
                catch { }
            }
            catch { }
        }

        /// <summary>Uses the scripting host that ships with Windows rather than COM interop by hand.</summary>
        public static void CreateShortcut(string linkPath, string target, string description)
        {
            try
            {
                Directory.CreateDirectory(Path.GetDirectoryName(linkPath));

                var type = Type.GetTypeFromProgID("WScript.Shell");
                if (type == null) return;

                dynamic shell = Activator.CreateInstance(type);
                dynamic link = shell.CreateShortcut(linkPath);
                link.TargetPath = target;
                link.WorkingDirectory = Path.GetDirectoryName(target);
                link.Description = description;
                link.IconLocation = target + ",0";
                link.Save();
            }
            catch { }
        }

        private static void ScheduleFolderRemoval(string folder)
        {
            try
            {
                Process.Start(new ProcessStartInfo("cmd.exe",
                    $"/c timeout /t 3 /nobreak >nul & rd /s /q \"{folder}\"")
                {
                    UseShellExecute = false,
                    CreateNoWindow = true
                });
            }
            catch { }
        }
    }
}
