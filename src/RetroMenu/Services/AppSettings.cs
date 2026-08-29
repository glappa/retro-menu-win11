using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
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
        public string Theme { get; set; } = "Windows XP Blue";
        public bool FollowRetroBarTheme { get; set; } = true;
        public string Language { get; set; } = "auto";     // auto | de | en
        public string WinKeyMode { get; set; } = "Neutralize"; // Neutralize | Swallow | Off
        public int FrequentCount { get; set; } = 6;
        public bool ShowSearchBox { get; set; } = true;

        /// <summary>Search files through the Windows index as well as programs.</summary>
        public bool SearchFiles { get; set; } = false;

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
