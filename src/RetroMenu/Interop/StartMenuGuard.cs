using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading;
using RetroMenu.Services;

namespace RetroMenu.Interop
{
    public enum GuardMode
    {
        /// <summary>Leave the Windows 11 start menu alone; the user wants to keep it.</summary>
        Off,

        /// <summary>
        /// Watch for it and send it away again as soon as it appears, then show
        /// ours in its place. The default, and the only thing the guard does.
        /// </summary>
        Watch
    }

    /// <summary>
    /// The safety net under the keyboard hook.
    ///
    /// Catching the Windows key keeps the Windows 11 start menu shut, and it is
    /// the cheapest way — but it is not the only way that menu opens, and on some
    /// machines it does not hold at all:
    ///
    /// * the Start button of the *Windows* taskbar is a mouse click, and no
    ///   keyboard hook ever sees one,
    /// * Ctrl+Esc and the Start key of some keyboards take their own route,
    /// * above a window running as administrator, an ordinary hook is not asked,
    /// * and Windows drops a low level hook whose thread once answered too slowly,
    ///   without a word, after which the Windows key is Windows' again.
    ///
    /// So this watches the other end: <c>StartMenuExperienceHost</c>, the process
    /// the Windows 11 menu lives in. Whenever its window comes up it is shown the
    /// door and the retro menu takes its place — whatever opened it, and whether
    /// or not the keyboard hook was even asked.
    ///
    /// The door is Escape. Bringing our own window to the front is not enough:
    /// the search box that sits at the top of that menu is a window of its own,
    /// belonging to <c>SearchHost</c>, and as a system window it keeps the
    /// foreground. Escape is what the menu itself answers to, and it closes the
    /// whole thing, search box and all — measured, not assumed. It is only sent
    /// while that menu really holds the foreground, so the key can land nowhere
    /// else.
    ///
    /// Hiding that window with ShowWindow was tried and thrown out: it does take
    /// the menu off the screen, but the shell goes on believing it is open and
    /// never shows it again — the Windows key does nothing at all afterwards,
    /// until StartMenuExperienceHost is restarted. Breaking Windows 11 to hide a
    /// menu is not a trade worth making, so the guard only ever knocks; it never
    /// reaches into that window.
    /// </summary>
    internal sealed class StartMenuGuard : IDisposable
    {
        private const string ProcessName = "StartMenuExperienceHost";

        /// <summary>
        /// The search box at the top of the Windows 11 menu is a window of its
        /// own, in another process. It is not watched — Windows search has every
        /// right to open — but it has to be recognised, because while the menu is
        /// up this is the window holding the foreground.
        /// </summary>
        private const string SearchProcessName = "SearchHost";

        private const string MenuClass = "Windows.UI.Core.CoreWindow";

        private readonly HookThread _thread = new HookThread("RetroMenu start menu guard");
        private readonly NativeMethods.WinEventProc _callback; // kept alive on purpose
        private readonly List<IntPtr> _hooks = new List<IntPtr>();
        private readonly object _gate = new object();

        private Timer _watchdog;
        private Timer _followUp;
        private int _knocks;
        private IntPtr _lastWindow;
        private uint _pid;
        private uint _searchPid;
        private GuardMode _mode = GuardMode.Watch;

        // Somebody else's window, hidden by us, in a fight we cannot win would be
        // an unusable desktop. So the guard counts its own interventions and steps
        // back when they start coming thick and fast.
        private int _acted;
        private int _windowStarted;
        private int _quietUntil;

        /// <summary>Shutting down comes twice: Quit, and OnExit behind it.</summary>
        private bool _disposed;

        /// <summary>
        /// Raised on the guard's own thread when the Windows 11 menu turned up.
        /// The handler is expected to put the retro menu in front of it.
        /// </summary>
        public event Action Appeared;

        public StartMenuGuard()
        {
            _callback = OnWindowEvent;
        }

        public GuardMode Mode
        {
            get => _mode;
            set
            {
                if (_mode == value) return;
                _mode = value;
                if (value == GuardMode.Off) Stop(); else Start();
            }
        }

        public bool IsWatching
        {
            get { lock (_gate) return _hooks.Count > 0; }
        }

