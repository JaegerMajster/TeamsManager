using System;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using TeamsManager.Core.Abstractions;
using TeamsManager.Core.Abstractions.Services;
using TeamsManager.Core.Enums;
using TeamsManager.UI.Services.Abstractions;

namespace TeamsManager.UI.Services
{
    /// <summary>
    /// Serwis odpowiedzialny za synchronizację użytkowników po logowaniu.
    /// Sprawdza czy zalogowany użytkownik istnieje w lokalnej bazie danych
    /// i tworzy go jeśli nie istnieje, używając danych z Microsoft Graph.
    /// </summary>
    public interface IUserSynchronizationService
    {
        /// <summary>
        /// Synchronizuje zalogowanego użytkownika z lokalną bazą danych
        /// </summary>
        /// <param name="accessToken">Token dostępu do Microsoft Graph</param>
        /// <param name="userUpn">UPN zalogowanego użytkownika</param>
        /// <returns>True jeśli synchronizacja przebiegła pomyślnie</returns>
        Task<bool> SynchronizeLoggedUserAsync(string accessToken, string userUpn);
    }

    public class UserSynchronizationService : IUserSynchronizationService
    {
        private readonly IUserService _userService;
        private readonly IGraphUserProfileService _graphUserProfileService;
        private readonly ICurrentUserService _currentUserService;
        private readonly ILogger<UserSynchronizationService> _logger;

