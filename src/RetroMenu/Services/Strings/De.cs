using System.Collections.Generic;

namespace RetroMenu.Services.Strings
{
    /// <summary>Deutsch.</summary>
    internal static class De
    {
        internal static readonly Dictionary<string, string> Table = new Dictionary<string, string>
        {
            // ---- menu and search ----
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

            ["ProgramFilter"] = "Filtern:",
            ["ProgramGroups"] = "Gruppen",
            ["ProgramCount"] = "{0} Programme",

            ["AdvancedSearch"] = "Erweiterte Suche",
            ["SearchFor"] = "Suchen nach:",
            ["Categories"] = "Gesucht wird",
            ["FileType"] = "Art",
            ["AnyType"] = "Alle Dateien",
            ["TypeDocuments"] = "Dokumente",
            ["TypePictures"] = "Bilder",
            ["TypeMusic"] = "Musik",
            ["TypeVideos"] = "Videos",
            ["TypeFolders"] = "Ordner",
            ["Location"] = "Suchen in",
            ["AnyLocation"] = "Überall",
            ["Modified"] = "Geändert",
            ["AnyTime"] = "Egal wann",
            ["Today"] = "Heute",
            ["ThisWeek"] = "Diese Woche",
            ["ThisMonth"] = "Dieser Monat",
            ["ThisYear"] = "Dieses Jahr",
            ["Back"] = "Zurück",
            ["Hits"] = "{0} Treffer",

            // ---- the places on the right ----
            ["Documents"] = "Eigene Dateien",
            ["RecentDocuments"] = "Zuletzt verwendete Dokumente",
            ["Pictures"] = "Eigene Bilder",
            ["Music"] = "Eigene Musik",
            ["Computer"] = "Arbeitsplatz",
            ["ControlPanel"] = "Systemsteuerung",
            ["SetProgramAccess"] = "Programmzugriff und -standards festlegen",
            ["Connections"] = "Verbindungen",
            ["RemoteDesktop"] = "Remotedesktop",
            ["SshConnection"] = "SSH-Verbindung…",
            ["SshPrompt"] = "Benutzer@Rechner:",
            ["SshTitle"] = "SSH-Verbindung",
            ["SshSubtitle"] = "Verbunden wird mit dem OpenSSH von Windows.",
            ["SshKey"] = "Schlüsseldatei:",
            ["SshKeyHint"] = "Schlüssel aus deinem .ssh-Ordner. Ohne einen fragt ssh nach dem Kennwort.",
            ["SshNoKey"] = "(keiner – Kennwort)",
            ["SshBrowse"] = "Schlüsseldatei wählen…",
            ["SshConnectButton"] = "Verbinden",
            ["SftpConnection"] = "SFTP-Verbindung…",
            ["TelnetConnection"] = "Telnet-Verbindung…",
            ["FtpConnection"] = "FTP-Verbindung…",
            ["NetworkFolder"] = "Netzwerkordner öffnen…",
            ["WslShell"] = "Linux (WSL)",
            ["ConnectPort"] = "Port:",
            ["HostPrompt"] = "Rechner:",
            ["FolderPrompt"] = "\\\\Server\\Freigabe:",
            ["ConnectSubtitle"] = "Den Client dafür bringt Windows selbst mit.",
            ["MapDrive"] = "Netzlaufwerk verbinden…",
            ["AllConnections"] = "Alle Verbindungen anzeigen",
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

            // ---- shutting down ----
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
            ["SwitchUser"] = "Benutzer wechseln",
            ["LogOffTitle"] = "Windows abmelden",
            ["Cancel"] = "Abbrechen",
            ["PowerQuestion"] = "Was soll der Computer tun?",

            // ---- right-click menu ----
            ["Open"] = "Öffnen",
            ["RunAsAdmin"] = "Als Administrator ausführen",
            ["Pin"] = "Zu Favoriten hinzufügen",
            ["Unpin"] = "Aus Favoriten entfernen",
            ["MoveToFolder"] = "In Ordner verschieben",
            ["NewFolder"] = "Neuer Ordner…",
            ["FolderDefaultName"] = "Ordner",
            ["OutOfFolder"] = "Aus dem Ordner heraus",
            ["RenameFolder"] = "Ordner umbenennen…",
            ["DissolveFolder"] = "Ordner auflösen",
            ["FolderNamePrompt"] = "Name des Ordners:",
            ["Ok"] = "OK",
            ["RemoveFromList"] = "Aus dieser Liste entfernen",
            ["OpenFileLocation"] = "Dateipfad öffnen",

            // ---- notification icon ----
            ["TrayOpen"] = "Startmenü öffnen",
            ["TraySettings"] = "Einstellungen…",
            ["TrayRefresh"] = "Programmliste neu einlesen",
            ["TrayExit"] = "Beenden",

            // ---- settings window ----
            ["SettingsTitle"] = "Retro-Menü – Einstellungen",
            ["Appearance"] = "Darstellung",
            ["Theme"] = "Design",
            ["FollowRetroBar"] = "Design von RetroBar übernehmen",
            ["Behaviour"] = "Verhalten",
            ["WinKey"] = "Windows-Taste",
            ["WinKeyNeutralize"] = "Abfangen (empfohlen)",
            ["WinKeySwallow"] = "Vollständig schlucken",
            ["WinKeyOff"] = "Nicht anfassen",
            ["Win11Menu"] = "Windows-11-Menü",
            ["Win11MenuWatch"] = "Unterdrücken (empfohlen)",
            ["Win11MenuKeep"] = "Behalten",
            ["FrequentCount"] = "Häufig verwendet: Anzahl",
            ["ShowRecent"] = "Zuletzt gestartete Programme zeigen",
            ["ShowTiles"] = "Favoriten als Kachelbereich rechts",
            ["ShowTilesHint"] = "Ein dritter Bereich zeigt die Favoriten und ihre Ordner als Kacheln, wie das Angeheftet-Raster von Windows 11.",
            ["TilesHeader"] = "Angeheftet",
            ["ShowRecentHint"] = "Statt der am häufigsten gestarteten steht dann in der linken Spalte, was zuletzt an der Reihe war.",
            ["ShowSearchBox"] = "Suchfeld anzeigen (hatte XP nicht)",
            ["SearchFilesSetting"] = "Suche schließt Dateien ein",
            ["MenuScale"] = "Menügröße",
            ["KeepTaskbar"] = "Taskleiste beim Öffnen einblenden",
            ["ShowStoreApps"] = "Store-Apps mit auflisten",
            ["UseXpExplorer"] = "Ordner im XP-Dateifenster öffnen",
            ["UseXpExplorerHint"] = "windows-xp-explorer-win-11 ist da und zeigt Eigene Dateien, "
                                    + "Arbeitsplatz und die anderen Orte im alten Anstrich.",
            ["UseXpExplorerMissing"] = "Nicht gefunden. Ohne windows-xp-explorer-win-11 öffnet "
                                       + "der Windows-Explorer die Ordner.",
            ["UseXpExplorerBroken"] = "Gefunden, aber die Installation ist unvollständig — die Ordner öffnet der Windows-Explorer.",
            ["AutoStart"] = "Mit Windows starten",
            ["Language"] = "Sprache",
            ["Close"] = "Schließen",
            ["StoreApps"] = "Store-Apps",

            // ---- the settings program ----
            ["SettingsSubtitle"] = "Alles, was das Startmenü zeigt — und ob es überhaupt erscheint.",
            ["PageGeneral"] = "Allgemein",
            ["PageAppearance"] = "Darstellung",
            ["PageMenu"] = "Startmenü",
            ["PagePlaces"] = "Rechte Spalte",
            ["PageSearch"] = "Suche",
            ["PageAdvanced"] = "Erweitert",

            ["UseRetroMenu"] = "Retro-Startmenü verwenden",
            ["UseRetroMenuHint"] = "Ausgeschaltet behält Windows 11 sein eigenes Startmenü: die "
                                   + "Windows-Taste wird nicht mehr angefasst und der Startknopf "
                                   + "von RetroBar auch nicht. Alles andere bleibt stehen, das "
                                   + "Wiedereinschalten ist also ein Klick.",
            ["MenuRunning"] = "Das Menü läuft.",
            ["MenuNotRunning"] = "Das Menü läuft gerade nicht.",
            ["MenuOff"] = "Ausgeschaltet — Windows 11 zeigt sein eigenes Startmenü.",
            ["StartMenuNow"] = "Jetzt starten",
            ["SwitchedOff"] = "ausgeschaltet",
            ["StartHeading"] = "Start",
            ["WinKeyHint"] = "Wie die Windows-Taste abgefangen wird. „Abfangen“ lässt alle "
                             + "Win+X-Tastenkürzel unberührt; „Schlucken“ ist für Rechner, auf "
                             + "denen das Windows-11-Menü trotzdem durchkommt.",
            ["Win11MenuHint"] = "Falls das Windows-11-Menü trotzdem aufgeht – über den Start-Knopf "
                             + "der Windows-Taskleiste, über Strg+Esc, über einem Fenster mit "
                             + "Administratorrechten oder weil Windows den Tastatur-Hook stillschweigend "
                             + "verworfen hat. „Unterdrücken“ schickt es dann sofort wieder weg und "
                             + "zeigt stattdessen dieses Menü; „Behalten“ lässt Windows 11 sein eigenes.",
            ["Win11MenuOffHint"] = "Solange die Windows-Taste nicht angefasst wird, bleibt auch das "
                             + "Windows-11-Menü unangetastet.",
            ["HookFailed"] = "Der Tastatur-Hook konnte nicht gesetzt werden. Die Windows-Taste "
                             + "öffnet weiter das Windows-11-Menü.",

            ["HeaderHeading"] = "Blaue Kopfzeile",
            ["ShowUserPicture"] = "Kontobild anzeigen",
            ["UserNameSetting"] = "Angezeigter Name",
            ["UserNameHint"] = "Leer heißt: der Name, unter dem Windows Sie kennt.",
            ["SoundHeading"] = "Klang",
            ["PlaySounds"] = "Beim Öffnen den Menüklang abspielen",
            ["PlaySoundsHint"] = "Fragt nach dem Klang „Menü öffnen“ Ihres eigenen Schemas — wo "
                                 + "keiner zugewiesen ist, bleibt es still.",

            ["PinnedHeading"] = "Angeheftete Programme",
            ["ShowFavourites"] = "Angeheftete Programme anzeigen",
            ["ShowFavouritesHint"] = "Ausgeschaltet stehen in der linken Spalte nur noch die "
                                     + "Internet- und E-Mail-Plätze über den häufig verwendeten "
                                     + "Programmen, so wie ein frisches XP aussah, bevor etwas "
                                     + "angeheftet war. Das Angeheftete bleibt gespeichert und "
                                     + "kommt mit dem Schalter zurück.",
            ["ColumnHeading"] = "Linke Spalte",
            ["ShowDefaultSlots"] = "Internet- und E-Mail-Platz anzeigen",
            ["ShowDefaultSlotsHint"] = "Die beiden Einträge ganz oben, die Ihren Standardbrowser "
                                       + "und Ihr Mailprogramm nennen.",
            ["ShowAllProgramsButton"] = "Schaltfläche „Alle Programme“ anzeigen",
            ["ShowSwitchUserButton"] = "Schaltfläche „Benutzer wechseln“ anzeigen",
            ["FooterHeading"] = "Untere Leiste",
            ["ShowSwitchUserButtonHint"] = "Wechselt zu einem anderen Konto, ohne dass Ihre Programme "
                                           + "geschlossen werden. Unter „Abmelden“ steht es in jedem Fall.",
            ["ForgetAll"] = "Liste der verwendeten Programme leeren",
            ["ForgetAllDone"] = "Die Liste ist leer. Sie füllt sich wieder, sobald Programme "
                                + "gestartet werden.",

            ["PlacesHeading"] = "Einträge der rechten Spalte",
            ["PlacesHint"] = "Was hier ausgeschaltet ist, verschwindet aus der rechten Spalte — "
                             + "samt einer Trennlinie, die sonst allein stehen bliebe.",
            ["SelectAll"] = "Alle",
            ["ControlPanelHeading"] = "Aus der Systemsteuerung",
            ["ControlPanelHint"] = "Was hier angehakt ist, kommt unten in der rechten Spalte "
                                   + "dazu, unter einer eigenen Trennlinie. Namen und Symbole "
                                   + "steuert Windows selbst bei.",
            ["ControlPanelEmpty"] = "Die Systemsteuerung konnte nicht gelesen werden.",
            ["SelectNone"] = "Keinen",

            ["HotkeyHeading"] = "Tastenkürzel",
            ["SearchHotkey"] = "Windows+S öffnet diese Suche",
            ["SearchHotkeyHint"] = "Nimmt der Windows-11-Suche die Tastenkombination ab. "
                                   + "Gilt auch, wenn die Windows-Taste selbst unangetastet "
                                   + "bleiben soll; ohne Suchfeld öffnet sich stattdessen die "
                                   + "große Suche.",
            ["SearchHeading"] = "Suchfeld",
            ["ShowSearchBoxHint"] = "Windows XP hatte keins; dieses findet Programme, "
                                    + "Windows-Einstellungen und auf Wunsch auch Dateien.",
            ["SearchFilesHint"] = "Nutzt den Suchindex von Windows und findet deshalb nur, was "
                                  + "Windows bereits erfasst hat.",
            ["ProgramsHeading"] = "Programmliste",
            ["ShowStoreAppsHint"] = "Apps aus dem Microsoft Store haben keine Verknüpfung auf der "
                                    + "Platte und stehen in einer eigenen Gruppe.",

            ["DesktopHeading"] = "Desktop",
            ["KeepTaskbarHint"] = "Eine automatisch ausgeblendete RetroBar fährt hoch, solange das "
                                  + "Menü offen ist — so wie Windows XP es gemacht hat.",
            ["ShowRunAsAdmin"] = "„Als Administrator ausführen“ im Rechtsklickmenü anbieten",

            ["FilesHeading"] = "Einstellungsdatei",
            ["OpenSettingsFolder"] = "Ordner öffnen",
            ["OpenLog"] = "Protokoll öffnen",
            ["ExportSettings"] = "Kopie sichern…",
            ["ImportSettings"] = "Kopie laden…",
            ["ResetSettings"] = "Zurücksetzen",
            ["ResetConfirm"] = "Alle Einstellungen wieder so setzen, wie sie ausgeliefert wurden? "
                               + "Angeheftete Programme und die Liste der verwendeten Programme "
                               + "bleiben erhalten.",
            ["ImportFailed"] = "Das ist keine Einstellungsdatei von Retro Menu.",
            ["SettingsFileFilter"] = "Einstellungen",
            ["AboutHeading"] = "Über",
            ["AboutText"] = "Ein Startmenü im Stil älterer Windows-Versionen, als Gegenstück zu "
                            + "RetroBar.",
            ["SettingsProgramMissing"] = "Das Einstellungsprogramm konnte nicht gestartet werden.",
            ["SettingsShortcut"] = "Retro Menu Einstellungen",

            // ---- picking a language ----
            ["LangAutoWindows"] = "Automatisch (Windows-Sprache)",
            ["LangAutoRetroBar"] = "Automatisch (RetroBar)",
            ["LangDetected"] = "Windows ist auf {0} eingestellt.",
            ["LangShowing"] = "Das Menü spricht {0}.",
            ["LangUntranslated"] = "Für {0} gibt es noch keine Übersetzung, deshalb springt Englisch ein.",
            ["RetroBarFound"] = "RetroBar gefunden – Design „{0}“ → „{1}“.",
            ["RetroBarMissing"] = "RetroBar wurde nicht gefunden.",

            // ---- setup wizard ----
            ["SetupTitle"] = "Retro Menu einrichten",
            ["SetupSubtitle"] = "Ein Startmenü im Stil älterer Windows-Versionen.",
            ["SetupIntro"] = "Das Programm wird in Ihren eigenen Benutzerordner gelegt. Es sind "
                             + "keine Administratorrechte nötig, und es wird nichts am System verändert.",
            ["SetupTarget"] = "Zielordner",
            ["SetupDuring"] = "Beim Einrichten",
            ["SetupStartMenu"] = "Verknüpfung im Startmenü anlegen",
            ["SetupDesktop"] = "Verknüpfung auf dem Desktop anlegen",
            ["SetupRetroBar"] = "RetroBar mitinstallieren – die Taskleiste im XP-Stil",
            ["SetupRetroBarNote"] = "Wird direkt von github.com/dremin/RetroBar geladen. Die "
                                    + "Prüfsumme des Downloads steht anschließend im Protokoll.",
            ["SetupRetroBarPresent"] = "RetroBar ist bereits vorhanden",
            ["SetupInstall"] = "Installieren",
            ["SetupUpdate"] = "Aktualisieren",
            ["SetupUpdateSubtitle"] = "Eine vorhandene Installation wird aktualisiert.",
            ["SetupRemoveTitle"] = "Retro Menu entfernen",
            ["SetupRemoveSubtitle"] = "Das Programm wird aus Ihrem Benutzerordner entfernt.",
            ["SetupRemoveButton"] = "Entfernen",
            ["SetupRemoveText"] = "Retro Menu wird beendet und aus {0} entfernt. Ihre Einstellungen "
                                  + "bleiben erhalten, damit eine spätere Installation sie wiederfindet.",
            ["SetupWorking"] = "Wird eingerichtet…",
            ["SetupRemoving"] = "Wird entfernt…",
            ["SetupDone"] = "Fertig.",
            ["SetupRemoved"] = "Entfernt.",
            ["SetupErrors"] = "Mit Fehlern beendet.",
            ["SetupFailed"] = "Fehlgeschlagen: {0}",
            ["SetupStart"] = "Starten",
        };
    }
}
