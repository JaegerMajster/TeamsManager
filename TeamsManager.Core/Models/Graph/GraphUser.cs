using System;
using System.Collections.Generic;
using System.Linq;
using TeamsManager.Core.Enums;

namespace TeamsManager.Core.Models.Graph
{
    /// <summary>
    /// Reprezentuje użytkownika Microsoft Graph API z zachowaniem kompatybilności z lokalnym modelem User.
    /// Dodaje Graph API specific properties i funkcjonalności.
    /// </summary>
    public class GraphUser
    {
        /// <summary>
        /// Graph API Object ID (identyfikator użytkownika w Graph API).
        /// </summary>
        public string? Id { get; set; }

        /// <summary>
        /// Imię użytkownika.
        /// Zachowuje kompatybilność z User.FirstName.
        /// </summary>
        public string? GivenName { get; set; }

        /// <summary>
        /// Nazwisko użytkownika.
        /// Zachowuje kompatybilność z User.LastName.
        /// </summary>
        public string? Surname { get; set; }

        /// <summary>
        /// User Principal Name.
        /// Zachowuje kompatybilność z User.UPN.
        /// </summary>
        public string? UserPrincipalName { get; set; }

        /// <summary>
        /// Główny adres e-mail.
        /// </summary>
        public string? Mail { get; set; }

        /// <summary>
        /// Alias mail (krótka nazwa).
        /// </summary>
        public string? MailNickname { get; set; }

        /// <summary>
        /// Typ użytkownika (Member, Guest).
        /// </summary>
        public string? UserType { get; set; }

        /// <summary>
        /// Czy konto jest aktywne.
        /// Zachowuje kompatybilność z User.IsActive.
        /// </summary>
        public bool AccountEnabled { get; set; } = true;

        /// <summary>
        /// Data utworzenia konta.
        /// </summary>
        public DateTime? CreatedDateTime { get; set; }

        /// <summary>
        /// Data ostatniego logowania.
        /// </summary>
        public DateTime? LastSignInDateTime { get; set; }

        /// <summary>
        /// Stanowisko.
        /// </summary>
        public string? JobTitle { get; set; }

        /// <summary>
        /// Dział.
        /// </summary>
        public string? Department { get; set; }

        /// <summary>
        /// Nazwa firmy/organizacji.
        /// </summary>
        public string? CompanyName { get; set; }

        /// <summary>
        /// Lokalizacja biura.
        /// </summary>
        public string? OfficeLocation { get; set; }

        /// <summary>
        /// Numer telefonu służbowego.
        /// </summary>
        public string? BusinessPhone { get; set; }

        /// <summary>
        /// Numer telefonu komórkowego.
        /// </summary>
        public string? MobilePhone { get; set; }

        /// <summary>
        /// Numer faksu.
        /// </summary>
        public string? FaxNumber { get; set; }

        /// <summary>
        /// Adres służbowy.
        /// </summary>
        public string? StreetAddress { get; set; }

        /// <summary>
        /// Miasto.
        /// </summary>
        public string? City { get; set; }

        /// <summary>
        /// Stan/województwo.
        /// </summary>
        public string? State { get; set; }

        /// <summary>
        /// Kod pocztowy.
        /// </summary>
        public string? PostalCode { get; set; }

        /// <summary>
        /// Kraj.
        /// </summary>
        public string? Country { get; set; }

        /// <summary>
        /// Identyfikator menedżera.
        /// </summary>
        public string? ManagerId { get; set; }

        /// <summary>
        /// Informacje o menedżerze.
        /// </summary>
        public GraphUser? Manager { get; set; }

        /// <summary>
        /// Lista podwładnych.
        /// </summary>
        public List<GraphUser> DirectReports { get; set; } = new List<GraphUser>();

        /// <summary>
        /// Lista przypisanych licencji.
        /// </summary>
        public List<GraphLicense> AssignedLicenses { get; set; } = new List<GraphLicense>();

        /// <summary>
        /// Lista planów usług.
        /// </summary>
        public List<GraphServicePlan> ServicePlans { get; set; } = new List<GraphServicePlan>();

        /// <summary>
        /// Typ licencji użytkownika.
        /// </summary>
        public string? LicenseType { get; set; }

