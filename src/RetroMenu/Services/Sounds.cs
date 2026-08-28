using System;
using System.Runtime.InteropServices;

namespace RetroMenu.Services
{
    /// <summary>
    /// Windows XP played the "Menu popup" scheme sound when the start menu opened.
    /// Asking for it by alias means the user's own sound scheme decides — including
    /// the common case where nothing is assigned and nothing is heard.
    /// </summary>
    public static class Sounds
    {
        private const uint SND_ASYNC = 0x0001;
        private const uint SND_NODEFAULT = 0x0002;
        private const uint SND_ALIAS = 0x00010000;

        [DllImport("winmm.dll", CharSet = CharSet.Unicode, EntryPoint = "PlaySoundW")]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool PlaySound(string pszSound, IntPtr hmod, uint fdwSound);

        public static void MenuPopup()
        {
            if (!AppSettings.Instance.PlaySounds) return;
            try { PlaySound("MenuPopup", IntPtr.Zero, SND_ALIAS | SND_ASYNC | SND_NODEFAULT); }
            catch { }
        }
    }
}
