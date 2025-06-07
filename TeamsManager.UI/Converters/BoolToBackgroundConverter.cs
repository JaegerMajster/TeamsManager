using System;
using System.Globalization;
using System.Windows.Data;
using System.Windows.Media;

namespace TeamsManager.UI.Converters
{
    /// <summary>
    /// Converter do zmiany tła na podstawie wartości bool
    /// True = tło dla wybranego elementu, False = przezroczyste
    /// </summary>
    public class BoolToBackgroundConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is bool boolValue && boolValue)
            {
                // Wybrane - tło w kolorze akcentowym z przezroczystością
                return new SolidColorBrush(Color.FromArgb(40, 0, 120, 212)); // AccentBlue z alpha 40
            }
            
            // Niewybrane - przezroczyste tło
            return Brushes.Transparent;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
} 