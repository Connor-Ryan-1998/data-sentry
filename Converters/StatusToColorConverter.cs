using System;
using System.Globalization;
using Avalonia.Data.Converters;
using Avalonia.Media;

namespace data_sentry.Converters
{
    public class StatusToColorConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is string status)
            {
                status = status.ToLowerInvariant();

                if (status.Contains("success") || status.Contains("pass") || status.Contains("ok"))
                    return new SolidColorBrush(Color.Parse("#4CAF50")); // Green

                if (status.Contains("fail") || status.Contains("error"))
                    return new SolidColorBrush(Color.Parse("#F44336")); // Red

                if (status.Contains("warn") || status.Contains("pending"))
                    return new SolidColorBrush(Color.Parse("#FFA500")); // Amber/Orange

                if (status.Contains("check"))
                    return new SolidColorBrush(Color.Parse("#3B82F6")); // Blue
            }

            // Default gray
            return new SolidColorBrush(Color.Parse("#BBBBBB"));
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}