using System;
using System.Drawing;
using System.Windows;
using Forms = System.Windows.Forms;

namespace RetroMenu.Services
{
    public sealed class TrayIconService : IDisposable
    {
        private Forms.NotifyIcon _icon;
        private Forms.ToolStripMenuItem _enabled;
        private Forms.ToolStripMenuItem _open;
        private Forms.ToolStripMenuItem _settings;
        private Forms.ToolStripMenuItem _refresh;
        private Forms.ToolStripMenuItem _exit;

        public event Action OpenRequested;

        /// <summary>The master switch, so it can be reached without a window.</summary>
        public event Action<bool> EnabledToggled;

        public event Action SettingsRequested;
        public event Action RefreshRequested;
        public event Action ExitRequested;

        public void Show()
        {
            _enabled = new Forms.ToolStripMenuItem { CheckOnClick = true, Checked = true };
            _enabled.Click += (_, __) => EnabledToggled?.Invoke(_enabled.Checked);
            _open = new Forms.ToolStripMenuItem();
            _open.Click += (_, __) => OpenRequested?.Invoke();
            _settings = new Forms.ToolStripMenuItem();
            _settings.Click += (_, __) => SettingsRequested?.Invoke();
            _refresh = new Forms.ToolStripMenuItem();
            _refresh.Click += (_, __) => RefreshRequested?.Invoke();
            _exit = new Forms.ToolStripMenuItem();
            _exit.Click += (_, __) => ExitRequested?.Invoke();

            var menu = new Forms.ContextMenuStrip();
            menu.Items.Add(_enabled);
            menu.Items.Add(new Forms.ToolStripSeparator());
            menu.Items.Add(_open);
            menu.Items.Add(_settings);
            menu.Items.Add(_refresh);
            menu.Items.Add(new Forms.ToolStripSeparator());
            menu.Items.Add(_exit);

            _icon = new Forms.NotifyIcon
            {
                Icon = LoadIcon(),
                Text = "Retro Menu",
                Visible = true,
                ContextMenuStrip = menu
            };
            _icon.MouseClick += (_, e) =>
            {
                if (e.Button == Forms.MouseButtons.Left) OpenRequested?.Invoke();
            };

            Localize();
        }

        public void Localize()
        {
            if (_open == null) return;
            _enabled.Text = Lang.T("UseRetroMenu");
            _open.Text = Lang.T("TrayOpen");
            _settings.Text = Lang.T("TraySettings");
            _refresh.Text = Lang.T("TrayRefresh");
            _exit.Text = Lang.T("TrayExit");
        }

        /// <summary>
        /// Shows the master switch. Switched off, opening the menu is not offered
        /// either: the Windows key belongs to Windows again, and so does the icon.
        /// </summary>
        public void SetEnabled(bool on)
        {
            if (_enabled == null) return;
            _enabled.Checked = on;
            _open.Enabled = on;
            if (_icon != null) _icon.Text = on ? "Retro Menu" : "Retro Menu (" + Lang.T("SwitchedOff") + ")";
        }

        private static Icon LoadIcon()
        {
            try
            {
                var stream = Application.GetResourceStream(
                    new Uri("/RetroMenu;component/Assets/retromenu.ico", UriKind.Relative));
                if (stream != null) return new Icon(stream.Stream);
            }
            catch { }
            return SystemIcons.Application;
        }

        public void Dispose()
        {
            if (_icon == null) return;
            _icon.Visible = false;
            _icon.Dispose();
            _icon = null;
        }
    }
}
