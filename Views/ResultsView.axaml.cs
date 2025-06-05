using Avalonia;
using Avalonia.Controls;
using Avalonia.Markup.Xaml;
using data_sentry.ViewModels;
using System.Text.Json;

namespace data_sentry.Views
{
    public partial class ResultsView : Window
    {
        public ResultsView()
        {
            InitializeComponent();
        }

        public ResultsView(string title, JsonElement? resultData) : this()
        {
            var options = new JsonSerializerOptions { WriteIndented = true };
            string resultJson = resultData.HasValue
                ? JsonSerializer.Serialize(resultData.Value, options)
                : "No result data available";

            DataContext = new ResultsViewModel(title, resultJson);
        }

        private void InitializeComponent()
        {
            AvaloniaXamlLoader.Load(this);
        }
    }
}