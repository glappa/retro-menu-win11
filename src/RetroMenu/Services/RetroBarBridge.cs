using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;

namespace RetroMenu.Services
{
    /// <summary>
    /// Reads RetroBar's own settings file so the menu can follow its theme and
    /// language, and can seed its pinned list from RetroBar's Quick Launch order.
    /// Nothing is ever written back.
    /// </summary>
    public sealed class RetroBarBridge : IDisposable
    {
        public static string SettingsPath => Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "RetroBar", "settings.json");

        private FileSystemWatcher _watcher;

        public string Theme { get; private set; }
        public string Language { get; private set; }
        public bool AllowFontSmoothing { get; private set; } = true;
        public List<string> QuickLaunchOrder { get; private set; } = new List<string>();
        public bool IsPresent { get; private set; }

        /// <summary>Fires (debounced by the file system) when RetroBar rewrites its settings.</summary>
        public event Action Changed;

        public RetroBarBridge()
        {
            Read();
        }

        public void Read()
        {
            try
            {
                if (!File.Exists(SettingsPath))
                {
                    IsPresent = false;
                    return;
                }

                // RetroBar may be mid-write; a shared read avoids the occasional lock.
                using var stream = new FileStream(SettingsPath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
                using var document = JsonDocument.Parse(stream);
                var root = document.RootElement;

                Theme = root.TryGetProperty("Theme", out var theme) ? theme.GetString() : null;
                Language = root.TryGetProperty("Language", out var lang) ? lang.GetString() : null;
                AllowFontSmoothing = !root.TryGetProperty("AllowFontSmoothing", out var smoothing)
                                     || smoothing.ValueKind != JsonValueKind.False;

                var order = new List<string>();
                if (root.TryGetProperty("QuickLaunchOrder", out var quick) &&
                    quick.ValueKind == JsonValueKind.Array)
                {
                    foreach (var element in quick.EnumerateArray())
                    {
                        string value = element.GetString();
                        if (!string.IsNullOrWhiteSpace(value)) order.Add(value);
                    }
                }
                QuickLaunchOrder = order;
                IsPresent = true;
            }
            catch
            {
                IsPresent = false;
            }
        }

        public void Watch()
        {
            try
            {
                string folder = Path.GetDirectoryName(SettingsPath);
                if (folder == null || !Directory.Exists(folder)) return;

                _watcher = new FileSystemWatcher(folder, "settings.json")
                {
                    NotifyFilter = NotifyFilters.LastWrite | NotifyFilters.Size,
                    EnableRaisingEvents = true
                };
                _watcher.Changed += (_, __) =>
                {
                    string before = Theme + "|" + Language;
                    // RetroBar writes the file in more than one step.
                    System.Threading.Thread.Sleep(250);
                    Read();
                    if (before != Theme + "|" + Language) Changed?.Invoke();
                };
            }
            catch { /* watching is a nicety, not a requirement */ }
        }

        public void Dispose()
        {
            _watcher?.Dispose();
            _watcher = null;
        }
    }
}