        /// <summary>
        /// Właściwości kompatybilności - Imię.
        /// </summary>
        public string? FirstName => GivenName;

        /// <summary>
        /// Właściwości kompatybilności - Nazwisko.
        /// </summary>
        public string? LastName => Surname;

        /// <summary>
        /// Właściwości kompatybilności - UPN.
        /// </summary>
        public string? UPN => UserPrincipalName;

        /// <summary>
        /// Właściwości kompatybilności - IsActive.
        /// </summary>
        public bool IsActive => AccountEnabled;

        /// <summary>
        /// Pełne imię i nazwisko.
        /// Zachowuje kompatybilność z User.FullName.
        /// </summary>
        public string FullName => $"{GivenName} {Surname}".Trim();

        /// <summary>
        /// Nazwa wyświetlana.
        /// </summary>
        public string DisplayName => FullName;

        /// <summary>
        /// Status aktywności użytkownika.
        /// </summary>
        public string ActivityStatus
        {
            get
            {
                if (!AccountEnabled) return "Nieaktywny";
                if (!LastSignInDateTime.HasValue) return "Nigdy nie zalogowany";
                
                var daysSinceLastSignIn = DaysSinceLastSignIn;
                if (daysSinceLastSignIn <= 7) return "Bardzo aktywny";
                if (daysSinceLastSignIn <= 30) return "Aktywny";
                if (daysSinceLastSignIn <= 90) return "Umiarkowanie aktywny";
                return "Nieaktywny";
            }
        }

        /// <summary>
        /// Liczba dni od ostatniego logowania.
        /// </summary>
        public int? DaysSinceLastSignIn
        {
            get
            {
                if (!LastSignInDateTime.HasValue) return null;
                return (int)(DateTime.UtcNow - LastSignInDateTime.Value).TotalDays;
            }
        }

        /// <summary>
        /// Czy użytkownik był niedawno aktywny (w ciągu ostatnich 30 dni).
        /// </summary>
        public bool IsRecentlyActive => DaysSinceLastSignIn <= 30;

        /// <summary>
        /// Sprawdza czy użytkownik ma określoną licencję.
        /// </summary>
        /// <param name="licenseSkuId">SKU ID licencji</param>
        /// <returns>True jeśli ma licencję</returns>
        public bool HasLicense(string licenseSkuId)
        {
            return AssignedLicenses.Any(l => l.SkuId == licenseSkuId);
        }

        /// <summary>
        /// Sprawdza czy użytkownik ma rolę administratora.
        /// </summary>
        /// <returns>True jeśli ma rolę administratora</returns>
        public bool HasAdminRole()
        {
            // Logika sprawdzenia ról administratora w Graph API
            // To będzie implementowane na podstawie rzeczywistych danych Graph API
            return false;
        }

        /// <summary>
        /// Sprawdza czy użytkownik jest członkiem określonej grupy.
        /// </summary>
        /// <param name="groupId">ID grupy</param>
        /// <returns>True jeśli jest członkiem</returns>
        public bool IsMemberOfGroup(string groupId)
        {
            // Logika sprawdzenia członkostwa w grupie
            // To będzie implementowane na podstawie rzeczywistych danych Graph API
            return false;
        }

        /// <summary>
        /// Pobiera licencję o określonym SKU ID.
        /// </summary>
        /// <param name="licenseSkuId">SKU ID licencji</param>
        /// <returns>Licencja lub null</returns>
        public GraphLicense? GetLicense(string licenseSkuId)
        {
            return AssignedLicenses.FirstOrDefault(l => l.SkuId == licenseSkuId);
        }

        /// <summary>
        /// Konwertuje GraphUser na lokalny model User.
        /// </summary>
        /// <returns>Lokalny model User</returns>
        public User ToLocalUser()
        {
            return new User
            {
                FirstName = GivenName ?? string.Empty,
                LastName = Surname ?? string.Empty,
                UPN = UserPrincipalName ?? string.Empty,
                ExternalId = Id,
                Phone = BusinessPhone ?? MobilePhone,
                Position = JobTitle,
                EmploymentDate = CreatedDateTime,
                LastLoginDate = LastSignInDateTime,
                // Mapowanie innych właściwości według potrzeb
            };
        }

