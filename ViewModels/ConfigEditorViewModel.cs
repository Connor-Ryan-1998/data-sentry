using System;
using System.ComponentModel;
using System.IO;
using System.Runtime.CompilerServices;
using System.Text.Json;
using System.Threading.Tasks;
using Avalonia.Controls;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using data_sentry.Models;
using System.Collections.Generic;

namespace data_sentry.ViewModels
{
    public partial class ConfigEditorViewModel : ObservableObject
    {
        // Existing properties
        [ObservableProperty]
        private string configJson;

        [ObservableProperty]
        private string configStatus;

        // New search properties
        [ObservableProperty]
        private string searchText;

        [ObservableProperty]
        private string searchStatus;

        // Current search state
        private int currentSearchPosition = -1;
        private List<int> searchMatches = new();

        // Commands
        public IRelayCommand LoadConfigCommand { get; }
        public IRelayCommand SaveConfigCommand { get; }
        public IRelayCommand FindNextCommand { get; }
        public IRelayCommand FindPreviousCommand { get; }

        private readonly string _configPath = "config.json";

        public ConfigEditorViewModel()
        {
            LoadConfigCommand = new RelayCommand(LoadConfig);
            SaveConfigCommand = new RelayCommand(SaveConfig);
            FindNextCommand = new RelayCommand(FindNext, () => !string.IsNullOrEmpty(SearchText));
            FindPreviousCommand = new RelayCommand(FindPrevious, () => !string.IsNullOrEmpty(SearchText));

            // When search text changes, reset search position
            this.PropertyChanged += (s, e) =>
            {
                if (e.PropertyName == nameof(SearchText))
                {
                    ResetSearch();

                    (FindNextCommand as RelayCommand)?.NotifyCanExecuteChanged();
                    (FindPreviousCommand as RelayCommand)?.NotifyCanExecuteChanged();
                }
            };

            LoadConfig();
        }

        private void ResetSearch()
        {
            currentSearchPosition = -1;
            searchMatches.Clear();

            if (string.IsNullOrEmpty(SearchText))
            {
                SearchStatus = string.Empty;
                return;
            }

            // Find all occurrences
            int index = 0;
            while ((index = ConfigJson.IndexOf(SearchText, index, StringComparison.OrdinalIgnoreCase)) != -1)
            {
                searchMatches.Add(index);
                index += SearchText.Length;
            }

            SearchStatus = searchMatches.Count > 0
                ? $"Found {searchMatches.Count} matches"
                : "No matches found";
        }

        private void FindNext()
        {
            if (searchMatches.Count == 0)
            {
                ResetSearch();
                if (searchMatches.Count == 0)
                    return;
            }

            currentSearchPosition = (currentSearchPosition + 1) % searchMatches.Count;
            ScrollToMatch();
        }

        private void FindPrevious()
        {
            if (searchMatches.Count == 0)
            {
                ResetSearch();
                if (searchMatches.Count == 0)
                    return;
            }

            currentSearchPosition = currentSearchPosition <= 0
                ? searchMatches.Count - 1
                : currentSearchPosition - 1;

            ScrollToMatch();
        }

        private void ScrollToMatch()
        {
            if (currentSearchPosition >= 0 && currentSearchPosition < searchMatches.Count)
            {
                // In a real implementation, you would use a reference to the TextBox
                // to select the text. For now, we just update the status.
                SearchStatus = $"Match {currentSearchPosition + 1} of {searchMatches.Count}";

                // We can broadcast this via an event that the View can listen to
                // to scroll to the position
                OnSearchPositionChanged(searchMatches[currentSearchPosition], SearchText.Length);
            }
        }

        // Event to notify the view about search position changes
        public event EventHandler<SearchPositionEventArgs> SearchPositionChanged;

        protected virtual void OnSearchPositionChanged(int position, int length)
        {
            SearchPositionChanged?.Invoke(this, new SearchPositionEventArgs(position, length));
        }

        // Existing methods
        private void LoadConfig()
        {
            try
            {
                if (File.Exists(_configPath))
                {
                    ConfigJson = File.ReadAllText(_configPath);
                    ConfigStatus = "Configuration loaded.";
                }
                else
                {
                    ConfigJson = "{\n  \n}";
                    ConfigStatus = "No config file found. Created new template.";
                }
            }
            catch (Exception ex)
            {
                ConfigStatus = $"Error loading config: {ex.Message}";
            }
        }

        private void SaveConfig()
        {
            try
            {
                // Validate JSON before saving
                JsonDocument.Parse(ConfigJson);
                File.WriteAllText(_configPath, ConfigJson);
                ConfigStatus = "Configuration saved.";
            }
            catch (JsonException)
            {
                ConfigStatus = "Invalid JSON. Please correct errors before saving.";
            }
            catch (Exception ex)
            {
                ConfigStatus = $"Error saving config: {ex.Message}";
            }
        }
    }

    public class SearchPositionEventArgs : EventArgs
    {
        public int Position { get; }
        public int Length { get; }

        public SearchPositionEventArgs(int position, int length)
        {
            Position = position;
            Length = length;
        }
    }
}