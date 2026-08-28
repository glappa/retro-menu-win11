using System;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;

namespace RetroMenu.Services
{
    /// <summary>
    /// The two special slots at the top of XP's left column: "Internet" with the
    /// default browser underneath it, and "E-mail" with the default mail client.
    /// </summary>
    public static class DefaultApps
    {
        private const uint ASSOCF_NONE = 0;
        private const uint ASSOCF_IS_PROTOCOL = 0x00001000;
        private const uint ASSOCSTR_EXECUTABLE = 2;
        private const uint ASSOCSTR_FRIENDLYAPPNAME = 4;

        [DllImport("shlwapi.dll", CharSet = CharSet.Unicode, ExactSpelling = true)]
        private static extern int AssocQueryStringW(uint flags, uint str, string pszAssoc,
            string pszExtra, StringBuilder pszOut, ref uint pcchOut);

        public sealed class AppInfo
        {
            public string FriendlyName;
            public string ExecutablePath;
            public bool IsUsable => !string.IsNullOrEmpty(ExecutablePath) && File.Exists(ExecutablePath);
        }

        public static AppInfo Browser() => Query("http");

        public static AppInfo Mail() => Query("mailto");

        private static AppInfo Query(string protocol)
        {
            var info = new AppInfo
            {
                ExecutablePath = Ask(ASSOCSTR_EXECUTABLE, protocol),
                FriendlyName = Ask(ASSOCSTR_FRIENDLYAPPNAME, protocol)
            };

            if (string.IsNullOrWhiteSpace(info.FriendlyName) && info.IsUsable)
                info.FriendlyName = Path.GetFileNameWithoutExtension(info.ExecutablePath);

            return info;
        }

        private static string Ask(uint what, string protocol)
        {
            try
            {
                uint length = 0;
                // First call sizes the buffer; S_FALSE is the documented answer there.
                AssocQueryStringW(ASSOCF_NONE | ASSOCF_IS_PROTOCOL, what, protocol, "open", null, ref length);
                if (length == 0 || length > 4096) length = 1024;

                var buffer = new StringBuilder((int)length);
                if (AssocQueryStringW(ASSOCF_NONE | ASSOCF_IS_PROTOCOL, what, protocol, "open", buffer, ref length) != 0)
                    return null;

                string value = buffer.ToString();
                return string.IsNullOrWhiteSpace(value) ? null : value;
            }
            catch
            {
                return null;
            }
        }
    }
}
