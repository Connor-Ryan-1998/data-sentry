using System.Text.RegularExpressions;
using System.Text.Json;
using CommunityToolkit.Mvvm.ComponentModel;

namespace data_sentry.ViewModels
{
    public partial class ResultsViewModel : ObservableObject
    {
        [ObservableProperty]
        private string title;

        [ObservableProperty]
        private string resultJson;

        public ResultsViewModel(string title, string resultJson)
        {
            this.title = title;
            ResultJson = UnescapeNestedJson(resultJson);
            this.resultJson = ResultJson;
        }

        private string UnescapeNestedJson(string json)
        {
            // Find JSON strings that look like escaped JSON objects
            var regex = new Regex(@"""({\\u0022.*?\\u0022})""");
            return regex.Replace(json, match =>
            {
                var escapedJson = match.Groups[1].Value;

                // Replace escaped quotes and other escapes
                escapedJson = escapedJson
                    .Replace("\\u0022", "\"")
                    .Replace("\\\\", "\\")
                    .Replace("\\r", "\r")
                    .Replace("\\n", "\n")
                    .Replace("\\t", "\t");

                try
                {
                    // Parse and re-serialize with indentation
                    var parsedObj = JsonSerializer.Deserialize<JsonElement>(escapedJson);
                    return JsonSerializer.Serialize(parsedObj, new JsonSerializerOptions
                    {
                        WriteIndented = true
                    });
                }
                catch
                {
                    // Return the original if parsing fails
                    return match.Value;
                }
            });
        }
    }
}