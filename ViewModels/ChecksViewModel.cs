using Avalonia.Platform.Storage; // Add this using statement
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using data_sentry.Models;
using data_sentry.Views;
using Avalonia.Controls;
using data_sentry.Services;

namespace data_sentry.ViewModels
{
    public partial class ChecksViewModel : ObservableObject, IDisposable
    {

        // Dictionary to store connections by identifier (e.g., account name)
        private Dictionary<string, object> _connectionPool = new Dictionary<string, object>();

        // Config editor properties
        [ObservableProperty]
        private string configJson;
        [ObservableProperty]
        private string changeConfigJson;

        [ObservableProperty]
        private string configStatus;
        [ObservableProperty]
        private string name;

        [ObservableProperty]
        private string status;
        [ObservableProperty]
        private bool isDaemonMode;

        public DaemonService? DaemonService { get; set; }

        public IRelayCommand<CheckRecord> OpenResultsCommand { get; }
        public IRelayCommand RunAllChecksCommand { get; }
        public IAsyncRelayCommand ExportResultsCommand { get; }
        public IRelayCommand<CheckRecord> CheckCommand { get; }
        [RelayCommand]
        private void ToggleDaemonMode()
        {
            if (DaemonService != null)
            {
                // IsDaemonMode = !IsDaemonMode;
                DaemonService.IsDaemonMode = IsDaemonMode;
                // We only realistically need to hit everything once per hour
                DaemonService.CheckInterval = TimeSpan.FromHours(1);
            }
        }

        private readonly string _configPath = "config.json";

        // Checks grid properties
        [ObservableProperty]
        private ObservableCollection<CheckRecord> activeChecks = new();

        [ObservableProperty]
        private string statusMessage;
        public int SuccessfulChecksCount => ActiveChecks.Count(c => c.Status.Contains("Success") || c.Status.StartsWith("Checked at"));
        public int FailedChecksCount => ActiveChecks.Count(c => c.Status.Contains("Failed") || c.Status.Contains("Error"));
        public int PendingChecksCount => ActiveChecks.Count(c => c.Status == "Pending" || c.Status == "Ongoing");

        public double SuccessfulChecksPercentage => ActiveChecks.Count > 0 ? (double)SuccessfulChecksCount / ActiveChecks.Count : 0;
        public double FailedChecksPercentage => ActiveChecks.Count > 0 ? (double)FailedChecksCount / ActiveChecks.Count : 0;
        public double PendingChecksPercentage => ActiveChecks.Count > 0 ? (double)PendingChecksCount / ActiveChecks.Count : 0;


        public ChecksViewModel()
        {
            CheckCommand = new RelayCommand<CheckRecord>(Check);
            OpenResultsCommand = new RelayCommand<CheckRecord>(OpenResults);
            RunAllChecksCommand = new RelayCommand(RunAllChecks);
            ExportResultsCommand = new AsyncRelayCommand(ExportResultsAsync);

            LoadChecks();
        }

