using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Reflection;
using System.Runtime.InteropServices;
using RetroMenu.Interop;

namespace RetroMenu.Services
{
    /// <summary>
    /// The Network Connections folder, which is what XP's "Connect To" listed.
    ///
    /// These entries cannot be started like a file: the shell gives every
    /// connection the same parsing name ({BA126ADB-…}, the class of connection
    /// items), so opening one by path either does nothing or opens the wrong thing.
    /// They are opened the way the shell itself does it — by asking the folder item
    /// to run its default verb, which is Status for a live connection and Connect
    /// for one that is not up.
    /// </summary>
    public static class NetworkConnections
    {
        private const string FolderName = "shell:ConnectionsFolder";

        /// <summary>The connections, by name, in the order the folder hands them out.</summary>
        public static List<string> Names()
        {
            var names = new List<string>();
            object shell = null;

            try
            {
                shell = CreateShell();
                if (shell == null) return names;

                object folder = Call(shell, "Namespace", FolderName);
                if (folder == null) return names;

                object items = Call(folder, "Items");
                if (items == null) return names;

                int count = Convert.ToInt32(Get(items, "Count"));
                for (int i = 0; i < count; i++)
                {
                    object item = Call(items, "Item", i);
                    if (item == null) continue;

                    if (Get(item, "Name") is string name && !string.IsNullOrWhiteSpace(name))
                        names.Add(name);

                    Release(item);
                }

                Release(items);
                Release(folder);
            }
            catch
            {
                // No shell, no folder, no connections. The menu simply shows the
                // "all connections" entry on its own.
            }
            finally { Release(shell); }

            return names;
        }

        /// <summary>
        /// Shows a connection the way double-clicking it in the folder does: its
        /// status window, or its properties if there is no status to show.
        ///
        /// The verb is picked by its canonical name, which stays "status" whatever
        /// language Windows speaks. That matters twice over: the visible text would
        /// have to be guessed in 24 languages, and the same menu also carries
        /// Disable, Delete and Rename — none of which may ever be reached by
        /// accident from a start menu click. Anything unexpected opens the folder.
        /// </summary>
        public static void Open(string name)
        {
            if (string.IsNullOrWhiteSpace(name)) { OpenFolder(); return; }

            try
            {
                using var menu = new ShellContextMenu();
                if (menu.OpenChild(FolderName, name, IntPtr.Zero) &&
                    menu.InvokePreferred("status", "properties"))
                    return;
            }
            catch { }

            OpenFolder();
        }

        /// <summary>Opens the Network Connections window itself.</summary>
        public static void OpenFolder()
        {
            try
            {
                Process.Start(new ProcessStartInfo("control.exe", "ncpa.cpl")
                {
                    UseShellExecute = true
                });
            }
            catch
            {
                try { Process.Start(new ProcessStartInfo("ms-settings:network-ethernet")
                    { UseShellExecute = true }); }
                catch { }
            }
        }

        // ---- late-bound Shell.Application, so no COM reference is needed ----

        private static object CreateShell()
        {
            Type type = Type.GetTypeFromProgID("Shell.Application");
            return type == null ? null : Activator.CreateInstance(type);
        }

        private static object Call(object target, string member, params object[] args) =>
            target?.GetType().InvokeMember(member, BindingFlags.InvokeMethod, null, target, args);

        private static object Get(object target, string member) =>
            target?.GetType().InvokeMember(member, BindingFlags.GetProperty, null, target, null);

        private static void Release(object com)
        {
            try
            {
                if (com != null && Marshal.IsComObject(com)) Marshal.ReleaseComObject(com);
            }
            catch { }
        }
    }
}
