using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Win32;

namespace RetroMenu.Services
{
    public sealed class AppSettings
    {
        private const string RunKey = @"Software\Microsoft\Windows\CurrentVersion\Run";
        private const string RunValue = "RetroMenu";

        private static readonly JsonSerializerOptions JsonOptions = new JsonSerializerOptions
        {
            WriteIndented = true,
            DefaultIgnoreCondition = JsonIgnoreCondition.Never
        };

        public static string Folder => Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "RetroMenuWin11");

        public static string FilePath => Path.Combine(Folder, "settings.json");

        public static AppSettings Instance { get; private set; } = new AppSettings();

        // ---- persisted state ----

        /// <summary>
        /// The master switch, and the only setting the whole program hangs on.
        /// Off leaves Windows 11 exactly as it was: the Windows key is not touched
        /// any more and the retro menu never opens. The program keeps running so
        /// the switch can be turned back on from the settings program.
        /// </summary>
        public bool Enabled { get; set; } = true;

        public string Theme { get; set; } = "Windows XP Blue";
        public bool FollowRetroBarTheme { get; set; } = true;
        /// <summary>
        /// "auto" follows the Windows display language, "auto-retrobar" follows
        /// RetroBar, anything else is a fixed tag from <see cref="Lang.Languages"/>.
        /// </summary>
        public string Language { get; set; } = "auto";
        public string WinKeyMode { get; set; } = "Neutralize"; // Neutralize | Swallow | Off

        /// <summary>
        /// What happens when the Windows 11 start menu comes up all the same —
        /// through the taskbar's own Start button, through Ctrl+Esc, above a
        /// window running as administrator, or because Windows quietly dropped
        /// our keyboard hook. "Watch" sends it away again and shows ours instead;
        /// "Off" leaves the Windows 11 menu to whoever wants to keep it.
        /// </summary>
        public string StartMenuGuard { get; set; } = "Watch"; // Watch | Off
        public int FrequentCount { get; set; } = 6;
        public bool ShowSearchBox { get; set; } = true;

        /// <summary>Search files through the Windows index as well as programs.</summary>
        public bool SearchFiles { get; set; } = false;

        /// <summary>
        /// Take Windows+S away from the Windows 11 search and open this menu's own
        /// search instead. Unlike the Windows key on its own, this holds even when
        /// <see cref="WinKeyMode"/> is set to leave that key alone.
        /// </summary>
        public bool SearchHotkey { get; set; } = true;

        /// <summary>
        /// The XP menu is 384 device pixels wide. On a big modern screen that can
        /// read as tiny, so the whole menu can be scaled without losing proportions.
        /// </summary>
        public double MenuScale { get; set; } = 1.0;
        /// <summary>
        /// Bring an auto-hidden RetroBar taskbar back up while the menu is open,
        /// the way Windows XP did.
        /// </summary>
        public bool KeepTaskbarVisible { get; set; } = true;

        /// <summary>Play the system "Menu popup" sound, as XP did.</summary>
        public bool PlaySounds { get; set; } = true;

        /// <summary>
        /// Fill the lower list on the left with the programs started most recently
        /// rather than the ones started most often.
        /// </summary>
        public bool ShowRecentPrograms { get; set; } = false;

        /// <summary>
        /// Show the favourites a second time as a panel of tiles on the right, the
        /// way Windows 11 lays its pinned apps out. Off keeps the classic width.
        /// </summary>
        public bool ShowTilePanel { get; set; } = false;

        /// <summary>
        /// Show the pinned programs at all. Off leaves the left column with the
        /// Internet and e-mail slots above the frequently used ones, the way a
        /// freshly installed XP looked before anything had been pinned.
        /// </summary>
        public bool ShowFavourites { get; set; } = true;

        /// <summary>The two special slots at the top: "Internet" and "E-mail".</summary>
        public bool ShowDefaultAppSlots { get; set; } = true;

        /// <summary>The "All Programs" button at the foot of the left column.</summary>
        public bool ShowAllProgramsButton { get; set; } = true;

