using System;
using System.Collections.Generic;
using System.Linq;
using RetroMenu.Services.Strings;

namespace RetroMenu.Services
{
    /// <summary>One language: its tag, the name it calls itself, and its strings.</summary>
    public sealed class LanguageInfo
    {
        public LanguageInfo(string code, string native, Dictionary<string, string> table)
        {
            Code = code;
            Native = native;
            Table = table;
        }

        /// <summary>BCP-47-ish tag, e.g. "de", "pt-BR", "zh-Hans".</summary>
        public string Code { get; }

        /// <summary>What the language calls itself, for the settings list.</summary>
        public string Native { get; }

        public Dictionary<string, string> Table { get; }
    }

    /// <summary>
    /// Hand-kept string tables, one file per language under Services/Strings.
    /// English is the backstop: any key a table forgets is answered from it, so a
    /// half-finished translation shows English words rather than raw key names.
    ///
    /// The menu picks its language from the display language Windows itself is set
    /// to (see <see cref="SystemLanguage"/>), or from RetroBar, or from a fixed
    /// choice in the settings window.
    /// </summary>
    public static class Lang
    {
        /// <summary>Settings value: follow the Windows display language.</summary>
        public const string AutoWindows = "auto";

        /// <summary>Settings value: follow whatever RetroBar is set to.</summary>
        public const string AutoRetroBar = "auto-retrobar";

        /// <summary>
        /// The order the settings list shows: the two the menu grew up with, then
        /// the rest by the name each language gives itself.
        /// </summary>
        public static readonly IReadOnlyList<LanguageInfo> Languages = new List<LanguageInfo>
        {
            new LanguageInfo("en",      "English",            En.Table),
            new LanguageInfo("de",      "Deutsch",            De.Table),
            new LanguageInfo("cs",      "Čeština",            Cs.Table),
            new LanguageInfo("da",      "Dansk",              Da.Table),
            new LanguageInfo("es",      "Español",            Es.Table),
            new LanguageInfo("fr",      "Français",           Fr.Table),
            new LanguageInfo("it",      "Italiano",           It.Table),
            new LanguageInfo("hu",      "Magyar",             Hu.Table),
            new LanguageInfo("nl",      "Nederlands",         Nl.Table),
            new LanguageInfo("nb",      "Norsk bokmål",       Nb.Table),
            new LanguageInfo("pl",      "Polski",             Pl.Table),
            new LanguageInfo("pt",      "Português",          Pt.Table),
            new LanguageInfo("pt-BR",   "Português (Brasil)", PtBr.Table),
            new LanguageInfo("ro",      "Română",             Ro.Table),
            new LanguageInfo("fi",      "Suomi",              Fi.Table),
            new LanguageInfo("sv",      "Svenska",            Sv.Table),
            new LanguageInfo("tr",      "Türkçe",             Tr.Table),
            new LanguageInfo("el",      "Ελληνικά",           El.Table),
            new LanguageInfo("ru",      "Русский",            Ru.Table),
            new LanguageInfo("uk",      "Українська",         Uk.Table),
            new LanguageInfo("ja",      "日本語",              Ja.Table),
            new LanguageInfo("ko",      "한국어",              Ko.Table),
            new LanguageInfo("zh-Hans", "简体中文",            ZhHans.Table),
            new LanguageInfo("zh-Hant", "繁體中文",            ZhHant.Table),
        };

        /// <summary>
        /// Tags Windows may report that mean one of ours under another name.
        /// </summary>
        private static readonly Dictionary<string, string> Aliases =
            new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                ["no"] = "nb",      // Norwegian with no written form named
                ["nn"] = "nb",      // Nynorsk: bokmål is far closer than English
                ["mo"] = "ro",      // Moldovan, retired in favour of Romanian
            };

