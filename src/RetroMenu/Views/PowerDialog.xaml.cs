using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Shapes;
using RetroMenu.Services;

namespace RetroMenu.Views
{
    /// <summary>The XP style "Turn off computer" panel, with the coloured orbs.</summary>
    public partial class PowerDialog : Window
    {
        private static readonly Dictionary<string, (string Path, Color From, Color To)> Orbs =
            new()
            {
                ["standby"] = ("M 10,2 A 8,8 0 1 0 18,10 A 6.4,6.4 0 1 1 10,2 Z",
                               Color.FromRgb(0xFF, 0xC7, 0x4A), Color.FromRgb(0xD2, 0x86, 0x00)),
                ["hibernate"] = ("M 10,2 A 8,8 0 1 0 18,10 A 6.4,6.4 0 1 1 10,2 Z",
                                 Color.FromRgb(0x9E, 0xC8, 0xF0), Color.FromRgb(0x2E, 0x6C, 0xB0)),
                ["shutdown"] = ("M 10,2 L 10,10 M 5.2,4.8 A 6.6,6.6 0 1 0 14.8,4.8",
                                Color.FromRgb(0xF0, 0x74, 0x60), Color.FromRgb(0xB0, 0x1C, 0x10)),
                ["restart"] = ("M 10,3 A 7,7 0 1 0 17,10 M 10,0 L 10,6 M 7,3 L 13,3",
                               Color.FromRgb(0x8C, 0xCB, 0x4A), Color.FromRgb(0x33, 0x74, 0x14)),
                ["logoff"] = ("M 8,3 L 3,3 L 3,17 L 8,17 M 10,10 L 17,10 M 14,7 L 17,10 L 14,13",
                              Color.FromRgb(0xFF, 0xB8, 0x43), Color.FromRgb(0xC0, 0x6A, 0x00)),
                ["lock"] = ("M 5,9 L 15,9 L 15,17 L 5,17 Z M 7,9 A 3,3 0 0 1 13,9",
                            Color.FromRgb(0x8E, 0xB8, 0xE8), Color.FromRgb(0x21, 0x50, 0xA8)),
            };

        public PowerDialog((string Key, string Command)[] choices)
        {
            InitializeComponent();

            TitleText.Text = Lang.T("PowerTitle");
            CancelButton.Content = Lang.T("Cancel");

            foreach (var (key, command) in choices)
                Choices.Children.Add(BuildOrb(key, command));

            PreviewKeyDown += (_, e) =>
            {
                if (e.Key == Key.Escape) Close();
            };
        }

        private Button BuildOrb(string labelKey, string command)
        {
            Orbs.TryGetValue(command, out var art);
            if (art.Path == null) art = Orbs["shutdown"];

            var glyph = new Path
            {
                Data = Geometry.Parse(art.Path),
                Stroke = Brushes.White,
                StrokeThickness = 1.8,
                StrokeStartLineCap = PenLineCap.Round,
                StrokeEndLineCap = PenLineCap.Round,
                Width = 20,
                Height = 20,
                Stretch = Stretch.None,
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center
            };

            var orb = new Border
            {
                Width = 46,
                Height = 46,
                CornerRadius = new CornerRadius(23),
                BorderBrush = new SolidColorBrush(Color.FromArgb(0x88, 0xFF, 0xFF, 0xFF)),
                BorderThickness = new Thickness(2),
                Background = new LinearGradientBrush(art.From, art.To, 90),
                Child = glyph,
                HorizontalAlignment = HorizontalAlignment.Center
            };

            var label = new TextBlock
            {
                Text = Lang.T(labelKey),
                Foreground = Brushes.White,
                FontWeight = FontWeights.Bold,
                Margin = new Thickness(0, 7, 0, 0),
                HorizontalAlignment = HorizontalAlignment.Center,
                TextAlignment = TextAlignment.Center
            };

            var stack = new StackPanel { Width = 96 };
            stack.Children.Add(orb);
            stack.Children.Add(label);

            var button = new Button
            {
                Style = (Style)FindResource("OrbButton"),
                Content = stack
            };
            button.Click += (_, __) =>
            {
                Close();
                Launcher.Power(command);
            };
            return button;
        }

        private void OnCancel(object sender, RoutedEventArgs e) => Close();
    }
}
