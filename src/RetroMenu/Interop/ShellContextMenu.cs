using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Text;

namespace RetroMenu.Interop
{
    internal sealed class ShellMenuEntry
    {
        public string Text;
        public uint Id;
        public bool IsSeparator;
        public bool IsEnabled = true;
        public List<ShellMenuEntry> Children = new List<ShellMenuEntry>();
        public bool HasChildren => Children.Count > 0;
    }

    [StructLayout(LayoutKind.Sequential)]
    internal struct CMINVOKECOMMANDINFOEX
    {
        public int cbSize;
        public int fMask;
        public IntPtr hwnd;
        public IntPtr lpVerb;
        public IntPtr lpParameters;
        public IntPtr lpDirectory;
        public int nShow;
        public int dwHotKey;
        public IntPtr hIcon;
        public IntPtr lpTitle;
        public IntPtr lpVerbW;
        public IntPtr lpParametersW;
        public IntPtr lpDirectoryW;
        public IntPtr lpTitleW;
        public NativeMethods.POINT ptInvoke;
    }

    /// <summary>
    /// The real Explorer context menu for a start menu entry — Open, Run as
    /// administrator, Send to, Cut, Copy, Delete, Rename, Properties and whatever
    /// shell extensions add. XP showed exactly this menu on its start menu items.
    ///
    /// The commands are read out of the shell's own HMENU and then drawn as ordinary
    /// WPF items, so the menu keeps the retro styling instead of appearing as a
    /// modern Windows popup.
    /// </summary>
    internal sealed class ShellContextMenu : IDisposable
    {
        private const uint IdFirst = 1;
        private const uint IdLast = 0x7FFF;

        private const uint CMF_NORMAL = 0x00000000;
        private const uint CMF_EXTENDEDVERBS = 0x00000100;

        private const uint MIIM_STATE = 0x00000001;
        private const uint MIIM_ID = 0x00000002;
        private const uint MIIM_SUBMENU = 0x00000004;
        private const uint MIIM_FTYPE = 0x00000100;

        private const uint MFT_SEPARATOR = 0x00000800;
        private const uint MFS_DISABLED = 0x00000003;
        private const uint MF_BYPOSITION = 0x00000400;

        private const uint WM_INITMENUPOPUP = 0x0117;

        private const int CMIC_MASK_UNICODE = 0x00004000;
        private const int SW_SHOWNORMAL = 1;

        private IntPtr _pidl;
        private IntPtr _menu;
        private IShellFolder _parent;
        private IContextMenu _contextMenu;
        private IContextMenu2 _contextMenu2;
        private IntPtr _owner;

        public List<ShellMenuEntry> Entries { get; } = new List<ShellMenuEntry>();

        /// <summary>Reads the shell menu for one file. Returns false if the shell declines.</summary>
        public bool Open(string path, IntPtr owner, bool extendedVerbs)
        {
            _owner = owner;

            try
            {
                if (SHParseDisplayName(path, IntPtr.Zero, out _pidl, 0, out _) != 0 || _pidl == IntPtr.Zero)
                    return false;

                Guid folderIid = ShellGuids.IShellFolder;
                if (SHBindToParent(_pidl, ref folderIid, out IntPtr folderPtr, out IntPtr childPidl) != 0
                    || folderPtr == IntPtr.Zero)
                    return false;

                _parent = (IShellFolder)Marshal.GetObjectForIUnknown(folderPtr);
                Marshal.Release(folderPtr);

                Guid menuIid = ShellGuids.IContextMenu;
                if (_parent.GetUIObjectOf(owner, 1, new[] { childPidl }, ref menuIid, IntPtr.Zero,
                        out IntPtr menuPtr) != 0 || menuPtr == IntPtr.Zero)
                    return false;

                _contextMenu = (IContextMenu)Marshal.GetObjectForIUnknown(menuPtr);
                Marshal.Release(menuPtr);
                _contextMenu2 = _contextMenu as IContextMenu2;

                _menu = CreatePopupMenu();
                if (_menu == IntPtr.Zero) return false;

                uint flags = CMF_NORMAL | (extendedVerbs ? CMF_EXTENDEDVERBS : 0);
                if (_contextMenu.QueryContextMenu(_menu, 0, IdFirst, IdLast, flags) < 0)
                    return false;

                Read(_menu, Entries, 0);
                return Entries.Count > 0;
            }
            catch
            {
                return false;
            }
        }

