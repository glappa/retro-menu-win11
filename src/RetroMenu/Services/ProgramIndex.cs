using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Win32;
using RetroMenu.Model;

namespace RetroMenu.Services
{
    /// <summary>
    /// Every program on the machine, not just the ones with a Start Menu shortcut.
    /// Three sources, cheapest first: the App Paths registry, the uninstall entries,
    /// and finally a bounded walk through the usual install folders.
    /// </summary>
    public sealed class ProgramIndex
    {
        private const int MaxPrograms = 6000;
        private const int MaxDepth = 4;

        /// <summary>Folder names that never hold a program worth offering.</summary>
        private static readonly HashSet<string> SkipFolders = new(StringComparer.OrdinalIgnoreCase)
        {
            "node_modules", "cache", "caches", "temp", "tmp", "logs", "backup",
            "installer", "installers", "redist", "redistributable", "vcredist",
            "crashpad", "crashreports", "symbols", "locales", "resources",
            "sdk", "runtimes", "packages", "obj", "bin.old", ".git", ".vs",
        };

        /// <summary>Executables that are plumbing, not programs.</summary>
        private static readonly string[] SkipNameParts =
        {
            "unins", "setup", "install", "update", "updater", "crash", "report",
            "helper", "service", "daemon", "elevate", "vcredist", "dxsetup",
            "cleanup", "repair", "diagnos", "watchdog", "launcher_", "redist",
        };

        private volatile List<StartItem> _programs = new List<StartItem>();

        public IReadOnlyList<StartItem> Programs => _programs;
        public bool IsReady { get; private set; }

        public event Action Refreshed;

        public Task RefreshAsync() => Task.Run(Refresh);

        public void Refresh()
        {
            var byPath = new Dictionary<string, StartItem>(StringComparer.OrdinalIgnoreCase);

            try { FromAppPaths(byPath); } catch { }
            try { FromUninstallEntries(byPath); } catch { }
            try { FromInstallFolders(byPath); } catch { }

            _programs = byPath.Values
                .OrderBy(p => p.Name, StringComparer.CurrentCultureIgnoreCase)
                .ToList();

            IsReady = true;
            Refreshed?.Invoke();
        }

        public List<StartItem> Search(string query, int max)
        {
            if (string.IsNullOrWhiteSpace(query)) return new List<StartItem>();
            query = query.Trim();

            return _programs
                .Select(item => new { item, rank = SearchRank.Of(item.Name, query) })
                .Where(x => x.rank > 0)
                .OrderByDescending(x => x.rank)
                .ThenBy(x => x.item.Name, StringComparer.CurrentCultureIgnoreCase)
                .Take(max)
                .Select(x => x.item)
                .ToList();
        }

        // ---------------------------------------------------------------- sources

        private static void FromAppPaths(Dictionary<string, StartItem> into)
        {
            const string path = @"SOFTWARE\Microsoft\Windows\CurrentVersion\App Paths";

            foreach (var (root, view) in Roots())
            {
                using var baseKey = RegistryKey.OpenBaseKey(root, view);
                using var appPaths = baseKey.OpenSubKey(path);
                if (appPaths == null) continue;

                foreach (var name in appPaths.GetSubKeyNames())
                {
                    try
                    {
                        using var key = appPaths.OpenSubKey(name);
                        string exe = key?.GetValue(null) as string;
                        if (string.IsNullOrWhiteSpace(exe)) continue;

                        exe = exe.Trim('"');
                        Add(into, exe, Path.GetFileNameWithoutExtension(name));
                    }
                    catch { }
                }
            }
        }

