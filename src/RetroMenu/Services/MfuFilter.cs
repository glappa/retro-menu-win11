using System;
using System.Collections.Generic;
using System.IO;
using Microsoft.Win32;

namespace RetroMenu.Services
{
    /// <summary>
    /// Windows XP never let just anything into the "most frequently used" list on
    /// the left. Installers, uninstallers, help files and a set of system tools were
    /// kept out, and any program could opt out for itself with a NoStartPage value in
    /// the registry. This rebuilds that behaviour from the documented pieces.
    /// </summary>
    public static class MfuFilter
    {
        /// <summary>
        /// Names the classic shell kept out of the list. Matched against the file
        /// name without extension, case insensitively.
        /// </summary>
        private static readonly HashSet<string> Excluded = new(StringComparer.OrdinalIgnoreCase)
        {
            // installers and their helpers
            "setup", "install", "installer", "uninstall", "uninstaller", "unins000",
            "isuninst", "unwise", "unwise32", "st5unst", "msiexec", "iexpress",
            "dfshim", "lnkstub", "msoobe", "icwconn1",
            // generic launchers that say nothing about what was started
            "rundll32", "mshta", "cmd", "control", "explorer", "packager", "regsvr32",
            // documentation
            "readme", "liesmich", "hilfe", "help", "hh", "helpctr", "license", "lizenz",
            "documentation", "dokumentation", "release notes", "whatsnew",
            // maintenance tools XP hid as well
            "backup", "chkdsk", "cleanmgr", "defrag", "dumprep", "dvdplay", "findfast",
            "inoculan", "mmc", "msconfig", "msinfo32", "mstsc", "ntbackup", "perfmon",
            "regedit", "regedt32", "rstrui", "sfc", "sigverif", "sndvol32", "taskmgr",
            "tourstart", "verifier", "winmsd", "winver", "wscui", "wupdmgr",
        };

        /// <summary>Substrings that mark an entry as not worth remembering.</summary>
        private static readonly string[] ExcludedParts =
        {
            "uninstall", "deinstall", "readme", "liesmich", "release notes",
            "hilfe", " help", "help ", "handbuch", "manual", "dokumentation",
        };

        public static bool ShouldRemember(string id)
        {
            if (string.IsNullOrWhiteSpace(id)) return false;

            // Store apps are addressed by AppUserModelID and have no file name.
            if (id.StartsWith("shell:AppsFolder\\", StringComparison.OrdinalIgnoreCase))
                return true;

            string name;
            try { name = Path.GetFileNameWithoutExtension(id); }
            catch { return true; }

            if (string.IsNullOrWhiteSpace(name)) return false;
            if (Excluded.Contains(name)) return false;

            foreach (var part in ExcludedParts)
            {
                if (name.IndexOf(part, StringComparison.OrdinalIgnoreCase) >= 0) return false;
            }

            return !OptedOut(name);
        }

        /// <summary>
        /// A program can keep itself out of the list with a NoStartPage value, either
        /// under its App Paths key or under its Applications key.
        /// </summary>
        private static bool OptedOut(string baseName)
        {
            string exe = baseName + ".exe";

            foreach (var root in new[] { Registry.CurrentUser, Registry.LocalMachine })
            {
                try
                {
                    using var key = root.OpenSubKey(
                        @"SOFTWARE\Microsoft\Windows\CurrentVersion\App Paths\" + exe);
                    if (key?.GetValue("NoStartPage") != null) return true;
                }
                catch { }
            }

            try
            {
                using var key = Registry.ClassesRoot.OpenSubKey(@"Applications\" + exe);
                if (key?.GetValue("NoStartPage") != null) return true;
            }
            catch { }

            return false;
        }
    }
}