        /// <summary>
        /// Tworzy GraphUser na podstawie lokalnego modelu User.
        /// </summary>
        /// <param name="user">Lokalny model User</param>
        /// <returns>GraphUser</returns>
        public static GraphUser FromLocalUser(User user)
        {
            return new GraphUser
            {
                Id = user.ExternalId,
                GivenName = user.FirstName,
                Surname = user.LastName,
                UserPrincipalName = user.UPN,
                AccountEnabled = user.IsActive,
                JobTitle = user.Position,
                BusinessPhone = user.Phone,
                CreatedDateTime = user.EmploymentDate,
                LastSignInDateTime = user.LastLoginDate,
                // Mapowanie innych właściwości według potrzeb
            };
        }

        /// <summary>
        /// Pobiera podsumowanie użytkownika.
        /// </summary>
        /// <returns>Podsumowanie</returns>
        public string GetSummary()
        {
            var status = AccountEnabled ? "Aktywny" : "Nieaktywny";
            var department = !string.IsNullOrEmpty(Department) ? $" ({Department})" : "";
            var licenses = $"{AssignedLicenses.Count} licencji";

            return $"{FullName}{department}: {status}, {licenses}";
        }

        /// <summary>
        /// Pobiera szczegółowe informacje o użytkowniku.
        /// </summary>
        /// <returns>Szczegółowe informacje</returns>
        public string GetDetailedInfo()
        {
            var info = new List<string>
            {
                $"Nazwa: {FullName}",
                $"UPN: {UserPrincipalName ?? "Brak"}",
                $"Email: {Mail ?? "Brak"}",
                $"Status: {(AccountEnabled ? "Aktywny" : "Nieaktywny")}",
                $"Typ: {UserType ?? "Nieznany"}",
                $"Stanowisko: {JobTitle ?? "Brak"}",
                $"Dział: {Department ?? "Brak"}",
                $"Utworzony: {CreatedDateTime?.ToString("yyyy-MM-dd") ?? "Nieznane"}",
                $"Ostatnie logowanie: {LastSignInDateTime?.ToString("yyyy-MM-dd HH:mm") ?? "Nigdy"}",
                $"Status aktywności: {ActivityStatus}",
                $"Liczba licencji: {AssignedLicenses.Count}",
                $"Liczba podwładnych: {DirectReports.Count}"
            };

            if (AssignedLicenses.Count > 0)
            {
                info.Add("Licencje:");
                foreach (var license in AssignedLicenses)
                {
                    info.Add($"  • {license.SkuPartNumber}");
                }
            }

            return string.Join(Environment.NewLine, info);
        }
    }

    /// <summary>
    /// Licencja użytkownika Graph API.
    /// </summary>
    public class GraphLicense
    {
        /// <summary>
        /// SKU ID licencji.
        /// </summary>
        public string? SkuId { get; set; }

        /// <summary>
        /// Nazwa SKU licencji.
        /// </summary>
        public string? SkuPartNumber { get; set; }

        /// <summary>
        /// Lista wyłączonych planów usług.
        /// </summary>
        public List<string> DisabledPlans { get; set; } = new List<string>();

        /// <summary>
        /// Data przypisania licencji.
        /// </summary>
        public DateTime? AssignedDateTime { get; set; }

        /// <summary>
        /// Stan licencji.
        /// </summary>
        public string? State { get; set; }

        /// <summary>
        /// Czy licencja jest aktywna.
        /// </summary>
        public bool IsActive => State == "Active";
    }

    /// <summary>
    /// Plan usług Graph API.
    /// </summary>
    public class GraphServicePlan
    {
        /// <summary>
        /// ID planu usług.
        /// </summary>
        public string? ServicePlanId { get; set; }

        /// <summary>
        /// Nazwa planu usług.
        /// </summary>
        public string? ServicePlanName { get; set; }

        /// <summary>
        /// Stan planu usług (Success, Disabled, PendingInput, PendingActivation).
        /// </summary>
        public string? ProvisioningStatus { get; set; }

        /// <summary>
        /// Typ zastosowania planu.
        /// </summary>
        public string? AppliesTo { get; set; }

        /// <summary>
        /// Czy plan usług jest włączony.
        /// </summary>
        public bool IsEnabled => ProvisioningStatus == "Success";
    }
} 