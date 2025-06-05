using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Text.Json;
using System.Net.Http;
using System.Net.Http.Headers;
using Azure.Core;
using Azure.Identity;
using Microsoft.Rest;
using CommunityToolkit.Mvvm.ComponentModel;

namespace data_sentry.Models
{
    public partial class AzureDataFactoryModel : ObservableObject, IDisposable
    {
        private HttpClient _httpClient;
        private bool _isDisposed;
        private string _accessToken;
        private DateTime _tokenExpiration = DateTime.MinValue;

        // Connection properties
        [ObservableProperty]
        private string subscriptionId;

        [ObservableProperty]
        private string resourceGroupName;

        [ObservableProperty]
        private string factoryName;

        [ObservableProperty]
        private List<string> shirList = new List<string>();

        [ObservableProperty]
        private int timezoneDelta;

        // Status
        [ObservableProperty]
        private string connectionStatus;

        [ObservableProperty]
        private bool isConnected;

        public AzureDataFactoryModel()
        {
            _httpClient = new HttpClient();
            _httpClient.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
        }

        private async Task<string> GetAccessTokenAsync()
        {
            // Check if we need a new token
            if (string.IsNullOrEmpty(_accessToken) || DateTime.UtcNow >= _tokenExpiration)
            {
                try
                {
                    // Use DefaultAzureCredential to authenticate as the local user
                    var credential = new DefaultAzureCredential();
                    var token = await credential.GetTokenAsync(
                        new TokenRequestContext(new[] { "https://management.azure.com/.default" }));

                    _accessToken = token.Token;
                    _tokenExpiration = DateTime.UtcNow.AddMinutes(55); // Tokens typically last 60 minutes
                    IsConnected = true;
                    ConnectionStatus = "Authentication successful";
                }
                catch (Exception ex)
                {
                    ConnectionStatus = $"Authentication failed: {ex.Message}";
                    IsConnected = false;
                    throw;
                }
            }

            return _accessToken;
        }

