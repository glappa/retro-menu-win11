using System.Collections.Generic;

namespace RetroMenu.Services.Strings
{
    /// <summary>
    /// English — the reference table. Every other language is looked up here first
    /// when it is missing a key, so this file is the one that must stay complete.
    /// </summary>
    internal static class En
    {
        internal static readonly Dictionary<string, string> Table = new Dictionary<string, string>
        {
            // ---- menu and search ----
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

            ["ProgramFilter"] = "Filter:",
            ["ProgramGroups"] = "Groups",
            ["ProgramCount"] = "{0} programs",

            ["AdvancedSearch"] = "Advanced search",
            ["SearchFor"] = "Search for:",
            ["Categories"] = "Look for",
            ["FileType"] = "Type",
            ["AnyType"] = "All files",
            ["TypeDocuments"] = "Documents",
            ["TypePictures"] = "Pictures",
            ["TypeMusic"] = "Music",
            ["TypeVideos"] = "Videos",
            ["TypeFolders"] = "Folders",
            ["Location"] = "Look in",
            ["AnyLocation"] = "Everywhere",
            ["Modified"] = "Changed",
            ["AnyTime"] = "Any time",
            ["Today"] = "Today",
            ["ThisWeek"] = "This week",
            ["ThisMonth"] = "This month",
            ["ThisYear"] = "This year",
            ["Back"] = "Back",
            ["Hits"] = "{0} hits",

            // ---- the places on the right ----
            ["Documents"] = "My Documents",
            ["RecentDocuments"] = "My Recent Documents",
            ["Pictures"] = "My Pictures",
            ["Music"] = "My Music",
            ["Computer"] = "My Computer",
            ["ControlPanel"] = "Control Panel",
            ["SetProgramAccess"] = "Set Program Access and Defaults",
            ["Connections"] = "Connections",
            ["RemoteDesktop"] = "Remote Desktop",
            ["SshConnection"] = "SSH connection…",
            ["SshPrompt"] = "user@host:",
            ["SshTitle"] = "SSH connection",
            ["SshSubtitle"] = "Windows' own OpenSSH client does the connecting.",
            ["SshKey"] = "Key file:",
            ["SshKeyHint"] = "Keys found in your .ssh folder. Without one, ssh asks for the password.",
            ["SshNoKey"] = "(none – password)",
            ["SshBrowse"] = "Choose a key file…",
            ["SshConnectButton"] = "Connect",
            ["SftpConnection"] = "SFTP connection…",
            ["TelnetConnection"] = "Telnet connection…",
            ["FtpConnection"] = "FTP connection…",
            ["NetworkFolder"] = "Open network folder…",
            ["WslShell"] = "Linux (WSL)",
            ["ConnectPort"] = "Port:",
            ["HostPrompt"] = "Computer:",
            ["FolderPrompt"] = "\\\\server\\share:",
            ["ConnectSubtitle"] = "Windows brings the client for this along itself.",
            ["MapDrive"] = "Map network drive…",
            ["AllConnections"] = "Show all connections",
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

            // ---- shutting down ----
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

            // ---- right-click menu ----
            ["Open"] = "Open",
            ["RunAsAdmin"] = "Run as administrator",
            ["Pin"] = "Add to favourites",
            ["Unpin"] = "Remove from favourites",
            ["MoveToFolder"] = "Move to folder",
            ["NewFolder"] = "New folder…",
            ["FolderDefaultName"] = "Folder",
            ["OutOfFolder"] = "Move out of the folder",
            ["RenameFolder"] = "Rename folder…",
            ["DissolveFolder"] = "Dissolve folder",
            ["FolderNamePrompt"] = "Folder name:",
            ["Ok"] = "OK",
            ["RemoveFromList"] = "Remove from this list",
            ["OpenFileLocation"] = "Open file location",

            // ---- notification icon ----
            ["TrayOpen"] = "Open start menu",
            ["TraySettings"] = "Settings…",
            ["TrayRefresh"] = "Rescan programs",
            ["TrayExit"] = "Exit",

            // ---- settings window ----
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
            ["ShowRecent"] = "Show recently started programs",
            ["ShowTiles"] = "Favourites as a tile panel on the right",
            ["ShowTilesHint"] = "A third panel shows the favourites and their folders as tiles, the way Windows 11 lays out its pinned apps.",
            ["TilesHeader"] = "Pinned",
            ["ShowRecentHint"] = "The left column then lists what was started last rather than what was started most.",
            ["ShowSearchBox"] = "Show search box (XP had none)",
            ["SearchFilesSetting"] = "Search includes files",
            ["MenuScale"] = "Menu size",
            ["KeepTaskbar"] = "Raise the taskbar while open",
            ["ShowStoreApps"] = "List Store apps",
            ["UseXpExplorer"] = "Open folders in the XP file window",
            ["UseXpExplorerHint"] = "windows-xp-explorer-win-11 is installed and will show "
                                    + "My Documents, My Computer and the other places.",
            ["UseXpExplorerMissing"] = "Not found. Without windows-xp-explorer-win-11 the "
                                       + "Windows Explorer opens folders.",
            ["UseXpExplorerBroken"] = "Found, but the installation is incomplete, so the Windows Explorer opens folders.",
            ["AutoStart"] = "Start with Windows",
            ["Language"] = "Language",
            ["Close"] = "Close",
            ["StoreApps"] = "Store apps",

            // ---- the settings program ----
            ["SettingsSubtitle"] = "Everything the start menu shows, and whether it shows at all.",
            ["PageGeneral"] = "General",
            ["PageAppearance"] = "Appearance",
            ["PageMenu"] = "Start menu",
            ["PagePlaces"] = "Right column",
            ["PageSearch"] = "Search",
            ["PageAdvanced"] = "Advanced",

            ["UseRetroMenu"] = "Use the retro start menu",
            ["UseRetroMenuHint"] = "Switched off, Windows 11 keeps its own start menu: "
                                   + "the Windows key is not touched any more and neither is "
                                   + "RetroBar's Start button. Everything else stays as it is, "
                                   + "so switching it back on takes one click.",
            ["MenuRunning"] = "The menu is running.",
            ["MenuNotRunning"] = "The menu is not running.",
            ["MenuOff"] = "Switched off — Windows 11 shows its own start menu.",
            ["StartMenuNow"] = "Start now",
            ["SwitchedOff"] = "switched off",
            ["StartHeading"] = "Starting",
            ["WinKeyHint"] = "How the Windows key is caught. \"Intercept\" leaves every Win+X "
                             + "shortcut alone; \"Swallow\" is for machines where the Windows 11 "
                             + "menu still slips through.",

            ["HeaderHeading"] = "Blue header",
            ["ShowUserPicture"] = "Show the account picture",
            ["UserNameSetting"] = "Name shown",
            ["UserNameHint"] = "Empty means the name Windows knows you by.",
            ["SoundHeading"] = "Sound",
            ["PlaySounds"] = "Play the menu sound when opening",
            ["PlaySoundsHint"] = "Asks for the \"Menu popup\" sound of your own scheme, so nothing "
                                 + "is heard where nothing is assigned.",

            ["PinnedHeading"] = "Pinned programs",
            ["ShowFavourites"] = "Show pinned programs",
            ["ShowFavouritesHint"] = "Switched off, the left column holds the Internet and e-mail "
                                     + "slots above the frequently used programs, the way a fresh "
                                     + "XP looked before anything had been pinned. What is pinned "
                                     + "is kept and comes back with the switch.",
            ["ColumnHeading"] = "Left column",
            ["ShowDefaultSlots"] = "Show the Internet and e-mail slots",
            ["ShowDefaultSlotsHint"] = "The two entries at the very top, naming your default "
                                       + "browser and mail program.",
            ["ShowAllProgramsButton"] = "Show the \"All Programs\" button",
            ["ForgetAll"] = "Empty the list of used programs",
            ["ForgetAllDone"] = "The list is empty. It fills up again as programs are started.",

            ["PlacesHeading"] = "Entries in the right column",
            ["PlacesHint"] = "Everything switched off here disappears from the right hand column, "
                             + "along with any dividing line left standing on its own.",
            ["SelectAll"] = "All",
            ["SelectNone"] = "None",

            ["SearchHeading"] = "Search box",
            ["ShowSearchBoxHint"] = "Windows XP had none; this one searches programs, Windows "
                                    + "settings and, if you let it, files.",
            ["SearchFilesHint"] = "Uses the Windows search index, so it only finds what Windows "
                                  + "has already indexed.",
            ["ProgramsHeading"] = "Program list",
            ["ShowStoreAppsHint"] = "Apps from the Microsoft Store have no shortcut on disk and "
                                    + "are gathered into a group of their own.",

            ["DesktopHeading"] = "Desktop",
            ["KeepTaskbarHint"] = "An auto-hidden RetroBar comes up while the menu is open, the "
                                  + "way Windows XP did it.",
            ["ShowRunAsAdmin"] = "Offer \"Run as administrator\" on right-click",

            ["FilesHeading"] = "Settings file",
            ["OpenSettingsFolder"] = "Open folder",
            ["OpenLog"] = "Open log",
            ["ExportSettings"] = "Save a copy…",
            ["ImportSettings"] = "Load a copy…",
            ["ResetSettings"] = "Reset",
            ["ResetConfirm"] = "Put every setting back the way it came? Pinned programs and the "
                               + "list of used programs are kept.",
            ["ImportFailed"] = "That is not a Retro Menu settings file.",
            ["SettingsFileFilter"] = "Settings",
            ["AboutHeading"] = "About",
            ["AboutText"] = "A start menu in the style of older Windows versions, as a companion "
                            + "to RetroBar.",
            ["SettingsProgramMissing"] = "The settings program could not be started.",
            ["SettingsShortcut"] = "Retro Menu Settings",

            // ---- picking a language ----
            ["LangAutoWindows"] = "Automatic (Windows language)",
            ["LangAutoRetroBar"] = "Automatic (RetroBar)",
            ["LangDetected"] = "Windows is set to {0}.",
            ["LangShowing"] = "The menu speaks {0}.",
            ["LangUntranslated"] = "{0} has no translation yet, so English steps in.",
            ["RetroBarFound"] = "RetroBar found – theme \"{0}\" maps to \"{1}\".",
            ["RetroBarMissing"] = "RetroBar was not found.",

            // ---- setup wizard ----
            ["SetupTitle"] = "Set up Retro Menu",
            ["SetupSubtitle"] = "A start menu in the style of older Windows versions.",
            ["SetupIntro"] = "The program is placed in your own user folder. No administrator "
                             + "rights are needed and nothing in the system is changed.",
            ["SetupTarget"] = "Destination folder",
            ["SetupDuring"] = "While setting up",
            ["SetupStartMenu"] = "Create a shortcut in the Start menu",
            ["SetupDesktop"] = "Create a shortcut on the desktop",
            ["SetupRetroBar"] = "Install RetroBar as well – the XP-style taskbar",
            ["SetupRetroBarNote"] = "Downloaded straight from github.com/dremin/RetroBar. The "
                                    + "checksum of the download appears in the log afterwards.",
            ["SetupRetroBarPresent"] = "RetroBar is already installed",
            ["SetupInstall"] = "Install",
            ["SetupUpdate"] = "Update",
            ["SetupUpdateSubtitle"] = "An existing installation will be updated.",
            ["SetupRemoveTitle"] = "Remove Retro Menu",
            ["SetupRemoveSubtitle"] = "The program will be removed from your user folder.",
            ["SetupRemoveButton"] = "Remove",
            ["SetupRemoveText"] = "Retro Menu will be closed and removed from {0}. Your settings "
                                  + "are kept, so a later installation finds them again.",
            ["SetupWorking"] = "Setting up…",
            ["SetupRemoving"] = "Removing…",
            ["SetupDone"] = "Done.",
            ["SetupRemoved"] = "Removed.",
            ["SetupErrors"] = "Finished with errors.",
            ["SetupFailed"] = "Failed: {0}",
            ["SetupStart"] = "Start",
        };
    }
}
