using System;
using System.Collections.Generic;
using System.Diagnostics;

namespace RetroMenu.Interop
{
    internal enum TaskbarEdge { Left, Top, Right, Bottom }

    internal sealed class TaskbarInfo
    {
        public NativeMethods.RECT Bar;      // device pixels
        public NativeMethods.RECT Monitor;  // device pixels
        public TaskbarEdge Edge = TaskbarEdge.Bottom;
        public string Source = "workarea";
    }

    /// <summary>
    /// Finds the bar the menu has to sit against. RetroBar first, because with
    /// RetroBar running the real Windows taskbar is hidden and its reported
    /// position no longer matches what the user sees.
    /// </summary>
    internal static class TaskbarLocator
    {
        public static TaskbarInfo Locate()
        {
            NativeMethods.GetCursorPos(out var cursor);
            var monitor = MonitorFor(cursor);

            var bar = FindRetroBar(monitor);
            string source = "retrobar";

            if (bar == null)
            {
                bar = FindSystemTaskbar();
                source = "shell";
            }

            if (bar == null)
            {
                // Last resort: whatever the work area leaves free at the bottom.
                var info = MonitorInfoFor(cursor);
                var r = info.rcMonitor;
                r.Top = info.rcWork.Bottom;
                bar = r;
                source = "workarea";
            }

            var result = new TaskbarInfo
            {
                Bar = bar.Value,
                Monitor = monitor,
                Source = source
            };
            result.Edge = EdgeOf(bar.Value, monitor);
            return result;
        }

        private static TaskbarEdge EdgeOf(NativeMethods.RECT bar, NativeMethods.RECT monitor)
        {
            if (bar.Width >= bar.Height)
                return bar.Top - monitor.Top <= monitor.Bottom - bar.Bottom ? TaskbarEdge.Top : TaskbarEdge.Bottom;

            return bar.Left - monitor.Left <= monitor.Right - bar.Right ? TaskbarEdge.Left : TaskbarEdge.Right;
        }

        private static NativeMethods.RECT MonitorFor(NativeMethods.POINT pt) => MonitorInfoFor(pt).rcMonitor;

        private static NativeMethods.MONITORINFO MonitorInfoFor(NativeMethods.POINT pt)
        {
            IntPtr hMon = NativeMethods.MonitorFromPoint(pt, NativeMethods.MONITOR_DEFAULTTONEAREST);
            var mi = new NativeMethods.MONITORINFO { cbSize = System.Runtime.InteropServices.Marshal.SizeOf<NativeMethods.MONITORINFO>() };
            NativeMethods.GetMonitorInfo(hMon, ref mi);
            return mi;
        }

        private static NativeMethods.RECT? FindRetroBar(NativeMethods.RECT monitor)
        {
            var pids = new HashSet<uint>();
            try
            {
                foreach (var p in Process.GetProcessesByName("RetroBar"))
                {
                    pids.Add((uint)p.Id);
                    p.Dispose();
                }
            }
            catch { }

            if (pids.Count == 0) return null;

            NativeMethods.RECT? best = null;
            long bestArea = 0;

            NativeMethods.EnumWindows((hwnd, _) =>
            {
                if (!NativeMethods.IsWindowVisible(hwnd)) return true;
                NativeMethods.GetWindowThreadProcessId(hwnd, out uint pid);
                if (!pids.Contains(pid)) return true;
                if (!NativeMethods.GetWindowRect(hwnd, out var rect)) return true;

                int w = rect.Width, h = rect.Height;
                if (w <= 0 || h <= 0) return true;

                // A taskbar spans most of one monitor edge; menus and tooltips do not.
                bool spansWidth = w >= (monitor.Width * 0.5);
                bool spansHeight = h >= (monitor.Height * 0.5);
                if (!spansWidth && !spansHeight) return true;

                // Prefer a bar on the monitor the user is pointing at.
                bool sameMonitor = rect.Left < monitor.Right && rect.Right > monitor.Left &&
                                   rect.Top < monitor.Bottom && rect.Bottom > monitor.Top;
                long area = (long)w * h * (sameMonitor ? 2 : 1);
                if (area > bestArea)
                {
                    bestArea = area;
                    best = rect;
                }
                return true;
            }, IntPtr.Zero);

            return best;
        }

        private static NativeMethods.RECT? FindSystemTaskbar()
        {
            var abd = new NativeMethods.APPBARDATA
            {
                cbSize = (uint)System.Runtime.InteropServices.Marshal.SizeOf<NativeMethods.APPBARDATA>()
            };
            if (NativeMethods.SHAppBarMessage(NativeMethods.ABM_GETTASKBARPOS, ref abd) == IntPtr.Zero)
                return null;
            if (abd.rc.Width <= 0 || abd.rc.Height <= 0) return null;
            return abd.rc;
        }
    }
}
