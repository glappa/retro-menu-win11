using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;

namespace RetroMenu.Services
{
    public enum MenuLayout
    {
        /// <summary>Two columns with a header and a footer, as XP introduced.</summary>
        Panel,

        /// <summary>One column with the version name down a strip on the left, as 9x had.</summary>
        Classic
    }

    public static class ThemeManager
    {
        /// <summary>Display name to resource file, oldest first.</summary>
        private static readonly (string Name, string File)[] Catalog =
        {
            ("Windows 95", "Windows95"),
            ("Windows 98", "Windows98"),
            ("Windows Me", "WindowsMe"),
            ("Windows 2000", "Windows2000"),
            ("Windows XP Blue", "XPBlue"),
            ("Windows XP Olive Green", "XPOlive"),
            ("Windows XP Silver", "XPSilver"),
            ("Windows XP Royale", "XPRoyale"),
        };

        /// <summary>
        /// RetroBar has more themes than this menu does. Anything unknown lands on
        /// the closest relative so "follow RetroBar" never leaves us unstyled.
        /// </summary>
        private static readonly Dictionary<string, string> RetroBarAliases =
            new(StringComparer.OrdinalIgnoreCase)
            {
                ["Windows 95-98"] = "Windows 98",
                ["Windows Me"] = "Windows Me",
                ["Windows 2000"] = "Windows 2000",
                ["Windows XP Classic"] = "Windows 2000",
                ["Windows Vista Classic"] = "Windows 2000",
                ["System"] = "Windows 2000",
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
            };

        public static IEnumerable<string> Names => Catalog.Select(c => c.Name);

        public static string Current { get; private set; }

        /// <summary>Which of the two window layouts the current theme asks for.</summary>
        public static MenuLayout Layout
        {
            get
            {
                var value = Application.Current?.TryFindResource("MenuLayout") as string;
                return string.Equals(value, "Classic", StringComparison.OrdinalIgnoreCase)
                    ? MenuLayout.Classic
                    : MenuLayout.Panel;
            }
        }

        public static string MapFromRetroBar(string retroBarTheme)
        {
            if (string.IsNullOrWhiteSpace(retroBarTheme)) return "Windows XP Blue";
            return RetroBarAliases.TryGetValue(retroBarTheme, out var mapped) ? mapped : "Windows XP Blue";
        }

        public static void Apply(string name)
        {
            var entry = Catalog.FirstOrDefault(c => string.Equals(c.Name, name, StringComparison.OrdinalIgnoreCase));
            if (entry.File == null) entry = Catalog.First(c => c.Name == "Windows XP Blue");

            var uri = new Uri($"/RetroMenu;component/Themes/{entry.File}.xaml", UriKind.Relative);
            var dictionary = (ResourceDictionary)Application.LoadComponent(uri);

            var merged = Application.Current.Resources.MergedDictionaries;
            if (merged.Count == 0) merged.Add(dictionary);
            else merged[0] = dictionary;

            Current = entry.Name;
        }
    }
}