        /// <summary>The native names RetroBar writes into its own settings file.</summary>
        private static readonly Dictionary<string, string> RetroBarNames =
            new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                ["english"] = "en",
                ["deutsch"] = "de",
                ["čeština"] = "cs",
                ["cestina"] = "cs",
                ["dansk"] = "da",
                ["español"] = "es",
                ["espanol"] = "es",
                ["français"] = "fr",
                ["francais"] = "fr",
                ["italiano"] = "it",
                ["magyar"] = "hu",
                ["nederlands"] = "nl",
                ["norsk"] = "nb",
                ["norsk bokmål"] = "nb",
                ["polski"] = "pl",
                ["português"] = "pt",
                ["portugues"] = "pt",
                ["português (brasil)"] = "pt-BR",
                ["portugues (brasil)"] = "pt-BR",
                ["português (brasileiro)"] = "pt-BR",
                ["română"] = "ro",
                ["romana"] = "ro",
                ["suomi"] = "fi",
                ["svenska"] = "sv",
                ["türkçe"] = "tr",
                ["turkce"] = "tr",
                ["ελληνικά"] = "el",
                ["русский"] = "ru",
                ["українська"] = "uk",
                ["日本語"] = "ja",
                ["한국어"] = "ko",
                ["简体中文"] = "zh-Hans",
                ["中文(简体)"] = "zh-Hans",
                ["繁體中文"] = "zh-Hant",
                ["中文(繁體)"] = "zh-Hant",
            };

        private static LanguageInfo _active = Languages[0];

        /// <summary>The tag actually in use, e.g. "de" or "pt-BR".</summary>
        public static string Current => _active.Code;

        /// <summary>What that language calls itself.</summary>
        public static string CurrentNative => _active.Native;

        /// <summary>How the language was arrived at, for the settings window.</summary>
        public static string Source { get; private set; } = "windows";

        /// <summary>
        /// Settles on a language. <paramref name="setting"/> is the saved choice:
        /// "auto" (Windows), "auto-retrobar", or a fixed tag. Anything unknown is
        /// treated as "auto", and English catches whatever is left.
        /// </summary>
        public static void Apply(string setting, string retroBarLanguage)
        {
            string choice = string.IsNullOrWhiteSpace(setting) ? AutoWindows : setting.Trim();
            string fromRetroBar = FromRetroBar(retroBarLanguage);

            LanguageInfo found = null;
            string source = null;

            if (string.Equals(choice, AutoRetroBar, StringComparison.OrdinalIgnoreCase))
            {
                found = Find(fromRetroBar);
                if (found != null) source = "retrobar";
            }
            else if (!string.Equals(choice, AutoWindows, StringComparison.OrdinalIgnoreCase))
            {
                found = Find(choice);
                if (found != null) source = "fixed";
            }

            if (found == null)
            {
                // The Windows display languages, in the user's own order.
                foreach (string tag in SystemLanguage.Preferred)
                {
                    found = Find(tag);
                    if (found != null) { source = "windows"; break; }
                }
            }

            // Last resort before English: whatever the rest of the desktop speaks.
            if (found == null)
            {
                found = Find(fromRetroBar);
                if (found != null) source = "retrobar";
            }

            _active = found ?? Languages[0];
            Source = source ?? "fallback";
        }

        /// <summary>The table entry, falling back to English and then to the key itself.</summary>
        public static string T(string key)
        {
            if (key == null) return string.Empty;
            if (_active.Table.TryGetValue(key, out var value)) return value;
            if (En.Table.TryGetValue(key, out var english)) return english;
            return key;
        }

        /// <summary>A table entry with {0}, {1}… filled in.</summary>
        public static string F(string key, params object[] args)
        {
            string text = T(key);
            try { return string.Format(text, args); }
            catch (FormatException) { return text; }
        }

        /// <summary>Matches a tag against the tables: exact, then script, then language.</summary>
        public static LanguageInfo Find(string tag)
        {
            if (string.IsNullOrWhiteSpace(tag)) return null;
            tag = tag.Trim().Replace('_', '-');

            var exact = Languages.FirstOrDefault(
                l => string.Equals(l.Code, tag, StringComparison.OrdinalIgnoreCase));
            if (exact != null) return exact;

            string primary = tag.Split('-')[0];
            if (Aliases.TryGetValue(primary, out var alias)) primary = alias;

            // Chinese splits by script rather than by country: zh-CN and zh-SG are
            // written simplified, zh-TW, zh-HK and zh-MO traditional.
            if (string.Equals(primary, "zh", StringComparison.OrdinalIgnoreCase))
            {
                bool traditional =
                    tag.IndexOf("Hant", StringComparison.OrdinalIgnoreCase) >= 0 ||
                    tag.EndsWith("-TW", StringComparison.OrdinalIgnoreCase) ||
                    tag.EndsWith("-HK", StringComparison.OrdinalIgnoreCase) ||
                    tag.EndsWith("-MO", StringComparison.OrdinalIgnoreCase);
                return ByCode(traditional ? "zh-Hant" : "zh-Hans");
            }

            // pt-BR has its own table; every other Portuguese is the European one.
            if (string.Equals(primary, "pt", StringComparison.OrdinalIgnoreCase))
                return ByCode(tag.EndsWith("-BR", StringComparison.OrdinalIgnoreCase) ? "pt-BR" : "pt");

            return Languages.FirstOrDefault(
                l => string.Equals(l.Code, primary, StringComparison.OrdinalIgnoreCase));
        }

        private static LanguageInfo ByCode(string code) =>
            Languages.FirstOrDefault(l => l.Code == code);

        /// <summary>Turns RetroBar's "Deutsch" (or a bare tag) into one of our codes.</summary>
        public static string FromRetroBar(string value)
        {
            if (string.IsNullOrWhiteSpace(value)) return null;
            string name = value.Trim();
            if (RetroBarNames.TryGetValue(name, out var code)) return code;
            return Find(name)?.Code;   // in case a build starts writing tags
        }
    }
}