        /// <summary>The process the Windows 11 menu lives in, 0 while it is not found.</summary>
        public uint WatchedProcess => _pid;

        public void Start()
        {
            if (_mode == GuardMode.Off) return;

            _thread.Start();
            _thread.Post(Attach);

            // StartMenuExperienceHost is restarted by Windows now and then — after
            // an update, after a crash, after "restart" in the task manager. The
            // hook goes with it, so it is looked for again from time to time.
            _watchdog ??= new Timer(_ => _thread.Post(Attach), null, 5000, 5000);
        }

        public void Stop()
        {
            if (_disposed) return;

            _watchdog?.Dispose();
            _watchdog = null;
            try { _followUp?.Change(Timeout.Infinite, Timeout.Infinite); }
            catch (ObjectDisposedException) { }
            _thread.Invoke(Detach, 1500);
        }

        /// <summary>Finds the Windows menu's process and hooks its windows. Guard thread.</summary>
        private void Attach()
        {
            if (_mode == GuardMode.Off) return;

            uint pid = FindProcess();
            if (pid == 0)
            {
                // Nothing to hook onto — look again at the next round.
                if (_pid != 0) { Detach(); _pid = 0; }
                return;
            }

            _searchPid = FindProcess(SearchProcessName);

            if (pid == _pid && IsWatching) return;

            Detach();
            _pid = pid;

            // Three ways the same thing arrives, depending on the Windows build: a
            // window that is shown, one that is only uncloaked again — Windows 11
            // keeps its menu around and merely cloaks it away — and one that is
            // handed the foreground. Cloaking is watched too, so that the menu
            // going away can call off a pending second look.
            Hook(NativeMethods.EVENT_SYSTEM_FOREGROUND, NativeMethods.EVENT_SYSTEM_FOREGROUND);
            Hook(NativeMethods.EVENT_OBJECT_SHOW, NativeMethods.EVENT_OBJECT_HIDE);
            Hook(NativeMethods.EVENT_OBJECT_CLOAKED, NativeMethods.EVENT_OBJECT_UNCLOAKED);

            Log.Write($"guard: watching {ProcessName} ({pid}), hooks={_hooks.Count}, mode={_mode}");
        }

        private void Hook(uint from, uint to)
        {
            IntPtr hook = NativeMethods.SetWinEventHook(from, to, IntPtr.Zero, _callback, _pid, 0,
                NativeMethods.WINEVENT_OUTOFCONTEXT | NativeMethods.WINEVENT_SKIPOWNPROCESS);

            if (hook != IntPtr.Zero) { lock (_gate) _hooks.Add(hook); }
            else Log.Write($"guard: SetWinEventHook {from:X}..{to:X} failed");
        }

        private void Detach()
        {
            List<IntPtr> hooks;
            lock (_gate)
            {
                if (_hooks.Count == 0) return;
                hooks = new List<IntPtr>(_hooks);
                _hooks.Clear();
            }

            foreach (var hook in hooks) NativeMethods.UnhookWinEvent(hook);
        }

        private static uint FindProcess() => FindProcess(ProcessName);

        private static uint FindProcess(string name)
        {
            try
            {
                var found = Process.GetProcessesByName(name);
                uint pid = found.Length > 0 ? (uint)found[0].Id : 0;
                foreach (var process in found) process.Dispose();
                return pid;
            }
            catch { return 0; }
        }

        // ------------------------------------------------------------- the watch

        private void OnWindowEvent(IntPtr hook, uint type, IntPtr hwnd,
            int idObject, int idChild, uint thread, uint time)
        {
            // Called on the guard thread while it waits for messages, so it stays
            // short: no process lookups, no window enumeration.
            if (hwnd == IntPtr.Zero) return;
            if (idObject != NativeMethods.OBJID_WINDOW || idChild != NativeMethods.CHILDID_SELF) return;
            if (_mode == GuardMode.Off) return;

            if (type == NativeMethods.EVENT_OBJECT_HIDE || type == NativeMethods.EVENT_OBJECT_CLOAKED)
            {
                // It went away on its own; no second look needed.
                if (hwnd == _lastWindow) _followUp?.Change(Timeout.Infinite, Timeout.Infinite);
                return;
            }

            if (!IsTheStartMenu(hwnd)) return;
            if (!NativeMethods.IsOnScreen(hwnd)) return;
            if (BackingOff()) return;

            Log.Write($"guard: the Windows 11 menu came up (event 0x{type:X}) — sending it back");

            _lastWindow = hwnd;
            _knocks = 0;
            SendItAway();

            try { Appeared?.Invoke(); }
            catch (Exception ex) { Log.Write("guard: " + ex); }

            // At this instant the Windows menu usually does not hold the
            // foreground yet — the search box on top of it takes it a moment
            // later — so the first knock lands nowhere and it is repeated.
            _followUp ??= new Timer(SecondLook, null, Timeout.Infinite, Timeout.Infinite);
            _followUp.Change(60, Timeout.Infinite);
        }

