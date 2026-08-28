using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;

namespace RetroMenu.Interop
{
    internal sealed class ShellFolderEntry
    {
        public string Name;

        /// <summary>Relative parsing name, e.g. an AppUserModelID under AppsFolder.</summary>
        public string Relative;

        /// <summary>Full parsing name the shell can resolve again.</summary>
        public string ParsingName;

        /// <summary>Packaged (Store/UWP) apps carry a "!" in their AppUserModelID.</summary>
        public bool IsPackaged => Relative != null && Relative.Contains("!");
    }

    /// <summary>
    /// Enumerates a shell namespace folder. Used for the virtual "Applications"
    /// folder where Store apps live, for Recent Documents and for the network
    /// connections behind XP's "Connect To".
    /// </summary>
    internal static class ShellFolder
    {
        public static List<ShellFolderEntry> Enumerate(string folderParsingName, int max = 400)
        {
            var result = new List<ShellFolderEntry>();

            Guid iidItem = ShellGuids.IShellItem;
            if (NativeMethods.SHCreateItemFromParsingName(folderParsingName, IntPtr.Zero, ref iidItem, out object root) != 0
                || root == null)
                return result;

            var folder = (IShellItem)root;
            IEnumShellItems enumerator = null;

            try
            {
                Guid bhid = ShellGuids.BHID_EnumItems;
                Guid iidEnum = ShellGuids.IEnumShellItems;
                folder.BindToHandler(IntPtr.Zero, ref bhid, ref iidEnum, out object handler);
                enumerator = handler as IEnumShellItems;
                if (enumerator == null) return result;

                var buffer = new IShellItem[1];
                while (result.Count < max && enumerator.Next(1, buffer, out uint fetched) == 0 && fetched == 1)
                {
                    var item = buffer[0];
                    buffer[0] = null;
                    if (item == null) continue;

                    try
                    {
                        item.GetDisplayName(Sigdn.NORMALDISPLAY, out string name);
                        item.GetDisplayName(Sigdn.PARENTRELATIVEPARSING, out string relative);
                        if (string.IsNullOrWhiteSpace(name) || string.IsNullOrWhiteSpace(relative)) continue;

                        string full;
                        try
                        {
                            item.GetDisplayName(Sigdn.DESKTOPABSOLUTEPARSING, out full);
                        }
                        catch
                        {
                            full = folderParsingName.TrimEnd('\\') + "\\" + relative;
                        }

                        result.Add(new ShellFolderEntry
                        {
                            Name = name,
                            Relative = relative,
                            ParsingName = string.IsNullOrWhiteSpace(full)
                                ? folderParsingName.TrimEnd('\\') + "\\" + relative
                                : full
                        });
                    }
                    catch { }
                    finally { Marshal.ReleaseComObject(item); }
                }
            }
            catch { }
            finally
            {
                if (enumerator != null) Marshal.ReleaseComObject(enumerator);
                Marshal.ReleaseComObject(folder);
            }

            return result;
        }

        /// <summary>The Store apps, which have no shortcut on disk.</summary>
        public static List<ShellFolderEntry> Apps()
        {
            var entries = Enumerate("shell:AppsFolder", 2000);
            foreach (var entry in entries)
                entry.ParsingName = "shell:AppsFolder\\" + entry.Relative;
            return entries;
        }
    }
}
