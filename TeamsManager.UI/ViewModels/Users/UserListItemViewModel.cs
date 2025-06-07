using System;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using TeamsManager.Core.Models;
using TeamsManager.Core.Enums;

namespace TeamsManager.UI.ViewModels.Users
{
    /// <summary>
    /// ViewModel dla pojedynczego użytkownika w liście.
    /// Wrapper dla modelu User z dodatkowymi właściwościami UI.
    /// </summary>
    public class UserListItemViewModel : INotifyPropertyChanged
    {
        private readonly User _user;
        private bool _isSelected;

        public UserListItemViewModel(User user)
        {
            _user = user ?? throw new ArgumentNullException(nameof(user));
        }

        // Exposed User properties
        public string Id => _user.Id;
        public string FirstName => _user.FirstName;
        public string LastName => _user.LastName;
        public string FullName => _user.FullName;
        public string UPN => _user.UPN;
        public string Email => _user.Email;
        public UserRole Role => _user.Role;
        public string RoleDisplayName => _user.RoleDisplayName;
        public string? DepartmentName => _user.Department?.Name;
        public string? Position => _user.Position;
        public bool IsActive => _user.IsActive;
        public bool IsSystemAdmin => _user.IsSystemAdmin;
        public DateTime? LastLoginDate => _user.LastLoginDate;
        public string Initials => _user.Initials;

        // UI specific properties
        public bool IsSelected
        {
            get => _isSelected;
            set
            {
                if (_isSelected != value)
                {
                    _isSelected = value;
                    OnPropertyChanged();
                }
            }
        }

        // Computed properties for UI
        public string StatusText => IsActive ? "Aktywny" : "Nieaktywny";
        public string StatusIcon => IsActive ? "CheckCircle" : "Cancel";
        public string StatusColor => IsActive ? "#4CAF50" : "#F44336";

        public string LastLoginText
        {
            get
            {
                if (!LastLoginDate.HasValue)
                    return "Nigdy";

                var daysSinceLogin = (DateTime.Now - LastLoginDate.Value).Days;
                return daysSinceLogin switch
                {
                    0 => "Dzisiaj",
                    1 => "Wczoraj",
                    < 7 => $"{daysSinceLogin} dni temu",
                    < 30 => $"{daysSinceLogin / 7} tygodni temu",
                    < 365 => $"{daysSinceLogin / 30} miesięcy temu",
                    _ => "Ponad rok temu"
                };
            }
        }

        public string RoleIcon
        {
            get
            {
                return Role switch
                {
                    UserRole.Uczen => "School",
                    UserRole.Sluchacz => "AccountSchool",
                    UserRole.Nauczyciel => "Teach",
                    UserRole.Wicedyrektor => "AccountSupervisor",
                    UserRole.Dyrektor => "AccountCog",
                    _ => "Account"
                };
            }
        }

        // Access to underlying model for operations
        public User Model => _user;

        #region INotifyPropertyChanged
        public event PropertyChangedEventHandler? PropertyChanged;

        protected virtual void OnPropertyChanged([CallerMemberName] string? propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
        #endregion
    }
} 