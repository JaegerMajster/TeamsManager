using System;
using System.Globalization;
using System.Windows.Data;

namespace TeamsManager.UI.Converters
{
    /// <summary>
    /// Konwerter zmieniający TimeSpan na czytelny format tekstowy
    /// </summary>
    public class TimeSpanToReadableConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value == null)
                return "N/A";

            TimeSpan timeSpan;
            
            // Obsługa różnych typów wejściowych
            switch (value)
            {
                case TimeSpan ts:
                    timeSpan = ts;
                    break;
                case double milliseconds:
                    timeSpan = TimeSpan.FromMilliseconds(milliseconds);
                    break;
                case int seconds:
                    timeSpan = TimeSpan.FromSeconds(seconds);
                    break;
                default:
                    return "N/A";
            }

            // Obsługa ujemnych wartości
            if (timeSpan < TimeSpan.Zero)
                return "Nieprawidłowy czas";

            // Formatowanie w zależności od długości czasu
            if (timeSpan.TotalMilliseconds < 1000)
            {
                return $"{(int)timeSpan.TotalMilliseconds} ms";
            }
            else if (timeSpan.TotalSeconds < 1)
            {
                return "Mniej niż sekunda";
            }
            else if (timeSpan.TotalSeconds < 60)
            {
                return $"{timeSpan.TotalSeconds:F1} s";
            }
            else if (timeSpan.TotalMinutes < 60)
            {
                var minutes = (int)timeSpan.TotalMinutes;
                var seconds = timeSpan.Seconds;
                
                if (seconds == 0)
                    return $"{minutes} min";
                else
                    return $"{minutes} min {seconds} s";
            }
            else if (timeSpan.TotalHours < 24)
            {
                var hours = (int)timeSpan.TotalHours;
                var minutes = timeSpan.Minutes;
                
                if (minutes == 0)
                    return $"{hours} godz";
                else
                    return $"{hours} godz {minutes} min";
            }
            else
            {
                var days = (int)timeSpan.TotalDays;
                var hours = timeSpan.Hours;
                
                if (hours == 0)
                    return $"{days} dni";
                else
                    return $"{days} dni {hours} godz";
            }
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException("ConvertBack is not supported for TimeSpanToReadableConverter");
        }
    }

    /// <summary>
    /// Konwerter DateTime na względny czas ("5 minut temu")
    /// </summary>
    public class DateTimeToRelativeConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is not DateTime dateTime)
                return "N/A";

            var timeSpan = DateTime.Now - dateTime;
            
            // Przyszłość (nie powinna się zdarzyć, ale obsłużmy)
            if (timeSpan < TimeSpan.Zero)
                return "W przyszłości";

            // Formatowanie względne
            if (timeSpan.TotalSeconds < 30)
                return "Przed chwilą";
            else if (timeSpan.TotalMinutes < 1)
                return $"{(int)timeSpan.TotalSeconds} sekund temu";
            else if (timeSpan.TotalMinutes < 60)
                return $"{(int)timeSpan.TotalMinutes} min temu";
            else if (timeSpan.TotalHours < 24)
                return $"{(int)timeSpan.TotalHours} godz temu";
            else if (timeSpan.TotalDays < 7)
                return $"{(int)timeSpan.TotalDays} dni temu";
            else if (timeSpan.TotalDays < 30)
                return $"{(int)(timeSpan.TotalDays / 7)} tyg temu";
            else if (timeSpan.TotalDays < 365)
                return $"{(int)(timeSpan.TotalDays / 30)} mies temu";
            else
                return dateTime.ToString("dd.MM.yyyy");
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException("ConvertBack is not supported for DateTimeToRelativeConverter");
        }
    }

    /// <summary>
    /// Konwerter dla progresu operacji batch (ProcessedItems/TotalItems)
    /// </summary>
    public class ProgressToPercentageConverter : IMultiValueConverter
    {
        public object Convert(object[] values, Type targetType, object parameter, CultureInfo culture)
        {
            if (values == null || values.Length != 2)
                return 0.0;

            if (values[0] is not int processedItems || values[1] is not int totalItems)
                return 0.0;

            if (totalItems == 0)
                return 0.0;

            return (double)processedItems / totalItems * 100.0;
        }

        public object[] ConvertBack(object value, Type[] targetTypes, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException("ConvertBack is not supported for ProgressToPercentageConverter");
        }
    }

    /// <summary>
    /// Konwerter formatujący progress jako tekst "X/Y (Z%)"
    /// </summary>
    public class ProgressToTextConverter : IMultiValueConverter
    {
        public object Convert(object[] values, Type targetType, object parameter, CultureInfo culture)
        {
            if (values == null || values.Length != 2)
                return "N/A";

            if (values[0] is not int processedItems || values[1] is not int totalItems)
                return "N/A";

            if (totalItems == 0)
                return "Brak elementów";

            var percentage = (double)processedItems / totalItems * 100.0;
            return $"{processedItems}/{totalItems} ({percentage:F1}%)";
        }

        public object[] ConvertBack(object value, Type[] targetTypes, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException("ConvertBack is not supported for ProgressToTextConverter");
        }
    }
} 