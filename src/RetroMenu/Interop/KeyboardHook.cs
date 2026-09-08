using System;
using System.Runtime.InteropServices;

namespace RetroMenu.Interop
{
    public enum WinKeyMode
    {
        /// <summary>Do not touch the Windows key at all.</summary>
        Off,

        /// <summary>
        /// Let the Win key travel normally, but slip a harmless undefined key in
        /// before its key-up so Windows keeps its own Start menu closed. All Win+X
        /// shortcuts stay completely native. This is the default.
        /// </summary>
        Neutralize,

        /// <summary>
        /// Swallow the Win key entirely and re-inject it only when it turns out to
        /// be part of a combination. Use this if Neutralize still lets the Windows
        /// 11 Start menu slip through on your machine.
        /// </summary>
        Swallow
    }

    /// <summary>
    /// Low level keyboard hook that turns a lone Windows key press into a request
    /// for our own start menu, and Windows+S into a request for its search.
    ///
    /// RetroBar's Start button calls ManagedShell's ShellHelper.ShowStartMenu(),
    /// which simulates exactly such a lone Win key press. Because a WH_KEYBOARD_LL
    /// hook also sees injected input, clicking RetroBar's Start button ends up here
    /// too and opens the retro menu — no patching of RetroBar required.
    /// </summary>
    public sealed class KeyboardHook : IDisposable
    {
        // dwExtraInfo stamp on the input we inject ourselves, so the hook can
        // recognise it and let it pass untouched.
        private const uint Marker = 0x52544D31; // "RTM1"

        private readonly NativeMethods.HookProc _callback; // kept alive on purpose
        private IntPtr _hook;
        private bool _winDown;
        private bool _winCombo;

        /// <summary>
        /// Set when the combination was Win+S and we ate the S. It has to be told
        /// apart from a real combination, which in Swallow mode gets the Windows
        /// key handed back to the system — this one must not.
        /// </summary>
        private bool _searchCombo;
        private int _lastRaise;

        /// <summary>Set RETROMENU_DEBUG=1 to trace every Windows key event to the log.</summary>
        public static readonly bool Verbose =
            Environment.GetEnvironmentVariable("RETROMENU_DEBUG") == "1";

        public WinKeyMode Mode { get; set; } = WinKeyMode.Neutralize;

        /// <summary>
        /// Take Windows+S away from the Windows 11 search and hand it to the menu's
        /// own. Works whatever <see cref="Mode"/> is: leaving the Windows key alone
        /// is a promise about the key on its own, not about this combination.
        /// </summary>
        public bool SearchHotkey { get; set; } = true;

        /// <summary>Raised on the hook thread. Handlers must return immediately.</summary>
        public event Action StartMenuRequested;

        /// <summary>Windows+S. Raised on the hook thread, like the one above.</summary>
        public event Action SearchRequested;

        public bool IsInstalled => _hook != IntPtr.Zero;

        public KeyboardHook()
        {
            _callback = HookCallback;
        }

        public bool Install()
        {
            if (_hook != IntPtr.Zero) return true;
            IntPtr module = NativeMethods.GetModuleHandle(null);
            _hook = NativeMethods.SetWindowsHookEx(NativeMethods.WH_KEYBOARD_LL, _callback, module, 0);
            return _hook != IntPtr.Zero;
        }

        public void Uninstall()
        {
            if (_hook == IntPtr.Zero) return;
            NativeMethods.UnhookWindowsHookEx(_hook);
            _hook = IntPtr.Zero;
            _winDown = false;
            _winCombo = false;
            _searchCombo = false;
        }

