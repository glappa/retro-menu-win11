using System.Windows;
using System.Windows.Input;
using RetroMenu.Services;

namespace RetroMenu.Views
{
    /// <summary>A one line prompt, for naming and renaming favourite folders.</summary>
    public partial class InputDialog : Window
    {
        public string Value => Input.Text.Trim();

        public InputDialog(string prompt, string value = "")
        {
            InitializeComponent();

            PromptText.Text = prompt;
            Input.Text = value ?? "";
            OkButton.Content = Lang.T("Ok");
            CancelButton.Content = Lang.T("Cancel");

            Loaded += (_, __) => { Input.Focus(); Input.SelectAll(); };
        }

        /// <summary>Shows the prompt and returns the text, or null if it was dismissed.</summary>
        public static string Ask(Window owner, string prompt, string value = "")
        {
            var dialog = new InputDialog(prompt, value);
            if (owner != null && owner.IsVisible)
            {
                dialog.Owner = owner;
                dialog.WindowStartupLocation = WindowStartupLocation.CenterOwner;
            }

            return dialog.ShowDialog() == true && dialog.Value.Length > 0 ? dialog.Value : null;
        }

        private void OnKeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter) { OnOk(sender, e); e.Handled = true; }
            else if (e.Key == Key.Escape) { OnCancel(sender, e); e.Handled = true; }
        }

        private void OnOk(object sender, RoutedEventArgs e)
        {
            if (Value.Length == 0) return;
            DialogResult = true;
        }

        private void OnCancel(object sender, RoutedEventArgs e) => DialogResult = false;
    }
}
