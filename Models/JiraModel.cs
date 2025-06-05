using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;

namespace data_sentry.Models
{
    public partial class JiraModel : ObservableObject, IDisposable
    {
        private HttpClient _httpClient;
        private bool _isDisposed;

        // Connection properties
        [ObservableProperty]
        private string? jiraUrl;

        [ObservableProperty]
        private string? email;

        [ObservableProperty]
        private string? apiKey;

        // Options
        [ObservableProperty]
        private int requestTimeout = 30;

        // Status
        [ObservableProperty]
        private string? connectionStatus;

        // Track connection state
        [ObservableProperty]
        private bool isConnected;

        // Constructor
        public JiraModel()
        {
            _httpClient = new HttpClient();
            _httpClient.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
            _httpClient.Timeout = TimeSpan.FromSeconds(RequestTimeout);
        }

        // Configure the HTTP client with authentication
        private void ConfigureHttpClient()
        {
            if (_httpClient == null)
            {
                _httpClient = new HttpClient();
                _httpClient.DefaultRequestHeaders.Accept.Clear();
                _httpClient.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
            }

            _httpClient.Timeout = TimeSpan.FromSeconds(RequestTimeout);

            // Add basic auth header with email and API key
            string auth = Convert.ToBase64String(Encoding.ASCII.GetBytes($"{Email}:{ApiKey}"));
            _httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Basic", auth);
        }
        // Search for issues using JQL (Jira Query Language)
        public async Task<JsonElement> ExecuteJqlQueryAsync(string jql, int maxResults = 100, int startAt = 0, List<string> fields = null)
        {
            try
            {
                ConfigureHttpClient();

                // Create search payload
                var searchData = new Dictionary<string, object>
                {
                    ["jql"] = jql,
                    ["startAt"] = startAt,
                    ["maxResults"] = maxResults
                };

                // Add fields if specified
                if (fields != null && fields.Count > 0)
                {
                    searchData["fields"] = fields;
                }

                var searchContent = new StringContent(
                    JsonSerializer.Serialize(searchData),
                    Encoding.UTF8,
                    "application/json");

                // Execute search
                var response = await _httpClient.PostAsync($"{JiraUrl}/rest/api/2/search", searchContent);

                if (response.IsSuccessStatusCode)
                {
                    var content = await response.Content.ReadAsStringAsync();
                    var searchResults = JsonDocument.Parse(content).RootElement;

                    int totalResults = searchResults.GetProperty("total").GetInt32();
                    ConnectionStatus = $"Query executed successfully. Total issues: {totalResults}";

                    return searchResults;
                }
                else
                {
                    var errorContent = await response.Content.ReadAsStringAsync();
                    ConnectionStatus = $"Query failed: {response.StatusCode} - {errorContent}";
                    return JsonDocument.Parse("{ \"error\": true, \"message\": \"" + response.StatusCode + "\" }").RootElement;
                }
            }
            catch (Exception ex)
            {
                ConnectionStatus = $"Query failed: {ex.Message}";
                Console.WriteLine(ex.ToString());

                // Create a JSON structure with error details instead of returning an empty array
                var errorResult = new Dictionary<string, object>
                {
                    ["error"] = true,
                    ["message"] = ex.Message,
                    ["details"] = ex.ToString(),
                    ["errorType"] = ex.GetType().Name
                };

                string jsonString = JsonSerializer.Serialize(errorResult, new JsonSerializerOptions
                {
                    WriteIndented = true,
                    PropertyNamingPolicy = JsonNamingPolicy.CamelCase
                });
                return JsonDocument.Parse(jsonString).RootElement;
            }
        }

