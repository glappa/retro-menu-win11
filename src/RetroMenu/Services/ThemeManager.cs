using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;

namespace RetroMenu.Services
{
    public static class ThemeManager
    {
        /// <summary>Display name to resource file, in the order the settings list shows them.</summary>
        private static readonly (string Name, string File)[] Catalog =
        {
            ("Windows XP Blue", "XPBlue"),
            ("Windows XP Olive Green", "XPOlive"),
            ("Windows XP Silver", "XPSilver"),
            ("Windows XP Royale", "XPRoyale"),
            ("Classic Grey", "ClassicGrey"),
        };

        /// <summary>
        /// RetroBar has far more themes than this menu does. Anything unknown lands
        /// on the closest relative so "follow RetroBar" never leaves us unstyled.
        /// </summary>
        private static readonly Dictionary<string, string> RetroBarAliases =
            new(StringComparer.OrdinalIgnoreCase)
            {
                ["Windows XP Blue"] = "Windows XP Blue",
                ["Windows XP Embedded Style"] = "Windows XP Blue",
                ["Windows XP Olive Green"] = "Windows XP Olive Green",
                ["Windows XP Silver"] = "Windows XP Silver",
                ["Windows XP Royale"] = "Windows XP Royale",
                ["Windows XP Royale Noir"] = "Windows XP Royale",
                ["Windows XP Zune Style"] = "Windows XP Royale",
                ["Watercolor"] = "Windows XP Royale",
                ["Windows Longhorn Aero"] = "Windows XP Royale",
                ["Windows Vista Aero"] = "Windows XP Royale",
                ["Windows Vista Basic"] = "Windows XP Blue",
                ["System Vista"] = "Windows XP Blue",
                ["System XP"] = "Windows XP Blue",
                ["Windows 95-98"] = "Classic Grey",
                ["Windows 2000"] = "Classic Grey",
                ["Windows Me"] = "Classic Grey",
                ["Windows XP Classic"] = "Classic Grey",
                ["Windows Vista Classic"] = "Classic Grey",
                ["System"] = "Classic Grey",
            };

        public static IEnumerable<string> Names => Catalog.Select(c => c.Name);

        public static string Current { get; private set; }

        public static string MapFromRetroBar(string retroBarTheme)
        {
            if (string.IsNullOrWhiteSpace(retroBarTheme)) return "Windows XP Blue";
            return RetroBarAliases.TryGetValue(retroBarTheme, out var mapped) ? mapped : "Windows XP Blue";
        }

        public static void Apply(string name)
        {
            var entry = Catalog.FirstOrDefault(c => string.Equals(c.Name, name, StringComparison.OrdinalIgnoreCase));
            if (entry.File == null) entry = Catalog[0];

            var uri = new Uri($"/RetroMenu;component/Themes/{entry.File}.xaml", UriKind.Relative);
            var dictionary = (ResourceDictionary)Application.LoadComponent(uri);

            var merged = Application.Current.Resources.MergedDictionaries;
            if (merged.Count == 0) merged.Add(dictionary);
            else merged[0] = dictionary;

            Current = entry.Name;
        }
    }
}
