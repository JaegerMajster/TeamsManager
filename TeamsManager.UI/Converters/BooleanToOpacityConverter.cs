using System;
using System.Globalization;
using System.Windows.Data;

namespace TeamsManager.UI.Converters
{
    /// <summary>
    /// Konwerter konwertujący wartość boolean na opacity (przezroczystość).
    /// True = 1.0 (pełna nieprzezroczystość), False = 0.3 (przezroczysty)
    /// </summary>
    public class BooleanToOpacityConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is bool boolValue)
            {
                return boolValue ? 1.0 : 0.3;
            }
            
            return 1.0; // Domyślnie pełna nieprzezroczystość
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException("BooleanToOpacityConverter nie obsługuje konwersji wstecznej.");
        }
    }
} 