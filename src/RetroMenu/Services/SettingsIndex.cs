using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using RetroMenu.Interop;
using RetroMenu.Model;

namespace RetroMenu.Services
{
    /// <summary>
    /// Windows settings, so searching for "Maus" or "Bildschirm" finds the page that
    /// changes it — the way the Windows 11 search does. Both sources are shell
    /// folders Windows keeps for exactly this: the Control Panel and the flat list of
    /// every control panel task.
    /// </summary>
    public sealed class SettingsIndex
    {
        private const string ControlPanel = "shell:ControlPanelFolder";

        /// <summary>The "All Tasks" folder: every control panel task as one entry.</summary>
        private const string AllTasks = "shell:::{ED7BA470-8E54-465E-825C-99712043E01C}";

        private volatile List<StartItem> _entries = new List<StartItem>();

        public IReadOnlyList<StartItem> Entries => _entries;

        public Task RefreshAsync() => Task.Run(Refresh);

        public void Refresh()
        {
            var byName = new Dictionary<string, StartItem>(StringComparer.CurrentCultureIgnoreCase);

            Collect(byName, ControlPanel, null);
            Collect(byName, AllTasks, Lang.T("SettingsGroup"));

            _entries = byName.Values
                .OrderBy(e => e.Name, StringComparer.CurrentCultureIgnoreCase)
                .ToList();
        }

        private static void Collect(Dictionary<string, StartItem> into, string folder, string subtext)
        {
            List<ShellFolderEntry> entries;
            try { entries = ShellFolder.Enumerate(folder, 400); }
            catch { return; }

            foreach (var entry in entries)
            {
                if (string.IsNullOrWhiteSpace(entry.Name)) continue;
                if (into.ContainsKey(entry.Name)) continue;

                into[entry.Name] = new StartItem
                {
                    Name = entry.Name,
                    Subtext = subtext,
                    ParsingName = entry.ParsingName,
                    Kind = StartItemKind.Command,
                    Command = "place:" + entry.ParsingName
                };
            }
        }

        public List<StartItem> Search(string query, int max)
        {
            if (string.IsNullOrWhiteSpace(query)) return new List<StartItem>();
            query = query.Trim();

            return _entries
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
