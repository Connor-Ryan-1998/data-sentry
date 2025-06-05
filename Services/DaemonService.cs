using Avalonia.Controls;
using data_sentry.ViewModels;
using System;
using System.Threading;
using System.Threading.Tasks;

namespace data_sentry.Services
{
    public class DaemonService : IDisposable
    {
        private readonly ChecksViewModel _checksViewModel;
        public TrayIcon _trayIcon;
        private Timer? _timer;
        private bool _isDaemonMode = false;
        private TimeSpan _checkInterval = TimeSpan.FromHours(1);

        public DaemonService(ChecksViewModel checksViewModel)
        {
            _checksViewModel = checksViewModel;
        }

        public bool IsDaemonMode
        {
            get => _isDaemonMode;
            set
            {
                _isDaemonMode = value;
                if (_isDaemonMode)
                {
                    StartDaemonMode();
                }
                else
                {
                    StopDaemonMode();
                }
            }
        }

        public TimeSpan CheckInterval
        {
            get => _checkInterval;
            set
            {
                _checkInterval = value;
                if (_isDaemonMode)
                {
                    // Restart timer with new interval
                    StopDaemonMode();
                    StartDaemonMode();
                }
            }
        }

        private void StartDaemonMode()
        {

            if (_timer == null)
            {
                // Set up timer for periodic runs
                _timer = new Timer(OnTimerElapsed, null,
                    _checkInterval, _checkInterval);
                UpdateToolTip($"Data Sentry - Monitoring Last Run ({GetIntervalDisplayText()})");
            }
        }

        private void StopDaemonMode()
        {
            if (_timer != null)
            {
                _timer.Dispose();
                _timer = null;
            }
        }

        private void OnTimerElapsed(object? state)
        {
            RunAllChecks();
        }

        private async void RunAllChecks()
        {
            try
            {
                // Run all checks via the view model
                await Task.Run(() => _checksViewModel.RunAllChecksCommand.Execute(null));

                UpdateToolTip($"Data Sentry - ERROR: {GetIntervalDisplayText()}");
            }
            catch (Exception ex)
            {
                UpdateToolTip($"Data Sentry - ERROR: {ex.Message}");
            }
        }

        private string GetIntervalDisplayText()
        {
            if (_checkInterval.TotalHours >= 1)
                return $"{_checkInterval.TotalHours:F1} hours";
            else if (_checkInterval.TotalMinutes >= 1)
                return $"{_checkInterval.TotalMinutes:F0} minutes";
            else
                return $"{_checkInterval.TotalSeconds:F0} seconds";
        }

        public void Dispose()
        {
            if (_timer != null)
            {
                _timer.Dispose();
                _timer = null;
            }
        }
        internal void UpdateToolTip(string ToolTipText = "Data Sentry - Monitoring Active")
        {
            if (_trayIcon != null)
            {
                _trayIcon.ToolTipText = ToolTipText;
            }
        }
    }
}