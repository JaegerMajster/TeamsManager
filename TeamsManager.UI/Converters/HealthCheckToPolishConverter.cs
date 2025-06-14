using System;
using System.Globalization;
using System.Windows.Data;
using TeamsManager.UI.Models.Monitoring;

namespace TeamsManager.UI.Converters
{
    /// <summary>
    /// Konwerter dla HealthCheck na polskie tłumaczenia
    /// </summary>
    public class HealthCheckToPolishConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is HealthCheck healthCheck)
            {
                return healthCheck switch
                {
                    HealthCheck.Healthy => "Sprawny",
                    HealthCheck.Warning => "Ostrzeżenie",
                    HealthCheck.Critical => "Krytyczny",
                    HealthCheck.Unknown => "Nieznany",
                    _ => value.ToString()
                };
            }
            
            return value?.ToString() ?? string.Empty;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException("ConvertBack is not supported for HealthCheckToPolishConverter");
        }
    }
} 