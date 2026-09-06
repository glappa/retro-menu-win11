using System;
using System.Collections.Generic;
using System.Globalization;
using System.Runtime.InteropServices;

namespace RetroMenu.Services
{
    /// <summary>
    /// Asks Windows which display language it is actually set to.
    ///
    /// <see cref="CultureInfo.CurrentUICulture"/> alone is not enough: it answers
    /// with the thread's culture, which a managed host or an odd start-up order can
    /// have moved somewhere else. GetUserPreferredUILanguages is the setting the
    /// user picked under Time and Language, in their own order of preference, so a
    /// Swiss machine that lists French before German is honoured as French.
    /// </summary>
    public static class SystemLanguage
    {
        private const uint MUI_LANGUAGE_NAME = 0x8;

        [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
        private static extern bool GetUserPreferredUILanguages(
            uint dwFlags, out uint pulNumLanguages, IntPtr pwszLanguagesBuffer,
            ref uint pcchLanguagesBuffer);

        [DllImport("kernel32.dll")]
        private static extern ushort GetUserDefaultUILanguage();

        private static List<string> _cache;

        /// <summary>
        /// The display languages as BCP-47 tags, best first, e.g. de-DE, en-US.
        /// Never empty: en falls in at the end if Windows tells us nothing.
        /// </summary>
        public static IReadOnlyList<string> Preferred => _cache ??= Read();

        /// <summary>Forgets the cached answer, so a language change is picked up.</summary>
        public static void Forget() => _cache = null;

        /// <summary>The first tag, the one Windows draws its own menus in.</summary>
        public static string Display => Preferred.Count > 0 ? Preferred[0] : "en";

        /// <summary>"Deutsch (de-DE)" — for the line under the language box.</summary>
        public static string DisplayName()
        {
            string tag = Display;
            try
            {
                var culture = CultureInfo.GetCultureInfo(tag);
                string native = culture.NativeName;
                if (native.Length > 0) native = char.ToUpper(native[0], culture) + native.Substring(1);
                return $"{native} ({tag})";
            }
            catch { return tag; }
        }

        private static List<string> Read()
        {
            var tags = new List<string>();

            foreach (string tag in FromMui()) Add(tags, tag);

            // Fallbacks, in descending order of how much they know about the user.
            try { Add(tags, CultureInfo.GetCultureInfo(GetUserDefaultUILanguage()).Name); } catch { }
            try { Add(tags, CultureInfo.InstalledUICulture.Name); } catch { }
            try { Add(tags, CultureInfo.CurrentUICulture.Name); } catch { }

            if (tags.Count == 0) tags.Add("en");
            return tags;
        }

        private static void Add(List<string> tags, string tag)
        {
            if (string.IsNullOrWhiteSpace(tag)) return;
            foreach (string known in tags)
                if (string.Equals(known, tag, StringComparison.OrdinalIgnoreCase)) return;
            tags.Add(tag);
        }

        /// <summary>The MUI list, read the usual way: once for the size, once for real.</summary>
        private static IEnumerable<string> FromMui()
        {
            IntPtr buffer = IntPtr.Zero;
            try
            {
                uint count = 0, chars = 0;
                if (!GetUserPreferredUILanguages(MUI_LANGUAGE_NAME, out count, IntPtr.Zero, ref chars)
                    || chars == 0)
                    return Array.Empty<string>();

                buffer = Marshal.AllocHGlobal((int)chars * sizeof(char));
                if (!GetUserPreferredUILanguages(MUI_LANGUAGE_NAME, out count, buffer, ref chars))
                    return Array.Empty<string>();

                // The buffer holds "de-DE\0en-US\0\0": tags until an empty one.
                var found = new List<string>();
                int offset = 0;
                while (offset < (int)chars)
                {
                    string tag = Marshal.PtrToStringUni(buffer + offset * sizeof(char));
                    if (string.IsNullOrEmpty(tag)) break;
                    found.Add(tag);
                    offset += tag.Length + 1;
                }
                return found;
            }
            catch { return Array.Empty<string>(); }
            finally { if (buffer != IntPtr.Zero) Marshal.FreeHGlobal(buffer); }
        }
    }
}