        private void OpenResults(CheckRecord record)
        {
            if (record != null && record.ResultData.HasValue)
            {
                var window = new ResultsView(record.Description, record.ResultData);
                window.Show();
            }
        }
        public void LoadConfig()
        {
            try
            {
                if (File.Exists(_configPath))
                {
                    // state comparison. This can be null
                    ChangeConfigJson = ConfigJson;

                    // Read the configuration file
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
        private SqlServerModel GetSqlServerConnection(CheckRecord record)
        {
            // Use server as the connection key
            string connectionKey = "sqlserver_" + record.JsonData.GetProperty("server").GetString();

            if (!_connectionPool.ContainsKey(connectionKey))
            {
                var model = new SqlServerModel
                {
                    Server = record.JsonData.GetProperty("server").GetString(),

                    // Check if database is specified, otherwise use master
                    Database = record.JsonData.TryGetProperty("database", out var db)
                        ? db.GetString()
                        : "master",

                    // Check for user credentials or use integrated security
                    UseIntegratedSecurity = !record.JsonData.TryGetProperty("user", out var _) ||
                                            string.IsNullOrEmpty(record.JsonData.GetProperty("user").GetString())
                };

                // Only set user and password if integrated security is not used
                if (!model.UseIntegratedSecurity)
                {
                    model.User = record.JsonData.GetProperty("user").GetString();
                    model.Password = record.JsonData.GetProperty("password").GetString();
                }

                // Check for connection timeout if specified
                if (record.JsonData.TryGetProperty("timeout", out var timeout))
                {
                    model.ConnectionTimeout = timeout.GetInt32();
                }

                _connectionPool[connectionKey] = model;
            }

            return (SqlServerModel)_connectionPool[connectionKey];
        }
        private SnowflakeModel GetConnection(CheckRecord record)
        {
            string connectionKey = record.JsonData.GetProperty("account").GetString();

            if (!_connectionPool.ContainsKey(connectionKey))
            {
                var model = new SnowflakeModel
                {
                    Account = record.JsonData.GetProperty("account").GetString(),
                    User = record.JsonData.GetProperty("user_name").GetString(),
                    Database = "DEV_ODS_ANZ",
                    Schema = "",
                    Password = record.JsonData.GetProperty("password").GetString(),
                    Warehouse = record.JsonData.GetProperty("warehouse").GetString(),
                    Role = record.JsonData.GetProperty("role").GetString(),
                    Authenticator = record.JsonData.GetProperty("authenticator").GetString(),
                };

                _connectionPool[connectionKey] = model;
            }

            return (SnowflakeModel)_connectionPool[connectionKey];
        }

        public void LoadChecks()
        {
            try
            {
                LoadConfig();
                JsonDocument.Parse(ConfigJson);

                // Parse the JSON document
                var jsonDoc = JsonDocument.Parse(ConfigJson);
                var rootElement = jsonDoc.RootElement;

                if (ChangeConfigJson == null || ChangeConfigJson != ConfigJson)
                {
                    // Clear existing checks
                    ActiveChecks.Clear();
                    // Process each property in the root element as a check
                    if (rootElement.ValueKind == JsonValueKind.Array)
                    {
                        foreach (var element in rootElement.EnumerateArray())
                        {
                            if (element.TryGetProperty("sentry_type", out JsonElement nameElement))
                            {
                                var checkType = nameElement.GetString() ?? "Unnamed Check";
                                var checkDescription = element.TryGetProperty("description", out JsonElement descriptionElement)
                                    ? descriptionElement.GetString() ?? "No description"
                                    : "No description";
                                var checkStatus = "Pending";


                                // Add the check to the collection
                                ActiveChecks.Add(new CheckRecord
                                {
                                    Type = checkType,
                                    Description = checkDescription,
                                    Status = checkStatus,
                                    JsonData = element.Clone()
                                });
                            }
                        }
                    }
                }
                StatusMessage = $"Loaded {ActiveChecks.Count} checks from configuration.";

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

        // Add this new method to return a Task
        private async Task CheckAsync(CheckRecord record)
        {
            try
            {
                if (record != null)
                {
                    record.Status = "Ongoing";

                    // Run a check operation
                    if (record.Type == "snowflake")
                    {
                        var model = GetConnection(record);

                        // Act - Simple query that should work on any Snowflake instance
                        var sqlQuery = record.JsonData.GetProperty("sql_query").GetString() ?? "SELECT 'Query not set' AS message";
                        record.ResultData = await model.ExecuteQueryAsync(sqlQuery);
                    }
                    else if (record.Type == "adf")
                    {
                        var model = new AzureDataFactoryModel
                        {
                            SubscriptionId = record.JsonData.GetProperty("subscription_id").GetString(),
                            ResourceGroupName = record.JsonData.GetProperty("resource_group_name").GetString(),
                            FactoryName = record.JsonData.GetProperty("factory_name").GetString(),
                            TimezoneDelta = record.JsonData.TryGetProperty("timezone_delta", out var tz) ? tz.GetInt32() : 0,
                        };
                        // Add SHIR list if present
                        if (record.JsonData.TryGetProperty("shir_list", out var shirList) && shirList.ValueKind == JsonValueKind.Array)
                        {
                            foreach (var shir in shirList.EnumerateArray())
                            {
                                model.ShirList.Add(shir.GetString());
                            }
                        }
                        record.ResultData = await model.GetComprehensiveStatusAsync();
                    }
                    else if (record.Type == "sqlserver")
                    {
                        // Get or create a SQL Server connection
                        var model = GetSqlServerConnection(record);

                        // Execute the SQL query from the configuration
                        var sqlQuery = record.JsonData.GetProperty("sql_query").GetString() ?? "SELECT 'Query not set' AS message";
                        record.ResultData = await model.ExecuteQueryAsync(sqlQuery);
                    }
                    else if (record.Type == "jira")
                    {
                        var model = new JiraModel
                        {
                            JiraUrl = record.JsonData.GetProperty("server").GetString(),
                            Email = record.JsonData.GetProperty("username").GetString(),
                            ApiKey = record.JsonData.GetProperty("access_token").GetString(),
                            RequestTimeout = record.JsonData.TryGetProperty("timeout", out var timeout) ? timeout.GetInt32() : 30,
                        };

                        // Get JQL query from configuration
                        string jql = record.JsonData.GetProperty("jql_query").GetString();

                        // Get optional parameters if they exist
                        int maxResults = record.JsonData.TryGetProperty("max_results", out var max) ? max.GetInt32() : 100;

                        // Execute the JQL query
                        record.ResultData = await model.GetIssueKeysAndSummariesAsync(jql, maxResults);

                        // Check if there are issues based on count
                        if (record.ResultData.HasValue && record.ResultData.Value.TryGetProperty("total", out var totalIssues))
                        {
                            int total = totalIssues.GetInt32();
                            record.Status = total > 0
                                ? $"Found {total} issues at {DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss")}"
                                : "No issues found at " + DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");
                        }
                        else
                        {
                            record.Status = "Investigate";
                        }
                    }
                    // Status check logic
                    record.StatusCheckValidation();
                }
            }
            catch (Exception ex)
            {
                StatusMessage = $"Error checking {record.Type}: {ex.Message}";
                record.Status = "Error";
            }
            finally
            {
                // Notify UI of property changes
                OnPropertyChanged(nameof(SuccessfulChecksCount));
                OnPropertyChanged(nameof(FailedChecksCount));
                OnPropertyChanged(nameof(PendingChecksCount));
                OnPropertyChanged(nameof(SuccessfulChecksPercentage));
                OnPropertyChanged(nameof(FailedChecksPercentage));
                OnPropertyChanged(nameof(PendingChecksPercentage));

                // Update the status message
                StatusMessage = $"Checked {record.Description} at " + DateTime.Now.ToLongDateString();
            }
        }

        private async void Check(CheckRecord record)
        {
            _ = CheckAsync(record).ContinueWith(_ =>
            {
                // Update the status message in the UI thread
                StatusMessage = $"Checked {record.Description} at " + DateTime.Now.ToLongDateString();
            }, TaskScheduler.FromCurrentSynchronizationContext());
        }

        // Make RunAllChecks async and properly await each check
        private async void RunAllChecks()
        {
            try
            {
                StatusMessage = "Running all checks...";
                int completed = 0;
                int total = ActiveChecks.Count(c => c.Status != "Ongoing");

                foreach (var record in ActiveChecks)
                {
                    if (record.Status != "Ongoing")
                    {
                        await CheckAsync(record);
                        completed++;
                        StatusMessage = $"Completed {completed} of {total} checks...";

                        // Add a small delay to ensure clean state between checks
                        await Task.Delay(100);
                    }
                }

                // This will now be set only after all checks have completed
                StatusMessage = "All checks completed at " + DateTime.Now.ToLongDateString();
            }
            catch (Exception ex)
            {
                // Use the global exception handler
                App.ShowExceptionDialog(ex, "Error Running Checks");
            }
        }
        private async Task ExportResultsAsync()
        {
            StatusMessage = "Preparing to export results...";

            // Get the top-level window that contains our view
            var topLevel = TopLevel.GetTopLevel(App.MainWindow);
            if (topLevel == null)
            {
                StatusMessage = "Unable to open file dialog";
                return;
            }

            // Configure save file dialog
            var options = new FilePickerSaveOptions
            {
                Title = "Export Check Results",
                SuggestedFileName = $"check-results-{DateTime.Now:yyyyMMdd-HHmmss}",
                FileTypeChoices = new[]
                {
                new FilePickerFileType("CSV Files (*.csv)") { Patterns = new[] { "*.csv" } },
                new FilePickerFileType("JSON Files (*.json)") { Patterns = new[] { "*.json" } }
            }
            };

            // Show the save dialog
            var file = await topLevel.StorageProvider.SaveFilePickerAsync(options);
            if (file == null)
            {
                StatusMessage = "Export cancelled";
                return;
            }

            try
            {
                string extension = Path.GetExtension(file.Name).ToLowerInvariant();
                await using var stream = await file.OpenWriteAsync();
                await using var writer = new StreamWriter(stream);

                if (extension == ".csv")
                {
                    await ExportToCsvAsync(writer);
                }
                else if (extension == ".json")
                {
                    await ExportToJsonAsync(writer);
                }

                StatusMessage = $"Results exported successfully to {file.Name}";
            }
            catch (Exception ex)
            {
                StatusMessage = $"Export failed: {ex.Message}";
            }
        }

        private async Task ExportToCsvAsync(TextWriter writer)
        {
            // Write CSV header
            await writer.WriteLineAsync("Description,Type,Status,Result");

            // Write data rows
            foreach (var check in ActiveChecks)
            {
                string resultText = check.ResultData.HasValue ?
                    check.ResultData.Value.ToString().Replace("\"", "\"\"") : "";

                await writer.WriteLineAsync(
                    $"\"{check.Description}\",\"{check.Type}\",\"{check.Status}\",\"{resultText}\"");
            }
        }

        private async Task ExportToJsonAsync(TextWriter writer)
        {
            var exportData = ActiveChecks.Select(c => new
            {
                Description = c.Description,
                Type = c.Type,
                Status = c.Status,
                Result = c.ResultData.HasValue ?
                    JsonSerializer.Deserialize<object>(c.ResultData.Value.GetRawText()) : null
            }).ToList();

            var options = new JsonSerializerOptions
            {
                WriteIndented = true
            };

            await writer.WriteAsync(JsonSerializer.Serialize(exportData, options));
        }
        // Clean up all connections when the ViewModel is disposed
        public void Dispose()
        {
            foreach (var connection in _connectionPool.Values)
            {
                (connection as IDisposable)?.Dispose();
            }
            _connectionPool.Clear();
        }
    }
}
