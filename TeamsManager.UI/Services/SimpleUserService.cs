using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using TeamsManager.Core.Abstractions.Services;
using TeamsManager.Core.Enums;
using TeamsManager.Core.Models;
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

        public SimpleUserService(TeamsManagerDbContext context, ILogger<SimpleUserService> logger)
        {
            _context = context ?? throw new ArgumentNullException(nameof(context));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
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

        // Pozostałe metody interfejsu (nie implementowane - zwracają domyślne wartości)
        public async Task<User?> CreateUserAsync(User user, string apiAccessToken)
        {
            _logger.LogWarning("CreateUserAsync nie jest zaimplementowana w SimpleUserService");
            return await Task.FromResult<User?>(null);
        }

        public async Task<bool> UpdateUserAsync(User user, string apiAccessToken)
        {
            _logger.LogWarning("UpdateUserAsync nie jest zaimplementowana w SimpleUserService");
            return await Task.FromResult(false);
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

        public async Task<User?> CreateUserAsync(string firstName, string lastName, string upn, UserRole role, string departmentId, string password, string accessToken, bool sendWelcomeEmail = false)
        {
            _logger.LogWarning("CreateUserAsync nie jest zaimplementowana w SimpleUserService");
            return await Task.FromResult<User?>(null);
        }

        public async Task<bool> DeactivateUserAsync(string userId, string accessToken, bool deactivateM365Account = true)
        {
            _logger.LogWarning("DeactivateUserAsync nie jest zaimplementowana w SimpleUserService");
            return await Task.FromResult(false);
        }

        public async Task<bool> ActivateUserAsync(string userId, string accessToken, bool activateM365Account = true)
        {
            _logger.LogWarning("ActivateUserAsync nie jest zaimplementowana w SimpleUserService");
            return await Task.FromResult(false);
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