        private IntPtr HookCallback(int nCode, IntPtr wParam, IntPtr lParam)
        {
            // "Leave the Windows key alone" still leaves Windows+S to catch, so the
            // hook only steps aside entirely when there is nothing at all to do.
            if (nCode < 0 || (Mode == WinKeyMode.Off && !SearchHotkey))
                return NativeMethods.CallNextHookEx(_hook, nCode, wParam, lParam);

            var info = Marshal.PtrToStructure<NativeMethods.KBDLLHOOKSTRUCT>(lParam);

            // Never react to our own synthetic keys.
            if (info.dwExtraInfo == (UIntPtr)Marker)
                return NativeMethods.CallNextHookEx(_hook, nCode, wParam, lParam);

            int msg = (int)wParam;
            bool isDown = msg == NativeMethods.WM_KEYDOWN || msg == NativeMethods.WM_SYSKEYDOWN;
            bool isUp = msg == NativeMethods.WM_KEYUP || msg == NativeMethods.WM_SYSKEYUP;
            bool isWin = info.vkCode == NativeMethods.VK_LWIN || info.vkCode == NativeMethods.VK_RWIN;

            if (Verbose && isWin)
            {
                Services.Log.Write($"key vk=0x{info.vkCode:X2} {(isDown ? "down" : isUp ? "up" : "?")} " +
                                   $"flags=0x{info.flags:X2} extra=0x{(ulong)info.dwExtraInfo:X} " +
                                   $"winDown={_winDown} combo={_winCombo}");
            }

            if (isWin)
            {
                if (isDown)
                {
                    if (!_winDown)
                    {
                        _winDown = true;
                        _winCombo = false;
                        _searchCombo = false;
                    }

                    if (Mode == WinKeyMode.Swallow)
                        return (IntPtr)1;
                }
                else if (isUp)
                {
                    bool wasCombo = _winCombo;
                    bool wasSearch = _searchCombo;
                    _winDown = false;
                    _winCombo = false;
                    _searchCombo = false;

                    if (Mode == WinKeyMode.Swallow)
                    {
                        // Win+S never reached the system, so there is no Windows key
                        // out there to let go of.
                        if (wasCombo && !wasSearch)
                            Inject((ushort)info.vkCode, true);
                        else if (!wasCombo)
                            Raise();
                        return (IntPtr)1;
                    }

                    if (Mode == WinKeyMode.Neutralize && !wasCombo)
                    {
                        // Make Windows believe this was a combination, then let the
                        // real key-up through so no modifier stays stuck.
                        Inject(NativeMethods.VK_NEUTRALIZER, false);
                        Inject(NativeMethods.VK_NEUTRALIZER, true);
                        Raise();
                    }
                }
            }
            else if (_winDown && isDown && SearchHotkey && info.vkCode == NativeMethods.VK_S
                     && !OtherModifierDown())
            {
                // Windows would open its own search on this key down. Eat it whole —
                // and because the S is gone, Windows would then see a Windows key
                // pressed and released on its own and open its start menu instead.
                // The neutraliser is what tells it otherwise; in Swallow mode it
                // never saw the Windows key go down in the first place.
                if (!_winCombo && Mode != WinKeyMode.Swallow)
                {
                    Inject(NativeMethods.VK_NEUTRALIZER, false);
                    Inject(NativeMethods.VK_NEUTRALIZER, true);
                }

                bool first = !_searchCombo;
                _winCombo = true;
                _searchCombo = true;

                // Holding the keys down repeats the key; the search is already open.
                if (first) RaiseSearch();
                return (IntPtr)1;
            }
            else if (_searchCombo && info.vkCode == NativeMethods.VK_S)
            {
                // The key-up of an S nobody downstream ever saw pressed.
                return (IntPtr)1;
            }
            else if (_winDown && isDown && !_winCombo)
            {
                _winCombo = true;
                if (Mode == WinKeyMode.Swallow)
                {
                    // The combination is real after all: put the Win key back down
                    // before the other key reaches the system.
                    Inject(NativeMethods.VK_LWIN, false);
                }
            }

            return NativeMethods.CallNextHookEx(_hook, nCode, wParam, lParam);
        }

        private void Raise()
        {
            // RetroBar's Start button simulates a Win press, and on some machines a
            // single press arrives twice. Without this the menu opens and closes again
            // in the same blink.
            int now = Environment.TickCount;
            if (unchecked(now - _lastRaise) < 250) return;
            _lastRaise = now;

            try { StartMenuRequested?.Invoke(); }
            catch { /* a broken handler must never stall the input queue */ }
        }

        /// <summary>
        /// Win+S is ours; Win+Shift+S is the screenshot everybody uses and
        /// Win+Ctrl+S is speech recognition. Only the plain combination is taken.
        /// </summary>
        private static bool OtherModifierDown() =>
            Down(NativeMethods.VK_SHIFT) || Down(NativeMethods.VK_CONTROL) || Down(NativeMethods.VK_MENU);

        private static bool Down(int key) =>
            (NativeMethods.GetAsyncKeyState(key) & 0x8000) != 0;

        private void RaiseSearch()
        {
            try { SearchRequested?.Invoke(); }
            catch { /* a broken handler must never stall the input queue */ }
        }

        private static void Inject(int vk, bool keyUp)
        {
            var input = new NativeMethods.INPUT
            {
                type = NativeMethods.INPUT_KEYBOARD,
                u = new NativeMethods.InputUnion
                {
                    ki = new NativeMethods.KEYBDINPUT
                    {
                        wVk = (ushort)vk,
                        wScan = 0,
                        dwFlags = keyUp ? NativeMethods.KEYEVENTF_KEYUP : 0,
                        time = 0,
                        dwExtraInfo = (IntPtr)Marker
                    }
                }
            };

            NativeMethods.SendInput(1, new[] { input }, Marshal.SizeOf<NativeMethods.INPUT>());
        }

        public void Dispose() => Uninstall();
    }
}
