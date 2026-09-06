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

            // The wizard speaks the same language as the menu it is about to install,
            // which App has already settled from the Windows display language.
            Title = Lang.T("SetupTitle");
            HeaderTitle.Text = Lang.T("SetupTitle");
            Subtitle.Text = Lang.T("SetupSubtitle");
            IntroText.Text = Lang.T("SetupIntro");
            TargetLabel.Text = Lang.T("SetupTarget");
            DuringLabel.Text = Lang.T("SetupDuring");
            StartMenuBox.Content = Lang.T("SetupStartMenu");
            DesktopBox.Content = Lang.T("SetupDesktop");
            AutoStartBox.Content = Lang.T("AutoStart");
            RetroBarBox.Content = Lang.T("SetupRetroBar");
            RetroBarNote.Text = Lang.T("SetupRetroBarNote");
            CancelButton.Content = Lang.T("Cancel");
            InstallButton.Content = Lang.T("SetupInstall");

            if (uninstall)
            {
                Title = Lang.T("SetupRemoveTitle");
                HeaderTitle.Text = Lang.T("SetupRemoveTitle");
                Subtitle.Text = Lang.T("SetupRemoveSubtitle");
                InstallButton.Content = Lang.T("SetupRemoveButton");
                OptionsPanel.Children.Clear();
                OptionsPanel.Children.Add(new System.Windows.Controls.TextBlock
                {
                    Text = Lang.F("SetupRemoveText", Installer.InstallDirectory),
                    TextWrapping = TextWrapping.Wrap
                });
            }
            else if (Installer.IsInstalled)
            {
                Subtitle.Text = Lang.T("SetupUpdateSubtitle");
                InstallButton.Content = Lang.T("SetupUpdate");
            }

            if (RetroBarInstaller.IsInstalled)
            {
                RetroBarBox.IsEnabled = false;
                RetroBarBox.Content = Lang.T("SetupRetroBarPresent");
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
            StatusText.Text = Lang.T(_uninstalling ? "SetupRemoving" : "SetupWorking");

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
                Log(Lang.F("SetupFailed", ex.Message));
            }

            _finished = true;
            StatusText.Text = ok
                ? Lang.T(_uninstalling ? "SetupRemoved" : "SetupDone")
                : Lang.T("SetupErrors");
            InstallButton.Content = Lang.T(_uninstalling ? "Close" : "SetupStart");
            InstallButton.IsEnabled = true;
            CancelButton.Content = Lang.T("Close");
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
