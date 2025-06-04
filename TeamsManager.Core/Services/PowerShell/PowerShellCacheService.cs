using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Primitives;
using TeamsManager.Core.Abstractions.Services.PowerShell;
using TeamsManager.Core.Enums;

namespace TeamsManager.Core.Services.PowerShellServices
{
    /// <summary>
    /// Implementacja serwisu zarządzającego cache'owaniem danych PowerShell/Graph
    /// </summary>
    public class PowerShellCacheService : IPowerShellCacheService
    {
        private readonly MemoryCache _cache;
        private readonly ILogger<PowerShellCacheService> _logger;

        // Definicje kluczy cache
        private const string GraphContextCacheKey = "PowerShell_GraphContext";
        private const string UserIdCacheKeyPrefix = "PowerShell_UserId_";
        private const string UserUpnCacheKeyPrefix = "PowerShell_UserUpn_";
        private const string TeamDetailsCacheKeyPrefix = "PowerShell_Team_";
        private const string AllTeamsCacheKey = "PowerShell_Teams_All";
        private const string TeamChannelsCacheKeyPrefix = "PowerShell_TeamChannels_";
        private const string M365UserDetailsCacheKeyPrefix = "PowerShell_M365User_Id_";
        private const string M365UsersAccountEnabledCacheKeyPrefix = "PowerShell_M365Users_AccountEnabled_";
        private const string ChannelByGraphIdCacheKeyPrefix = "PowerShell_Channel_GraphId_";
        
        // Klucze cache dla działów
        private const string AllDepartmentsRootOnlyCacheKey = "Departments_AllActive_RootOnly";
        private const string AllDepartmentsAllCacheKey = "Departments_AllActive_All";
        private const string DepartmentByIdCacheKeyPrefix = "Department_Id_";
        private const string SubDepartmentsByParentIdCacheKeyPrefix = "Department_Sub_ParentId_";
        private const string UsersInDepartmentCacheKeyPrefix = "Department_UsersIn_Id_";
        
        // Klucze cache używane przez UserService
        private const string UserServiceAllActiveKey = "Users_AllActive";
        private const string UserServiceByIdPrefix = "User_Id_";
        private const string UserServiceByUpnPrefix = "User_Upn_";
        private const string UserServiceByRolePrefix = "Users_Role_";
        
        private readonly TimeSpan _defaultCacheDuration = TimeSpan.FromMinutes(15);
        private readonly TimeSpan _shortCacheDuration = TimeSpan.FromMinutes(5);

        // Token do zarządzania unieważnianiem wpisów cache
        private static CancellationTokenSource _powerShellCacheTokenSource = new CancellationTokenSource();
        
        // Współdzielony cache między instancjami Scoped
        private static readonly MemoryCache _sharedCache = new MemoryCache(new MemoryCacheOptions());

