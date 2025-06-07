using System;
using System.Globalization;
using System.Windows;
using System.Windows.Data;
using TeamsManager.Core.Enums;

namespace TeamsManager.UI.Converters
{
    public class TeamStatusToArchiveVisibilityConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is TeamStatus status)
            {
                return status == TeamStatus.Active ? Visibility.Visible : Visibility.Collapsed;
            }
            return Visibility.Collapsed;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }

    public class TeamStatusToRestoreVisibilityConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is TeamStatus status)
            {
                return status == TeamStatus.Archived ? Visibility.Visible : Visibility.Collapsed;
            }
            return Visibility.Collapsed;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
} 