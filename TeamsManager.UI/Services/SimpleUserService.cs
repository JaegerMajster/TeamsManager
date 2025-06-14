using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using TeamsManager.Core.Abstractions.Services;
using TeamsManager.Core.Enums;
using TeamsManager.Core.Models;
using TeamsManager.Core.Services;
using TeamsManager.Data;

namespace TeamsManager.UI.Services
{
    /// <summary>
    /// Prosta implementacja IUserService która pobiera użytkowników z lokalnej bazy danych.
    /// Używana tymczasowo zamiast komunikacji z API.
    /// </summary>
    public class SimpleUserService : IUserService
    {
        private readonly TeamsManagerDbContext _context;
        private readonly ILogger<SimpleUserService> _logger;
        private readonly SeedDataService _seedDataService;

        public SimpleUserService(TeamsManagerDbContext context, ILogger<SimpleUserService> logger, SeedDataService seedDataService)
        {
            _context = context ?? throw new ArgumentNullException(nameof(context));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _seedDataService = seedDataService ?? throw new ArgumentNullException(nameof(seedDataService));
        }

        public async Task<User?> GetUserByUpnAsync(string upn, bool forceRefresh = false, string? apiAccessToken = null)
        {
            try
            {
                _logger.LogInformation("Pobieranie użytkownika {UPN} z bazy danych", upn);

                var user = await _context.Users
                    .Include(u => u.Department)
                    .FirstOrDefaultAsync(u => u.UPN == upn);

                if (user != null)
                {
                    _logger.LogInformation("Znaleziono użytkownika {UPN}: {FirstName} {LastName}", 
                        upn, user.FirstName, user.LastName);
                }
                else
                {
                    _logger.LogWarning("Nie znaleziono użytkownika {UPN}", upn);
                }

                return user;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Błąd podczas pobierania użytkownika {UPN}", upn);
                return null;
            }
        }

        public async Task<User?> GetUserByIdAsync(string userId, bool forceRefresh = false, string? apiAccessToken = null)
        {
            try
            {
                _logger.LogInformation("Pobieranie użytkownika {UserId} z bazy danych", userId);

                var user = await _context.Users
                    .Include(u => u.Department)
                    .FirstOrDefaultAsync(u => u.Id == userId);

                if (user != null)
                {
                    _logger.LogInformation("Znaleziono użytkownika {UserId}: {FirstName} {LastName}", 
                        userId, user.FirstName, user.LastName);
                }
                else
                {
                    _logger.LogWarning("Nie znaleziono użytkownika {UserId}", userId);
                }

                return user;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Błąd podczas pobierania użytkownika {UserId}", userId);
                return null;
            }
        }

        public async Task<IEnumerable<User>> GetAllUsersAsync(bool includeInactive = false, bool forceRefresh = false, string? apiAccessToken = null)
        {
            try
            {
                _logger.LogInformation("Pobieranie wszystkich użytkowników z bazy danych (includeInactive: {IncludeInactive})", includeInactive);

                // Sprawdź połączenie z bazą danych
                if (!await _context.Database.CanConnectAsync())
                {
                    _logger.LogError("Nie można połączyć się z bazą danych");
                    throw new InvalidOperationException("Brak połączenia z bazą danych");
                }

                var query = _context.Users.Include(u => u.Department).AsQueryable();

                if (!includeInactive)
                {
                    query = query.Where(u => u.IsActive);
                }

                var users = await query.ToListAsync();

                _logger.LogInformation("Znaleziono {Count} użytkowników", users.Count);
                return users;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Błąd podczas pobierania użytkowników: {Message}", ex.Message);
                throw; // Przekaż błąd do ViewModelu dla lepszej obsługi
            }
        }

        public async Task<IEnumerable<User>> GetAllActiveUsersAsync(bool forceRefresh = false, string? apiAccessToken = null)
        {
            return await GetAllUsersAsync(includeInactive: false, forceRefresh: forceRefresh, apiAccessToken: apiAccessToken);
        }