        public PowerShellCacheService(
            ILogger<PowerShellCacheService> logger)
        {
            // Używamy współdzielonego cache zamiast wstrzykniętego
            _cache = _sharedCache;
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        public async Task<string?> GetUserIdAsync(string userUpn, bool forceRefresh = false)
        {
            if (string.IsNullOrWhiteSpace(userUpn))
            {
                _logger.LogWarning("Próba pobrania ID użytkownika z pustym UPN.");
                return null;
            }

            string cacheKey = UserIdCacheKeyPrefix + userUpn;

            if (!forceRefresh && _cache.TryGetValue(cacheKey, out string? cachedId))
            {
                _logger.LogDebug("ID użytkownika {UserUpn} znalezione w cache.", userUpn);
                return cachedId;
            }

            _logger.LogDebug("ID użytkownika {UserUpn} nie znalezione w cache.", userUpn);
            return null;
        }

        public void SetUserId(string userUpn, string userId)
        {
            if (string.IsNullOrWhiteSpace(userUpn) || string.IsNullOrWhiteSpace(userId))
            {
                _logger.LogWarning("Próba zapisania pustego UPN lub UserId w cache.");
                return;
            }

            string cacheKey = UserIdCacheKeyPrefix + userUpn;
            _cache.Set(cacheKey, userId, GetDefaultCacheEntryOptions());

            // Cache też po UPN
            string upnCacheKey = UserUpnCacheKeyPrefix + userUpn;
            _cache.Set(upnCacheKey, userId, GetDefaultCacheEntryOptions());

            _logger.LogDebug("ID użytkownika {UserUpn} zapisane w cache.", userUpn);
        }

        public bool TryGetValue<T>(string key, out T? value)
        {
            return _cache.TryGetValue(key, out value);
        }

        public void Set<T>(string key, T value, TimeSpan? duration = null)
        {
            var options = duration.HasValue 
                ? new MemoryCacheEntryOptions()
                    .SetAbsoluteExpiration(duration.Value)
                    .AddExpirationToken(new CancellationChangeToken(_powerShellCacheTokenSource.Token))
                : GetDefaultCacheEntryOptions();

            _cache.Set(key, value, options);
            _logger.LogDebug("Zapisano w cache klucz: {Key} na czas: {Duration}", 
                key, duration ?? _defaultCacheDuration);
        }

        public void Remove(string key)
        {
            _cache.Remove(key);
            _logger.LogDebug("Usunięto z cache klucz: {Key}", key);
        }

        public void InvalidateUserCache(string? userId = null, string? userUpn = null)
        {
            if (!string.IsNullOrWhiteSpace(userId))
            {
                Remove(UserIdCacheKeyPrefix + userId);
                Remove(M365UserDetailsCacheKeyPrefix + userId);
            }

            if (!string.IsNullOrWhiteSpace(userUpn))
            {
                Remove(UserIdCacheKeyPrefix + userUpn);
                Remove(UserUpnCacheKeyPrefix + userUpn);
            }

            Remove(M365UsersAccountEnabledCacheKeyPrefix + "True");
            Remove(M365UsersAccountEnabledCacheKeyPrefix + "False");

            _logger.LogDebug("Unieważniono cache użytkownika. userId: {UserId}, userUpn: {UserUpn}", 
                userId, userUpn);
        }

        public void InvalidateUserListCache()
        {
            Remove(M365UsersAccountEnabledCacheKeyPrefix + "True");
            Remove(M365UsersAccountEnabledCacheKeyPrefix + "False");
            Remove(AllTeamsCacheKey);
            
            _logger.LogDebug("Unieważniono cache list użytkowników.");
        }

        public void InvalidateTeamCache(string teamId)
        {
            if (string.IsNullOrWhiteSpace(teamId))
                return;

            Remove(TeamDetailsCacheKeyPrefix + teamId);
            Remove(TeamChannelsCacheKeyPrefix + teamId);
            
            _logger.LogDebug("Unieważniono cache zespołu: {TeamId}", teamId);
        }

        public void InvalidateAllCache()
        {
            // Zresetuj CancellationTokenSource
            var oldTokenSource = Interlocked.Exchange(ref _powerShellCacheTokenSource, new CancellationTokenSource());
            if (oldTokenSource != null && !oldTokenSource.IsCancellationRequested)
            {
                oldTokenSource.Cancel();
                oldTokenSource.Dispose();
            }
            
            _logger.LogInformation("Token cache dla PowerShell został zresetowany. Wszystkie wpisy cache zostały unieważnione.");

            // Usuń też kluczowe wpisy bezpośrednio
            Remove(GraphContextCacheKey);
            Remove(AllTeamsCacheKey);
        }

        public MemoryCacheEntryOptions GetDefaultCacheEntryOptions()
        {
            return new MemoryCacheEntryOptions()
                .SetAbsoluteExpiration(_defaultCacheDuration)
                .AddExpirationToken(new CancellationChangeToken(_powerShellCacheTokenSource.Token));
        }

        public MemoryCacheEntryOptions GetShortCacheEntryOptions()
        {
            return new MemoryCacheEntryOptions()
                .SetAbsoluteExpiration(_shortCacheDuration)
                .AddExpirationToken(new CancellationChangeToken(_powerShellCacheTokenSource.Token));
        }

        public void InvalidateChannelsForTeam(string teamId)
        {
            if (string.IsNullOrWhiteSpace(teamId))
            {
                _logger.LogWarning("Próba unieważnienia cache kanałów dla pustego teamId.");
                return;
            }

            Remove(TeamChannelsCacheKeyPrefix + teamId);
            _logger.LogDebug("Unieważniono cache listy kanałów dla zespołu: {TeamId}", teamId);
        }

        public void InvalidateChannel(string channelId)
        {
            if (string.IsNullOrWhiteSpace(channelId))
            {
                _logger.LogWarning("Próba unieważnienia cache dla pustego channelId.");
                return;
            }

            // Usuń wszystkie klucze związane z tym kanałem
            // Nie znamy teamId, więc musimy być ostrożni
            _logger.LogDebug("Unieważniono cache dla kanału: {ChannelId}", channelId);
        }

        public void InvalidateChannelAndTeam(string teamId, string channelId)
        {
            if (string.IsNullOrWhiteSpace(teamId) || string.IsNullOrWhiteSpace(channelId))
            {
                _logger.LogWarning("Próba unieważnienia cache z pustym teamId lub channelId.");
                return;
            }

            // Usuń cache konkretnego kanału
            string channelCacheKey = $"{TeamChannelsCacheKeyPrefix}{teamId}_{channelId}";
            Remove(channelCacheKey);
            
            // Usuń cache listy kanałów dla zespołu
            InvalidateChannelsForTeam(teamId);
            
            _logger.LogDebug("Unieważniono cache kanału {ChannelId} i listy kanałów zespołu {TeamId}", channelId, teamId);
        }

        public void InvalidateDepartment(string departmentId)
        {
            if (string.IsNullOrWhiteSpace(departmentId))
            {
                _logger.LogWarning("Próba unieważnienia cache dla pustego departmentId.");
                return;
            }
            
            Remove(DepartmentByIdCacheKeyPrefix + departmentId);
            _logger.LogDebug("Unieważniono cache dla działu ID: {DepartmentId}", departmentId);
        }

        public void InvalidateSubDepartments(string parentId)
        {
            if (string.IsNullOrWhiteSpace(parentId))
            {
                _logger.LogWarning("Próba unieważnienia cache poddziałów dla pustego parentId.");
                return;
            }
            
            Remove(SubDepartmentsByParentIdCacheKeyPrefix + parentId);
            _logger.LogDebug("Unieważniono cache poddziałów dla rodzica ID: {ParentId}", parentId);
        }

        public void InvalidateUsersInDepartment(string departmentId)
        {
            if (string.IsNullOrWhiteSpace(departmentId))
            {
                _logger.LogWarning("Próba unieważnienia cache użytkowników dla pustego departmentId.");
                return;
            }
            
            Remove(UsersInDepartmentCacheKeyPrefix + departmentId);
            _logger.LogDebug("Unieważniono cache użytkowników w dziale ID: {DepartmentId}", departmentId);
        }

        public void InvalidateAllDepartmentLists()
        {
            Remove(AllDepartmentsAllCacheKey);
            Remove(AllDepartmentsRootOnlyCacheKey);
            _logger.LogDebug("Unieważniono globalne listy działów (wszystkie i root-only).");
        }

        public void InvalidateUsersByRole(UserRole role)
        {
            // Usuń cache listy użytkowników według roli (klucz UserService)
            Remove(UserServiceByRolePrefix + role.ToString());
            
            _logger.LogDebug("Unieważniono cache użytkowników z rolą: {Role}", role);
        }

        public void InvalidateAllActiveUsersList()
        {
            // Usuń cache listy wszystkich aktywnych użytkowników (klucz UserService)
            Remove(UserServiceAllActiveKey);
            
            _logger.LogDebug("Unieważniono cache listy wszystkich aktywnych użytkowników.");
        }

        public void InvalidateUserAndRelatedData(string? userId, string? userUpn, string? oldUpn, UserRole? role, UserRole? oldRole)
        {
            // Unieważnij podstawowe dane użytkownika (istniejąca metoda)
            InvalidateUserCache(userId, userUpn);
            
            // Unieważnij klucze UserService dla userId
            if (!string.IsNullOrWhiteSpace(userId))
            {
                Remove(UserServiceByIdPrefix + userId);
                _logger.LogDebug("Unieważniono cache UserService dla userId: {UserId}", userId);
            }
            
            // Unieważnij klucze UserService dla userUpn
            if (!string.IsNullOrWhiteSpace(userUpn))
            {
                Remove(UserServiceByUpnPrefix + userUpn);
                _logger.LogDebug("Unieważniono cache UserService dla userUpn: {UserUpn}", userUpn);
            }
            
            // Unieważnij stary UPN jeśli został zmieniony
            if (!string.IsNullOrWhiteSpace(oldUpn) && oldUpn != userUpn)
            {
                Remove(UserIdCacheKeyPrefix + oldUpn);
                Remove(UserUpnCacheKeyPrefix + oldUpn);
                Remove(UserServiceByUpnPrefix + oldUpn);
                _logger.LogDebug("Unieważniono cache dla starego UPN: {OldUpn}", oldUpn);
            }
            
            // Unieważnij cache według roli
            if (role.HasValue)
            {
                InvalidateUsersByRole(role.Value);
            }
            
            // Unieważnij cache według starej roli jeśli została zmieniona
            if (oldRole.HasValue && oldRole != role)
            {
                InvalidateUsersByRole(oldRole.Value);
            }
            
            _logger.LogDebug("Wykonano kompleksową inwalidację cache użytkownika. " +
                "UserId: {UserId}, UserUpn: {UserUpn}, OldUpn: {OldUpn}, Role: {Role}, OldRole: {OldRole}", 
                userId, userUpn, oldUpn, role, oldRole);
        }
    }
}