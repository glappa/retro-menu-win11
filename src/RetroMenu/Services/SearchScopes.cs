using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.InteropServices;
using RetroMenu.Interop;

namespace RetroMenu.Services
{
    /// <summary>One entry of the "look in" list: what it is called and where it is.</summary>
    public sealed class SearchScope
    {
        public SearchScope(string name, string path)
        {
            Name = name;
            Path = path;
        }

        public string Name { get; }

        /// <summary>Null means the whole index.</summary>
        public string Path { get; }

        public override string ToString() => Name;
    }

    /// <summary>
    /// The places the advanced search can be pointed at. The names come from the
    /// shell itself rather than from a string table: Windows already knows what it
    /// calls Downloads in every language it ships, and the folders are renamed by
    /// some users anyway.
    /// </summary>
    public static class SearchScopes
    {
        public static List<SearchScope> Folders()
        {
            var found = new List<SearchScope>();

            void Add(string path)
            {
                if (string.IsNullOrWhiteSpace(path)) return;
                try { if (!Directory.Exists(path)) return; } catch { return; }

                foreach (var known in found)
                    if (string.Equals(known.Path, path, StringComparison.OrdinalIgnoreCase)) return;

                found.Add(new SearchScope(DisplayName(path), path));
            }

            Add(Folder(Environment.SpecialFolder.UserProfile));
            Add(Folder(Environment.SpecialFolder.Desktop));
            Add(Folder(Environment.SpecialFolder.MyDocuments));
            Add(Downloads());
            Add(Folder(Environment.SpecialFolder.MyPictures));
            Add(Folder(Environment.SpecialFolder.MyMusic));
            Add(Folder(Environment.SpecialFolder.MyVideos));

            return found;
        }

        private static string Folder(Environment.SpecialFolder which)
        {
            try { return Environment.GetFolderPath(which); }
            catch { return null; }
        }

        private static string Downloads()
        {
            IntPtr buffer = IntPtr.Zero;
            try
            {
                Guid id = NativeMethods.FolderIdDownloads;
                if (NativeMethods.SHGetKnownFolderPath(ref id, 0, IntPtr.Zero, out buffer) != 0)
                    return null;
                return Marshal.PtrToStringUni(buffer);
            }
            catch { return null; }
            finally { if (buffer != IntPtr.Zero) Marshal.FreeCoTaskMem(buffer); }
        }

        /// <summary>What the shell calls this folder, falling back to its own name.</summary>
        private static string DisplayName(string path)
        {
            try
            {
                var info = new NativeMethods.SHFILEINFO();
                if (NativeMethods.SHGetFileInfo(path, 0, ref info,
                        (uint)Marshal.SizeOf<NativeMethods.SHFILEINFO>(),
                        NativeMethods.SHGFI_DISPLAYNAME) != IntPtr.Zero
                    && !string.IsNullOrWhiteSpace(info.szDisplayName))
                    return info.szDisplayName;
            }
            catch { }

            try { return new DirectoryInfo(path).Name; }
            catch { return path; }
        }
    }
}
