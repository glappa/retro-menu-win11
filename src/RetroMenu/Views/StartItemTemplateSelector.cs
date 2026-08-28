using System.Windows;
using System.Windows.Controls;
using RetroMenu.Model;
using RetroMenu.Services;

namespace RetroMenu.Views
{
    public sealed class StartItemTemplateSelector : DataTemplateSelector
    {
        public DataTemplate ItemTemplate { get; set; }
        public DataTemplate SeparatorTemplate { get; set; }

        public override DataTemplate SelectTemplate(object item, DependencyObject container)
        {
            if (item is StartItem entry && entry.Command == Launcher.Separator)
                return SeparatorTemplate;
            return ItemTemplate;
        }
    }
}