        private void Read(IntPtr menu, List<ShellMenuEntry> into, int depth)
        {
            int count = GetMenuItemCount(menu);
            if (count <= 0) return;

            for (int index = 0; index < count; index++)
            {
                var info = new MENUITEMINFO
                {
                    cbSize = (uint)Marshal.SizeOf<MENUITEMINFO>(),
                    fMask = MIIM_STATE | MIIM_ID | MIIM_SUBMENU | MIIM_FTYPE
                };
                if (!GetMenuItemInfo(menu, (uint)index, true, ref info)) continue;

                if ((info.fType & MFT_SEPARATOR) != 0)
                {
                    into.Add(new ShellMenuEntry { IsSeparator = true });
                    continue;
                }

                var buffer = new StringBuilder(512);
                GetMenuString(menu, (uint)index, buffer, buffer.Capacity, MF_BYPOSITION);
                string text = Clean(buffer.ToString());

                // Owner-drawn entries from shell extensions have no text to read.
                if (string.IsNullOrWhiteSpace(text)) continue;

                var entry = new ShellMenuEntry
                {
                    Text = text,
                    Id = info.wID,
                    IsEnabled = (info.fState & MFS_DISABLED) == 0
                };

                if (info.hSubMenu != IntPtr.Zero && depth < 2)
                {
                    // Submenus such as "Send to" only fill themselves once the shell
                    // has been told the popup is about to open.
                    try { _contextMenu2?.HandleMenuMsg(WM_INITMENUPOPUP, info.hSubMenu, (IntPtr)index); }
                    catch { }
                    Read(info.hSubMenu, entry.Children, depth + 1);
                    if (entry.Children.Count == 0) continue;
                }

                into.Add(entry);
            }
        }

        /// <summary>Drops the accelerator markers and the shortcut key column.</summary>
        private static string Clean(string text)
        {
            if (string.IsNullOrEmpty(text)) return text;

            int tab = text.IndexOf('\t');
            if (tab >= 0) text = text.Substring(0, tab);

            const string keep = "";
            return text.Replace("&&", keep).Replace("&", "").Replace(keep, "&").Trim();
        }

        public void Invoke(uint id)
        {
            if (_contextMenu == null || id < IdFirst) return;

            var invoke = new CMINVOKECOMMANDINFOEX
            {
                cbSize = Marshal.SizeOf<CMINVOKECOMMANDINFOEX>(),
                fMask = CMIC_MASK_UNICODE,
                hwnd = _owner,
                lpVerb = (IntPtr)(id - IdFirst),
                lpVerbW = (IntPtr)(id - IdFirst),
                nShow = SW_SHOWNORMAL
            };

            try { _contextMenu.InvokeCommand(ref invoke); }
            catch { }
        }

        public void Dispose()
        {
            if (_menu != IntPtr.Zero)
            {
                DestroyMenu(_menu);
                _menu = IntPtr.Zero;
            }

            if (_contextMenu != null)
            {
                Marshal.ReleaseComObject(_contextMenu);
                _contextMenu = null;
                _contextMenu2 = null;
            }

            if (_parent != null)
            {
                Marshal.ReleaseComObject(_parent);
                _parent = null;
            }

            if (_pidl != IntPtr.Zero)
            {
                CoTaskMemFree(_pidl);
                _pidl = IntPtr.Zero;
            }
        }

        // ---------------------------------------------------------------- interop

        [StructLayout(LayoutKind.Sequential)]
        private struct MENUITEMINFO
        {
            public uint cbSize;
            public uint fMask;
            public uint fType;
            public uint fState;
            public uint wID;
            public IntPtr hSubMenu;
            public IntPtr hbmpChecked;
            public IntPtr hbmpUnchecked;
            public IntPtr dwItemData;
            public IntPtr dwTypeData;
            public uint cch;
            public IntPtr hbmpItem;
        }

