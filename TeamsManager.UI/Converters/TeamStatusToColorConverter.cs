using System;
using System.Globalization;
using System.Windows.Data;
using System.Windows.Media;
using TeamsManager.Core.Enums;

namespace TeamsManager.UI.Converters
{
    public class TeamStatusToColorConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is TeamStatus status)
            {
                return status switch
                {
                    TeamStatus.Active => new SolidColorBrush(Color.FromRgb(76, 175, 80)), // Green
                    TeamStatus.Archived => new SolidColorBrush(Color.FromRgb(158, 158, 158)), // Gray
                    _ => new SolidColorBrush(Color.FromRgb(158, 158, 158))
                };
            }
            return Brushes.Gray;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
} 