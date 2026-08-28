using System;
using System.Runtime.InteropServices;

namespace RetroMenu.Interop
{
    /// <summary>
    /// Tells RetroBar that a start menu is open, so an auto-hidden taskbar slides
    /// back into view while the menu is up — the way Windows XP behaved.
    ///
    /// RetroBar polls for a start menu ten times a second and accepts three of them:
    /// the modern launcher, the Windows 7 shell (window class "DV2ControlHost") and
    /// Open-Shell (window class "OpenShell.CMenuContainer"). The last one is the
    /// path meant for replacement start menus, so we take it: while our menu is up
    /// we keep an empty, fully transparent, click-through window of that class over
    /// the menu's own rectangle. RetroBar sees it, keeps the bar visible, and even
    /// puts it on the right monitor for us.
    /// </summary>
    internal sealed class TaskbarPresence : IDisposable
    {
        private const string ClassName = "OpenShell.CMenuContainer";

        private const int WS_POPUP = unchecked((int)0x80000000);
        private const int WS_EX_LAYERED = 0x00080000;
        private const int WS_EX_TRANSPARENT = 0x00000020;
        private const int WS_EX_NOACTIVATE = 0x08000000;
        private const int WS_EX_TOOLWINDOW = 0x00000080;

        private const int SW_HIDE = 0;
        private const int SW_SHOWNOACTIVATE = 4;

        private const uint SWP_NOACTIVATE = 0x0010;
        private const uint SWP_NOZORDER = 0x0004;
        private const uint LWA_ALPHA = 0x00000002;

        private delegate IntPtr WndProc(IntPtr hWnd, uint msg, IntPtr wParam, IntPtr lParam);

        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
        private struct WNDCLASSEX
        {
            public uint cbSize;
            public uint style;
            public IntPtr lpfnWndProc;
            public int cbClsExtra;
            public int cbWndExtra;
            public IntPtr hInstance;
            public IntPtr hIcon;
            public IntPtr hCursor;
            public IntPtr hbrBackground;
            public string lpszMenuName;
            public string lpszClassName;
            public IntPtr hIconSm;
        }

        [DllImport("user32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
        private static extern ushort RegisterClassEx(ref WNDCLASSEX lpwcx);

        [DllImport("user32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
        private static extern IntPtr CreateWindowEx(int dwExStyle, string lpClassName, string lpWindowName,
            int dwStyle, int x, int y, int nWidth, int nHeight,
            IntPtr hWndParent, IntPtr hMenu, IntPtr hInstance, IntPtr lpParam);

        [DllImport("user32.dll", CharSet = CharSet.Unicode)]
        private static extern IntPtr DefWindowProc(IntPtr hWnd, uint msg, IntPtr wParam, IntPtr lParam);

        [DllImport("user32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool DestroyWindow(IntPtr hWnd);

        [DllImport("user32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool ShowWindow(IntPtr hWnd, int nCmdShow);

        [DllImport("user32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool SetWindowPos(IntPtr hWnd, IntPtr hWndInsertAfter,
            int x, int y, int cx, int cy, uint flags);

        [DllImport("user32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool SetLayeredWindowAttributes(IntPtr hwnd, uint crKey, byte bAlpha, uint dwFlags);

        // The delegate must outlive the window class it was registered with.
        private static WndProc _proc;
        private static bool _classRegistered;

        private IntPtr _handle;
        private bool _visible;

        public bool IsAvailable => _handle != IntPtr.Zero;

        public bool Create()
        {
            if (_handle != IntPtr.Zero) return true;

            IntPtr instance = NativeMethods.GetModuleHandle(null);

            if (!_classRegistered)
            {
                _proc = DefWindowProc;
                var wc = new WNDCLASSEX
                {
                    cbSize = (uint)Marshal.SizeOf<WNDCLASSEX>(),
                    lpfnWndProc = Marshal.GetFunctionPointerForDelegate(_proc),
                    hInstance = instance,
                    lpszClassName = ClassName
                };

                // A zero result with "class already exists" is fine; anything else is not.
                if (RegisterClassEx(ref wc) == 0 && Marshal.GetLastWin32Error() != 1410)
                    return false;

                _classRegistered = true;
            }

            _handle = CreateWindowEx(
                WS_EX_LAYERED | WS_EX_TRANSPARENT | WS_EX_NOACTIVATE | WS_EX_TOOLWINDOW,
                ClassName, string.Empty, WS_POPUP,
                0, 0, 1, 1, IntPtr.Zero, IntPtr.Zero, instance, IntPtr.Zero);

            if (_handle == IntPtr.Zero) return false;

            // Fully transparent and click-through: present for RetroBar, absent for
            // everyone else.
            SetLayeredWindowAttributes(_handle, 0, 0, LWA_ALPHA);
            return true;
        }

        /// <summary>Announce an open start menu covering the given screen rectangle.</summary>
        public void Show(NativeMethods.RECT rect)
        {
            if (_handle == IntPtr.Zero && !Create()) return;

            SetWindowPos(_handle, IntPtr.Zero, rect.Left, rect.Top,
                Math.Max(1, rect.Width), Math.Max(1, rect.Height),
                SWP_NOACTIVATE | SWP_NOZORDER);

            if (_visible) return;
            ShowWindow(_handle, SW_SHOWNOACTIVATE);
            _visible = true;
        }

        public void Hide()
        {
            if (_handle == IntPtr.Zero || !_visible) return;
            ShowWindow(_handle, SW_HIDE);
            _visible = false;
        }

        public void Dispose()
        {
            if (_handle == IntPtr.Zero) return;
            DestroyWindow(_handle);
            _handle = IntPtr.Zero;
            _visible = false;
        }
    }
}