        [DllImport("shell32.dll", CharSet = CharSet.Unicode)]
        private static extern int SHParseDisplayName(string pszName, IntPtr pbc, out IntPtr ppidl,
            uint sfgaoIn, out uint psfgaoOut);

        [DllImport("shell32.dll")]
        private static extern int SHBindToParent(IntPtr pidl, ref Guid riid, out IntPtr ppv, out IntPtr ppidlLast);

        [DllImport("ole32.dll")]
        private static extern void CoTaskMemFree(IntPtr pv);

        [DllImport("user32.dll")]
        private static extern IntPtr CreatePopupMenu();

        [DllImport("user32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool DestroyMenu(IntPtr hMenu);

        [DllImport("user32.dll")]
        private static extern int GetMenuItemCount(IntPtr hMenu);

        [DllImport("user32.dll", CharSet = CharSet.Unicode, EntryPoint = "GetMenuStringW")]
        private static extern int GetMenuString(IntPtr hMenu, uint uIDItem, StringBuilder lpString,
            int nMaxCount, uint uFlag);

        [DllImport("user32.dll", CharSet = CharSet.Unicode, EntryPoint = "GetMenuItemInfoW")]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool GetMenuItemInfo(IntPtr hMenu, uint item,
            [MarshalAs(UnmanagedType.Bool)] bool fByPosition, ref MENUITEMINFO lpmii);
    }

    [ComImport, Guid("000214E6-0000-0000-C000-000000000046"),
     InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    internal interface IShellFolder
    {
        [PreserveSig] int ParseDisplayName(IntPtr hwnd, IntPtr pbc, IntPtr pszDisplayName,
            IntPtr pchEaten, out IntPtr ppidl, IntPtr pdwAttributes);
        [PreserveSig] int EnumObjects(IntPtr hwnd, int grfFlags, out IntPtr ppenumIDList);
        [PreserveSig] int BindToObject(IntPtr pidl, IntPtr pbc, ref Guid riid, out IntPtr ppv);
        [PreserveSig] int BindToStorage(IntPtr pidl, IntPtr pbc, ref Guid riid, out IntPtr ppv);
        [PreserveSig] int CompareIDs(IntPtr lParam, IntPtr pidl1, IntPtr pidl2);
        [PreserveSig] int CreateViewObject(IntPtr hwndOwner, ref Guid riid, out IntPtr ppv);
        [PreserveSig] int GetAttributesOf(uint cidl, IntPtr apidl, ref uint rgfInOut);
        [PreserveSig] int GetUIObjectOf(IntPtr hwndOwner, uint cidl,
            [In, MarshalAs(UnmanagedType.LPArray)] IntPtr[] apidl, ref Guid riid,
            IntPtr rgfReserved, out IntPtr ppv);
        [PreserveSig] int GetDisplayNameOf(IntPtr pidl, uint uFlags, IntPtr pName);
        [PreserveSig] int SetNameOf(IntPtr hwnd, IntPtr pidl, IntPtr pszName, uint uFlags, out IntPtr ppidlOut);
    }

    [ComImport, Guid("000214E4-0000-0000-C000-000000000046"),
     InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    internal interface IContextMenu
    {
        [PreserveSig] int QueryContextMenu(IntPtr hMenu, uint indexMenu, uint idCmdFirst,
            uint idCmdLast, uint uFlags);
        [PreserveSig] int InvokeCommand(ref CMINVOKECOMMANDINFOEX pici);
        [PreserveSig] int GetCommandString(UIntPtr idCmd, uint uType, IntPtr pReserved,
            IntPtr pszName, uint cchMax);
    }

    [ComImport, Guid("000214F4-0000-0000-C000-000000000046"),
     InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    internal interface IContextMenu2
    {
        [PreserveSig] int QueryContextMenu(IntPtr hMenu, uint indexMenu, uint idCmdFirst,
            uint idCmdLast, uint uFlags);
        [PreserveSig] int InvokeCommand(ref CMINVOKECOMMANDINFOEX pici);
        [PreserveSig] int GetCommandString(UIntPtr idCmd, uint uType, IntPtr pReserved,
            IntPtr pszName, uint cchMax);
        [PreserveSig] int HandleMenuMsg(uint uMsg, IntPtr wParam, IntPtr lParam);
    }
}