        private static void FromUninstallEntries(Dictionary<string, StartItem> into)
        {
            const string path = @"SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall";

            foreach (var (root, view) in Roots())
            {
                using var baseKey = RegistryKey.OpenBaseKey(root, view);
                using var uninstall = baseKey.OpenSubKey(path);
                if (uninstall == null) continue;

                foreach (var name in uninstall.GetSubKeyNames())
                {
                    try
                    {
                        using var key = uninstall.OpenSubKey(name);
                        if (key == null) continue;
                        if (key.GetValue("SystemComponent") is int component && component != 0) continue;

                        string display = key.GetValue("DisplayName") as string;
                        if (string.IsNullOrWhiteSpace(display)) continue;

                        // DisplayIcon usually points at the program's own executable.
                        string icon = (key.GetValue("DisplayIcon") as string)?.Trim('"');
                        if (string.IsNullOrWhiteSpace(icon)) continue;

                        int comma = icon.LastIndexOf(',');
                        if (comma > 2) icon = icon.Substring(0, comma);
                        if (!icon.EndsWith(".exe", StringComparison.OrdinalIgnoreCase)) continue;

                        Add(into, icon, display);
                    }
                    catch { }
                }
            }
        }

        private static IEnumerable<(RegistryHive Hive, RegistryView View)> Roots()
        {
            yield return (RegistryHive.LocalMachine, RegistryView.Registry64);
            yield return (RegistryHive.LocalMachine, RegistryView.Registry32);
            yield return (RegistryHive.CurrentUser, RegistryView.Default);
        }

        private static void FromInstallFolders(Dictionary<string, StartItem> into)
        {
            foreach (var root in InstallRoots())
            {
                if (into.Count >= MaxPrograms) return;
                try { Walk(into, new DirectoryInfo(root), 0); } catch { }
            }
        }

        private static IEnumerable<string> InstallRoots()
        {
            string local = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);

            var roots = new[]
            {
                Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles),
                Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86),
                Path.Combine(local, "Programs"),
                Path.Combine(local, "Microsoft", "WindowsApps"),
            };

            return roots.Where(r => !string.IsNullOrEmpty(r) && Directory.Exists(r)).Distinct();
        }

        private static void Walk(Dictionary<string, StartItem> into, DirectoryInfo folder, int depth)
        {
            if (depth > MaxDepth || into.Count >= MaxPrograms) return;

            FileInfo[] files;
            try { files = folder.GetFiles("*.exe"); }
            catch { return; }

            foreach (var file in files)
            {
                if (into.Count >= MaxPrograms) return;
                string name = Path.GetFileNameWithoutExtension(file.Name);
                if (IsPlumbing(name)) continue;
                Add(into, file.FullName, name);
            }

            DirectoryInfo[] children;
            try { children = folder.GetDirectories(); }
            catch { return; }

            foreach (var child in children)
            {
                if (child.Attributes.HasFlag(FileAttributes.ReparsePoint)) continue;
                if (SkipFolders.Contains(child.Name)) continue;
                Walk(into, child, depth + 1);
            }
        }

        private static bool IsPlumbing(string name)
        {
            foreach (var part in SkipNameParts)
            {
                if (name.IndexOf(part, StringComparison.OrdinalIgnoreCase) >= 0) return true;
            }
            return false;
        }

        private static void Add(Dictionary<string, StartItem> into, string exe, string name)
        {
            if (string.IsNullOrWhiteSpace(exe) || string.IsNullOrWhiteSpace(name)) return;
            if (into.ContainsKey(exe)) return;
            if (!File.Exists(exe)) return;

            into[exe] = new StartItem
            {
                Name = name,
                Subtext = ShortFolder(exe),
                ParsingName = exe,
                Target = exe,
                Kind = StartItemKind.Shortcut
            };
        }

        /// <summary>The last two folder names, which is usually the vendor and product.</summary>
        private static string ShortFolder(string file)
        {
            try
            {
                string folder = Path.GetDirectoryName(file);
                if (string.IsNullOrEmpty(folder)) return null;

                var parts = folder.Split(Path.DirectorySeparatorChar);
                return parts.Length <= 2 ? folder : string.Join("\\", parts.Skip(parts.Length - 2));
            }
            catch { return null; }
        }
    }
}
