using Avalonia.Controls;

namespace data_sentry.Views;

using data_sentry.ViewModels;

public partial class ConfigEditorView : UserControl
{
    public ConfigEditorView()
    {
        InitializeComponent();
        DataContext = new ConfigEditorViewModel();
    }
}
