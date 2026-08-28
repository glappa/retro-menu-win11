using System;
using System.Collections.Generic;
using System.Globalization;

namespace RetroMenu.Services
{
    /// <summary>
    /// Two hand-kept string tables. RetroBar ships dozens of languages; this menu
    /// starts with the two its author actually uses and keeps the lookup trivial so
    /// adding another is one dictionary entry per string.
    /// </summary>
    public static class Lang
    {
        private static readonly Dictionary<string, string> De = new()
        {
            ["AllPrograms"] = "Alle Programme",
            ["Search"] = "Suchen",
            ["SearchHint"] = "Programme durchsuchen",
            ["NoResults"] = "Keine Treffer",
            ["Loading"] = "Wird geladen…",

            ["Documents"] = "Eigene Dateien",
            ["Pictures"] = "Eigene Bilder",
            ["Music"] = "Eigene Musik",
            ["Videos"] = "Eigene Videos",
            ["Downloads"] = "Downloads",
            ["Computer"] = "Arbeitsplatz",
            ["Network"] = "Netzwerkumgebung",
            ["ControlPanel"] = "Systemsteuerung",
            ["Settings"] = "Einstellungen",
            ["Run"] = "Ausführen…",
            ["SearchPlace"] = "Suchen",
            ["Help"] = "Hilfe und Support",

            ["LogOff"] = "Abmelden",
            ["ShutDown"] = "Computer ausschalten",
            ["PowerTitle"] = "Computer ausschalten",
            ["Standby"] = "Standby",
            ["Hibernate"] = "Ruhezustand",
            ["TurnOff"] = "Ausschalten",
            ["Restart"] = "Neu starten",
            ["Lock"] = "Sperren",
            ["Cancel"] = "Abbrechen",
            ["PowerQuestion"] = "Was soll der Computer tun?",

            ["Open"] = "Öffnen",
            ["RunAsAdmin"] = "Als Administrator ausführen",
            ["Pin"] = "An Startmenü anheften",
            ["Unpin"] = "Vom Startmenü lösen",
            ["RemoveFromList"] = "Aus dieser Liste entfernen",
            ["OpenFileLocation"] = "Dateipfad öffnen",

            ["TrayOpen"] = "Startmenü öffnen",
            ["TraySettings"] = "Einstellungen…",
            ["TrayRefresh"] = "Programmliste neu einlesen",
            ["TrayExit"] = "Beenden",

            ["SettingsTitle"] = "Retro-Menü – Einstellungen",
            ["Appearance"] = "Darstellung",
            ["Theme"] = "Design",
            ["FollowRetroBar"] = "Design von RetroBar übernehmen",
            ["Behaviour"] = "Verhalten",
            ["WinKey"] = "Windows-Taste",
            ["WinKeyNeutralize"] = "Abfangen (empfohlen)",
            ["WinKeySwallow"] = "Vollständig schlucken",
            ["WinKeyOff"] = "Nicht anfassen",
            ["FrequentCount"] = "Häufig verwendet: Anzahl",
            ["ShowSearchBox"] = "Suchfeld anzeigen",
            ["ShowStoreApps"] = "Store-Apps mit auflisten",
            ["AutoStart"] = "Mit Windows starten",
            ["Language"] = "Sprache",
            ["Close"] = "Schließen",
            ["StoreApps"] = "Store-Apps",
        };

        private static readonly Dictionary<string, string> En = new()
        {
            ["AllPrograms"] = "All Programs",
            ["Search"] = "Search",
            ["SearchHint"] = "Search programs",
            ["NoResults"] = "No matches",
            ["Loading"] = "Loading…",

            ["Documents"] = "My Documents",
            ["Pictures"] = "My Pictures",
            ["Music"] = "My Music",
            ["Videos"] = "My Videos",
            ["Downloads"] = "Downloads",
            ["Computer"] = "My Computer",
            ["Network"] = "My Network Places",
            ["ControlPanel"] = "Control Panel",
            ["Settings"] = "Settings",
            ["Run"] = "Run…",
            ["SearchPlace"] = "Search",
            ["Help"] = "Help and Support",

            ["LogOff"] = "Log Off",
            ["ShutDown"] = "Turn Off Computer",
            ["PowerTitle"] = "Turn off computer",
            ["Standby"] = "Stand By",
            ["Hibernate"] = "Hibernate",
            ["TurnOff"] = "Turn Off",
            ["Restart"] = "Restart",
            ["Lock"] = "Lock",
            ["Cancel"] = "Cancel",
            ["PowerQuestion"] = "What should the computer do?",

            ["Open"] = "Open",
            ["RunAsAdmin"] = "Run as administrator",
            ["Pin"] = "Pin to Start menu",
            ["Unpin"] = "Unpin from Start menu",
            ["RemoveFromList"] = "Remove from this list",
            ["OpenFileLocation"] = "Open file location",

            ["TrayOpen"] = "Open start menu",
            ["TraySettings"] = "Settings…",
            ["TrayRefresh"] = "Rescan programs",
            ["TrayExit"] = "Exit",

            ["SettingsTitle"] = "Retro Menu – Settings",
            ["Appearance"] = "Appearance",
            ["Theme"] = "Theme",
            ["FollowRetroBar"] = "Follow RetroBar's theme",
            ["Behaviour"] = "Behaviour",
            ["WinKey"] = "Windows key",
            ["WinKeyNeutralize"] = "Intercept (recommended)",
            ["WinKeySwallow"] = "Swallow completely",
            ["WinKeyOff"] = "Leave alone",
            ["FrequentCount"] = "Frequently used: count",
            ["ShowSearchBox"] = "Show search box",
            ["ShowStoreApps"] = "List Store apps",
            ["AutoStart"] = "Start with Windows",
            ["Language"] = "Language",
            ["Close"] = "Close",
            ["StoreApps"] = "Store apps",
        };

        private static Dictionary<string, string> _active = En;

        public static string Current { get; private set; } = "en";

        public static void Apply(string setting, string retroBarLanguage)
        {
            string choice = setting;

            if (string.IsNullOrWhiteSpace(choice) || choice == "auto")
            {
                if (!string.IsNullOrWhiteSpace(retroBarLanguage))
                    choice = retroBarLanguage.StartsWith("Deutsch", StringComparison.OrdinalIgnoreCase) ? "de" : "en";
                else
                    choice = CultureInfo.CurrentUICulture.TwoLetterISOLanguageName;
            }

            Current = string.Equals(choice, "de", StringComparison.OrdinalIgnoreCase) ? "de" : "en";
            _active = Current == "de" ? De : En;
        }

        public static string T(string key) =>
            _active.TryGetValue(key, out var value) ? value : key;
    }
}
