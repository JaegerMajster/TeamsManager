using System;
using System.Globalization;
using System.Windows.Data;
using TeamsManager.Core.Enums;

namespace TeamsManager.UI.Converters
{
    /// <summary>
    /// Converter to display TeamMemberRole enum values in Polish
    /// </summary>
    public class TeamMemberRoleToStringConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is TeamMemberRole role)
            {
                return role switch
                {
                    TeamMemberRole.Owner => "Właściciel",
                    TeamMemberRole.Member => "Członek",
                    _ => role.ToString()
                };
            }
            return value?.ToString() ?? string.Empty;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is string str)
            {
                return str switch
                {
                    "Właściciel" => TeamMemberRole.Owner,
                    "Członek" => TeamMemberRole.Member,
                    _ => throw new ArgumentException($"Unknown role: {str}")
                };
            }
            return TeamMemberRole.Member;
        }
    }
} 