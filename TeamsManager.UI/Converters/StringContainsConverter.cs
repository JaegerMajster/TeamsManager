using System;
using System.Globalization;
using System.Windows.Data;

namespace TeamsManager.UI.Converters
{
    /// <summary>
    /// Konwerter sprawdzający czy string zawiera określony tekst
    /// </summary>
    public class StringContainsConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is string stringValue && parameter is string searchText)
            {
                return stringValue.Contains(searchText, StringComparison.OrdinalIgnoreCase);
            }
            return false;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException("StringContainsConverter nie obsługuje konwersji zwrotnej");
        }
    }
} 