        /// <summary>
        /// "Switch User" in the footer, next to Log Off. The Log Off panel offers
        /// it either way, as XP's did.
        /// </summary>
        public bool ShowSwitchUserButton { get; set; } = true;

        /// <summary>The account picture in the blue header, next to the name.</summary>
        public bool ShowUserPicture { get; set; } = true;

        /// <summary>
        /// Entries of the right hand column that are switched off, named by the
        /// key they are built with in <see cref="Launcher.BuildPlaces"/>. This is
        /// the list XP's "Customize Start Menu" put behind its Advanced tab.
        /// </summary>
        public List<string> HiddenPlaces { get; set; } = new List<string>();

        /// <summary>
        /// Applets of the Control Panel the user has added to that column, by the
        /// parsing name the shell knows them under. The other way round from
        /// <see cref="HiddenPlaces"/>: these are switched on, not off.
        /// </summary>
        public List<string> ExtraPlaces { get; set; } = new List<string>();

        /// <summary>
        /// Open folders in windows-xp-explorer-win-11 when it is installed, so the
        /// file window matches the menu instead of being the Windows 11 one.
        /// </summary>
        public bool UseXpExplorer { get; set; } = true;

        /// <summary>Where that file window is, if it sits somewhere unusual.</summary>
        public string XpExplorerPath { get; set; } = "";

        public bool ShowRunAsAdmin { get; set; } = true;
        public bool ShowStoreApps { get; set; } = true;
        public string UserName { get; set; } = "";

        /// <summary>Set once the first-run pins have been taken over from RetroBar.</summary>
        public bool Seeded { get; set; }

        /// <summary>Older settings files kept a flat list; it is read once and converted.</summary>
        public List<string> Pinned { get; set; } = new List<string>();

        /// <summary>The favourites group, which may contain folders.</summary>
        public List<FavouriteEntry> Favourites { get; set; } = new List<FavouriteEntry>();
        public Dictionary<string, int> LaunchCounts { get; set; } = new Dictionary<string, int>();

        /// <summary>When each entry was last started, for the "recently used" list.</summary>
        public Dictionary<string, DateTime> LaunchTimes { get; set; } = new Dictionary<string, DateTime>();

        /// <summary>Everything the catalogue has seen, so new arrivals can be marked.</summary>
        public List<string> KnownPrograms { get; set; } = new List<string>();

        [JsonIgnore]
        public bool AutoStart
        {
            get
            {
                using var key = Registry.CurrentUser.OpenSubKey(RunKey);
                return key?.GetValue(RunValue) != null;
            }
            set
            {
                using var key = Registry.CurrentUser.CreateSubKey(RunKey);
                if (key == null) return;
                if (value)
                {
                    // Assembly.Location is empty in a single-file build, so this has
                    // to come from the process itself.
                    string exe = Environment.ProcessPath;
                    if (!string.IsNullOrEmpty(exe)) key.SetValue(RunValue, "\"" + exe + "\"");
                }
                else
                {
                    key.DeleteValue(RunValue, false);
                }
            }
        }

        /// <summary>
        /// Reads the file again over the running instance. The settings program
        /// writes it from another process, and says so through
        /// <see cref="SettingsBridge"/>; this is what the menu does about it.
        /// </summary>
        public static void Reload() => Load();

        public static void Load()
        {
            try
            {
                if (File.Exists(FilePath))
                {
                    var loaded = JsonSerializer.Deserialize<AppSettings>(File.ReadAllText(FilePath), JsonOptions);
                    if (loaded != null)
                    {
                        loaded.Pinned ??= new List<string>();
                        loaded.LaunchCounts ??= new Dictionary<string, int>();
                        loaded.KnownPrograms ??= new List<string>();
                        loaded.LaunchTimes ??= new Dictionary<string, DateTime>();
                        loaded.Favourites ??= new List<FavouriteEntry>();
                        loaded.HiddenPlaces ??= new List<string>();
                        loaded.ExtraPlaces ??= new List<string>();

                        // Carry a flat pinned list from an older version over once,
                        // then let it go so the file does not keep two truths.
                        if (loaded.Favourites.Count == 0 && loaded.Pinned.Count > 0)
                        {
                            foreach (var id in loaded.Pinned)
                                loaded.Favourites.Add(new FavouriteEntry { Id = id });

                            loaded.Pinned.Clear();
                            loaded.Save();
                        }

                        Instance = loaded;
                    }
                }
            }
            catch
            {
                Instance = new AppSettings();
            }
        }

