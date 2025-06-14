using System;
using System.Globalization;
using System.Windows.Data;
using TeamsManager.Core.Enums;
using TeamsManager.Core.Extensions;

namespace TeamsManager.UI.Converters
{
    /// <summary>
    /// Konwerter dla UserRole na polskie tłumaczenia
    /// </summary>
    public class UserRoleToPolishConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is UserRole role)
            {
                return role.ToPolishString();
            }
            
            return value?.ToString() ?? string.Empty;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException("ConvertBack is not supported for UserRoleToPolishConverter");
        }
    }
} 