        public async Task<IEnumerable<User>> GetUsersByRoleAsync(UserRole role, bool forceRefresh = false, string? apiAccessToken = null)
        {
            try
            {
                _logger.LogInformation("Pobieranie użytkowników z rolą {Role}", role);

                var users = await _context.Users
                    .Where(u => u.Role == role && u.IsActive)
                    .Include(u => u.Department)
                    .ToListAsync();

                _logger.LogInformation("Znaleziono {Count} użytkowników z rolą {Role}", users.Count, role);
                return users;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Błąd podczas pobierania użytkowników z rolą {Role}", role);
                return Enumerable.Empty<User>();
            }
        }

        // Implementacja metod tworzenia i aktualizacji użytkowników
        public async Task<User?> CreateUserAsync(User user, string apiAccessToken)
        {
            try
            {
                if (user == null)
                {
                    _logger.LogError("Nie można utworzyć użytkownika - obiekt user jest null");
                    return null;
                }

                _logger.LogInformation("Tworzenie użytkownika {UPN} w bazie danych", user.UPN);

                // Sprawdź czy użytkownik już istnieje
                var existingUser = await _context.Users.FirstOrDefaultAsync(u => u.UPN == user.UPN);
                if (existingUser != null)
                {
                    _logger.LogWarning("Użytkownik {UPN} już istnieje w bazie danych", user.UPN);
                    return existingUser;
                }

                // Dodaj użytkownika do kontekstu
                _context.Users.Add(user);
                await _context.SaveChangesAsync();

                _logger.LogInformation("Utworzono użytkownika {UPN}: {DisplayName}", user.UPN, user.DisplayName);
                return user;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Błąd podczas tworzenia użytkownika {UPN}", user?.UPN);
                return null;
            }
        }

        public async Task<bool> UpdateUserAsync(User user, string apiAccessToken)
        {
            try
            {
                if (user == null)
                {
                    _logger.LogError("Nie można zaktualizować użytkownika - obiekt user jest null");
                    return false;
                }

                _logger.LogInformation("Aktualizacja użytkownika {UPN} w bazie danych", user.UPN);

                // Znajdź istniejącego użytkownika
                var existingUser = await _context.Users.FirstOrDefaultAsync(u => u.Id == user.Id);
                if (existingUser == null)
                {
                    _logger.LogWarning("Nie znaleziono użytkownika {UserId} do aktualizacji", user.Id);
                    return false;
                }

                // Zaktualizuj właściwości
                existingUser.FirstName = user.FirstName;
                existingUser.LastName = user.LastName;
                existingUser.AlternateEmail = user.AlternateEmail;
                existingUser.Position = user.Position;
                existingUser.IsActive = user.IsActive;
                existingUser.ModifiedBy = user.ModifiedBy;
                existingUser.ModifiedDate = user.ModifiedDate;

                await _context.SaveChangesAsync();

                _logger.LogInformation("Zaktualizowano użytkownika {UPN}: {DisplayName}", user.UPN, user.DisplayName);
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Błąd podczas aktualizacji użytkownika {UPN}", user?.UPN);
                return false;
            }
        }

        public async Task<bool> DeactivateUserAsync(string userId, string apiAccessToken)
        {
            _logger.LogWarning("DeactivateUserAsync nie jest zaimplementowana w SimpleUserService");
            return await Task.FromResult(false);
        }

        public async Task<Dictionary<string, string>> SynchronizeAllUsersAsync(string apiAccessToken, IProgress<int>? progress = null)
        {
            _logger.LogWarning("SynchronizeAllUsersAsync nie jest zaimplementowana w SimpleUserService");
            progress?.Report(100);
            return await Task.FromResult(new Dictionary<string, string>
            {
                ["status"] = "skipped",
                ["message"] = "Synchronizacja pomijana w SimpleUserService"
            });
        }