        public void Save()
        {
            try
            {
                Directory.CreateDirectory(Folder);
                File.WriteAllText(FilePath, JsonSerializer.Serialize(this, JsonOptions));
            }
            catch { /* a read-only profile must not take the menu down */ }
        }

        /// <summary>
        /// What the running menu keeps writing behind the settings program's back:
        /// pins, launch counts and the list of programs it has already seen.
        /// </summary>
        private static readonly HashSet<string> MenuOwned = new HashSet<string>
        {
            nameof(Pinned), nameof(Favourites), nameof(LaunchCounts),
            nameof(LaunchTimes), nameof(KnownPrograms), nameof(Seeded)
        };

        /// <summary>
        /// Writes the preferences without touching the lists above. The settings
        /// program runs in its own process, so between opening its window and
        /// saving, the menu may well have pinned a program or counted a launch —
        /// serialising our whole copy would quietly undo that. So the file is read
        /// again here and only the switches are carried over.
        /// </summary>
        public void SaveOptions()
        {
            var target = ReadFile() ?? new AppSettings();

            foreach (var property in typeof(AppSettings)
                         .GetProperties(BindingFlags.Public | BindingFlags.Instance))
            {
                if (!property.CanRead || !property.CanWrite) continue;
                if (MenuOwned.Contains(property.Name)) continue;
                // AutoStart lives in the registry, not in the file, and setting it
                // here would write it a second time for no reason.
                if (property.IsDefined(typeof(JsonIgnoreAttribute), true)) continue;

                property.SetValue(target, property.GetValue(this));
            }

            target.Save();
        }

        /// <summary>
        /// Empties the "frequently used" and "recently used" lists, which is the
        /// button XP had under Customize Start Menu. They belong to the menu, so
        /// they are cleared straight in the file and the menu is told to read it.
        /// </summary>
        public static void ClearLaunchHistory()
        {
            var target = ReadFile() ?? new AppSettings();
            target.LaunchCounts.Clear();
            target.LaunchTimes.Clear();
            target.Save();

            Instance.LaunchCounts.Clear();
            Instance.LaunchTimes.Clear();
        }

        /// <summary>
        /// Whether a file picked for import really is a settings file. Any JSON
        /// deserialises into this class without complaint, so a value that is only
        /// ever written by us has to answer for the rest.
        /// </summary>
        public static bool LooksLikeSettings(string path)
        {
            try
            {
                using var document = JsonDocument.Parse(File.ReadAllText(path));
                return document.RootElement.ValueKind == JsonValueKind.Object &&
                       document.RootElement.TryGetProperty(nameof(Theme), out _) &&
                       document.RootElement.TryGetProperty(nameof(WinKeyMode), out _);
            }
            catch { return false; }
        }

        private static AppSettings ReadFile()
        {
            try
            {
                if (!File.Exists(FilePath)) return null;
                return JsonSerializer.Deserialize<AppSettings>(File.ReadAllText(FilePath), JsonOptions);
            }
            catch { return null; }
        }

        // ---- favourites ----
        private static bool Same(string a, string b) =>
            string.Equals(a, b, StringComparison.OrdinalIgnoreCase);

        /// <summary>True whether the entry sits at the top level or inside a folder.</summary>
        public bool IsFavourite(string id)
        {
            if (string.IsNullOrEmpty(id)) return false;
            return Favourites.Any(f => f.IsFolder
                ? f.Items.Any(i => Same(i, id))
                : Same(f.Id, id));
        }

        /// <summary>The folder an entry sits in, or null when it is at the top level.</summary>
        public string FolderOf(string id)
        {
            if (string.IsNullOrEmpty(id)) return null;
            return Favourites.FirstOrDefault(f => f.IsFolder && f.Items.Any(i => Same(i, id)))?.Folder;
        }

