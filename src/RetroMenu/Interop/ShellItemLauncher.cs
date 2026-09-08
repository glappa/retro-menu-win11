using System;
using System.Runtime.InteropServices;

namespace RetroMenu.Interop
{
    /// <summary>
    /// Opens a shell item by its parsing name.
    ///
    /// Handing such a name to explorer.exe only works for the items that happen to
    /// be folders. Several Control Panel applets are not — the Device Manager, for
    /// one, does nothing at all that way, on the command line just as much as from
    /// a menu. Giving ShellExecuteEx the item's ID list instead runs the same
    /// default verb the Control Panel itself runs, and then every applet opens.
    /// </summary>
    internal static class ShellItemLauncher
    {
        private const uint SEE_MASK_INVOKEIDLIST = 0x0000000C;   // includes _IDLIST
        private const uint SEE_MASK_NOASYNC = 0x00000100;
        private const int SW_SHOWNORMAL = 1;

        /// <summary>True when the shell took it on. Must run on an STA thread.</summary>
        public static bool Open(string parsingName)
        {
            if (string.IsNullOrWhiteSpace(parsingName)) return false;

            IntPtr pidl = IntPtr.Zero;
            try
            {
                if (SHParseDisplayName(parsingName, IntPtr.Zero, out pidl, 0, out _) != 0
                    || pidl == IntPtr.Zero)
                    return false;

                var info = new SHELLEXECUTEINFO
                {
                    cbSize = Marshal.SizeOf<SHELLEXECUTEINFO>(),
                    fMask = SEE_MASK_INVOKEIDLIST | SEE_MASK_NOASYNC,
                    lpIDList = pidl,
                    nShow = SW_SHOWNORMAL
                };

                return ShellExecuteEx(ref info);
            }
            catch { return false; }
            finally
            {
                if (pidl != IntPtr.Zero) CoTaskMemFree(pidl);
            }
        }

        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
        private struct SHELLEXECUTEINFO
        {
            public int cbSize;
            public uint fMask;
            public IntPtr hwnd;
            [MarshalAs(UnmanagedType.LPWStr)] public string lpVerb;
            [MarshalAs(UnmanagedType.LPWStr)] public string lpFile;
            [MarshalAs(UnmanagedType.LPWStr)] public string lpParameters;
            [MarshalAs(UnmanagedType.LPWStr)] public string lpDirectory;
            public int nShow;
            public IntPtr hInstApp;
            public IntPtr lpIDList;
            [MarshalAs(UnmanagedType.LPWStr)] public string lpClass;
            public IntPtr hkeyClass;
            public uint dwHotKey;
            public IntPtr hIcon;
            public IntPtr hProcess;
        }

        [DllImport("shell32.dll", CharSet = CharSet.Unicode)]
        private static extern int SHParseDisplayName(string pszName, IntPtr pbc, out IntPtr ppidl,
            uint sfgaoIn, out uint psfgaoOut);

        [DllImport("shell32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool ShellExecuteEx(ref SHELLEXECUTEINFO lpExecInfo);

        [DllImport("ole32.dll")]
        private static extern void CoTaskMemFree(IntPtr pv);
    }
}
