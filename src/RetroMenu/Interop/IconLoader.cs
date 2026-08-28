using System;
using System.Threading;
using System.Windows.Threading;

namespace RetroMenu.Interop
{
    /// <summary>
    /// Shell icon extraction has to happen on an STA thread with a message pump —
    /// several namespace extensions simply hand back nothing when they are called
    /// from an MTA thread pool thread, which shows up as a random subset of icons
    /// staying blank. One dedicated worker keeps the calls ordered and off the UI.
    /// </summary>
    internal static class IconLoader
    {
        private static readonly object Gate = new object();
        private static Dispatcher _dispatcher;

        public static void Enqueue(Action work)
        {
            if (work == null) return;
            var dispatcher = EnsureWorker();
            if (dispatcher == null) return;

            try { dispatcher.BeginInvoke(work, DispatcherPriority.Background); }
            catch { }
        }

        private static Dispatcher EnsureWorker()
        {
            lock (Gate)
            {
                if (_dispatcher != null) return _dispatcher;

                var ready = new ManualResetEventSlim(false);
                var thread = new Thread(() =>
                {
                    _dispatcher = Dispatcher.CurrentDispatcher;
                    ready.Set();
                    Dispatcher.Run();
                })
                {
                    IsBackground = true,
                    Name = "RetroMenu icons",
                    Priority = ThreadPriority.BelowNormal
                };
                thread.SetApartmentState(ApartmentState.STA);
                thread.Start();

                ready.Wait(5000);
                return _dispatcher;
            }
        }
    }
}
