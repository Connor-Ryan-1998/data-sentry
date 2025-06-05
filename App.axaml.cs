using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using data_sentry.ViewModels;
using data_sentry.Views;
using Avalonia.Controls;
using System;
using System.Threading.Tasks;

namespace data_sentry;

public partial class App : Application
{
    public override void Initialize()
    {
        AvaloniaXamlLoader.Load(this);
    }
    public static Window MainWindow { get; set; }

    public override void OnFrameworkInitializationCompleted()
    {
        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            desktop.MainWindow = new MainWindow();
            MainWindow = desktop.MainWindow; // Store reference
                                             // Set up global exception handler
            AppDomain.CurrentDomain.UnhandledException += OnUnhandledException;
            TaskScheduler.UnobservedTaskException += OnUnobservedTaskException;

        }

        base.OnFrameworkInitializationCompleted();
    }
    private void OnUnhandledException(object sender, UnhandledExceptionEventArgs e)
    {
        var exception = e.ExceptionObject as Exception;
        ShowExceptionDialog(exception, "Unhandled Application Exception");
    }

    private void OnUnobservedTaskException(object sender, UnobservedTaskExceptionEventArgs e)
    {
        e.SetObserved(); // Mark as observed so it won't crash the app
        ShowExceptionDialog(e.Exception, "Unhandled Task Exception");
    }

    public static void ShowExceptionDialog(Exception exception, string title = "Application Error")
    {
        if (Application.Current?.ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            Avalonia.Threading.Dispatcher.UIThread.InvokeAsync(async () =>
            {
                var errorViewModel = new ErrorViewModel
                {
                    Title = title,
                    Message = exception?.Message ?? "An unknown error occurred.",
                    Details = exception?.ToString() ?? ""
                };

                var dialog = new ErrorView
                {
                    DataContext = errorViewModel
                };

                await dialog.ShowDialog(desktop.MainWindow);
            });
        }
    }
}