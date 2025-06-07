using System.Globalization;
using System.Windows.Data;
using System.Windows.Media;
using TeamsManager.UI.Models.Monitoring;
using TeamsManager.UI.Services;

namespace TeamsManager.UI.Converters
{
    /// <summary>
    /// Konwerter statusu zdrowia na kolor
    /// </summary>
    public class HealthCheckToColorConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is HealthCheck status)
            {
                return status switch
                {
                    HealthCheck.Healthy => new SolidColorBrush(Color.FromRgb(76, 175, 80)), // Green
                    HealthCheck.Warning => new SolidColorBrush(Color.FromRgb(255, 152, 0)), // Orange
                    HealthCheck.Critical => new SolidColorBrush(Color.FromRgb(244, 67, 54)), // Red
                    _ => new SolidColorBrush(Colors.Gray)
                };
            }
            return new SolidColorBrush(Colors.Gray);
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }

    /// <summary>
    /// Konwerter poziomu alertu na kolor
    /// </summary>
    public class AlertLevelToColorConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is AlertLevel level)
            {
                return level switch
                {
                    AlertLevel.Info => new SolidColorBrush(Color.FromRgb(33, 150, 243)), // Blue
                    AlertLevel.Warning => new SolidColorBrush(Color.FromRgb(255, 152, 0)), // Orange
                    AlertLevel.Error => new SolidColorBrush(Color.FromRgb(244, 67, 54)), // Red
                    AlertLevel.Critical => new SolidColorBrush(Color.FromRgb(156, 39, 176)), // Purple
                    _ => new SolidColorBrush(Colors.Gray)
                };
            }
            return new SolidColorBrush(Colors.Gray);
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }

    /// <summary>
    /// Konwerter stanu połączenia na kolor
    /// </summary>
    public class ConnectionStateToColorConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is ConnectionState state)
            {
                return state switch
                {
                    ConnectionState.Connected => new SolidColorBrush(Color.FromRgb(76, 175, 80)), // Green
                    ConnectionState.Connecting => new SolidColorBrush(Color.FromRgb(255, 152, 0)), // Orange
                    ConnectionState.Reconnecting => new SolidColorBrush(Color.FromRgb(255, 152, 0)), // Orange
                    ConnectionState.Disconnected => new SolidColorBrush(Color.FromRgb(244, 67, 54)), // Red
                    ConnectionState.Error => new SolidColorBrush(Color.FromRgb(244, 67, 54)), // Red
                    _ => new SolidColorBrush(Colors.Gray)
                };
            }
            return new SolidColorBrush(Colors.Gray);
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }

    // InverseBooleanToVisibilityConverter moved to separate file

    /// <summary>
    /// Konwerter procentu na kolor progress bara
    /// </summary>
    public class PercentageToColorConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is double percentage)
            {
                return percentage switch
                {
                    <= 50 => new SolidColorBrush(Color.FromRgb(76, 175, 80)), // Green
                    <= 80 => new SolidColorBrush(Color.FromRgb(255, 152, 0)), // Orange
                    _ => new SolidColorBrush(Color.FromRgb(244, 67, 54)) // Red
                };
            }
            return new SolidColorBrush(Colors.Gray);
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }

    /// <summary>
    /// Konwerter czasu na czytelny format
    /// </summary>
    public class TimeSpanToStringConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is TimeSpan timeSpan)
            {
                if (timeSpan.TotalDays >= 1)
                    return $"{timeSpan.Days}d {timeSpan.Hours}h";
                if (timeSpan.TotalHours >= 1)
                    return $"{timeSpan.Hours}h {timeSpan.Minutes}m";
                if (timeSpan.TotalMinutes >= 1)
                    return $"{timeSpan.Minutes}m {timeSpan.Seconds}s";
                return $"{timeSpan.Seconds}s";
            }
            return "0s";
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
} 