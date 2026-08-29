using System;
using System.Collections.Generic;
using System.IO;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using RetroMenu.Model;

namespace RetroMenu.Services
{
    /// <summary>
    /// Started with --demo, the menu fills itself with placeholder content instead
    /// of the real user and their programs. That is what the screenshots in the
    /// README are taken from, so publishing them gives nothing away about whoever
    /// happened to run the build.
    ///
    /// Everything shown comes with Windows itself, so the pictures stay honest
    /// without naming a single installed application.
    /// </summary>
    public static class Demo
    {
        public static bool IsActive { get; set; }

        public const string UserName = "Max Mustermann";

        private static string Resolve(string exe)
        {
            string system = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.System), exe);
            if (File.Exists(system)) return system;

            string windows = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.Windows), exe);
            return File.Exists(windows) ? windows : system;
        }

        private static StartItem Program(string name, string exe, string subtext = null)
        {
            string path = Resolve(exe);
            return new StartItem
            {
                Name = name,
                Subtext = subtext,
                ParsingName = path,
                Target = path,
                Kind = StartItemKind.Shortcut
            };
        }

        /// <summary>The two special slots at the top of the left column.</summary>
        public static List<StartItem> DefaultAppSlots() => new List<StartItem>
        {
            new StartItem
            {
                Name = Lang.T("Internet"),
                Subtext = "Webbrowser",
                ParsingName = "res:imageres.dll,25",
                Kind = StartItemKind.Shortcut,
                Bold = true
            },
            new StartItem
            {
                Name = Lang.T("Email"),
                Subtext = "Mailprogramm",
                ParsingName = "res:imageres.dll,20",
                Kind = StartItemKind.Shortcut,
                Bold = true
            },
        };

        /// <summary>The name of the folder the demo favourites carry.</summary>
        public const string FolderName = "Werkzeuge";

        public static List<StartItem> Pinned() => new List<StartItem>
        {
            Program("Editor", "notepad.exe"),
            new StartItem
            {
                Name = FolderName,
                Kind = StartItemKind.Folder,
                ParsingName = "res:imageres.dll,18",
                SubmenuSource = Launcher.FavouriteFolderPrefix + FolderName
            },
            Program("Windows-Explorer", "explorer.exe"),
        };

        /// <summary>What that folder holds.</summary>
        public static List<StartItem> FolderContents() => new List<StartItem>
        {
            Program("Rechner", "calc.exe"),
            Program("Zeichentabelle", "charmap.exe"),
            Program("Bildschirmlupe", "magnify.exe"),
        };

        public static List<StartItem> Frequent() => new List<StartItem>
        {
            Program("Eingabeaufforderung", "cmd.exe"),
            Program("Task-Manager", "taskmgr.exe"),
            Program("Zeichentabelle", "charmap.exe"),
            Program("Bildschirmlupe", "magnify.exe"),
        };

        /// <summary>A believable, entirely generic All Programs tree.</summary>
        public static StartItem Tree()
        {
            var root = new StartItem { Name = "", Kind = StartItemKind.Folder };

            StartItem Folder(string name)
            {
                var folder = new StartItem { Name = name, Kind = StartItemKind.Folder };
                root.Children.Add(folder);
                return folder;
            }

            var accessories = Folder("Zubehör");
            accessories.Children.Add(Program("Editor", "notepad.exe"));
            accessories.Children.Add(Program("Rechner", "calc.exe"));
            accessories.Children.Add(Program("Bildschirmtastatur", "osk.exe"));
            accessories.Children.Add(Program("Zeichentabelle", "charmap.exe"));
            accessories.Children.Add(Program("Eingabeaufforderung", "cmd.exe"));

            var system = Folder("Systemprogramme");
            system.Children.Add(Program("Datenträgerbereinigung", "cleanmgr.exe"));
            system.Children.Add(Program("Defragmentierung", "dfrgui.exe"));
            system.Children.Add(Program("Systeminformationen", "msinfo32.exe"));
            system.Children.Add(Program("Registrierungs-Editor", "regedt32.exe"));

            var admin = Folder("Verwaltung");
            admin.Children.Add(Program("Computerverwaltung", "compmgmt.msc"));
            admin.Children.Add(Program("Dienste", "services.msc"));
            admin.Children.Add(Program("Ereignisanzeige", "eventvwr.msc"));

            Folder("Autostart");
            Folder("Spiele");

            root.Children.Add(Program("Windows-Explorer", "explorer.exe"));
            root.Children.Add(Program("Task-Manager", "taskmgr.exe"));

            return root;
        }

        /// <summary>A drawn stand-in for the account picture.</summary>
        public static BitmapSource UserPicture()
        {
            const int size = 96;
            var visual = new DrawingVisual();

            using (var dc = visual.RenderOpen())
            {
                dc.DrawRectangle(new SolidColorBrush(Color.FromRgb(0x6F, 0xA8, 0xDC)), null,
                    new Rect(0, 0, size, size));

                var figure = Brushes.White;
                dc.DrawEllipse(figure, null, new Point(size / 2.0, size * 0.36), size * 0.19, size * 0.19);
                dc.DrawGeometry(figure, null, new EllipseGeometry(
                    new Point(size / 2.0, size * 0.92), size * 0.32, size * 0.26));
            }

            var target = new RenderTargetBitmap(size, size, 96, 96, PixelFormats.Pbgra32);
            target.Render(visual);
            target.Freeze();
            return target;
        }
    }
}