        public IEnumerable<string> FolderNames =>
            Favourites.Where(f => f.IsFolder).Select(f => f.Folder);

        public void AddFavourite(string id)
        {
            if (string.IsNullOrEmpty(id) || IsFavourite(id)) return;
            Favourites.Add(new FavouriteEntry { Id = id });
            Save();
        }

        public void RemoveFavourite(string id)
        {
            if (string.IsNullOrEmpty(id)) return;
            Detach(id);
            PruneEmptyFolders();
            Save();
        }

        /// <summary>Takes an entry out of wherever it currently sits.</summary>
        private void Detach(string id)
        {
            Favourites.RemoveAll(f => !f.IsFolder && Same(f.Id, id));
            foreach (var folder in Favourites.Where(f => f.IsFolder))
                folder.Items.RemoveAll(i => Same(i, id));
        }

        private void PruneEmptyFolders() =>
            Favourites.RemoveAll(f => f.IsFolder && f.Items.Count == 0);

        public void MoveToFolder(string id, string folderName)
        {
            if (string.IsNullOrEmpty(id) || string.IsNullOrWhiteSpace(folderName)) return;

            Detach(id);
            var folder = Favourites.FirstOrDefault(f => f.IsFolder && Same(f.Folder, folderName));
            if (folder == null)
            {
                folder = new FavouriteEntry { Folder = folderName.Trim() };
                Favourites.Add(folder);
            }
            folder.Items.Add(id);

            PruneEmptyFolders();
            Save();
        }

        public void MoveOutOfFolder(string id)
        {
            if (string.IsNullOrEmpty(id)) return;
            if (FolderOf(id) == null) return;

            Detach(id);
            Favourites.Add(new FavouriteEntry { Id = id });
            PruneEmptyFolders();
            Save();
        }

        public void RenameFolder(string oldName, string newName)
        {
            if (string.IsNullOrWhiteSpace(newName)) return;
            var folder = Favourites.FirstOrDefault(f => f.IsFolder && Same(f.Folder, oldName));
            if (folder == null) return;
            folder.Folder = newName.Trim();
            Save();
        }

        /// <summary>Empties a folder back into the top level and drops it.</summary>
        public void DissolveFolder(string name)
        {
            int at = Favourites.FindIndex(f => f.IsFolder && Same(f.Folder, name));
            if (at < 0) return;

            var folder = Favourites[at];
            Favourites.RemoveAt(at);
            for (int i = 0; i < folder.Items.Count; i++)
                Favourites.Insert(at + i, new FavouriteEntry { Id = folder.Items[i] });

            Save();
        }

        /// <summary>
        /// Where a top level entry sits, so a drop can be put before or after it.
        /// Entries inside a folder have no place of their own and answer -1.
        /// </summary>
        public int IndexOfFavourite(string id)
        {
            if (string.IsNullOrEmpty(id)) return -1;
            return Favourites.FindIndex(f => !f.IsFolder && Same(f.Id, id));
        }

        public int IndexOfFolder(string name)
        {
            if (string.IsNullOrEmpty(name)) return -1;
            return Favourites.FindIndex(f => f.IsFolder && Same(f.Folder, name));
        }

        /// <summary>
        /// Carries a pinned program to another place in the row. One that sits in a
        /// folder is lifted out of it on the way: that is how it is dragged back out.
        /// </summary>
        public void ReorderFavourite(string id, int to)
        {
            int from = IndexOfFavourite(id);
            if (from >= 0)
            {
                Move(from, to);
                return;
            }

            if (to < 0 || FolderOf(id) == null) return;

            // Remember the entry it is to stand in front of rather than the number:
            // the folder it leaves may be left empty and drop out of the row.
            var before = to < Favourites.Count ? Favourites[to] : null;

            Detach(id);
            int at = before == null ? Favourites.Count : Favourites.IndexOf(before);
            Favourites.Insert(at, new FavouriteEntry { Id = id });

            PruneEmptyFolders();
            Save();
        }

