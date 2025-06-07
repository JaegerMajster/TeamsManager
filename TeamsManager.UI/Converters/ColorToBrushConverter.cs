using System;
using System.Globalization;
using System.Windows.Data;
using System.Windows.Media;

namespace TeamsManager.UI.Converters
{
    /// <summary>
    /// Konwerter HEX color string na SolidColorBrush
    /// </summary>
    public class ColorToBrushConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is string colorCode && !string.IsNullOrWhiteSpace(colorCode))
            {
                try
                {
                    // Dodaj # jeśli brakuje
                    if (!colorCode.StartsWith("#"))
                        colorCode = $"#{colorCode}";

                    var color = (Color)ColorConverter.ConvertFromString(colorCode);
                    return new SolidColorBrush(color);
                }
                catch
                {
                    // Fallback do domyślnego koloru
                }
            }

            // Domyślny kolor gdy brak lub błędny kod
            return new SolidColorBrush(Colors.Gray);
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is SolidColorBrush brush)
            {
                return brush.Color.ToString();
            }
            return null;
        }
    }
} 