using System;
using System.Diagnostics;
using System.Threading.Tasks;
using System.Windows;
using RetroMenu.Services;

namespace RetroMenu.Views
{
    /// <summary>
    /// The setup wizard. The same executable is both the installer and the program:
    /// run under its Setup name it offers to install itself, and the copy it leaves
    /// behind runs as the menu.
    /// </summary>
    public partial class SetupWindow : Window
    {
        private readonly bool _uninstalling;
        private bool _finished;

        public SetupWindow(bool uninstall = false)
        {
            InitializeComponent();

            _uninstalling = uninstall;
            PathText.Text = Installer.InstallDirectory;
            VersionText.Text = "Retro Menu " +
                (System.Reflection.Assembly.GetEntryAssembly()?.GetName().Version?.ToString(3) ?? "0.1.0");

            if (uninstall)
            {
                Title = "Retro Menu entfernen";
                Subtitle.Text = "Das Programm wird aus Ihrem Benutzerordner entfernt.";
                InstallButton.Content = "Entfernen";
                OptionsPanel.Children.Clear();
                OptionsPanel.Children.Add(new System.Windows.Controls.TextBlock
                {
                    Text = "Retro Menu wird beendet und aus " + Installer.InstallDirectory +
                           " entfernt. Ihre Einstellungen bleiben erhalten, damit eine spätere " +
                           "Installation sie wiederfindet.",
                    TextWrapping = TextWrapping.Wrap
                });
            }
            else if (Installer.IsInstalled)
            {
                Subtitle.Text = "Eine vorhandene Installation wird aktualisiert.";
                InstallButton.Content = "Aktualisieren";
            }

            if (RetroBarInstaller.IsInstalled)
            {
                RetroBarBox.IsEnabled = false;
                RetroBarBox.Content = "RetroBar ist bereits vorhanden";
                RetroBarNote.Visibility = Visibility.Collapsed;
            }
        }

        private void Log(string line)
        {
            Dispatcher.Invoke(() =>
            {
                LogText.Text += (LogText.Text.Length > 0 ? Environment.NewLine : "") + line;
                LogScroll.ScrollToEnd();
            });
        }

        private async void OnInstall(object sender, RoutedEventArgs e)
        {
            if (_finished)
            {
                if (!_uninstalling) Start();
                Close();
                return;
            }

            var options = new InstallOptions
            {
                StartMenuShortcut = StartMenuBox.IsChecked == true,
                DesktopShortcut = DesktopBox.IsChecked == true,
                AutoStart = AutoStartBox.IsChecked == true,
                InstallRetroBar = RetroBarBox.IsChecked == true && RetroBarBox.IsEnabled
            };

            OptionsPanel.Visibility = Visibility.Collapsed;
            ProgressPanel.Visibility = Visibility.Visible;
            InstallButton.IsEnabled = false;
            CancelButton.IsEnabled = false;
            StatusText.Text = _uninstalling ? "Wird entfernt…" : "Wird eingerichtet…";

            bool ok = true;

            try
            {
                if (_uninstalling)
                {
                    await Task.Run(() => Installer.Uninstall(Log));
                }
                else
                {
                    await Task.Run(() => Installer.Install(options, Log));
                    if (options.InstallRetroBar)
                        await RetroBarInstaller.InstallAsync(Log);
                }
            }
            catch (Exception ex)
            {
                ok = false;
                Log("Fehlgeschlagen: " + ex.Message);
            }

            _finished = true;
            StatusText.Text = ok
                ? (_uninstalling ? "Entfernt." : "Fertig.")
                : "Mit Fehlern beendet.";
            InstallButton.Content = _uninstalling ? "Schließen" : "Starten";
            InstallButton.IsEnabled = true;
            CancelButton.Content = "Schließen";
            CancelButton.IsEnabled = true;
        }

        private void Start()
        {
            try
            {
                if (!Installer.IsInstalled) return;
                Process.Start(new ProcessStartInfo(Installer.InstalledExecutable)
                {
                    UseShellExecute = true,
                    WorkingDirectory = Installer.InstallDirectory
                });

                if (RetroBarInstaller.IsInstalled &&
                    Process.GetProcessesByName("RetroBar").Length == 0)
                {
                    Process.Start(new ProcessStartInfo(RetroBarInstaller.ExecutablePath)
                    {
                        UseShellExecute = true,
                        WorkingDirectory = RetroBarInstaller.InstallDirectory
                    });
                }
            }
            catch { }
        }

        private void OnCancel(object sender, RoutedEventArgs e) => Close();
    }
}