        public void ReorderFolder(string name, int to) => Move(IndexOfFolder(name), to);

        private void Move(int from, int to)
        {
            if (from < 0 || to < 0) return;

            var entry = Favourites[from];
            Favourites.RemoveAt(from);

            // Everything behind the entry has just moved up one place.
            if (to > from) to--;

            Favourites.Insert(Math.Max(0, Math.Min(to, Favourites.Count)), entry);
            Save();
        }

        /// <summary>
        /// Dropping one pinned program on another makes a folder of the two, where
        /// the one that was dropped on stood. That is the grouping gesture of the
        /// Windows 11 menu, and it needs no dialog: the folder can be renamed
        /// afterwards from its own right-click menu.
        /// </summary>
        public string GroupInto(string targetId, string droppedId, string folderName)
        {
            if (string.IsNullOrEmpty(targetId) || string.IsNullOrEmpty(droppedId)) return null;
            if (Same(targetId, droppedId)) return null;

            var folder = new FavouriteEntry { Folder = UnusedFolderName(folderName) };
            folder.Items.Add(targetId);
            folder.Items.Add(droppedId);

            // Take the dropped one out first: it may have been sitting in front of
            // the target, and the place to put the folder is only settled after.
            Detach(droppedId);

            int at = IndexOfFavourite(targetId);
            if (at < 0) at = Favourites.Count;
            else Favourites.RemoveAt(at);

            Favourites.Insert(Math.Min(at, Favourites.Count), folder);
            PruneEmptyFolders();
            Save();
            return folder.Folder;
        }

        /// <summary>"Folder", "Folder 2", "Folder 3" — the first one still free.</summary>
        public string UnusedFolderName(string wanted)
        {
            string name = string.IsNullOrWhiteSpace(wanted) ? "Folder" : wanted.Trim();
            if (IndexOfFolder(name) < 0) return name;

            for (int number = 2; number < 1000; number++)
            {
                string candidate = name + " " + number;
                if (IndexOfFolder(candidate) < 0) return candidate;
            }
            return name;
        }

        public void RegisterLaunch(string id)
        {
            if (string.IsNullOrEmpty(id)) return;

            // Installers, uninstallers and help files never belonged in XP's list.
            if (!MfuFilter.ShouldRemember(id)) return;
            LaunchCounts.TryGetValue(id, out int count);
            LaunchCounts[id] = count + 1;
            LaunchTimes[id] = DateTime.UtcNow;

            // Keep the file from growing without bound.
            if (LaunchCounts.Count > 400)
            {
                LaunchCounts = LaunchCounts.OrderByDescending(p => p.Value).Take(200)
                    .ToDictionary(p => p.Key, p => p.Value);
            }

            if (LaunchTimes.Count > 400)
            {
                LaunchTimes = LaunchTimes.OrderByDescending(p => p.Value).Take(200)
                    .ToDictionary(p => p.Key, p => p.Value);
            }

            Save();
        }

        public void ForgetLaunch(string id)
        {
            if (id == null) return;
            bool changed = LaunchCounts.Remove(id);
            changed |= LaunchTimes.Remove(id);
            if (changed) Save();
        }

        public IEnumerable<string> MostUsed(int count) =>
            LaunchCounts.OrderByDescending(p => p.Value).ThenBy(p => p.Key)
                        .Take(count).Select(p => p.Key);

        /// <summary>
        /// Most recently started first. Entries carried over from before this was
        /// recorded have no time, so the frequently used ones fill up the rest and
        /// the list is never emptier than it used to be.
        /// </summary>
        public IEnumerable<string> MostRecent(int count)
        {
            var recent = LaunchTimes.OrderByDescending(p => p.Value)
                                    .Take(count).Select(p => p.Key).ToList();

            if (recent.Count >= count) return recent;

            var seen = new HashSet<string>(recent, StringComparer.OrdinalIgnoreCase);
            foreach (var id in MostUsed(count * 2))
            {
                if (recent.Count >= count) break;
                if (seen.Add(id)) recent.Add(id);
            }
            return recent;
        }
    }
}