        public async Task<bool> TestConnectionAsync()
        {
            try
            {
                // Verify we can get a token and make a basic API call
                var token = await GetAccessTokenAsync();

                // Test with a simple call to check if the factory exists
                var requestUrl = $"https://management.azure.com/subscriptions/{SubscriptionId}/resourceGroups/{ResourceGroupName}/providers/Microsoft.DataFactory/factories/{FactoryName}?api-version=2018-06-01";

                using (var request = new HttpRequestMessage(HttpMethod.Get, requestUrl))
                {
                    request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
                    var response = await _httpClient.SendAsync(request);

                    if (response.IsSuccessStatusCode)
                    {
                        ConnectionStatus = "Successfully connected to Azure Data Factory";
                        IsConnected = true;
                        return true;
                    }
                    else
                    {
                        var errorContent = await response.Content.ReadAsStringAsync();
                        ConnectionStatus = $"Failed to connect to ADF: {response.StatusCode} - {errorContent}";
                        IsConnected = false;
                        return false;
                    }
                }
            }
            catch (Exception ex)
            {
                ConnectionStatus = $"Connection test failed: {ex.Message}";
                IsConnected = false;
                return false;
            }
        }
        public async Task<JsonElement> GetPipelineFailuresAsync(int lookbackHours = 24)
        {
            try
            {
                var token = await GetAccessTokenAsync();
                var results = new List<Dictionary<string, object>>();
                string continuationToken = null;

                do
                {
                    // Build URL for pipeline runs query
                    var url = $"https://management.azure.com/subscriptions/{SubscriptionId}/resourceGroups/{ResourceGroupName}/providers/Microsoft.DataFactory/factories/{FactoryName}/queryPipelineRuns?api-version=2018-06-01";

                    // Time range for query
                    var localNow = DateTime.UtcNow.AddHours(TimezoneDelta);
                    var startTime = localNow.AddHours(-lookbackHours);

                    // Create request body with "Failed" status filter
                    var requestBody = new Dictionary<string, object>
                    {
                        ["lastUpdatedAfter"] = startTime.ToString("yyyy-MM-ddTHH:mm:ssZ"),
                        ["lastUpdatedBefore"] = localNow.ToString("yyyy-MM-ddTHH:mm:ssZ"),
                        ["filters"] = new[]
                        {
                    new
                    {
                        operand = "Status",
                        @operator = "Equals",
                        values = new[] { "Failed" }
                    }
                }
                    };

                    // Add continuation token if we have one from a previous call
                    if (!string.IsNullOrEmpty(continuationToken))
                    {
                        requestBody["continuationToken"] = continuationToken;
                    }

                    using (var request = new HttpRequestMessage(HttpMethod.Post, url))
                    {
                        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
                        request.Content = new StringContent(
                            JsonSerializer.Serialize(requestBody),
                            System.Text.Encoding.UTF8,
                            "application/json");

                        var response = await _httpClient.SendAsync(request);

                        if (response.IsSuccessStatusCode)
                        {
                            var content = await response.Content.ReadAsStringAsync();
                            var runsDoc = JsonDocument.Parse(content);
                            var runs = runsDoc.RootElement.GetProperty("value");

                            // Get continuation token for next page if available
                            continuationToken = null;
                            if (runsDoc.RootElement.TryGetProperty("continuationToken", out var tokenProp))
                            {
                                continuationToken = tokenProp.GetString();
                            }

                            // Process the pipeline runs from this page
                            foreach (var run in runs.EnumerateArray())
                            {
                                // Your existing code to process each run
                                var runInfo = new Dictionary<string, object>
                                {
                                    ["runId"] = run.GetProperty("runId").GetString(),
                                    ["pipelineName"] = run.GetProperty("pipelineName").GetString(),
                                    ["status"] = "Failed" // We know it's failed since we filtered
                                };

                                // ... (rest of your existing logic for adding run details)

                                // Error details
                                if (run.TryGetProperty("message", out var messageProp))
                                {
                                    runInfo["errorMessage"] = messageProp.GetString();
                                }

                                // Timing information
                                if (run.TryGetProperty("runStart", out var startProp))
                                {
                                    runInfo["runStart"] = startProp.GetString();
                                    if (DateTime.TryParse(startProp.GetString(), out var pipelineStartTime))
                                    {
                                        runInfo["localStartTime"] = pipelineStartTime.AddHours(TimezoneDelta).ToString("yyyy-MM-dd HH:mm:ss");
                                    }
                                }

                                if (run.TryGetProperty("runEnd", out var endProp))
                                {
                                    runInfo["runEnd"] = endProp.GetString();
                                    if (DateTime.TryParse(endProp.GetString(), out var pipelineEndTime))
                                    {
                                        runInfo["localEndTime"] = pipelineEndTime.AddHours(TimezoneDelta).ToString("yyyy-MM-dd HH:mm:ss");
                                    }
                                }

                                // Add parameters that were used in the failed run
                                if (run.TryGetProperty("parameters", out var paramsProp))
                                {
                                    var paramsDict = new Dictionary<string, string>();
                                    foreach (var param in paramsProp.EnumerateObject())
                                    {
                                        paramsDict[param.Name] = param.Value.GetString();
                                    }
                                    runInfo["parameters"] = paramsDict;
                                }

                                // Get detailed activity failure information if available
                                await AddActivityFailureDetails(token, run, runInfo);

                                results.Add(runInfo);
                            }
                        }
                        else
                        {
                            var errorContent = await response.Content.ReadAsStringAsync();
                            ConnectionStatus = $"Failed to get pipeline failures: {response.StatusCode} - {errorContent}";

                            var errorResult = new Dictionary<string, object>
                            {
                                ["error"] = true,
                                ["message"] = $"HTTP Error: {response.StatusCode}",
                                ["details"] = errorContent
                            };

                            string errorJsonString = JsonSerializer.Serialize(errorResult);
                            return JsonDocument.Parse(errorJsonString).RootElement;
                        }
                    }
                } while (!string.IsNullOrEmpty(continuationToken)); // Continue until no more pages

                // Summary stats
                var summary = new Dictionary<string, object>
                {
                    ["totalFailures"] = results.Count,
                    ["timeRange"] = $"Past {lookbackHours} hours",
                    ["runDetail"] = results
                };

                string jsonString = JsonSerializer.Serialize(summary);
                return JsonDocument.Parse(jsonString).RootElement;
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
        private async Task AddActivityFailureDetails(string token, JsonElement run, Dictionary<string, object> runInfo)
        {
            try
            {
                string runId = run.GetProperty("runId").GetString();
                var url = $"https://management.azure.com/subscriptions/{SubscriptionId}/resourceGroups/{ResourceGroupName}/providers/Microsoft.DataFactory/factories/{FactoryName}/pipelineruns/{runId}/queryActivityruns?api-version=2018-06-01";

                // Query for failed activities
                var requestBody = new
                {
                    filters = new[]
                    {
                        new
                        {
                            operand = "Status",
                            @operator = "Equals",
                            values = new[] { "Failed" }
                        }
                    }
                };

                using (var request = new HttpRequestMessage(HttpMethod.Post, url))
                {
                    request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
                    request.Content = new StringContent(
                        JsonSerializer.Serialize(requestBody),
                        System.Text.Encoding.UTF8,
                        "application/json");

                    var response = await _httpClient.SendAsync(request);

                    if (response.IsSuccessStatusCode)
                    {
                        var content = await response.Content.ReadAsStringAsync();
                        var doc = JsonDocument.Parse(content);
                        var activities = doc.RootElement.GetProperty("value");

                        var failedActivities = new List<Dictionary<string, object>>();

                        foreach (var activity in activities.EnumerateArray())
                        {
                            var activityInfo = new Dictionary<string, object>
                            {
                                ["activityName"] = activity.GetProperty("activityName").GetString(),
                                ["activityType"] = activity.GetProperty("activityType").GetString()
                            };

                            // Extract detailed error information
                            if (activity.TryGetProperty("error", out var errorProp))
                            {
                                var errorInfo = new Dictionary<string, object>();

                                if (errorProp.TryGetProperty("message", out var errorMsg))
                                    errorInfo["message"] = errorMsg.GetString();

                                if (errorProp.TryGetProperty("errorCode", out var errorCode))
                                    errorInfo["errorCode"] = errorCode.GetString();

                                activityInfo["error"] = errorInfo;
                            }

                            failedActivities.Add(activityInfo);
                        }

                        if (failedActivities.Count > 0)
                        {
                            runInfo["failedActivities"] = failedActivities;
                        }
                    }
                }
            }
            catch
            {
                // Skip activity details if there's an error
                runInfo["activityDetails"] = "Failed to retrieve";
            }
        }

        public async Task<JsonElement> GetShirStatusDetailsAsync()
        {
            try
            {
                var token = await GetAccessTokenAsync();
                var result = new Dictionary<string, object>();

                // Collect all integration runtimes first
                var irList = await GetAllIntegrationRuntimesAsync(token);

                // If no SHIR list is specifically provided, use all Self-Hosted IRs
                var shirsToCheck = ShirList.Count > 0
                    ? ShirList
                    : irList.Where(ir => ir.Type == "SelfHosted").Select(ir => ir.Name).ToList();

                if (shirsToCheck.Count == 0)
                {
                    result["message"] = "No Self-Hosted Integration Runtimes found";
                    string jsonSerializedString = JsonSerializer.Serialize(result);
                    return JsonDocument.Parse(jsonSerializedString).RootElement;
                }

                var shirDetails = new List<Dictionary<string, object>>();

                foreach (var shirName in shirsToCheck)
                {
                    var shirDetail = await GetSingleShirDetailsAsync(token, shirName);
                    shirDetails.Add(shirDetail);
                }

                result["integrationRuntimes"] = shirDetails;
                result["count"] = shirDetails.Count;
                result["currentTime"] = DateTime.UtcNow.ToString("o");
                result["localTime"] = DateTime.UtcNow.AddHours(TimezoneDelta).ToString("yyyy-MM-dd HH:mm:ss");

                string jsonString = JsonSerializer.Serialize(result);
                return JsonDocument.Parse(jsonString).RootElement;
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

        private class IntegrationRuntimeInfo
        {
            public string Name { get; set; }
            public string Type { get; set; }
        }

        private async Task<List<IntegrationRuntimeInfo>> GetAllIntegrationRuntimesAsync(string token)
        {
            var url = $"https://management.azure.com/subscriptions/{SubscriptionId}/resourceGroups/{ResourceGroupName}/providers/Microsoft.DataFactory/factories/{FactoryName}/integrationRuntimes?api-version=2018-06-01";
            var result = new List<IntegrationRuntimeInfo>();

            using (var request = new HttpRequestMessage(HttpMethod.Get, url))
            {
                request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
                var response = await _httpClient.SendAsync(request);

                if (response.IsSuccessStatusCode)
                {
                    var content = await response.Content.ReadAsStringAsync();
                    var doc = JsonDocument.Parse(content);
                    var irs = doc.RootElement.GetProperty("value");

                    foreach (var ir in irs.EnumerateArray())
                    {
                        var name = ir.GetProperty("name").GetString();
                        var type = "Unknown";

                        if (ir.TryGetProperty("properties", out var props) &&
                            props.TryGetProperty("type", out var typeProp))
                        {
                            type = typeProp.GetString();
                        }

                        result.Add(new IntegrationRuntimeInfo { Name = name, Type = type });
                    }
                }
            }

            return result;
        }

        private async Task<Dictionary<string, object>> GetSingleShirDetailsAsync(string token, string shirName)
        {
            var shirUrl = $"https://management.azure.com/subscriptions/{SubscriptionId}/resourceGroups/{ResourceGroupName}/providers/Microsoft.DataFactory/factories/{FactoryName}/integrationRuntimes/{shirName}?api-version=2018-06-01";
            var shirDetail = new Dictionary<string, object>
            {
                ["name"] = shirName
            };

            using (var request = new HttpRequestMessage(HttpMethod.Get, shirUrl))
            {
                request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
                var response = await _httpClient.SendAsync(request);

                if (response.IsSuccessStatusCode)
                {
                    var content = await response.Content.ReadAsStringAsync();
                    var shirInfo = JsonDocument.Parse(content).RootElement;

                    if (shirInfo.TryGetProperty("properties", out var props))
                    {
                        // Get basic SHIR properties
                        shirDetail["state"] = props.TryGetProperty("state", out var state) ? state.GetString() : "Unknown";
                        shirDetail["description"] = props.TryGetProperty("description", out var desc) ? desc.GetString() : "";

                        if (props.TryGetProperty("typeProperties", out var typeProps))
                        {
                            // Get more details about the SHIR
                            if (typeProps.TryGetProperty("autoUpdate", out var autoUpdate))
                                shirDetail["autoUpdate"] = autoUpdate.GetString();

                            if (typeProps.TryGetProperty("updateDelayOffset", out var updateDelay))
                                shirDetail["updateDelayOffset"] = updateDelay.GetString();

                            if (typeProps.TryGetProperty("autoUpdateETA", out var updateEta))
                                shirDetail["autoUpdateETA"] = updateEta.GetString();

                            if (typeProps.TryGetProperty("latestVersion", out var latestVer))
                                shirDetail["latestVersion"] = latestVer.GetString();

                            // Get detailed node information
                            if (typeProps.TryGetProperty("nodes", out var nodes))
                            {
                                var nodeList = new List<Dictionary<string, object>>();

                                foreach (var node in nodes.EnumerateArray())
                                {
                                    var nodeInfo = new Dictionary<string, object>();

                                    nodeInfo["nodeName"] = node.TryGetProperty("nodeName", out var name)
                                        ? name.GetString() : "Unknown";

                                    nodeInfo["status"] = node.TryGetProperty("status", out var status)
                                        ? status.GetString() : "Unknown";

                                    nodeInfo["version"] = node.TryGetProperty("version", out var version)
                                        ? version.GetString() : "Unknown";

                                    if (node.TryGetProperty("lastConnectTime", out var lastConnect))
                                        nodeInfo["lastConnectTime"] = lastConnect.GetString();

                                    if (node.TryGetProperty("lastStartTime", out var lastStart))
                                        nodeInfo["lastStartTime"] = lastStart.GetString();

                                    if (node.TryGetProperty("expiryTime", out var expiry))
                                        nodeInfo["expiryTime"] = expiry.GetString();

                                    if (node.TryGetProperty("machineName", out var machine))
                                        nodeInfo["machineName"] = machine.GetString();

                                    if (node.TryGetProperty("availableMemoryInMB", out var memory))
                                        nodeInfo["availableMemoryInMB"] = memory.GetInt32();

                                    if (node.TryGetProperty("cpuUtilization", out var cpu))
                                        nodeInfo["cpuUtilization"] = cpu.GetInt32();

                                    if (node.TryGetProperty("concurrentJobsLimit", out var jobLimit))
                                        nodeInfo["concurrentJobsLimit"] = jobLimit.GetInt32();

                                    if (node.TryGetProperty("concurrentJobsRunning", out var jobsRunning))
                                        nodeInfo["concurrentJobsRunning"] = jobsRunning.GetInt32();

                                    if (node.TryGetProperty("maxConcurrentJobs", out var maxJobs))
                                        nodeInfo["maxConcurrentJobs"] = maxJobs.GetInt32();

                                    nodeList.Add(nodeInfo);
                                }

                                shirDetail["nodes"] = nodeList;
                                shirDetail["nodeCount"] = nodeList.Count;
                            }
                        }
                    }
                }
                else
                {
                    shirDetail["error"] = $"Failed to get SHIR details: {response.StatusCode}";
                }
            }

            return shirDetail;
        }

        public async Task<JsonElement> GetComprehensiveStatusAsync(int lookbackHours = 24)
        {
            try
            {
                // Get all statuses in parallel
                var failuresTask = GetPipelineFailuresAsync(lookbackHours);
                var shirStatusTask = GetShirStatusDetailsAsync();

                await Task.WhenAll(failuresTask, shirStatusTask);

                var failures = await failuresTask;
                var shirStatus = await shirStatusTask;

                // Combine all results into one comprehensive object
                var result = new Dictionary<string, object>
                {
                    ["failures"] = JsonSerializer.Deserialize<object>(failures.GetRawText()),
                    ["shirStatus"] = JsonSerializer.Deserialize<object>(shirStatus.GetRawText()),
                    ["timeGenerated"] = DateTime.UtcNow.ToString("o"),
                    ["localTimeGenerated"] = DateTime.UtcNow.AddHours(TimezoneDelta).ToString("yyyy-MM-dd HH:mm:ss")
                };

                string jsonString = JsonSerializer.Serialize(result);
                return JsonDocument.Parse(jsonString).RootElement;
            }
            catch (Exception ex)
            {
                ConnectionStatus = $"Failed to get comprehensive status: {ex.Message}";
                var errorResult = new Dictionary<string, object>
                {
                    ["error"] = true,
                    ["message"] = ex.Message,
                    ["stackTrace"] = ex.StackTrace
                };

                string jsonString = JsonSerializer.Serialize(errorResult);
                return JsonDocument.Parse(jsonString).RootElement;
            }
        }

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
                    _httpClient.Dispose();
                }

                _isDisposed = true;
            }
        }

        ~AzureDataFactoryModel()
        {
            Dispose(false);
        }
    }
}