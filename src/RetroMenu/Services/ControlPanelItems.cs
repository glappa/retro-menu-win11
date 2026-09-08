using System;
using System.Collections.Generic;
using System.Linq;
using RetroMenu.Interop;

namespace RetroMenu.Services
{
    /// <summary>One applet of the Control Panel, as Windows itself names it.</summary>
    public sealed class ControlPanelItem
    {
        public string Name { get; set; }

        /// <summary>What the shell can open again, e.g. "::{26EE…}\0\::{6C8E…}".</summary>
        public string ParsingName { get; set; }
    }

    /// <summary>
    /// Everything the Control Panel shows under "All Control Panel Items", read
    /// from the shell rather than written down here: the names then arrive in the
    /// language Windows is set to, the icons come from the applets themselves, and
    /// a machine that has an entry more or fewer is right either way.
    /// </summary>
    public static class ControlPanelItems
    {
        private static List<ControlPanelItem> _all = new List<ControlPanelItem>();

        public static IReadOnlyList<ControlPanelItem> All => _all;

        /// <summary>False until the folder has been read once.</summary>
        public static bool Loaded { get; private set; }

        public static event Action Refreshed;

        /// <summary>
        /// Reads it on the shell worker thread. Namespace extensions hand back
        /// nothing at all from a thread pool thread, so this borrows the STA worker
        /// the icons already run on.
        /// </summary>
        public static void RefreshAsync() => IconLoader.Enqueue(Refresh);

        public static void Refresh()
        {
            try
            {
                var found = ShellFolder.Enumerate("shell:ControlPanelFolder", 300)
                    .Where(e => !string.IsNullOrWhiteSpace(e.ParsingName) && HasName(e.Name))
                    .GroupBy(e => e.ParsingName, StringComparer.OrdinalIgnoreCase)
                    .Select(g => new ControlPanelItem
                    {
                        Name = g.First().Name,
                        ParsingName = g.Key
                    })
                    .OrderBy(i => i.Name, StringComparer.CurrentCultureIgnoreCase)
                    .ToList();

                // An empty answer means the read failed, not that the machine has no
                // Control Panel — in that case the list we already have is better.
                if (found.Count > 0) _all = found;
            }
            catch { /* the menu is no worse off than before */ }

            Loaded = true;
            try { Refreshed?.Invoke(); }
            catch { }
        }

        /// <summary>
        /// An item the shell has no display name for answers with its own parsing
        /// name instead — a row of braces and hex. The Control Panel hides those,
        /// and so do we.
        /// </summary>
        private static bool HasName(string name) =>
            !string.IsNullOrWhiteSpace(name) && !name.StartsWith("::{", StringComparison.Ordinal);

        public static ControlPanelItem Find(string parsingName)
        {
            if (string.IsNullOrEmpty(parsingName)) return null;
            return _all.FirstOrDefault(
                i => string.Equals(i.ParsingName, parsingName, StringComparison.OrdinalIgnoreCase));
        }
    }
}
