using System;
using System.Globalization;
using System.Windows.Data;

namespace TeamsManager.UI.Converters
{
    /// <summary>
    /// Konwerter do konwersji string na DateTime i odwrotnie
    /// Obsługuje różne formaty daty
    /// </summary>
    public class StringToDateConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is string stringValue && !string.IsNullOrWhiteSpace(stringValue))
            {
                if (DateTime.TryParse(stringValue, culture, DateTimeStyles.None, out DateTime result))
                {
                    return result.Date;
                }
                
                // Próba parsowania różnych formatów
                string[] formats = { 
                    "yyyy-MM-dd", 
                    "dd/MM/yyyy", 
                    "MM/dd/yyyy", 
                    "dd-MM-yyyy",
                    "yyyy-MM-dd HH:mm:ss",
                    "dd/MM/yyyy HH:mm:ss"
                };

                foreach (string format in formats)
                {
                    if (DateTime.TryParseExact(stringValue, format, culture, DateTimeStyles.None, out result))
                    {
                        return result.Date;
                    }
                }
            }

            return DateTime.Today; // Zwracamy dzisiejszą datę jako domyślną wartość
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is DateTime dateValue)
            {
                return dateValue.ToString("yyyy-MM-dd", culture);
            }

            return string.Empty;
        }
    }
} 