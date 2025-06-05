
using data_sentry.ViewModels;
using Avalonia.Controls;
using Avalonia.Markup.Xaml;

namespace data_sentry.Views;

public partial class ErrorView : Window
{
    public ErrorView()
    {
        InitializeComponent();
        DataContext = new ErrorViewModel();
    }
    private void InitializeComponent()
    {
        AvaloniaXamlLoader.Load(this);
    }

    private void OkButton_Click(object sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        Close();
    }
}
