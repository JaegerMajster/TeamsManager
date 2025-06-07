using System;
using System.Globalization;
using System.Windows;
using System.Windows.Data;

namespace TeamsManager.UI.Converters
{
    /// <summary>
    /// Konwerter porównujący wartość z parametrem i zwracający Visibility.
    /// Używany do warunkowego wyświetlania elementów na podstawie równości.
    /// </summary>
    public class EqualToVisibilityConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value?.ToString() == parameter?.ToString())
                return Visibility.Visible;
            return Visibility.Collapsed;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException("EqualToVisibilityConverter nie obsługuje konwersji zwrotnej");
        }
    }
} 