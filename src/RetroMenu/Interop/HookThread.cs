using System;
using System.Collections.Concurrent;
using System.Threading;

namespace RetroMenu.Interop
{
    /// <summary>
    /// A thread with nothing on it but a message loop, for the hooks to live on.
    ///
    /// Windows calls a low level hook on the thread that installed it — and drops
    /// that hook without a word if the thread does not answer within
    /// <c>LowLevelHooksTimeout</c>, 300 milliseconds by default. It is never put
    /// back. The WPF thread builds menus, asks the shell for icons and enumerates
    /// the Control Panel, so on a busy or slow machine it is exactly the thread
    /// that must not carry the keyboard hook: one long shell call there and the
    /// Windows key belongs to Windows 11 again until the next restart. That is the
    /// difference between a machine where the menu works and one where it does
    /// not, so everything that has to answer at once lives here instead.
    /// </summary>
    internal sealed class HookThread : IDisposable
    {
        private const uint WM_WORK = NativeMethods.WM_USER + 1;

        private readonly ConcurrentQueue<Action> _work = new ConcurrentQueue<Action>();
        private readonly ManualResetEventSlim _ready = new ManualResetEventSlim(false);
        private readonly string _name;
        private readonly object _gate = new object();
        private Thread _thread;
        private uint _threadId;

        /// <summary>
        /// Set once and never cleared. Shutting down happens twice — Quit, and
        /// then OnExit behind it — and the second time must not start the thread
        /// up again.
        /// </summary>
        private volatile bool _stopping;

        public HookThread(string name) => _name = name;

        public bool IsRunning => _thread != null && !_stopping;

        public void Start()
        {
            lock (_gate)
            {
                if (_thread != null || _stopping) return;

                _thread = new Thread(Loop)
                {
                    IsBackground = true,
                    Name = _name,
                    Priority = ThreadPriority.AboveNormal
                };
                _thread.SetApartmentState(ApartmentState.STA);
                _thread.Start();
            }

            // Nothing may be posted before the thread owns a message queue.
            _ready.Wait(5000);
        }

        private void Loop()
        {
            // Asking for a message is what creates the queue PostThreadMessage
            // needs; only then may anyone else know about this thread.
            NativeMethods.PeekMessage(out _, IntPtr.Zero, 0, 0, NativeMethods.PM_NOREMOVE);
            _threadId = NativeMethods.GetCurrentThreadId();
            _ready.Set();

            while (NativeMethods.GetMessage(out var msg, IntPtr.Zero, 0, 0) > 0)
            {
                if (msg.message == WM_WORK)
                {
                    Drain();
                    continue;
                }

                NativeMethods.TranslateMessage(ref msg);
                NativeMethods.DispatchMessage(ref msg);
            }

            // Whatever was still queued when the loop was told to stop — the
            // unhooking, usually — has to run on this thread or not at all.
            Drain();
        }

        private void Drain()
        {
            while (_work.TryDequeue(out var action)) RunHere(action);
        }

        /// <summary>Runs the work on the hook thread and does not wait for it.</summary>
        public void Post(Action action)
        {
            if (action == null) return;

            Start();
            if (_stopping)
            {
                // On the way out there is no thread left to ask, and the work —
                // taking a hook down, usually — still has to happen.
                RunHere(action);
                return;
            }

            _work.Enqueue(action);
            if (!NativeMethods.PostThreadMessage(_threadId, WM_WORK, IntPtr.Zero, IntPtr.Zero))
                Drain(); // the thread is gone; better here than nowhere
        }

        /// <summary>
        /// Runs the work on the hook thread and waits for it. Installing and
        /// removing a hook has to happen on the thread that owns it, and the
        /// answer — whether it worked — is wanted straight away.
        /// </summary>
        public bool Invoke(Action action, int timeoutMs = 4000)
        {
            if (action == null) return false;
            if (Thread.CurrentThread == _thread || _stopping)
            {
                RunHere(action);
                return true;
            }

            // Deliberately not a using block: if the wait runs out, the work may
            // still be sitting in the queue, and setting a handle that has been
            // disposed in the meantime would throw on the hook thread. Leaving it
            // to the garbage collector costs nothing.
            var done = new ManualResetEventSlim(false);
            Post(() =>
            {
                try { action(); }
                finally { try { done.Set(); } catch (ObjectDisposedException) { } }
            });

            bool finished = done.Wait(timeoutMs);
            if (finished) done.Dispose();
            return finished;
        }

        private void RunHere(Action action)
        {
            try { action(); }
            catch (Exception ex) { Services.Log.Write(_name + ": " + ex); }
        }

        public void Dispose()
        {
            Thread thread;
            lock (_gate)
            {
                if (_stopping) return;
                _stopping = true;
                thread = _thread;
                _thread = null;
            }

            if (thread == null) return;

            NativeMethods.PostThreadMessage(_threadId, NativeMethods.WM_QUIT, IntPtr.Zero, IntPtr.Zero);
            thread.Join(1000);

            // _ready is left alone on purpose: a second Dispose, or a Post that was
            // already on its way, would otherwise wait on a handle that is gone.
        }
    }
}
