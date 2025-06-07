using System;
using System.Globalization;
using System.Windows.Data;

namespace TeamsManager.UI.Converters
{
    /// <summary>
    /// Konwerter do konwersji string na bool i odwrotnie
    /// Obsługuje różne reprezentacje wartości boolean
    /// </summary>
    public class StringToBoolConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is string stringValue)
            {
                return stringValue.ToLower() switch
                {
                    "true" => true,
                    "1" => true,
                    "tak" => true,
                    "yes" => true,
                    "false" => false,
                    "0" => false,
                    "nie" => false,
                    "no" => false,
                    _ => false
                };
            }

            if (value is bool boolValue)
                return boolValue;

            return false;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is bool boolValue)
                return boolValue.ToString().ToLower();

            return "false";
        }
    }
} 