using System;
using System.Globalization;
using System.Windows.Data;

namespace TeamsManager.UI.Converters
{
    /// <summary>
    /// Konwerter sprawdzający czy wartość jest większa niż zadany próg.
    /// Używany do walidacji obciążenia workload i kolorowania UI.
    /// </summary>
    public class GreaterThanConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is decimal decimalValue && parameter is string paramString && 
                decimal.TryParse(paramString, out decimal threshold))
            {
                return decimalValue > threshold;
            }
            
            if (value is double doubleValue && parameter is string paramString2 && 
                double.TryParse(paramString2, out double threshold2))
            {
                return doubleValue > threshold2;
            }
            
            if (value is int intValue && parameter is string paramString3 && 
                int.TryParse(paramString3, out int threshold3))
            {
                return intValue > threshold3;
            }
            
            return false;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException("GreaterThanConverter nie obsługuje konwersji zwrotnej");
        }
    }
} 