using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using RetroMenu.Model;

namespace RetroMenu.Services
{
    /// <summary>
    /// Walks the disk for names the Windows index does not know.
    ///
    /// The index only holds what Windows was told to index, which is essentially the
    /// user profile: Program Files, a Steam library on a second drive and anything
    /// else installed outside the profile is invisible to it — cs2.exe among them.
    /// This walks the file system itself, breadth first, starting with the places
    /// programs actually live, and gives up after a fixed time rather than turning
    /// into a full disk scan.
    /// </summary>
    public static class DiskSearch
    {
        /// <summary>Folders that only ever cost time: system innards and recycle bins.</summary>
        private static readonly string[] Skip =
        {
            "$recycle.bin", "system volume information", "winsxs", "servicing",
            "assembly", "installer", "driverstore", "windowsapps", "config.msi",
            "$windows.~bt", "$windows.~ws", "recovery",
        };

        /// <summary>
        /// Every match it finds within the budget, best places first. Runs on a
        /// background thread; <paramref name="cancel"/> stops it at once.
        /// </summary>
        public static List<StartItem> Find(string term, FileFilter filter, int max,
                                           TimeSpan budget, CancellationToken cancel,
                                           Action<List<StartItem>> report = null)
        {
            var found = new List<StartItem>();
            if (string.IsNullOrWhiteSpace(term) || term.Trim().Length < 2) return found;

            term = term.Trim();
            DateTime until = DateTime.UtcNow + budget;

            // A walk can take its whole budget, so what is already there is handed
            // over as it goes rather than in one lump at the end.
            DateTime nextReport = DateTime.UtcNow.AddMilliseconds(700);
            int reported = 0;

            void Report(bool force)
            {
                if (report == null || found.Count == reported) return;
                if (!force && DateTime.UtcNow < nextReport) return;

                reported = found.Count;
                nextReport = DateTime.UtcNow.AddMilliseconds(700);
                try { report(new List<StartItem>(found)); } catch { }
            }
            DateTime? changedAfter = filter?.Within == null
                ? (DateTime?)null
                : DateTime.Now - filter.Within.Value;

            var queue = new Queue<string>();
            var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            foreach (string start in Roots(filter?.Folder))
                if (seen.Add(start)) queue.Enqueue(start);

            while (queue.Count > 0 && found.Count < max && DateTime.UtcNow < until)
            {
                if (cancel.IsCancellationRequested) break;
                string folder = queue.Dequeue();

                // Files of this folder first: a hit near the top is worth more than
                // a complete sweep of one branch.
                try
                {
                    foreach (string file in Directory.EnumerateFiles(folder))
                    {
                        if (found.Count >= max || DateTime.UtcNow >= until) break;
                        if (cancel.IsCancellationRequested) break;

                        string name = Path.GetFileName(file);
                        if (name.IndexOf(term, StringComparison.OrdinalIgnoreCase) < 0) continue;
                        if (!Matches(file, name, filter, changedAfter, false)) continue;

                        found.Add(Entry(name, file));
                    }
                }
                catch { /* no rights, or the folder went away mid-walk */ }

                Report(false);

                try
                {
                    foreach (string child in Directory.EnumerateDirectories(folder))
                    {
                        if (cancel.IsCancellationRequested) break;

                        string name = Path.GetFileName(child);
                        if (Skip.Contains(name.ToLowerInvariant())) continue;

                        try
                        {
                            var info = new DirectoryInfo(child);
                            // Junctions and symlinks lead in circles.
                            if ((info.Attributes & FileAttributes.ReparsePoint) != 0) continue;
                        }
                        catch { continue; }

                        if (found.Count < max &&
                            name.IndexOf(term, StringComparison.OrdinalIgnoreCase) >= 0 &&
                            Matches(child, name, filter, changedAfter, true))
                            found.Add(Entry(name, child));

                        if (seen.Add(child)) queue.Enqueue(child);
                    }
                }
                catch { }

                Report(false);
            }

            if (!cancel.IsCancellationRequested) Report(true);
            return found;
        }

        private static StartItem Entry(string name, string path) => new StartItem
        {
            Name = name,
            Subtext = Path.GetDirectoryName(path),
            ParsingName = path,
            Target = path,
            Kind = StartItemKind.Shortcut
        };

