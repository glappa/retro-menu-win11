using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace RetroMenu.Interop
{
    /// <summary>
    /// Turns anything the shell can name — a .lnk, an .exe, a folder, or an
    /// "shell:AppsFolder\{aumid}" entry for a Store app — into a frozen
    /// BitmapSource, cached per path and size.
    /// </summary>
    internal static class ShellIcon
    {
        private const uint SIIGBF_BIGGERSIZEOK = 0x00000001;
        private const uint SIIGBF_ICONONLY = 0x00000004;

        /// <summary>
        /// Marks a "file,resourceId" pair instead of a shell path — needed where the
        /// shell only offers a generic icon (Windows 11 hands out a plain folder for
        /// This PC and Network, for instance).
        /// </summary>
        public const string ResourcePrefix = "res:";

        private static readonly object Gate = new object();
        private static readonly Dictionary<string, BitmapSource> Cache =
            new Dictionary<string, BitmapSource>(StringComparer.OrdinalIgnoreCase);

        public static BitmapSource Get(string parsingName, int size)
        {
            if (string.IsNullOrWhiteSpace(parsingName)) return null;

            string key = size + "|" + parsingName;
            lock (Gate)
            {
                if (Cache.TryGetValue(key, out var cached)) return cached;
            }

            BitmapSource image = null;

            if (parsingName.StartsWith(ResourcePrefix, StringComparison.OrdinalIgnoreCase))
            {
                try { image = FromResource(parsingName.Substring(ResourcePrefix.Length)); } catch { }
            }
            else
            {
                try { image = FromImageFactory(parsingName, size); } catch { }
                if (image == null)
                {
                    try { image = FromFileInfo(parsingName, size); } catch { }
                }
            }

            lock (Gate)
            {
                Cache[key] = image;
            }
            return image;
        }

        private static BitmapSource FromImageFactory(string parsingName, int size)
        {
            Guid iid = ShellGuids.IShellItemImageFactory;
            int hr = NativeMethods.SHCreateItemFromParsingName(parsingName, IntPtr.Zero, ref iid, out object obj);
            if (hr != 0 || obj == null) return null;

            var factory = (IShellItemImageFactory)obj;
            IntPtr hbitmap = IntPtr.Zero;
            try
            {
                var wanted = new NativeMethods.SIZE { cx = size, cy = size };
                if (factory.GetImage(wanted, SIIGBF_ICONONLY | SIIGBF_BIGGERSIZEOK, out hbitmap) != 0)
                    return null;

                return FromHBitmap(hbitmap);
            }
            finally
            {
                if (hbitmap != IntPtr.Zero) NativeMethods.DeleteObject(hbitmap);
                Marshal.ReleaseComObject(factory);
            }
        }

        private static BitmapSource FromHBitmap(IntPtr hbitmap)
        {
            if (hbitmap == IntPtr.Zero) return null;

            var dib = new NativeMethods.DIBSECTION();
            bool isDib = NativeMethods.GetObjectDib(hbitmap, Marshal.SizeOf<NativeMethods.DIBSECTION>(), ref dib)
                         >= Marshal.SizeOf<NativeMethods.DIBSECTION>();

            var bm = isDib ? dib.dsBm : new NativeMethods.BITMAP();
            if (!isDib && NativeMethods.GetObjectBitmap(hbitmap, Marshal.SizeOf<NativeMethods.BITMAP>(), ref bm) == 0)
                return null;

            // A DIB with a positive biHeight is stored bottom-up: row 0 of the buffer
            // is the *last* row of the picture. Reading it straight through turns every
            // icon upside down.
            bool bottomUp = !isDib || dib.dsBmih.biHeight > 0;

            if (bm.bmBitsPixel != 32 || bm.bmBits == IntPtr.Zero || bm.bmWidth <= 0 || bm.bmHeight <= 0)
            {
                // Not a 32bpp DIB section we can read directly; let WPF copy it.
                var plain = Imaging.CreateBitmapSourceFromHBitmap(
                    hbitmap, IntPtr.Zero, System.Windows.Int32Rect.Empty,
                    BitmapSizeOptions.FromEmptyOptions());
                plain.Freeze();
                return plain;
            }

            int stride = bm.bmWidthBytes;
            int height = bm.bmHeight;
            int length = stride * height;
            var pixels = new byte[length];

            if (bottomUp)
            {
                for (int row = 0; row < height; row++)
                {
                    IntPtr sourceRow = IntPtr.Add(bm.bmBits, (height - 1 - row) * stride);
                    Marshal.Copy(sourceRow, pixels, row * stride, stride);
                }
            }
            else
            {
                Marshal.Copy(bm.bmBits, pixels, 0, length);
            }

            // Some providers hand back a fully transparent alpha channel. Treat that
            // as "no alpha" rather than drawing an invisible icon.
            bool hasAlpha = false;
            for (int i = 3; i < length; i += 4)
            {
                if (pixels[i] != 0) { hasAlpha = true; break; }
            }
            if (!hasAlpha)
            {
                for (int i = 3; i < length; i += 4) pixels[i] = 255;
            }

            // The shell hands out premultiplied BGRA. Declaring straight Bgra32 here
            // makes WPF premultiply a second time, which washes light icons out until
            // they vanish against a light panel.
            var source = BitmapSource.Create(bm.bmWidth, bm.bmHeight, 96, 96,
                PixelFormats.Pbgra32, null, pixels, stride);
            source.Freeze();
            return source;
        }

        private static BitmapSource FromResource(string spec)
        {
            int comma = spec.LastIndexOf(',');
            if (comma <= 0 || !int.TryParse(spec.Substring(comma + 1), out int id)) return null;

            string file = spec.Substring(0, comma);
            if (!System.IO.Path.IsPathRooted(file))
                file = System.IO.Path.Combine(Environment.SystemDirectory, file);

            if (NativeMethods.ExtractIconEx(file, -id, out IntPtr large, out IntPtr small, 1) == 0)
                return null;

            IntPtr icon = large != IntPtr.Zero ? large : small;
            if (icon == IntPtr.Zero) return null;

            try
            {
                var source = Imaging.CreateBitmapSourceFromHIcon(
                    icon, System.Windows.Int32Rect.Empty, BitmapSizeOptions.FromEmptyOptions());
                source.Freeze();
                return source;
            }
            finally
            {
                if (large != IntPtr.Zero) NativeMethods.DestroyIcon(large);
                if (small != IntPtr.Zero) NativeMethods.DestroyIcon(small);
            }
        }

        private static BitmapSource FromFileInfo(string path, int size)
        {
            var shfi = new NativeMethods.SHFILEINFO();
            uint flags = NativeMethods.SHGFI_ICON |
                         (size <= 16 ? NativeMethods.SHGFI_SMALLICON : NativeMethods.SHGFI_LARGEICON);

            if (NativeMethods.SHGetFileInfo(path, 0, ref shfi,
                    (uint)Marshal.SizeOf<NativeMethods.SHFILEINFO>(), flags) == IntPtr.Zero)
                return null;

            if (shfi.hIcon == IntPtr.Zero) return null;

            try
            {
                var source = Imaging.CreateBitmapSourceFromHIcon(
                    shfi.hIcon, System.Windows.Int32Rect.Empty, BitmapSizeOptions.FromEmptyOptions());
                source.Freeze();
                return source;
            }
            finally
            {
                NativeMethods.DestroyIcon(shfi.hIcon);
            }
        }
    }
}
