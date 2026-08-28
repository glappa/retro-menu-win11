using System;
using System.IO;

namespace RetroMenu.Services
{
    /// <summary>
    /// A single rolling text file next to the settings. Keyboard hooks and shell
    /// COM fail in ways that leave no visible trace, so there has to be somewhere
    /// to look.
    /// </summary>
    public static class Log
    {
        private static readonly object Gate = new object();

        public static string FilePath => Path.Combine(AppSettings.Folder, "retromenu.log");

        public static void Write(string message)
        {
            try
            {
                lock (Gate)
                {
                    Directory.CreateDirectory(AppSettings.Folder);

                    var file = new FileInfo(FilePath);
                    if (file.Exists && file.Length > 256 * 1024)
                        file.Delete();

                    File.AppendAllText(FilePath,
                        DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss.fff") + "  " + message + Environment.NewLine);
                }
            }
            catch { }
        }
    }
}