        public UserSynchronizationService(
            IUserService userService,
            IGraphUserProfileService graphUserProfileService,
            ICurrentUserService currentUserService,
            ILogger<UserSynchronizationService> logger)
        {
            _userService = userService ?? throw new ArgumentNullException(nameof(userService));
            _graphUserProfileService = graphUserProfileService ?? throw new ArgumentNullException(nameof(graphUserProfileService));
            _currentUserService = currentUserService ?? throw new ArgumentNullException(nameof(currentUserService));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        public async Task<bool> SynchronizeLoggedUserAsync(string accessToken, string userUpn)
        {
            if (string.IsNullOrEmpty(accessToken))
            {
                _logger.LogWarning("Brak tokenu dostępu - nie można synchronizować użytkownika");
                return false;
            }

            if (string.IsNullOrEmpty(userUpn))
            {
                _logger.LogWarning("Brak UPN użytkownika - nie można synchronizować");
                return false;
            }

            try
            {
                _logger.LogInformation("=== ROZPOCZYNANIE SYNCHRONIZACJI UŻYTKOWNIKA {UserUpn} ===", userUpn);
                Console.WriteLine($"=== ROZPOCZYNANIE SYNCHRONIZACJI UŻYTKOWNIKA {userUpn} ===");

                // Sprawdź czy użytkownik już istnieje w lokalnej bazie
                Console.WriteLine($"=== SPRAWDZANIE CZY UŻYTKOWNIK ISTNIEJE: {userUpn} ===");
                var existingUser = await _userService.GetUserByUpnAsync(userUpn, forceRefresh: false);
                
                if (existingUser != null && existingUser.IsActive)
                {
                    _logger.LogDebug("Użytkownik '{DisplayName}' ({UserUpn}) już istnieje w bazie danych", 
                        existingUser.DisplayName, userUpn);
                    Console.WriteLine($"=== UŻYTKOWNIK JUŻ ISTNIEJE: {existingUser.DisplayName} ({userUpn}) ===");
                    return true;
                }
                
                Console.WriteLine($"=== UŻYTKOWNIK NIE ISTNIEJE, POBIERANIE PROFILU Z GRAPH ===");

                // Pobierz profil użytkownika z Microsoft Graph
                var userProfile = await _graphUserProfileService.GetUserProfileAsync(accessToken);
                if (userProfile == null)
                {
                    _logger.LogError("Nie udało się pobrać profilu użytkownika {UserUpn} z Microsoft Graph", userUpn);
                    Console.WriteLine($"=== BŁĄD: NIE UDAŁO SIĘ POBRAĆ PROFILU Z GRAPH ===");
                    return false;
                }
                
                Console.WriteLine($"=== PROFIL POBRANY: {userProfile.DisplayName} ({userProfile.UserPrincipalName}) ===");

                // Sprawdź czy UPN z profilu pasuje do zalogowanego użytkownika
                var profileUpn = userProfile.UserPrincipalName ?? userProfile.Mail;
                if (string.IsNullOrEmpty(profileUpn) || !profileUpn.Equals(userUpn, StringComparison.OrdinalIgnoreCase))
                {
                    _logger.LogWarning("UPN z profilu ({ProfileUpn}) nie pasuje do zalogowanego użytkownika ({UserUpn})", 
                        profileUpn, userUpn);
                    return false;
                }

                // Parsuj imię i nazwisko z DisplayName
                var displayName = userProfile.DisplayName ?? "";
                var nameParts = displayName.Split(' ', StringSplitOptions.RemoveEmptyEntries);
                
                var firstName = nameParts.Length > 0 ? nameParts[0] : "Nieznane";
                var lastName = nameParts.Length > 1 ? string.Join(" ", nameParts[1..]) : "Nazwisko";

                // Jeśli użytkownik istniał ale był nieaktywny, zaktualizuj go
                if (existingUser != null && !existingUser.IsActive)
                {
                    _logger.LogInformation("Reaktywowanie nieaktywnego użytkownika '{DisplayName}' ({UserUpn})", 
                        userProfile.DisplayName ?? $"{firstName} {lastName}", userUpn);
                    
                    // Zaktualizuj dane istniejącego użytkownika
                    existingUser.FirstName = firstName;
                    existingUser.LastName = lastName;
                    existingUser.AlternateEmail = userProfile.Mail ?? userUpn;
                    existingUser.Position = userProfile.JobTitle ?? "";
                    existingUser.IsActive = true;
                    existingUser.MarkAsModified(_currentUserService.GetCurrentUserUpn() ?? "system");

                    // Zaktualizuj użytkownika w bazie danych
                    var updateResult = await _userService.UpdateUserAsync(existingUser, accessToken);
                    if (updateResult)
                    {
                        _logger.LogInformation("Pomyślnie reaktywowano użytkownika '{DisplayName}' ({UserUpn})", 
                            userProfile.DisplayName ?? $"{firstName} {lastName}", userUpn);
                        return true;
                    }
                    else
                    {
                        _logger.LogError("Nie udało się reaktywować użytkownika '{DisplayName}' ({UserUpn})", 
                            userProfile.DisplayName ?? $"{firstName} {lastName}", userUpn);
                        return false;
                    }
                }
                else
                {
                    // Osoba logująca się otrzymuje rolę Administrator (ma dostęp do aplikacji)
                    var assignedRole = UserRole.Administrator;
                    
                    _logger.LogInformation("Tworzenie nowego użytkownika '{DisplayName}' ({UserUpn}) na podstawie danych z Graph. Stanowisko: '{JobTitle}', Dział: '{Department}' -> Rola: {Role}", 
                        userProfile.DisplayName ?? $"{firstName} {lastName}", userUpn, userProfile.JobTitle ?? "brak", userProfile.Department ?? "brak", assignedRole);
                    Console.WriteLine($"=== TWORZENIE NOWEGO UŻYTKOWNIKA: {firstName} {lastName} ({userUpn}) ===");
                    Console.WriteLine($"=== STANOWISKO: '{userProfile.JobTitle ?? "brak"}', DZIAŁ: '{userProfile.Department ?? "brak"}' -> ROLA: {assignedRole} ===");
                    
                    // Utwórz użytkownika w bazie danych używając CreateUserAsync
                    var createdUser = await _userService.CreateUserAsync(
                        firstName: firstName,
                        lastName: lastName,
                        upn: userUpn,
                        role: assignedRole,
                        departmentId: "", // Będzie wymagane ustawienie przez administratora
                        password: GenerateTemporaryPassword(), // Generujemy tymczasowe hasło
                        accessToken: accessToken,
                        sendWelcomeEmail: false,
                        phone: null,
                        alternateEmail: userProfile.Mail,
                        externalId: null,
                        birthDate: null,
                        employmentDate: null,
                        position: userProfile.JobTitle,
                        notes: null,
                        isSystemAdmin: false
                    );
                    
                    if (createdUser != null)
                    {
                        _logger.LogInformation("Pomyślnie utworzono użytkownika '{DisplayName}' ({UserUpn}) z rolą {Role} i stanowiskiem '{Position}'", 
                            createdUser.DisplayName, userUpn, assignedRole, createdUser.Position ?? "brak");
                        Console.WriteLine($"=== SUKCES: UTWORZONO UŻYTKOWNIKA {createdUser.DisplayName} ===");
                        return true;
                    }
                    else
                    {
                        _logger.LogError("Nie udało się utworzyć użytkownika '{DisplayName}' ({UserUpn})", 
                            userProfile.DisplayName ?? $"{firstName} {lastName}", userUpn);
                        Console.WriteLine($"=== BŁĄD: NIE UDAŁO SIĘ UTWORZYĆ UŻYTKOWNIKA ===");
                        return false;
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Błąd podczas synchronizacji użytkownika {UserUpn}", userUpn);
                Console.WriteLine($"=== WYJĄTEK PODCZAS SYNCHRONIZACJI: {ex.Message} ===");
                Console.WriteLine($"=== STACK TRACE: {ex.StackTrace} ===");
                return false;
            }
        }

        /// <summary>
        /// Generuje tymczasowe hasło dla nowego użytkownika
        /// </summary>
        private string GenerateTemporaryPassword()
        {
            // Generuj bezpieczne tymczasowe hasło
            var random = new Random();
            var chars = "ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz0123456789!@#$%^&*";
            var password = new char[12];
            
            for (int i = 0; i < password.Length; i++)
            {
                password[i] = chars[random.Next(chars.Length)];
            }
            
            return new string(password);
        }
    }
} 