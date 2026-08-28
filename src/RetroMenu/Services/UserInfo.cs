using System;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Windows.Media.Imaging;

namespace RetroMenu.Services
{
    /// <summary>The name and picture the classic menu shows in its blue header.</summary>
    public static class UserInfo
    {
        private const int NameDisplay = 3;

        [DllImport("secur32.dll", CharSet = CharSet.Unicode)]
        private static extern byte GetUserNameEx(int nameFormat, StringBuilder lpNameBuffer, ref uint lpnSize);

        public static string DisplayName()
        {
            if (Demo.IsActive) return Demo.UserName;

            string configured = AppSettings.Instance.UserName;
            if (!string.IsNullOrWhiteSpace(configured)) return configured;

            try
            {
                uint size = 256;
                var buffer = new StringBuilder((int)size);
                if (GetUserNameEx(NameDisplay, buffer, ref size) != 0)
                {
                    string name = buffer.ToString();
                    if (!string.IsNullOrWhiteSpace(name)) return name;
                }
            }
            catch { }

            return Environment.UserName;
        }

        public static System.Windows.Media.Imaging.BitmapSource Picture()
        {
            if (Demo.IsActive) return Demo.UserPicture();

            foreach (var candidate in Candidates())
            {
                try
                {
                    if (candidate == null || !File.Exists(candidate)) continue;
                    var image = new BitmapImage();
                    image.BeginInit();
                    image.CacheOption = BitmapCacheOption.OnLoad;
                    image.UriSource = new Uri(candidate);
                    image.DecodePixelWidth = 96;
                    image.EndInit();
                    image.Freeze();
                    return image;
                }
                catch { }
            }
            return null;
        }

        private static System.Collections.Generic.IEnumerable<string> Candidates()
        {
            string local = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);

            // Windows caches the current account picture here.
            yield return Path.Combine(local, "Temp", Environment.UserName + ".bmp");
            yield return Path.Combine(local, "Temp", Environment.UserName + ".png");

            string publicPictures = @"C:\Users\Public\AccountPictures";
            string best = null;
            if (Directory.Exists(publicPictures))
            {
                try
                {
                    best = Directory.EnumerateFiles(publicPictures, "*.*", SearchOption.AllDirectories)
                        .Where(f => f.EndsWith(".png", StringComparison.OrdinalIgnoreCase) ||
                                    f.EndsWith(".jpg", StringComparison.OrdinalIgnoreCase))
                        .OrderByDescending(f => new FileInfo(f).Length)
                        .FirstOrDefault();
                }
                catch { }
            }
            if (best != null) yield return best;

            yield return Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData),
                @"Microsoft\User Account Pictures\user.png");
        }
    }
}
