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
    /// for our own start menu.
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
        private int _lastRaise;

        /// <summary>Set RETROMENU_DEBUG=1 to trace every Windows key event to the log.</summary>
        public static readonly bool Verbose =
            Environment.GetEnvironmentVariable("RETROMENU_DEBUG") == "1";

        public WinKeyMode Mode { get; set; } = WinKeyMode.Neutralize;

        /// <summary>Raised on the hook thread. Handlers must return immediately.</summary>
        public event Action StartMenuRequested;

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
        }

        private IntPtr HookCallback(int nCode, IntPtr wParam, IntPtr lParam)
        {
            if (nCode < 0 || Mode == WinKeyMode.Off)
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
                    }

                    if (Mode == WinKeyMode.Swallow)
                        return (IntPtr)1;
                }
                else if (isUp)
                {
                    bool wasCombo = _winCombo;
                    _winDown = false;
                    _winCombo = false;

                    if (Mode == WinKeyMode.Swallow)
                    {
                        if (wasCombo)
                            Inject((ushort)info.vkCode, true);
                        else
                            Raise();
                        return (IntPtr)1;
                    }

                    if (!wasCombo)
                    {
                        // Make Windows believe this was a combination, then let the
                        // real key-up through so no modifier stays stuck.
                        Inject(NativeMethods.VK_NEUTRALIZER, false);
                        Inject(NativeMethods.VK_NEUTRALIZER, true);
                        Raise();
                    }
                }
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