        /// <summary>
        /// Escape, but only while the Windows menu really is the window in front,
        /// so the key cannot land in whatever the user was working in.
        /// </summary>
        private void SendItAway()
        {
            if (!ForegroundIsWindowsMenu()) return;

            NativeMethods.SendKey(NativeMethods.VK_ESCAPE, false);
            NativeMethods.SendKey(NativeMethods.VK_ESCAPE, true);
        }

        /// <summary>
        /// True while the window in front is the Windows 11 menu — its own window,
        /// or the search box that sits on top of it.
        /// </summary>
        private bool ForegroundIsWindowsMenu()
        {
            IntPtr fg = NativeMethods.GetForegroundWindow();
            if (fg == IntPtr.Zero) return false;

            NativeMethods.GetWindowThreadProcessId(fg, out uint pid);
            return pid != 0 && (pid == _pid || (pid == _searchPid && _searchPid != 0));
        }

        /// <summary>
        /// Sends the Windows menu away without putting ours up, for the one case
        /// where the retro menu must not open: the user has just closed it with
        /// the same key press that let the Windows menu through.
        /// </summary>
        public void CloseNow()
        {
            IntPtr hwnd = _lastWindow;
            if (hwnd == IntPtr.Zero || _mode == GuardMode.Off) return;
            if (!NativeMethods.IsOnScreen(hwnd)) return;

            Log.Write("guard: closing the Windows menu without opening ours");
            SendItAway();
        }

        private void SecondLook(object state)
        {
            IntPtr hwnd = _lastWindow;
            if (hwnd == IntPtr.Zero || _mode == GuardMode.Off) return;
            if (!NativeMethods.IsOnScreen(hwnd)) return;

            _knocks++;
            Log.Write($"guard: still up — knocking again ({_knocks})");
            SendItAway();

            // Four tries over about half a second, then it is left alone: better
            // a Windows menu on screen than a key going off every 150 ms.
            if (_knocks < 4)
            {
                try { _followUp?.Change(150, Timeout.Infinite); }
                catch (ObjectDisposedException) { }
            }
        }

        private static bool IsTheStartMenu(IntPtr hwnd)
        {
            // The process is already the right one — this only tells the menu
            // apart from the small helper windows it keeps beside it.
            if (string.Equals(NativeMethods.ClassNameOf(hwnd), MenuClass, StringComparison.Ordinal))
                return true;

            // A Windows build that renames its window class must not get past the
            // guard, so for anything else size decides: the menu is large.
            return NativeMethods.GetWindowRect(hwnd, out var rect) &&
                   rect.Width >= 200 && rect.Height >= 200;
        }

        /// <summary>
        /// True while the guard is holding back. Ten interventions inside three
        /// seconds are not a user opening a menu, they are two programs arguing —
        /// and then it is better to let Windows have its menu than to leave the
        /// desktop unusable.
        /// </summary>
        private bool BackingOff()
        {
            int now = Environment.TickCount;
            if (unchecked(now - _quietUntil) < 0) return true;

            if (_windowStarted == 0 || unchecked(now - _windowStarted) > 3000)
            {
                _windowStarted = now;
                _acted = 0;
            }

            if (++_acted <= 10) return false;

            _quietUntil = now + 10000;
            Log.Write("guard: stepping back for ten seconds — the Windows menu keeps coming back");
            return true;
        }

        public void Dispose()
        {
            if (_disposed) return;

            Stop();
            _disposed = true;
            _followUp?.Dispose();
            _followUp = null;
            _thread.Dispose();
        }
    }
}
