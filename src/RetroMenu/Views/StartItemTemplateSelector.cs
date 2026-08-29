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
        public DataTemplate HeaderTemplate { get; set; }
        public DataTemplate LogOffTemplate { get; set; }
        public DataTemplate ShutDownTemplate { get; set; }

        public override DataTemplate SelectTemplate(object item, DependencyObject container)
        {
            if (item is not StartItem entry) return ItemTemplate;

            if (entry.Command == Launcher.Separator) return SeparatorTemplate;
            if (entry.Command == Launcher.GroupHeader) return HeaderTemplate ?? ItemTemplate;

            if (entry.TemplateKey == "logoff" && LogOffTemplate != null) return LogOffTemplate;
            if (entry.TemplateKey == "shutdown" && ShutDownTemplate != null) return ShutDownTemplate;

            return ItemTemplate;
        }
    }
}
