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
    public partial class ExternalNotificationModel : ObservableObject, IDisposable
    {
        private HttpClient _httpClient;
        private bool _isDisposed;

        // Connection properties
        // Default to ops genie URL
        [ObservableProperty]
        private string? apiUrl = "https://api.opsgenie.com/v2";

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
        public ExternalNotificationModel()
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

            // OpsGenie uses API key in a header instead of Basic auth
            _httpClient.DefaultRequestHeaders.Remove("Authorization");
            _httpClient.DefaultRequestHeaders.Add("Authorization", $"{ApiKey}");
        }

        // Create a new alert in Reporting System
        public async Task<JsonElement> CreateAlertAsync(Dictionary<string, object> alertData)
        {
            try
            {
                ConfigureHttpClient();

                var content = new StringContent(
                    JsonSerializer.Serialize(alertData),
                    Encoding.UTF8,
                    "application/json");

                var response = await _httpClient.PostAsync($"{ApiUrl}/alerts", content);

                if (response.IsSuccessStatusCode)
                {
                    var responseContent = await response.Content.ReadAsStringAsync();
                    var result = JsonDocument.Parse(responseContent).RootElement;

                    // OpsGenie returns a requestId for tracking
                    string requestId = result.GetProperty("requestId").GetString();
                    ConnectionStatus = $"Successfully created alert. Request ID: {requestId}";
                    return result;
                }
                else
                {
                    var errorContent = await response.Content.ReadAsStringAsync();
                    ConnectionStatus = $"Failed to create alert: {response.StatusCode} - {errorContent}";
                    return JsonDocument.Parse("{ \"error\": true, \"message\": \"" + response.StatusCode + "\" }").RootElement;
                }
            }
            catch (Exception ex)
            {
                ConnectionStatus = $"Failed to create alert: {ex.Message}";
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
        ~ExternalNotificationModel()
        {
            Dispose(false);
        }
    }
}