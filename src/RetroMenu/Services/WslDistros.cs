using System;
using System.Collections.Generic;
using Microsoft.Win32;

namespace RetroMenu.Services
{
    /// <summary>
    /// The Linux systems installed under WSL, for the Connections submenu.
    ///
    /// `wsl.exe --list --quiet` is the documented way to ask, but it starts a
    /// process, and a submenu has to stand there the moment the pointer rests on
    /// its arrow. The list itself lies in the registry, where the WSL service keeps
    /// it, and reading it costs nothing. Starting a session still goes through
    /// wsl.exe, which is the part that has to be done properly.
    /// </summary>
    public static class WslDistros
    {
        private const string Key = @"Software\Microsoft\Windows\CurrentVersion\Lxss";

        /// <summary>
        /// The distributions, by the name wsl.exe knows them under. The systems
        /// Docker Desktop keeps for itself are left out: they hold no shell anybody
        /// would want to open from a start menu.
        /// </summary>
        public static List<string> Names()
        {
            var names = new List<string>();

            try
            {
                using var root = Registry.CurrentUser.OpenSubKey(Key);
                if (root == null) return names;

                foreach (string child in root.GetSubKeyNames())
                {
                    using var entry = root.OpenSubKey(child);
                    if (entry?.GetValue("DistributionName") is not string name) continue;
                    if (string.IsNullOrWhiteSpace(name)) continue;
                    if (name.StartsWith("docker-desktop", StringComparison.OrdinalIgnoreCase)) continue;

                    names.Add(name);
                }
            }
            catch
            {
                // No WSL, no key, no entry in the menu.
            }

            names.Sort(StringComparer.CurrentCultureIgnoreCase);
            return names;
        }
    }
}
