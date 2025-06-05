using System;
using System.Collections.Generic;
using System.Text.Json;
using System.Text.Json.Serialization;
using Apache.Arrow;
using CommunityToolkit.Mvvm.ComponentModel;

namespace data_sentry.Models
{
    public partial class CheckRecord : ObservableObject
    {
        // Core properties that most checks will have
        [ObservableProperty]
        private string type;

        [ObservableProperty]
        private string description;

        [ObservableProperty]
        private string status;

        [ObservableProperty]
        private string action = "Check";


        [ObservableProperty]
        private JsonElement jsonData;
        [ObservableProperty]
        private JsonElement? resultData;

        // Dynamic properties storage
        [ObservableProperty]
        private Dictionary<string, JsonElement> properties = new Dictionary<string, JsonElement>();

        // Timestamp for the check
        [ObservableProperty]
        private DateTime timestamp = DateTime.Now;

        [JsonIgnore]
        public string? ResultDataFormatted
        {
            get
            {
                if (ResultData.HasValue)
                {
                    try
                    {
                        return JsonSerializer.Serialize(ResultData.Value, new JsonSerializerOptions { WriteIndented = true });
                    }
                    catch
                    {
                        return ResultData.Value.ToString();
                    }
                }
                return null;
            }
        }

        public void StatusCheckValidation()
        {
            if (this.Type == "snowflake" && ResultData.HasValue)
            {
                try
                {
                    // Get the number of records in the array
                    int recordCount = ResultData.Value.GetArrayLength();

                    if (recordCount > 0)
                    {
                        // We strucutre the checks to ONLY return data if theres a failure to then notify any downstream consumers
                        this.Status = "Failed - Please investigate returned records";
                    }
                    else
                    {
                        this.Status = "Success - No data issues";
                    }
                }
                catch (Exception ex)
                {
                    this.Status = $"Error - {ex.Message}";
                }
            }
            else if (this.Type == "sqlserver" && ResultData.HasValue)
            {
                try
                {
                    // Get the number of records in the array
                    int recordCount = ResultData.Value.GetArrayLength();

                    if (recordCount > 0)
                    {
                        // We strucutre the checks to ONLY return data if theres a failure to then notify any downstream consumers
                        this.Status = "Failed - Please investigate returned records";
                    }
                    else
                    {
                        this.Status = "Success - No data issues";
                    }
                }
                catch (Exception ex)
                {
                    this.Status = $"Error - {ex.Message}";
                }
            }
            else if (this.Type == "adf" && ResultData.HasValue)
            {
                try
                {
                    // Get total failure count from the failures.totalFailures property
                    if (ResultData.Value.TryGetProperty("failures", out var failures) &&
                        failures.TryGetProperty("totalFailures", out var totalFailures))
                    {
                        int failureCount = totalFailures.GetInt32();

                        if (failureCount > 0)
                        {
                            this.Status = $"Failed - Investigate Failures ";
                        }
                        else
                        {
                            this.Status = "Success - No failures";
                        }

                        // TODO: Handle SHIR status
                        if (ResultData.Value.TryGetProperty("shirStatus", out var shirStatus) &&
                            shirStatus.TryGetProperty("integrationRuntimes", out var runtimes))
                        {
                            // Add SHIR details to status if any are not "Online"
                            // Implementation depends on how you want to present this info
                        }
                    }
                    else
                    {
                        this.Status = "Warning - Could not determine failure count";
                    }
                }
                catch (Exception ex)
                {
                    this.Status = $"Error - {ex.Message}";
                }
            }
            else if (this.Type == "jira" && ResultData.HasValue)
            {
                try
                {
                    // Get total failure count from the failures.totalFailures property
                    if (ResultData.Value.TryGetProperty("total", out var total))
                    {
                        int failureCount = total.GetInt32();

                        if (failureCount > 0)
                        {
                            this.Status = $"Failed - Investigate returned tickets ";
                        }
                        else
                        {
                            this.Status = "Success - No tickets found";
                        }
                    }

                }
                catch (Exception ex)
                {
                    this.Status = $"Error - {ex.Message}";
                }
            }
            else
            {
                this.Status = "Unknown - No validation logic for this type";
            }
        }

        // Methods to check if a property exists
        public bool HasProperty(string key) => Properties.ContainsKey(key);

        // Helper methods to get typed values
        public T GetValue<T>(string key, T defaultValue = default)
        {
            if (!Properties.ContainsKey(key))
                return defaultValue;

            try
            {
                var element = Properties[key];
                return JsonSerializer.Deserialize<T>(element.GetRawText());
            }
            catch
            {
                return defaultValue;
            }
        }

        // Create from JSON string
        public static CheckRecord FromJson(string json)
        {
            var options = new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            };

            var record = JsonSerializer.Deserialize<CheckRecord>(json, options);

            // Parse additional properties from JSON that aren't part of the class
            var jsonDoc = JsonDocument.Parse(json);
            foreach (var prop in jsonDoc.RootElement.EnumerateObject())
            {
                if (prop.Name != "name" && prop.Name != "status" &&
                    prop.Name != "properties" && prop.Name != "timestamp")
                {
                    record.Properties[prop.Name] = prop.Value.Clone();
                }
            }

            return record;
        }

        // Serialize to JSON
        public string ToJson()
        {
            var options = new JsonSerializerOptions
            {
                WriteIndented = true
            };

            // Start with a dictionary containing the base properties
            var allProps = new Dictionary<string, object>
            {
                ["type"] = Type,
                ["desciption"] = Description,
                ["status"] = Status,
                ["timestamp"] = Timestamp
            };

            // Add all dynamic properties
            foreach (var prop in Properties)
            {
                allProps[prop.Key] = prop.Value;
            }

            return JsonSerializer.Serialize(allProps, options);
        }
    }
}