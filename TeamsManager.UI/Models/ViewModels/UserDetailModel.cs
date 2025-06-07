using System;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using TeamsManager.Core.Enums;

namespace TeamsManager.UI.Models.ViewModels
{
    /// <summary>
    /// Model danych dla formularza tworzenia/edycji użytkownika.
    /// Implementuje walidację IDataErrorInfo oraz INotifyPropertyChanged.
    /// </summary>
    public class UserDetailModel : INotifyPropertyChanged, IDataErrorInfo
    {
        private string _firstName = string.Empty;
        private string _lastName = string.Empty;
        private string _upn = string.Empty;
        private UserRole _role = UserRole.Uczen;
        private string _departmentId = string.Empty;
        private string? _phone;
        private string? _alternateEmail;
        private DateTime? _birthDate;
        private DateTime? _employmentDate;
        private string? _position;
        private string? _notes;
        private bool _isSystemAdmin;
        private byte[]? _avatarData;

        // Properties with validation
        public string FirstName
        {
            get => _firstName;
            set => SetProperty(ref _firstName, value);
        }

        public string LastName
        {
            get => _lastName;
            set => SetProperty(ref _lastName, value);
        }

        public string Upn
        {
            get => _upn;
            set => SetProperty(ref _upn, value);
        }

        public UserRole Role
        {
            get => _role;
            set
            {
                if (SetProperty(ref _role, value))
                {
                    OnPropertyChanged(nameof(IsTeachingRole));
                    OnPropertyChanged(nameof(RolePermissionsDescription));
                }
            }
        }

        public string DepartmentId
        {
            get => _departmentId;
            set => SetProperty(ref _departmentId, value);
        }

        public string? Phone
        {
            get => _phone;
            set => SetProperty(ref _phone, value);
        }

        public string? AlternateEmail
        {
            get => _alternateEmail;
            set => SetProperty(ref _alternateEmail, value);
        }

        public DateTime? BirthDate
        {
            get => _birthDate;
            set => SetProperty(ref _birthDate, value);
        }

        public DateTime? EmploymentDate
        {
            get => _employmentDate;
            set => SetProperty(ref _employmentDate, value);
        }

        public string? Position
        {
            get => _position;
            set => SetProperty(ref _position, value);
        }

        public string? Notes
        {
            get => _notes;
            set => SetProperty(ref _notes, value);
        }

        public bool IsSystemAdmin
        {
            get => _isSystemAdmin;
            set => SetProperty(ref _isSystemAdmin, value);
        }

        public byte[]? AvatarData
        {
            get => _avatarData;
            set
            {
                if (SetProperty(ref _avatarData, value))
                {
                    OnPropertyChanged(nameof(HasAvatar));
                    OnPropertyChanged(nameof(AvatarImageSource));
                }
            }
        }

        // Computed properties for UI
        public bool HasAvatar => _avatarData != null && _avatarData.Length > 0;

        public object? AvatarImageSource
        {
            get
            {
                if (!HasAvatar) return null;
                
                try
                {
                    var stream = new System.IO.MemoryStream(_avatarData!);
                    var image = new System.Windows.Media.Imaging.BitmapImage();
                    image.BeginInit();
                    image.StreamSource = stream;
                    image.CacheOption = System.Windows.Media.Imaging.BitmapCacheOption.OnLoad;
                    image.EndInit();
                    image.Freeze();
                    return image;
                }
                catch
                {
                    return null;
                }
            }
        }

        public bool IsTeachingRole => Role >= UserRole.Nauczyciel;

        public string RolePermissionsDescription
        {
            get
            {
                return Role switch
                {
                    UserRole.Uczen => "• Członkostwo w zespołach jako uczeń\n• Dostęp do materiałów edukacyjnych\n• Brak uprawnień zarządzania",
                    UserRole.Sluchacz => "• Członkostwo w zespołach jako słuchacz\n• Dostęp do kursów i szkoleń\n• Brak uprawnień zarządzania",
                    UserRole.Nauczyciel => "• Tworzenie i zarządzanie zespołami\n• Właścicielstwo zespołów klasowych\n• Zarządzanie materiałami dydaktycznymi\n• Dostęp do narzędzi nauczyciela",
                    UserRole.Wicedyrektor => "• Wszystkie uprawnienia nauczyciela\n• Zarządzanie użytkownikami w swoich typach szkół\n• Nadzór nad zespołami w szkole\n• Dostęp do raportów i statystyk",
                    UserRole.Dyrektor => "• Pełne uprawnienia w całym systemie\n• Zarządzanie wszystkimi użytkownikami\n• Dostęp do ustawień systemowych\n• Zarządzanie strukturą organizacyjną",
                    _ => "Nieznana rola"
                };
            }
        }

        // IDataErrorInfo implementation
        public string Error => string.Empty;

