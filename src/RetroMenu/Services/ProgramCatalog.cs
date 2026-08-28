using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using RetroMenu.Interop;
using RetroMenu.Model;

namespace RetroMenu.Services
{
    /// <summary>
    /// Builds the program tree the way the classic Start menu did: the machine-wide
    /// and the per-user "Programs" folders merged into one, plus the Store apps that
    /// have no shortcut on disk.
    /// </summary>
    public sealed class ProgramCatalog
    {
        private static readonly string[] LaunchableExtensions =
            { ".lnk", ".url", ".appref-ms", ".exe", ".bat", ".cmd", ".msc", ".pif" };

        public StartItem Root { get; private set; } = new StartItem { Name = "", Kind = StartItemKind.Folder };

        /// <summary>Every launchable entry, flattened, for the search box.</summary>
        public List<StartItem> Flat { get; private set; } = new List<StartItem>();

        public event Action Refreshed;

        public Task RefreshAsync() => Task.Run(Refresh);

        public void Refresh()
        {
            if (Demo.IsActive)
            {
                Root = Demo.Tree();
                Flat = new List<StartItem>();
                Flatten(Root, Flat);
                Refreshed?.Invoke();
                return;
            }

            var root = new StartItem { Name = "", Kind = StartItemKind.Folder };

            foreach (var dir in StartMenuRoots())
            {
                try { Merge(root, dir); } catch { /* unreadable branch, keep going */ }
            }

            Sort(root);

            if (AppSettings.Instance.ShowStoreApps)
                AppendStoreApps(root);

            var flat = new List<StartItem>();
            Flatten(root, flat);
            MarkNewArrivals(flat);

            Root = root;
            Flat = flat;
            Refreshed?.Invoke();
        }

        private static IEnumerable<string> StartMenuRoots()
        {
            string common = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.CommonStartMenu), "Programs");
            string user = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.StartMenu), "Programs");

            if (Directory.Exists(common)) yield return common;
            if (Directory.Exists(user) && !string.Equals(user, common, StringComparison.OrdinalIgnoreCase))
                yield return user;
        }

        private static void Merge(StartItem parent, string directory)
        {
            foreach (var sub in Directory.EnumerateDirectories(directory))
            {
                var info = new DirectoryInfo(sub);
                if (info.Attributes.HasFlag(FileAttributes.Hidden)) continue;

                var folder = parent.Children.FirstOrDefault(
                    c => c.IsFolder && string.Equals(c.Name, info.Name, StringComparison.OrdinalIgnoreCase));

                if (folder == null)
                {
                    folder = new StartItem
                    {
                        Name = info.Name,
                        Kind = StartItemKind.Folder,
                        ParsingName = info.FullName
                    };
                    parent.Children.Add(folder);
                }

                try { Merge(folder, sub); } catch { }
            }

            foreach (var file in Directory.EnumerateFiles(directory))
            {
                var info = new FileInfo(file);
                if (info.Attributes.HasFlag(FileAttributes.Hidden)) continue;

                string ext = info.Extension;
                if (!LaunchableExtensions.Contains(ext, StringComparer.OrdinalIgnoreCase)) continue;

                string name = Path.GetFileNameWithoutExtension(info.Name);
                if (string.IsNullOrWhiteSpace(name)) continue;

                if (parent.Children.Any(c => !c.IsFolder &&
                        string.Equals(c.Name, name, StringComparison.OrdinalIgnoreCase)))
                    continue;

                parent.Children.Add(new StartItem
                {
                    Name = name,
                    Kind = StartItemKind.Shortcut,
                    ParsingName = info.FullName,
                    Target = info.FullName
                });
            }
        }

        private static void Sort(StartItem folder)
        {
            var ordered = folder.Children
                .OrderByDescending(c => c.IsFolder)
                .ThenBy(c => c.Name, StringComparer.CurrentCultureIgnoreCase)
                .ToList();

            folder.Children.Clear();
            folder.Children.AddRange(ordered);

            foreach (var child in folder.Children)
                if (child.IsFolder) Sort(child);
        }

        private static void AppendStoreApps(StartItem root)
        {
            List<ShellFolderEntry> entries;
            try { entries = ShellFolder.Apps(); }
            catch { return; }

            var known = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            CollectNames(root, known);

            var packaged = entries
                .Where(e => e.IsPackaged && !known.Contains(e.Name))
                .GroupBy(e => e.Name, StringComparer.OrdinalIgnoreCase)
                .Select(g => g.First())
                .OrderBy(e => e.Name, StringComparer.CurrentCultureIgnoreCase)
                .ToList();

            if (packaged.Count == 0) return;

            var folder = new StartItem { Name = Lang.T("StoreApps"), Kind = StartItemKind.Folder };
            foreach (var entry in packaged)
            {
                folder.Children.Add(new StartItem
                {
                    Name = entry.Name,
                    Kind = StartItemKind.StoreApp,
                    ParsingName = entry.ParsingName,
                    Target = entry.Relative
                });
            }

            root.Children.Insert(0, folder);
        }

        /// <summary>
        /// Anything that was not in the catalogue last time counts as newly
        /// installed, which is what XP highlighted in All Programs. On the very
        /// first run everything is simply recorded, or the whole menu would light up.
        /// </summary>
        private static void MarkNewArrivals(List<StartItem> flat)
        {
            var settings = AppSettings.Instance;
            bool firstRun = settings.KnownPrograms.Count == 0;
            var known = new HashSet<string>(settings.KnownPrograms, StringComparer.OrdinalIgnoreCase);

            var added = new List<string>();
            foreach (var item in flat)
            {
                if (string.IsNullOrEmpty(item.Id)) continue;
                if (known.Contains(item.Id)) continue;

                added.Add(item.Id);
                if (!firstRun) item.IsNew = true;
            }

            if (added.Count == 0) return;

            settings.KnownPrograms.AddRange(added);

            // Programs come and go; keep the list from growing without end.
            if (settings.KnownPrograms.Count > 4000)
                settings.KnownPrograms.RemoveRange(0, settings.KnownPrograms.Count - 3000);

            settings.Save();
        }

        private static void CollectNames(StartItem folder, HashSet<string> into)
        {
            foreach (var child in folder.Children)
            {
                if (child.IsFolder) CollectNames(child, into);
                else into.Add(child.Name);
            }
        }

        private static void Flatten(StartItem folder, List<StartItem> into)
        {
            foreach (var child in folder.Children)
            {
                if (child.IsFolder) Flatten(child, into);
                else into.Add(child);
            }
        }

        public StartItem Find(string id)
        {
            if (string.IsNullOrEmpty(id)) return null;
            return Flat.FirstOrDefault(i => string.Equals(i.Id, id, StringComparison.OrdinalIgnoreCase));
        }

        public List<StartItem> Search(string query, int max)
        {
            if (string.IsNullOrWhiteSpace(query)) return new List<StartItem>();
            query = query.Trim();

            return Flat
                .Select(item => new { item, rank = SearchRank.Of(item.Name, query) })
                .Where(x => x.rank > 0)
                .OrderByDescending(x => x.rank)
                .ThenBy(x => x.item.Name, StringComparer.CurrentCultureIgnoreCase)
                .Take(max)
                .Select(x => x.item)
                .ToList();
        }

    }
}
