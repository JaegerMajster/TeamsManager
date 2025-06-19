using System;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using TeamsManager.Core.Enums;
using TeamsManager.Core.Extensions;
using System.Collections.Generic;

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
        private string _password = string.Empty;

        // Properties with validation
        public string FirstName
        {
            get => _firstName;
            set 
            { 
                if (SetProperty(ref _firstName, value))
                {
                    // Automatycznie generuj UPN tylko dla nowych użytkowników (gdy UPN jest pusty lub ma domyślną domenę)
                    if (string.IsNullOrEmpty(Upn) || Upn.EndsWith("@ckziumm.edu.pl"))
                    {
                        GenerateUpnFromName();
                    }
                }
            }
        }

        public string LastName
        {
            get => _lastName;
            set 
            { 
                if (SetProperty(ref _lastName, value))
                {
                    // Automatycznie generuj UPN tylko dla nowych użytkowników (gdy UPN jest pusty lub ma domyślną domenę)
                    if (string.IsNullOrEmpty(Upn) || Upn.EndsWith("@ckziumm.edu.pl"))
                    {
                        GenerateUpnFromName();
                    }
                }
            }
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
                    OnPropertyChanged(nameof(PolishRole));
                }
            }
        }

        /// <summary>
        /// Rola użytkownika w języku polskim
        /// </summary>
        public string PolishRole => Role.ToPolishString();

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

        public string Password
        {
            get => _password;
            set => SetProperty(ref _password, value);
        }

        /// <summary>
        /// Automatycznie generuje UPN na podstawie imienia i nazwiska
        /// </summary>
        public void GenerateUpnFromName()
        {
            if (!string.IsNullOrWhiteSpace(FirstName) && !string.IsNullOrWhiteSpace(LastName))
            {
                var firstName = RemovePolishCharacters(FirstName.Trim().ToLowerInvariant());
                var lastName = RemovePolishCharacters(LastName.Trim().ToLowerInvariant());
                Upn = $"{firstName}.{lastName}@ckziumm.edu.pl";
            }
        }

        /// <summary>
        /// Usuwa polskie znaki diakrytyczne z tekstu
        /// </summary>
        private static string RemovePolishCharacters(string input)
        {
            if (string.IsNullOrEmpty(input))
                return input;

            var polishChars = new Dictionary<char, char>
            {
                {'ą', 'a'}, {'ć', 'c'}, {'ę', 'e'}, {'ł', 'l'}, {'ń', 'n'},
                {'ó', 'o'}, {'ś', 's'}, {'ź', 'z'}, {'ż', 'z'},
                {'Ą', 'A'}, {'Ć', 'C'}, {'Ę', 'E'}, {'Ł', 'L'}, {'Ń', 'N'},
                {'Ó', 'O'}, {'Ś', 'S'}, {'Ź', 'Z'}, {'Ż', 'Z'}
            };

            var result = new System.Text.StringBuilder(input.Length);
            foreach (char c in input)
            {
                result.Append(polishChars.TryGetValue(c, out char replacement) ? replacement : c);
            }

            return result.ToString();
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

        public bool IsTeachingRole => Role == UserRole.Nauczyciel || Role == UserRole.Wicedyrektor || Role == UserRole.Dyrektor;

        public string RolePermissionsDescription
        {
            get
            {
                return Role switch
                {
                    UserRole.Uczen => "• Członkostwo w zespołach jako uczeń\n• Dostęp do materiałów edukacyjnych\n• Brak uprawnień zarządzania",
                    UserRole.Sluchacz => "• Członkostwo w zespołach jako słuchacz\n• Dostęp do kursów i szkoleń\n• Brak uprawnień zarządzania",
                    UserRole.Nauczyciel => "• Tworzenie i zarządzanie zespołami\n• Właścicielstwo zespołów klasowych\n• Zarządzanie materiałami dydaktycznymi\n• Dostęp do narzędzi nauczyciela",
                    UserRole.PracownikAdministracyjny => "• Obsługa administracyjna szkoły\n• Dostęp do podstawowych funkcji zarządzania\n• Wsparcie w procesach organizacyjnych\n• Ograniczone uprawnienia do zespołów",
                    UserRole.Wicedyrektor => "• Wszystkie uprawnienia nauczyciela\n• Zarządzanie użytkownikami w swoich typach szkół\n• Nadzór nad zespołami w szkole\n• Dostęp do raportów i statystyk",
                    UserRole.Dyrektor => "• Pełne uprawnienia w całym systemie\n• Zarządzanie wszystkimi użytkownikami\n• Dostęp do ustawień systemowych\n• Zarządzanie strukturą organizacyjną",
                    UserRole.Administrator => "• Pełne uprawnienia techniczne i administracyjne\n• Zarządzanie systemem i konfiguracją\n• Dostęp do wszystkich funkcji aplikacji\n• Uprawnienia deweloperskie i diagnostyczne",
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
                    nameof(Password) => ValidatePassword(),
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

        private string ValidatePassword()
        {
            if (string.IsNullOrWhiteSpace(Password))
                return "Hasło jest wymagane";
            if (Password.Length < 8)
                return "Hasło musi mieć co najmniej 8 znaków";
            if (Password.Length > 50)
                return "Hasło może mieć maksymalnie 50 znaków";
            

            bool hasDigit = Password.Any(char.IsDigit);
            bool hasLetter = Password.Any(char.IsLetter);
            
            if (!hasDigit || !hasLetter)
                return "Hasło musi zawierać przynajmniej jedną literę i jedną cyfrę";
                
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