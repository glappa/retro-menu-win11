using System;
using System.Diagnostics;
using System.IO;
using System.Threading;

namespace RetroMenu.Services
{
    /// <summary>
    /// The line between the settings program and the running menu.
    ///
    /// Both are the same executable under two names, so there is no server and no
    /// protocol to speak of: the settings program writes the settings file and
    /// then sets a named event, and the menu reads the file again when it sees it.
    /// A menu that is not running misses nothing — it reads the file at startup
    /// anyway.
    /// </summary>
    public static class SettingsBridge
    {
        /// <summary>The menu waits on this; the settings program sets it.</summary>
        private const string ReloadEventName = @"Local\RetroMenuWin11.Reload";

        /// <summary>The menu holds this for as long as it lives.</summary>
        private const string MenuMutexName = "RetroMenuWin11.SingleInstance";

        /// <summary>A second start of the settings program waves at the first.</summary>
        private const string FrontEventName = @"Local\RetroMenuWin11.SettingsToFront";

        public static EventWaitHandle CreateFrontSignal() =>
            new EventWaitHandle(false, EventResetMode.AutoReset, FrontEventName);

        /// <summary>Asks an already open settings window to come forward.</summary>
        public static void NotifyFront()
        {
            try
            {
                if (!EventWaitHandle.TryOpenExisting(FrontEventName, out var handle)) return;
                handle.Set();
                handle.Dispose();
            }
            catch { }
        }

        /// <summary>Created by the menu, so it can be woken from outside.</summary>
        public static EventWaitHandle CreateReloadSignal() =>
            new EventWaitHandle(false, EventResetMode.AutoReset, ReloadEventName);

        /// <summary>True while a menu process is up and listening.</summary>
        public static bool MenuRunning
        {
            get
            {
                try
                {
                    if (!Mutex.TryOpenExisting(MenuMutexName, out var mutex)) return false;
                    mutex.Dispose();
                    return true;
                }
                catch (UnauthorizedAccessException) { return true; }  // there, just not ours
                catch { return false; }
            }
        }

        /// <summary>Asks a running menu to read the settings file again.</summary>
        public static void NotifyMenu()
        {
            try
            {
                if (!EventWaitHandle.TryOpenExisting(ReloadEventName, out var handle)) return;
                handle.Set();
                handle.Dispose();
            }
            catch { /* nobody listening is a perfectly normal state */ }
        }

        /// <summary>
        /// The menu executable: the installed one if there is one, otherwise the
        /// one sitting next to us, which is how the portable copy is unpacked.
        /// </summary>
        public static string MenuExecutable
        {
            get
            {
                if (Installer.IsInstalled) return Installer.InstalledExecutable;

                try
                {
                    string folder = Path.GetDirectoryName(Environment.ProcessPath);
                    if (string.IsNullOrEmpty(folder)) return null;
                    string beside = Path.Combine(folder, "RetroMenu.exe");
                    return File.Exists(beside) ? beside : null;
                }
                catch { return null; }
            }
        }

        public static bool CanStartMenu => MenuExecutable != null;

        /// <summary>Starts the menu, so switching it back on takes effect at once.</summary>
        public static bool StartMenu()
        {
            string exe = MenuExecutable;
            if (exe == null) return false;

            try
            {
                Process.Start(new ProcessStartInfo(exe)
                {
                    UseShellExecute = false,
                    WorkingDirectory = Path.GetDirectoryName(exe)
                });
                return true;
            }
            catch { return false; }
        }

        /// <summary>
        /// Opens the settings program: the separate executable if it was installed,
        /// otherwise this same one under --settings, which is the portable case.
        /// </summary>
        public static bool OpenSettingsProgram()
        {
            string exe = Installer.SettingsExecutable;
            string arguments = "";

            if (!File.Exists(exe))
            {
                exe = Environment.ProcessPath;
                arguments = "--settings";
            }

            try
            {
                if (string.IsNullOrEmpty(exe)) return false;
                Process.Start(new ProcessStartInfo(exe, arguments)
                {
                    UseShellExecute = false,
                    WorkingDirectory = Path.GetDirectoryName(exe)
                });
                return true;
            }
            catch (Exception ex)
            {
                Log.Write("settings program failed to start: " + ex.Message);
                return false;
            }
        }
    }
}