        public async Task<User?> CreateUserAsync(string firstName, string lastName, string upn, UserRole role, string departmentId, string password, string accessToken, bool sendWelcomeEmail = false, string? phone = null, string? alternateEmail = null, string? externalId = null, DateTime? birthDate = null, DateTime? employmentDate = null, string? position = null, string? notes = null, bool isSystemAdmin = false)
        {
            try
            {
                _logger.LogInformation("Tworzenie użytkownika {FirstName} {LastName} ({UPN}) w bazie danych", firstName, lastName, upn);

                // Sprawdź czy użytkownik już istnieje
                var existingUser = await _context.Users.FirstOrDefaultAsync(u => u.UPN == upn);
                if (existingUser != null)
                {
                    _logger.LogWarning("Użytkownik {UPN} już istnieje w bazie danych", upn);
                    return existingUser;
                }

                // Pobierz domyślny dział jeśli nie podano departmentId
                string finalDepartmentId = departmentId;
                if (string.IsNullOrEmpty(finalDepartmentId))
                {
                    _logger.LogInformation("Brak przypisanego działu dla użytkownika {UPN}, pobieranie domyślnego działu", upn);
                    finalDepartmentId = await _seedDataService.GetDefaultDepartmentIdAsync();
                    _logger.LogInformation("Przypisano domyślny dział {DepartmentId} dla użytkownika {UPN}", finalDepartmentId, upn);
                }

                // Utwórz nowego użytkownika
                var newUser = new User
                {
                    Id = Guid.NewGuid().ToString(),
                    FirstName = firstName,
                    LastName = lastName,
                    UPN = upn,
                    Role = role,
                    DepartmentId = finalDepartmentId, // Teraz zawsze będzie mieć wartość
                    Phone = phone,
                    AlternateEmail = alternateEmail,
                    ExternalId = externalId,
                    BirthDate = birthDate,
                    EmploymentDate = employmentDate,
                    Position = position,
                    Notes = notes,
                    IsSystemAdmin = isSystemAdmin,
                    IsActive = true,
                    CreatedBy = "UserSynchronization",
                    CreatedDate = DateTime.UtcNow
                };

                // Dodaj użytkownika do kontekstu
                _context.Users.Add(newUser);
                await _context.SaveChangesAsync();

                _logger.LogInformation("Utworzono użytkownika {UPN}: {DisplayName} (ID: {UserId}, DepartmentId: {DepartmentId})", 
                    newUser.UPN, newUser.DisplayName, newUser.Id, newUser.DepartmentId);
                
                return newUser;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Błąd podczas tworzenia użytkownika {FirstName} {LastName} ({UPN})", firstName, lastName, upn);
                return null;
            }
        }

        public async Task<bool> DeactivateUserAsync(string userId, string accessToken, bool deactivateM365Account = true)
        {
            _logger.LogWarning("DeactivateUserAsync nie jest zaimplementowana w SimpleUserService");
            return await Task.FromResult(false);
        }

        public async Task<bool> ActivateUserAsync(string userId, string accessToken, bool activateM365Account = true)
        {
            return await Task.FromResult(true);
        }

        public async Task<bool> DeleteUserAsync(string userId, string accessToken, bool deleteM365Account = true)
        {
            return await Task.FromResult(true);
        }

        public async Task<UserSchoolType?> AssignUserToSchoolTypeAsync(string userId, string schoolTypeId, DateTime startDate, DateTime? endDate, decimal? workloadPercentage, string? notes)
        {
            _logger.LogWarning("AssignUserToSchoolTypeAsync nie jest zaimplementowana w SimpleUserService");
            return await Task.FromResult<UserSchoolType?>(null);
        }

        public async Task<bool> RemoveUserFromSchoolTypeAsync(string userSchoolTypeId)
        {
            _logger.LogWarning("RemoveUserFromSchoolTypeAsync nie jest zaimplementowana w SimpleUserService");
            return await Task.FromResult(false);
        }

        public async Task<UserSubject?> AssignTeacherToSubjectAsync(string userId, string subjectId, DateTime startDate, string? notes)
        {
            _logger.LogWarning("AssignTeacherToSubjectAsync nie jest zaimplementowana w SimpleUserService");
            return await Task.FromResult<UserSubject?>(null);
        }

        public async Task<bool> RemoveTeacherFromSubjectAsync(string userSubjectId)
        {
            _logger.LogWarning("RemoveTeacherFromSubjectAsync nie jest zaimplementowana w SimpleUserService");
            return await Task.FromResult(false);
        }

        public async Task RefreshCacheAsync()
        {
            _logger.LogInformation("RefreshCacheAsync wykonana (brak cache w SimpleUserService)");
            await Task.CompletedTask;
        }
    }
} 