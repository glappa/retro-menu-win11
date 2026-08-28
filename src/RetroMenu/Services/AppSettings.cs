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
        /// <summary>Windows XP had no search box; off keeps the menu authentic.</summary>
        public bool ShowSearchBox { get; set; } = false;

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

        public bool ShowRunAsAdmin { get; set; } = true;
        public bool ShowStoreApps { get; set; } = true;
        public string UserName { get; set; } = "";

        /// <summary>Set once the first-run pins have been taken over from RetroBar.</summary>
        public bool Seeded { get; set; }

        public List<string> Pinned { get; set; } = new List<string>();
        public Dictionary<string, int> LaunchCounts { get; set; } = new Dictionary<string, int>();

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

        // ---- pinning and usage ----
        public bool IsPinned(string id) =>
            id != null && Pinned.Any(p => string.Equals(p, id, StringComparison.OrdinalIgnoreCase));

        public void Pin(string id)
        {
            if (string.IsNullOrEmpty(id) || IsPinned(id)) return;
            Pinned.Add(id);
            Save();
        }

        public void Unpin(string id)
        {
            if (string.IsNullOrEmpty(id)) return;
            Pinned.RemoveAll(p => string.Equals(p, id, StringComparison.OrdinalIgnoreCase));
            Save();
        }

        public void RegisterLaunch(string id)
        {
            if (string.IsNullOrEmpty(id)) return;
            LaunchCounts.TryGetValue(id, out int count);
            LaunchCounts[id] = count + 1;

            // Keep the file from growing without bound.
            if (LaunchCounts.Count > 400)
            {
                var keep = LaunchCounts.OrderByDescending(p => p.Value).Take(200)
                    .ToDictionary(p => p.Key, p => p.Value);
                LaunchCounts = keep;
            }
            Save();
        }

        public void ForgetLaunch(string id)
        {
            if (id != null && LaunchCounts.Remove(id)) Save();
        }

        public IEnumerable<string> MostUsed(int count) =>
            LaunchCounts.OrderByDescending(p => p.Value).ThenBy(p => p.Key)
                        .Take(count).Select(p => p.Key);
    }
}
