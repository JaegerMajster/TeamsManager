using System;
using System.Globalization;
using System.Windows.Data;

namespace TeamsManager.UI.Converters
{
    /// <summary>
    /// Konwerter do konwersji string na TimeSpan i odwrotnie
    /// Obsługuje różne formaty czasu
    /// </summary>
    public class StringToTimeConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is string stringValue && !string.IsNullOrWhiteSpace(stringValue))
            {
                // Próba parsowania jako pełna data i wyciągnięcie czasu
                if (DateTime.TryParse(stringValue, culture, DateTimeStyles.None, out DateTime dateTime))
                {
                    return dateTime.TimeOfDay;
                }
                
                // Próba parsowania jako TimeSpan
                if (TimeSpan.TryParse(stringValue, culture, out TimeSpan timeSpan))
                {
                    return timeSpan;
                }

                // Próba parsowania różnych formatów czasu
                string[] timeFormats = { 
                    "HH:mm", 
                    "HH:mm:ss", 
                    "H:mm", 
                    "H:mm:ss",
                    "hh:mm tt",
                    "h:mm tt"
                };

                foreach (string format in timeFormats)
                {
                    if (DateTime.TryParseExact(stringValue, format, culture, DateTimeStyles.None, out dateTime))
                    {
                        return dateTime.TimeOfDay;
                    }
                }
            }

            return TimeSpan.Zero; // Zwracamy domyślną wartość zamiast null
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is TimeSpan timeValue)
            {
                return timeValue.ToString(@"hh\:mm", culture);
            }

            if (value is DateTime dateTimeValue)
            {
                return dateTimeValue.ToString("HH:mm", culture);
            }

            return string.Empty;
        }
    }
} 