using System;
using System.Globalization;
using System.Windows;
using System.Windows.Data;

namespace TeamsManager.UI.Converters
{
    /// <summary>
    /// Konwertuje poziom hierarchii na margines dla wcięcia w TreeView
    /// </summary>
    public class HierarchyLevelToMarginConverter : IValueConverter
    {
        public double IndentSize { get; set; } = 20.0;

        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is int level)
            {
                var leftMargin = level * IndentSize;
                return new Thickness(leftMargin, 0, 0, 0);
            }

            return new Thickness(0);
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
} 