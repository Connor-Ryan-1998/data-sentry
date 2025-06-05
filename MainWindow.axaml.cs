using Avalonia.Controls;
using data_sentry.Services;
using data_sentry.ViewModels;
using Avalonia;
using System;
using System.Threading.Tasks;
using Avalonia.Platform;
using System.Linq;

namespace data_sentry.Views;

public partial class MainWindow : Window
{
    private DaemonService? _daemonService;
    private TrayIcon? _notifyIcon;

    public MainWindow()
    {
        InitializeComponent();

        // Create the view model ONCE at the application level - we pass application ViewModel to the required view.
        // This is to allow for Daemon services
        DataContext = new ChecksViewModel();

        var screen = Screens.Primary;
        if (screen != null)
        {
            Width = screen.WorkingArea.Width / 1.25;
            Height = screen.WorkingArea.Height / 1.25;
        }

        this.Closing += MainWindow_Closing;
        this.Opened += MainWindow_Opened;
    }

    private void MainWindow_Opened(object? sender, EventArgs e)
    {
        var checksViewModel = this.DataContext as ChecksViewModel;
        if (checksViewModel != null)
        {

            // Initialize daemon service - otherwise this gets reset on reopen from task bar
            if (_daemonService == null)
            {
                // This allows for the service to be reused across multiple windows
                _daemonService = new DaemonService(checksViewModel);
                checksViewModel.DaemonService = _daemonService;
            }

        }
    }

    private void MainWindow_Closing(object? sender, WindowClosingEventArgs e)
    {
        // If we're in daemon mode, just minimize to tray instead of closing
        if (_daemonService?.IsDaemonMode == true)
        {
            e.Cancel = true;
            this.WindowState = WindowState.Minimized;
            this.Hide();
            SystemTrayIcon();

            if (_notifyIcon != null)
            {
                // Shouldnt ever be null but VS Code yells at me
                _daemonService._trayIcon = _notifyIcon;
            }

        }
        else
        {
            // If not in daemon mode, clean up resources
            _daemonService?.Dispose();
        }
    }
    private void SystemTrayIcon()
    {
        try
        {
            // Early exit for already existing icon
            if (_notifyIcon != null)
            {
                // If the icon already exists, just update the tooltip
                _notifyIcon.ToolTipText = "Data Sentry - Monitoring Active";
                return;
            }

            using var iconStream = AssetLoader.Open(new Uri("avares://data-sentry/Assets/data-sentry.ico"));
            _notifyIcon = new TrayIcon
            {
                Icon = new WindowIcon(iconStream),
                ToolTipText = "Data Sentry - Monitoring Active"
            }
            ;

            // Set up context menu
            _notifyIcon.Menu = new NativeMenu();

            var showItem = new NativeMenuItem("Show Data Sentry");
            showItem.Click += (s, e) => ShowMainWindow();

            var exitItem = new NativeMenuItem("Exit Application");
            exitItem.Click += (s, e) => ExitApplication();



            _notifyIcon.Menu.Items.Add(showItem);
            _notifyIcon.Menu.Items.Add(new NativeMenuItemSeparator());
            _notifyIcon.Menu.Items.Add(exitItem);

            // Handle double-click to show the app
            _notifyIcon.Clicked += (s, e) => ShowMainWindow();

            // Make the icon visible
            _notifyIcon.IsVisible = true;

        }
        catch (Exception ex)
        {
            Console.WriteLine($"Failed to initialize taskbar icon: {ex.Message}");
        }
    }
    public void ShowMainWindow()
    {
        this.Show();
        this.WindowState = WindowState.Normal;
    }
    public void ExitApplication()
    {
        this.Close();
    }
}