        public string this[string propertyName]
        {
            get
            {
                return propertyName switch
                {
                    nameof(FirstName) => ValidateFirstName(),
                    nameof(LastName) => ValidateLastName(),
                    nameof(Upn) => ValidateUpn(),
                    nameof(DepartmentId) => ValidateDepartmentId(),
                    nameof(Phone) => ValidatePhone(),
                    nameof(AlternateEmail) => ValidateAlternateEmail(),
                    nameof(BirthDate) => ValidateBirthDate(),
                    nameof(EmploymentDate) => ValidateEmploymentDate(),
                    _ => string.Empty
                };
            }
        }

        // Validation methods
        private string ValidateFirstName()
        {
            if (string.IsNullOrWhiteSpace(FirstName))
                return "Imię jest wymagane";
            if (FirstName.Length < 2)
                return "Imię musi mieć co najmniej 2 znaki";
            if (FirstName.Length > 50)
                return "Imię może mieć maksymalnie 50 znaków";
            return string.Empty;
        }

        private string ValidateLastName()
        {
            if (string.IsNullOrWhiteSpace(LastName))
                return "Nazwisko jest wymagane";
            if (LastName.Length < 2)
                return "Nazwisko musi mieć co najmniej 2 znaki";
            if (LastName.Length > 50)
                return "Nazwisko może mieć maksymalnie 50 znaków";
            return string.Empty;
        }

        private string ValidateUpn()
        {
            if (string.IsNullOrWhiteSpace(Upn))
                return "UPN jest wymagany";
            
            if (!Upn.Contains("@"))
                return "UPN musi zawierać znak @";
            
            if (!Upn.EndsWith(".edu.pl"))
                return "UPN musi kończyć się na .edu.pl";
            
            var parts = Upn.Split('@');
            if (parts.Length != 2)
                return "Nieprawidłowy format UPN";
            
            if (string.IsNullOrWhiteSpace(parts[0]))
                return "Nazwa użytkownika nie może być pusta";
            
            if (parts[0].Length < 3)
                return "Nazwa użytkownika musi mieć co najmniej 3 znaki";
            
            return string.Empty;
        }

        private string ValidateDepartmentId()
        {
            if (string.IsNullOrEmpty(DepartmentId))
                return "Dział jest wymagany";
            return string.Empty;
        }

        private string ValidatePhone()
        {
            if (string.IsNullOrWhiteSpace(Phone))
                return string.Empty; // Phone is optional
            
            // Remove common formatting characters
            var cleanPhone = Phone.Replace(" ", "").Replace("-", "").Replace("(", "").Replace(")", "").Replace("+", "");
            
            if (cleanPhone.Length < 9)
                return "Numer telefonu musi mieć co najmniej 9 cyfr";
            
            if (!System.Text.RegularExpressions.Regex.IsMatch(cleanPhone, @"^\d+$"))
                return "Numer telefonu może zawierać tylko cyfry";
            
            return string.Empty;
        }

        private string ValidateAlternateEmail()
        {
            if (string.IsNullOrWhiteSpace(AlternateEmail))
                return string.Empty; // Optional field
            
            try
            {
                var addr = new System.Net.Mail.MailAddress(AlternateEmail);
                return addr.Address == AlternateEmail ? string.Empty : "Nieprawidłowy format adresu email";
            }
            catch
            {
                return "Nieprawidłowy format adresu email";
            }
        }

        private string ValidateBirthDate()
        {
            if (!BirthDate.HasValue)
                return string.Empty; // Optional field
            
            if (BirthDate.Value > DateTime.Today)
                return "Data urodzenia nie może być w przyszłości";
            
            var age = DateTime.Today.Year - BirthDate.Value.Year;
            if (BirthDate.Value.Date > DateTime.Today.AddYears(-age))
                age--;
            
            if (age < 16)
                return "Użytkownik musi mieć co najmniej 16 lat";
            
            if (age > 100)
                return "Nieprawidłowa data urodzenia";
            
            return string.Empty;
        }

        private string ValidateEmploymentDate()
        {
            if (!EmploymentDate.HasValue)
                return string.Empty; // Optional field
            
            if (EmploymentDate.Value > DateTime.Today)
                return "Data zatrudnienia nie może być w przyszłości";
            
            if (BirthDate.HasValue && EmploymentDate.Value < BirthDate.Value.AddYears(16))
                return "Data zatrudnienia musi być co najmniej 16 lat po dacie urodzenia";
            
            return string.Empty;
        }

        // Helper method for property changes
        protected bool SetProperty<T>(ref T field, T value, [CallerMemberName] string? propertyName = null)
        {
            if (System.Collections.Generic.EqualityComparer<T>.Default.Equals(field, value))
                return false;
            
            field = value;
            OnPropertyChanged(propertyName);
            return true;
        }

        public event PropertyChangedEventHandler? PropertyChanged;

        protected virtual void OnPropertyChanged([CallerMemberName] string? propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
} 