        // Add this method after the ExecuteJqlQueryAsync method
        public async Task<JsonElement> GetIssueKeysAndSummariesAsync(string jql, int maxResults = 100)
        {
            try
            {
                // Only request key and summary fields
                var fields = new List<string> { "summary" }; // Key is always returned by default

                // Execute the standard JQL query with limited fields
                var rawResult = await ExecuteJqlQueryAsync(jql, maxResults, 0, fields);

                // Process the results to limit summary to 50 characters
                if (rawResult.TryGetProperty("issues", out var issues))
                {
                    var processedIssues = new List<Dictionary<string, string>>();

                    foreach (var issue in issues.EnumerateArray())
                    {
                        var key = issue.GetProperty("key").GetString();
                        string summary = "N/A";

                        if (issue.TryGetProperty("fields", out var issueFields) &&
                            issueFields.TryGetProperty("summary", out var summaryProp))
                        {
                            summary = summaryProp.GetString();
                            // Limit summary to 50 characters
                            if (summary != null && summary.Length > 50)
                                summary = summary.Substring(0, 50) + "...";
                        }

                        processedIssues.Add(new Dictionary<string, string> {
                    { "Key", key },
                    { "Summary", summary }
                });
                    }

                    // Create a new result with just the information we want
                    var processedResult = new Dictionary<string, object> {
                { "total", rawResult.GetProperty("total").GetInt32() },
                { "issues", processedIssues }
            };

                    // Convert back to JsonElement
                    string jsonString = JsonSerializer.Serialize(processedResult);
                    return JsonDocument.Parse(jsonString).RootElement;
                }
                else
                {
                    // If there was an error, just use the raw result
                    return rawResult;
                }
            }
            catch (Exception ex)
            {
                ConnectionStatus = $"Query failed: {ex.Message}";
                Console.WriteLine(ex.ToString());

                // Create a JSON structure with error details instead of returning an empty array
                var errorResult = new Dictionary<string, object>
                {
                    ["error"] = true,
                    ["message"] = ex.Message,
                    ["details"] = ex.ToString(),
                    ["errorType"] = ex.GetType().Name
                };

                string jsonString = JsonSerializer.Serialize(errorResult, new JsonSerializerOptions
                {
                    WriteIndented = true,
                    PropertyNamingPolicy = JsonNamingPolicy.CamelCase
                });
                return JsonDocument.Parse(jsonString).RootElement;
            }
        }
        // Create a new issue
        public async Task<JsonElement> CreateIssueAsync(Dictionary<string, object> issueData)
        {
            try
            {
                ConfigureHttpClient();

                var content = new StringContent(
                    JsonSerializer.Serialize(new { fields = issueData }),
                    Encoding.UTF8,
                    "application/json");

                var response = await _httpClient.PostAsync($"{JiraUrl}/rest/api/2/issue", content);

                if (response.IsSuccessStatusCode)
                {
                    var responseContent = await response.Content.ReadAsStringAsync();
                    var result = JsonDocument.Parse(responseContent).RootElement;

                    string issueKey = result.GetProperty("key").GetString();
                    ConnectionStatus = $"Successfully created issue {issueKey}";
                    return result;
                }
                else
                {
                    var errorContent = await response.Content.ReadAsStringAsync();
                    ConnectionStatus = $"Failed to create issue: {response.StatusCode} - {errorContent}";
                    return JsonDocument.Parse("{ \"error\": true, \"message\": \"" + response.StatusCode + "\" }").RootElement;
                }
            }
            catch (Exception ex)
            {
                ConnectionStatus = $"Failed to create issue: {ex.Message}";
                Console.WriteLine(ex.ToString());
                return JsonDocument.Parse("{ \"error\": true, \"message\": \"" + ex.Message + "\" }").RootElement;
            }
        }

        // Get project information
        public async Task<JsonElement> GetProjectsAsync()
        {
            try
            {
                ConfigureHttpClient();

                var response = await _httpClient.GetAsync($"{JiraUrl}/rest/api/2/project");

                if (response.IsSuccessStatusCode)
                {
                    var content = await response.Content.ReadAsStringAsync();
                    var projects = JsonDocument.Parse(content).RootElement;

                    ConnectionStatus = "Successfully retrieved projects";
                    return projects;
                }
                else
                {
                    var errorContent = await response.Content.ReadAsStringAsync();
                    ConnectionStatus = $"Failed to get projects: {response.StatusCode} - {errorContent}";
                    return JsonDocument.Parse("{ \"error\": true, \"message\": \"" + response.StatusCode + "\" }").RootElement;
                }
            }
            catch (Exception ex)
            {
                ConnectionStatus = $"Failed to get projects: {ex.Message}";
                Console.WriteLine(ex.ToString());
                return JsonDocument.Parse("{ \"error\": true, \"message\": \"" + ex.Message + "\" }").RootElement;
            }
        }

        // Implement IDisposable to properly clean up
        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        protected virtual void Dispose(bool disposing)
        {
            if (!_isDisposed)
            {
                if (disposing)
                {
                    // Clean up managed resources
                    if (_httpClient != null)
                    {
                        _httpClient.Dispose();
                        _httpClient = null;
                    }
                }

                _isDisposed = true;
            }
        }

        // Finalizer
        ~JiraModel()
        {
            Dispose(false);
        }
    }
}