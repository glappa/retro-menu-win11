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
            ["SearchFiles"] = "Dateien mitsuchen",
            ["BestMatch"] = "Beste Übereinstimmung",
            ["AppsGroup"] = "Apps",
            ["SettingsGroup"] = "Einstellungen",
            ["FilesGroup"] = "Dateien",
            ["Searching"] = "Sucht in den Dateien…",
            ["NoIndex"] = "Windows-Suche ist nicht verfügbar",
            ["NoResults"] = "Keine Treffer",
            ["Loading"] = "Wird geladen…",

            ["Documents"] = "Eigene Dateien",
            ["RecentDocuments"] = "Zuletzt verwendete Dokumente",
            ["Pictures"] = "Eigene Bilder",
            ["Music"] = "Eigene Musik",
            ["Computer"] = "Arbeitsplatz",
            ["ControlPanel"] = "Systemsteuerung",
            ["SetProgramAccess"] = "Programmzugriff und -standards festlegen",
            ["ConnectTo"] = "Verbindung herstellen",
            ["PrintersAndFaxes"] = "Drucker und Faxgeräte",
            ["Help"] = "Hilfe und Support",
            ["SearchPlace"] = "Suchen",
            ["Run"] = "Ausführen...",
            ["WindowsUpdate"] = "Windows Update",
            ["Programs"] = "Programme",
            ["Favorites"] = "Favoriten",
            ["Internet"] = "Internet",
            ["Email"] = "E-Mail",
            ["Empty"] = "(Leer)",

            ["LogOff"] = "Abmelden",
            ["LogOffClassic"] = "Abmelden…",
            ["ShutDownClassic"] = "Beenden…",
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
            ["ShowSearchBox"] = "Suchfeld anzeigen (hatte XP nicht)",
            ["SearchFilesSetting"] = "Suche schließt Dateien ein",
            ["MenuScale"] = "Menügröße",
            ["KeepTaskbar"] = "Taskleiste beim Öffnen einblenden",
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
            ["SearchFiles"] = "Search files too",
            ["BestMatch"] = "Best match",
            ["AppsGroup"] = "Apps",
            ["SettingsGroup"] = "Settings",
            ["FilesGroup"] = "Files",
            ["Searching"] = "Searching files…",
            ["NoIndex"] = "Windows Search is unavailable",
            ["NoResults"] = "No matches",
            ["Loading"] = "Loading…",

            ["Documents"] = "My Documents",
            ["RecentDocuments"] = "My Recent Documents",
            ["Pictures"] = "My Pictures",
            ["Music"] = "My Music",
            ["Computer"] = "My Computer",
            ["ControlPanel"] = "Control Panel",
            ["SetProgramAccess"] = "Set Program Access and Defaults",
            ["ConnectTo"] = "Connect To",
            ["PrintersAndFaxes"] = "Printers and Faxes",
            ["Help"] = "Help and Support",
            ["SearchPlace"] = "Search",
            ["Run"] = "Run...",
            ["WindowsUpdate"] = "Windows Update",
            ["Programs"] = "Programs",
            ["Favorites"] = "Favorites",
            ["Internet"] = "Internet",
            ["Email"] = "E-mail",
            ["Empty"] = "(Empty)",

            ["LogOff"] = "Log Off",
            ["LogOffClassic"] = "Log Off…",
            ["ShutDownClassic"] = "Shut Down…",
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
            ["ShowSearchBox"] = "Show search box (XP had none)",
            ["SearchFilesSetting"] = "Search includes files",
            ["MenuScale"] = "Menu size",
            ["KeepTaskbar"] = "Raise the taskbar while open",
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
