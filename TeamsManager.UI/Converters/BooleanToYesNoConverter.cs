using System;
using System.Globalization;
using System.Windows.Data;

namespace TeamsManager.UI.Converters
{
    /// <summary>
    /// Konwerter boolean na "Tak"/"Nie"
    /// </summary>
    public class BooleanToYesNoConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is bool boolValue)
            {
                return boolValue ? "Tak" : "Nie";
            }
            return "Nie";
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is string stringValue)
            {
                return stringValue.Equals("Tak", StringComparison.OrdinalIgnoreCase);
            }
            return false;
        }
    }
} 