using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;

namespace RetroMenu.Interop
{
    internal sealed class AppsFolderEntry
    {
        public string Name;
        public string Aumid;

        /// <summary>Packaged (Store/UWP) apps carry a "!" in their AppUserModelID.</summary>
        public bool IsPackaged => Aumid != null && Aumid.Contains("!");

        public string ParsingName => "shell:AppsFolder\\" + Aumid;
    }

    /// <summary>
    /// Reads the virtual "Applications" shell folder. This is where Store apps live —
    /// they have no shortcut on disk, so the classic Start Menu folders never see them.
    /// </summary>
    internal static class AppsFolder
    {
        public static List<AppsFolderEntry> Enumerate()
        {
            var result = new List<AppsFolderEntry>();

            Guid iidItem = ShellGuids.IShellItem;
            if (NativeMethods.SHCreateItemFromParsingName("shell:AppsFolder", IntPtr.Zero, ref iidItem, out object root) != 0
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
                while (enumerator.Next(1, buffer, out uint fetched) == 0 && fetched == 1)
                {
                    var item = buffer[0];
                    buffer[0] = null;
                    if (item == null) continue;

                    try
                    {
                        item.GetDisplayName(Sigdn.NORMALDISPLAY, out string name);
                        item.GetDisplayName(Sigdn.PARENTRELATIVEPARSING, out string aumid);
                        if (!string.IsNullOrWhiteSpace(name) && !string.IsNullOrWhiteSpace(aumid))
                            result.Add(new AppsFolderEntry { Name = name, Aumid = aumid });
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
    }
}
