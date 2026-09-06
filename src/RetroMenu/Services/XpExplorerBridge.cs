using System;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using RetroMenu.Interop;

namespace RetroMenu.Services
{
    /// <summary>
    /// Finds windows-xp-explorer-win-11, the file window that goes with this menu.
    /// When it is there and can actually run, folders open in it instead of in the
    /// Windows Explorer, so the whole desktop keeps one look.
    ///
    /// "There" is not the same as "works": deleting an installation tends to leave
    /// the odd file behind, and half a .NET folder answers a double-click with a
    /// crash box rather than a window. So a copy has to pass a test before the menu
    /// hands anything to it, and one that dies on us is written off for good. In
    /// every one of those cases the folder opens in the Windows Explorer instead.
    /// </summary>
    public static class XpExplorerBridge
    {
        private const string ExeName = "XpExplorer.exe";

        /// <summary>SIGDN_FILESYSPATH: the shell only answers for real folders.</summary>
        private const uint SigdnFileSysPath = 0x80058000;

        private static string _found;
        private static DateTime _lookedAt = DateTime.MinValue;
        private static string _broken;

        /// <summary>True when the file window is installed, usable and switched on.</summary>
        public static bool Available =>
            AppSettings.Instance.UseXpExplorer && Path != null;

        /// <summary>
        /// True when a copy was found that cannot be started, or that gave up when
        /// it was. The settings window says so rather than leaving the user to
        /// wonder why the setting has no effect.
        /// </summary>
        public static bool IsBroken { get; private set; }

        /// <summary>
        /// Where the file window is, or null. The answer is kept for a minute so
        /// opening a folder does not go looking through half a dozen directories
        /// every time, while installing it still takes effect without a restart.
        /// </summary>
        public static string Path
        {
            get
            {
                if ((DateTime.UtcNow - _lookedAt).TotalSeconds < 60) return _found;
                _lookedAt = DateTime.UtcNow;
                _found = Find();
                return _found;
            }
        }

        /// <summary>Forgets the last answer, after a setting has been changed.</summary>
        public static void Recheck()
        {
            _lookedAt = DateTime.MinValue;
            _broken = null;
            IsBroken = false;
        }

        /// <summary>
        /// Opens a folder in the XP window. False means the menu should fall back
        /// to the Windows Explorer — because there is no usable copy, because the
        /// place is not a folder on disk at all, or because starting it failed.
        /// </summary>
        public static bool TryOpen(string place)
        {
            string exe = Path;
            if (exe == null || !IsRealFolder(place)) return false;

            try
            {
                var started = Process.Start(new ProcessStartInfo
                {
                    FileName = exe,
                    Arguments = "\"" + place + "\"",
                    UseShellExecute = true
                });

                if (started == null) return false;
                Watch(started, exe, place);
                return true;
            }
            catch
            {
                MarkBroken(exe);
                return false;
            }
        }

        /// <summary>
        /// Gives the new process a few seconds. One that ends with a failure code in
        /// that time never showed a window, so its copy is written off and the
        /// folder is opened in the Windows Explorer after all — the click is not
        /// lost. A crash box that waits for an OK keeps the process alive and slips
        /// through this, which is what the check before starting is for.
        /// </summary>
        private static void Watch(Process started, string exe, string place)
        {
            Task.Run(() =>
            {
                try
                {
                    if (!started.WaitForExit(4000)) return;
                    if (started.ExitCode == 0) return;

                    MarkBroken(exe);
                    Process.Start(new ProcessStartInfo("explorer.exe", place)
                    {
                        UseShellExecute = true
                    });
                }
                catch { }
                finally { started.Dispose(); }
            });
        }

        private static void MarkBroken(string exe)
        {
            _broken = exe;
            _lookedAt = DateTime.MinValue;
            IsBroken = true;
        }

        private static string Find()
        {
            bool sawBroken = false;

            string configured = AppSettings.Instance.XpExplorerPath;
            if (!string.IsNullOrWhiteSpace(configured) && File.Exists(configured))
            {
                if (Usable(configured)) { IsBroken = false; return configured; }
                sawBroken = true;
            }

            foreach (string candidate in Candidates())
            {
                try
                {
                    if (string.IsNullOrEmpty(candidate) || !File.Exists(candidate)) continue;
                    if (Usable(candidate)) { IsBroken = false; return candidate; }
                    sawBroken = true;
                }
                catch { }
            }

            IsBroken = sawBroken;
            return null;
        }

        /// <summary>
        /// Whether this copy stands a chance of starting. A .NET program launched
        /// through its small apphost needs its managed dll and the runtimeconfig
        /// next to it; a folder missing those is the remains of an installation,
        /// not an installation. A single file build has neither and is taken as is.
        /// </summary>
        private static bool Usable(string exe)
        {
            if (string.Equals(exe, _broken, StringComparison.OrdinalIgnoreCase)) return false;

            try
            {
                string managed = System.IO.Path.ChangeExtension(exe, ".dll");
                if (!File.Exists(managed)) return true;

                return File.Exists(System.IO.Path.ChangeExtension(exe, ".runtimeconfig.json"));
            }
            catch { return true; }
        }

        /// <summary>
        /// True only for places that exist as a path on disk. The Control Panel,
        /// Printers and the network connections are shell folders with no path
        /// behind them; a file window cannot show those, and the Windows Explorer
        /// is the right address for them whatever else is installed.
        /// </summary>
        private static bool IsRealFolder(string place)
        {
            if (string.IsNullOrWhiteSpace(place)) return false;

            object item = null;
            try
            {
                Guid iid = ShellGuids.IShellItem;
                if (NativeMethods.SHCreateItemFromParsingName(place, IntPtr.Zero, ref iid, out item) != 0
                    || item is not IShellItem shellItem)
                    return false;

                shellItem.GetDisplayName(SigdnFileSysPath, out string path);
                return !string.IsNullOrWhiteSpace(path) && Directory.Exists(path);
            }
            catch
            {
                return false;
            }
            finally
            {
                try { if (item != null && Marshal.IsComObject(item)) Marshal.ReleaseComObject(item); }
                catch { }
            }
        }

        private static System.Collections.Generic.IEnumerable<string> Candidates()
        {
            string here = AppContext.BaseDirectory;
            yield return System.IO.Path.Combine(here, ExeName);
            yield return System.IO.Path.Combine(here, "XpExplorer", ExeName);
            yield return System.IO.Path.Combine(here, "..", "XpExplorer", ExeName);

            foreach (var folder in new[]
                     {
                         Environment.SpecialFolder.LocalApplicationData,
                         Environment.SpecialFolder.ApplicationData,
                         Environment.SpecialFolder.ProgramFiles
                     })
            {
                string root = null;
                try { root = Environment.GetFolderPath(folder); } catch { }
                if (string.IsNullOrEmpty(root)) continue;

                yield return System.IO.Path.Combine(root, "Programs", "XpExplorer", ExeName);
                yield return System.IO.Path.Combine(root, "XpExplorer", ExeName);
                yield return System.IO.Path.Combine(root, "XpExplorerWin11", ExeName);
            }
        }
    }
}
