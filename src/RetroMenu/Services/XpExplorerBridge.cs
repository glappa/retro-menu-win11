using System;
using System.IO;

namespace RetroMenu.Services
{
    /// <summary>
    /// Finds windows-xp-explorer-win-11, the file window that goes with this menu.
    /// When it is there, folders open in it instead of in the Windows Explorer, so
    /// the whole desktop keeps one look.
    /// </summary>
    public static class XpExplorerBridge
    {
        private const string ExeName = "XpExplorer.exe";

        private static string _found;
        private static DateTime _lookedAt = DateTime.MinValue;

        /// <summary>True when the file window is installed and switched on.</summary>
        public static bool Available =>
            AppSettings.Instance.UseXpExplorer && Path != null;

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
        public static void Recheck() => _lookedAt = DateTime.MinValue;

        private static string Find()
        {
            string configured = AppSettings.Instance.XpExplorerPath;
            if (!string.IsNullOrWhiteSpace(configured) && File.Exists(configured))
                return configured;

            foreach (string candidate in Candidates())
            {
                try
                {
                    if (!string.IsNullOrEmpty(candidate) && File.Exists(candidate)) return candidate;
                }
                catch { }
            }

            return null;
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