        /// <summary>
        /// Where to start looking. A chosen folder wins; otherwise the places
        /// programs are installed come first, then the profile, then whole drives.
        /// </summary>
        private static IEnumerable<string> Roots(string chosen)
        {
            if (!string.IsNullOrWhiteSpace(chosen))
            {
                yield return chosen;
                yield break;
            }

            foreach (string steam in SteamLibraries()) yield return steam;

            foreach (var special in new[]
                     {
                         Environment.SpecialFolder.ProgramFiles,
                         Environment.SpecialFolder.ProgramFilesX86,
                         Environment.SpecialFolder.UserProfile,
                         Environment.SpecialFolder.CommonApplicationData,
                     })
            {
                string path = null;
                try { path = Environment.GetFolderPath(special); } catch { }
                if (!string.IsNullOrEmpty(path)) yield return path;
            }

            DriveInfo[] drives;
            try { drives = DriveInfo.GetDrives(); }
            catch { yield break; }

            foreach (var drive in drives)
            {
                string root = null;
                try
                {
                    if (drive.DriveType != DriveType.Fixed || !drive.IsReady) continue;
                    root = drive.RootDirectory.FullName;
                }
                catch { }
                if (!string.IsNullOrEmpty(root)) yield return root;
            }
        }

        /// <summary>
        /// Steam keeps its libraries in a text file next to itself; games on a second
        /// drive are the classic case of something the index never sees.
        /// </summary>
        private static IEnumerable<string> SteamLibraries()
        {
            var roots = new List<string>();

            foreach (var special in new[]
                     {
                         Environment.SpecialFolder.ProgramFilesX86,
                         Environment.SpecialFolder.ProgramFiles,
                     })
            {
                string vdf = null;
                try
                {
                    string steam = Path.Combine(Environment.GetFolderPath(special), "Steam");
                    vdf = Path.Combine(steam, "steamapps", "libraryfolders.vdf");
                    if (!File.Exists(vdf)) continue;
                }
                catch { continue; }

                try
                {
                    foreach (string line in File.ReadLines(vdf))
                    {
                        // "path"		"D:\\SteamLibrary"
                        int mark = line.IndexOf("\"path\"", StringComparison.OrdinalIgnoreCase);
                        if (mark < 0) continue;

                        int open = line.IndexOf('"', mark + 6);
                        if (open < 0) continue;
                        int close = line.IndexOf('"', open + 1);
                        if (close < 0) continue;

                        string path = line.Substring(open + 1, close - open - 1).Replace("\\\\", "\\");
                        string apps = Path.Combine(path, "steamapps", "common");
                        if (Directory.Exists(apps)) roots.Add(apps);
                        else if (Directory.Exists(path)) roots.Add(path);
                    }
                }
                catch { }
            }

            return roots;
        }

        /// <summary>The type and date filters, applied to what the walk turned up.</summary>
        private static bool Matches(string path, string name, FileFilter filter,
                                    DateTime? changedAfter, bool isFolder)
        {
            if (filter != null && !string.IsNullOrEmpty(filter.Kind))
            {
                if (filter.Kind == "folder")
                {
                    if (!isFolder) return false;
                }
                else
                {
                    if (isFolder) return false;
                    if (KindOf(Path.GetExtension(name)) != filter.Kind) return false;
                }
            }

            if (changedAfter.HasValue)
            {
                try
                {
                    DateTime changed = isFolder
                        ? Directory.GetLastWriteTime(path)
                        : File.GetLastWriteTime(path);
                    if (changed < changedAfter.Value) return false;
                }
                catch { return false; }
            }

            return true;
        }

        /// <summary>The same buckets the index sorts files into, by extension.</summary>
        private static string KindOf(string extension)
        {
            switch ((extension ?? string.Empty).ToLowerInvariant())
            {
                case ".doc": case ".docx": case ".odt": case ".rtf": case ".txt":
                case ".pdf": case ".md": case ".xls": case ".xlsx": case ".ods":
                case ".ppt": case ".pptx": case ".odp": case ".csv":
                    return "document";
                case ".jpg": case ".jpeg": case ".png": case ".gif": case ".bmp":
                case ".webp": case ".tif": case ".tiff": case ".ico": case ".svg":
                case ".heic": case ".xcf": case ".psd":
                    return "picture";
                case ".mp3": case ".wav": case ".flac": case ".ogg": case ".m4a":
                case ".wma": case ".aac": case ".opus": case ".mid":
                    return "music";
                case ".mp4": case ".mkv": case ".avi": case ".mov": case ".wmv":
                case ".webm": case ".m4v": case ".mpg": case ".mpeg":
                    return "video";
                default:
                    return null;
            }
        }
    }
}
