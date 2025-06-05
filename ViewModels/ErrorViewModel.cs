using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using System;

namespace data_sentry.ViewModels
{
    public partial class ErrorViewModel : ObservableObject
    {
        [ObservableProperty]
        private string title;

        [ObservableProperty]
        private string message;

        [ObservableProperty]
        private string details;

        [ObservableProperty]
        private bool showDetails;

        [RelayCommand]
        private void ToggleDetails()
        {
            ShowDetails = !ShowDetails;
        }
    }
}