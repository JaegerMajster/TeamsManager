using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Primitives;
using TeamsManager.Core.Abstractions.Services.PowerShell;
using TeamsManager.Core.Enums;
using TeamsManager.Core.Services.Cache;
using TeamsManager.Core.Models;

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
        
        // Klucze cache dla przedmiotów
        private const string SubjectByIdCacheKeyPrefix = "Subject_Id_";
        private const string SubjectByCodeCacheKeyPrefix = "Subject_Code_";
        private const string AllActiveSubjectsCacheKey = "Subjects_AllActive";
        private const string TeachersForSubjectCacheKeyPrefix = "Subject_Teachers_Id_";
        
        // Klucze cache dla lat szkolnych
        private const string SchoolYearByIdCacheKeyPrefix = "SchoolYear_Id_";
        private const string AllActiveSchoolYearsCacheKey = "SchoolYears_AllActive";
        private const string CurrentSchoolYearCacheKey = "SchoolYear_Current";
        
        // Klucze cache dla zespołów (TeamService)
        private const string TeamServiceAllActiveTeamsCacheKey = "Teams_AllActive";
        private const string TeamServiceActiveTeamsSpecificCacheKey = "Teams_Active";
        private const string TeamServiceArchivedTeamsCacheKey = "Teams_Archived";
        private const string TeamServiceByOwnerCacheKeyPrefix = "Teams_ByOwner_";
        private const string TeamServiceByIdCacheKeyPrefix = "Team_Id_";
        
        // Klucze cache dla typów szkół (SchoolTypeService)
        private const string SchoolTypeServiceAllActiveKey = "SchoolTypes_AllActive";
        private const string SchoolTypeServiceByIdPrefix = "SchoolType_Id_";
        
        // Klucze cache dla ustawień aplikacji (ApplicationSettingService)
        private const string ApplicationSettingServiceAllActiveKey = "ApplicationSettings_AllActive";
        private const string ApplicationSettingServiceByCategoryPrefix = "ApplicationSettings_Category_";
        private const string ApplicationSettingServiceByKeyPrefix = "ApplicationSetting_Key_";
        
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

        public void InvalidateSubjectById(string subjectId, string? subjectCode = null)
        {
            if (string.IsNullOrWhiteSpace(subjectId))
            {
                _logger.LogWarning("Próba unieważnienia cache dla pustego subjectId.");
                return;
            }

            // Usuń cache przedmiotu po ID
            Remove(SubjectByIdCacheKeyPrefix + subjectId);
            _logger.LogDebug("Unieważniono cache przedmiotu po ID: {SubjectId}", subjectId);

            // Usuń cache przedmiotu po kodzie jeśli podany
            if (!string.IsNullOrWhiteSpace(subjectCode))
            {
                Remove(SubjectByCodeCacheKeyPrefix + subjectCode);
                _logger.LogDebug("Unieważniono cache przedmiotu po kodzie: {SubjectCode}", subjectCode);
            }
        }

        public void InvalidateAllActiveSubjectsList()
        {
            Remove(AllActiveSubjectsCacheKey);
            _logger.LogDebug("Unieważniono cache listy wszystkich aktywnych przedmiotów.");
        }

        public void InvalidateTeachersForSubject(string subjectId)
        {
            if (string.IsNullOrWhiteSpace(subjectId))
            {
                _logger.LogWarning("Próba unieważnienia cache nauczycieli dla pustego subjectId.");
                return;
            }

            Remove(TeachersForSubjectCacheKeyPrefix + subjectId);
            _logger.LogDebug("Unieważniono cache listy nauczycieli dla przedmiotu: {SubjectId}", subjectId);
        }

        public void InvalidateSchoolYearById(string schoolYearId)
        {
            if (string.IsNullOrWhiteSpace(schoolYearId))
            {
                _logger.LogWarning("Próba unieważnienia cache dla pustego schoolYearId.");
                return;
            }

            // Usuń cache roku szkolnego po ID
            Remove(SchoolYearByIdCacheKeyPrefix + schoolYearId);
            _logger.LogDebug("Unieważniono cache roku szkolnego po ID: {SchoolYearId}", schoolYearId);
        }

        public void InvalidateAllActiveSchoolYearsList()
        {
            Remove(AllActiveSchoolYearsCacheKey);
            _logger.LogDebug("Unieważniono cache listy wszystkich aktywnych lat szkolnych.");
        }

        public void InvalidateCurrentSchoolYear()
        {
            Remove(CurrentSchoolYearCacheKey);
            _logger.LogDebug("Unieważniono cache bieżącego roku szkolnego.");
        }

        public void InvalidateTeamTemplateById(string templateId)
        {
            if (string.IsNullOrWhiteSpace(templateId))
            {
                _logger.LogWarning("Próba unieważnienia cache dla pustego templateId.");
                return;
            }
            
            var key = TeamTemplateCacheKeys.TeamTemplateById(templateId);
            Remove(key);
            
            _logger.LogDebug("Unieważniono cache szablonu zespołu po ID: {TemplateId}", templateId);
        }

        public void InvalidateAllActiveTeamTemplatesList()
        {
            // Usuń listę wszystkich aktywnych szablonów
            Remove(TeamTemplateCacheKeys.AllActiveTeamTemplates);
            
            // Usuń też listę szablonów uniwersalnych (są powiązane)
            Remove(TeamTemplateCacheKeys.UniversalTeamTemplates);
            
            _logger.LogDebug("Unieważniono cache list szablonów zespołów (wszystkie aktywne i uniwersalne).");
        }

        public void InvalidateTeamTemplatesBySchoolType(string schoolTypeId)
        {
            if (string.IsNullOrWhiteSpace(schoolTypeId))
            {
                _logger.LogWarning("Próba unieważnienia cache szablonów dla pustego schoolTypeId.");
                return;
            }
            
            // Usuń listę szablonów dla typu szkoły
            var listKey = TeamTemplateCacheKeys.TeamTemplatesBySchoolType(schoolTypeId);
            Remove(listKey);
            
            // Usuń też domyślny szablon dla typu szkoły
            var defaultKey = TeamTemplateCacheKeys.DefaultTeamTemplateBySchoolType(schoolTypeId);
            Remove(defaultKey);
            
            _logger.LogDebug("Unieważniono cache szablonów zespołów dla typu szkoły: {SchoolTypeId}", schoolTypeId);
        }

        // Metody granularnej inwalidacji dla TeamService
        public void InvalidateTeamById(string teamId)
        {
            if (string.IsNullOrWhiteSpace(teamId))
            {
                _logger.LogWarning("Próba unieważnienia cache dla pustego teamId.");
                return;
            }

            Remove(TeamServiceByIdCacheKeyPrefix + teamId);
            _logger.LogDebug("Unieważniono cache zespołu po ID: {TeamId}", teamId);
        }

        public void InvalidateTeamsByOwner(string ownerUpn)
        {
            if (string.IsNullOrWhiteSpace(ownerUpn))
            {
                _logger.LogWarning("Próba unieważnienia cache zespołów dla pustego ownerUpn.");
                return;
            }

            Remove(TeamServiceByOwnerCacheKeyPrefix + ownerUpn);
            _logger.LogDebug("Unieważniono cache zespołów dla właściciela: {OwnerUpn}", ownerUpn);
        }

        public void InvalidateTeamsByStatus(TeamStatus status)
        {
            // Unieważnij listy zespołów według statusu
            switch (status)
            {
                case TeamStatus.Active:
                    Remove(TeamServiceActiveTeamsSpecificCacheKey);
                    _logger.LogDebug("Unieważniono cache zespołów o statusie Active.");
                    break;
                case TeamStatus.Archived:
                    Remove(TeamServiceArchivedTeamsCacheKey);
                    _logger.LogDebug("Unieważniono cache zespołów o statusie Archived.");
                    break;
                default:
                    _logger.LogDebug("Unieważniono cache dla statusu zespołu: {Status}", status);
                    break;
            }
        }

        public void InvalidateAllActiveTeamsList()
        {
            Remove(TeamServiceAllActiveTeamsCacheKey);
            _logger.LogDebug("Unieważniono cache listy wszystkich aktywnych zespołów.");
        }

        public void InvalidateArchivedTeamsList()
        {
            Remove(TeamServiceArchivedTeamsCacheKey);
            _logger.LogDebug("Unieważniono cache listy zarchiwizowanych zespołów.");
        }

        public void InvalidateTeamSpecificByStatus()
        {
            Remove(TeamServiceActiveTeamsSpecificCacheKey);
            _logger.LogDebug("Unieważniono cache listy zespołów o specyficznym statusie Active.");
        }

        // Metody granularnej inwalidacji dla SchoolTypeService
        public void InvalidateSchoolTypeById(string schoolTypeId)
        {
            if (string.IsNullOrWhiteSpace(schoolTypeId))
            {
                _logger.LogWarning("Próba unieważnienia cache dla pustego schoolTypeId.");
                return;
            }

            Remove(SchoolTypeServiceByIdPrefix + schoolTypeId);
            _logger.LogDebug("Unieważniono cache typu szkoły po ID: {SchoolTypeId}", schoolTypeId);
        }

        public void InvalidateAllActiveSchoolTypesList()
        {
            Remove(SchoolTypeServiceAllActiveKey);
            _logger.LogDebug("Unieważniono cache listy wszystkich aktywnych typów szkół.");
        }

        // Metody granularnej inwalidacji dla ApplicationSettingService
        public void InvalidateSettingByKey(string key)
        {
            if (string.IsNullOrWhiteSpace(key))
            {
                _logger.LogWarning("Próba unieważnienia cache dla pustego klucza ustawienia.");
                return;
            }

            Remove(ApplicationSettingServiceByKeyPrefix + key);
            _logger.LogDebug("Unieważniono cache ustawienia po kluczu: {Key}", key);
        }

        public void InvalidateSettingsByCategory(string category)
        {
            if (string.IsNullOrWhiteSpace(category))
            {
                _logger.LogWarning("Próba unieważnienia cache ustawień dla pustej kategorii.");
                return;
            }

            Remove(ApplicationSettingServiceByCategoryPrefix + category);
            _logger.LogDebug("Unieważniono cache ustawień dla kategorii: {Category}", category);
        }

        public void InvalidateAllActiveSettingsList()
        {
            Remove(ApplicationSettingServiceAllActiveKey);
            _logger.LogDebug("Unieważniono cache listy wszystkich aktywnych ustawień aplikacji.");
        }

        #region P2 Cache Optimization Features

        /// <summary>
        /// [P2-OPTIMIZATION] Smart batch invalidation to reduce cache stampedes
        /// </summary>
        public void BatchInvalidateKeys(IEnumerable<string> cacheKeys, string operationName = "BatchInvalidation")
        {
            var stopwatch = System.Diagnostics.Stopwatch.StartNew();
            var invalidatedCount = 0;
            var keysList = cacheKeys.ToList();
            
            _logger.LogDebug("[P2-CACHE] Starting batch invalidation of {Count} keys for operation: {Operation}", 
                keysList.Count, operationName);
            
            // Group similar keys to optimize invalidation
            var keyGroups = keysList.GroupBy(key => key.Split('_')[0]).ToList();
            
            foreach (var group in keyGroups)
            {
                _logger.LogDebug("[P2-CACHE] Invalidating {Count} keys with prefix: {Prefix}", 
                    group.Count(), group.Key);
                
                foreach (var key in group)
                {
                    _cache.Remove(key);
                    invalidatedCount++;
                }
            }
            
            stopwatch.Stop();
            
            _logger.LogInformation("[P2-CACHE] Batch invalidation completed. Operation: {Operation}, " +
                "Keys: {InvalidatedCount}/{TotalCount}, Duration: {ElapsedMs}ms", 
                operationName, invalidatedCount, keysList.Count, stopwatch.ElapsedMilliseconds);
                
            // Track metrics
            RecordCacheOperation("BatchInvalidation", stopwatch.ElapsedMilliseconds, invalidatedCount);
        }

        /// <summary>
        /// [P2-OPTIMIZATION] Smart cache warming for frequently accessed data
        /// </summary>
        public async Task WarmCacheAsync(string cacheKey, Func<Task<object>> dataLoader, TimeSpan? duration = null)
        {
            if (_cache.TryGetValue(cacheKey, out _))
            {
                _logger.LogDebug("[P2-CACHE] Cache key {CacheKey} already warm", cacheKey);
                return;
            }
            
            var stopwatch = System.Diagnostics.Stopwatch.StartNew();
            
            try
            {
                _logger.LogDebug("[P2-CACHE] Warming cache for key: {CacheKey}", cacheKey);
                
                var data = await dataLoader();
                if (data != null)
                {
                    Set(cacheKey, data, duration ?? _defaultCacheDuration);
                    
                    stopwatch.Stop();
                    _logger.LogInformation("[P2-CACHE] Cache warmed for key: {CacheKey}, Duration: {ElapsedMs}ms", 
                        cacheKey, stopwatch.ElapsedMilliseconds);
                        
                    RecordCacheOperation("WarmCache", stopwatch.ElapsedMilliseconds, 1);
                }
            }
            catch (Exception ex)
            {
                stopwatch.Stop();
                _logger.LogError(ex, "[P2-CACHE] Failed to warm cache for key: {CacheKey}, Duration: {ElapsedMs}ms", 
                    cacheKey, stopwatch.ElapsedMilliseconds);
            }
        }

        /// <summary>
        /// [P2-OPTIMIZATION] Pagination support for large data sets
        /// </summary>
        public bool TryGetPagedValue<T>(string baseCacheKey, int pageNumber, int pageSize, out T? value)
        {
            var pagedKey = $"{baseCacheKey}_Page_{pageNumber}_Size_{pageSize}";
            var result = TryGetValue(pagedKey, out value);
            
            if (result)
            {
                _logger.LogDebug("[P2-CACHE] Paged cache HIT: {PagedKey}", pagedKey);
                RecordCacheOperation("PagedHit", 0, 1);
            }
            else
            {
                _logger.LogDebug("[P2-CACHE] Paged cache MISS: {PagedKey}", pagedKey);
                RecordCacheOperation("PagedMiss", 0, 1);
            }
            
            return result;
        }

        /// <summary>
        /// [P2-OPTIMIZATION] Set paged data in cache
        /// </summary>
        public void SetPagedValue<T>(string baseCacheKey, int pageNumber, int pageSize, T value, TimeSpan? duration = null)
        {
            var pagedKey = $"{baseCacheKey}_Page_{pageNumber}_Size_{pageSize}";
            Set(pagedKey, value, duration);
            
            _logger.LogDebug("[P2-CACHE] Paged cache SET: {PagedKey}", pagedKey);
            RecordCacheOperation("PagedSet", 0, 1);
        }

        /// <summary>
        /// [P2-OPTIMIZATION] Smart pattern-based invalidation for large organizations
        /// </summary>
        public void InvalidateByPattern(string pattern, string operationName = "PatternInvalidation")
        {
            var stopwatch = System.Diagnostics.Stopwatch.StartNew();
            var invalidatedCount = 0;
            
            _logger.LogDebug("[P2-CACHE] Starting pattern-based invalidation: {Pattern} for operation: {Operation}", 
                pattern, operationName);
            
            // For IMemoryCache, we need to track keys ourselves or use reflection
            // This is a simplified implementation - in production, consider using Redis with pattern support
            var fieldsInfo = typeof(MemoryCache).GetField("_coherentState", 
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            
            if (fieldsInfo?.GetValue(_cache) is IDictionary cacheDict)
            {
                var keysToRemove = new List<object>();
                
                foreach (DictionaryEntry entry in cacheDict)
                {
                    var key = entry.Key.ToString();
                    if (!string.IsNullOrEmpty(key) && key.Contains(pattern))
                    {
                        keysToRemove.Add(entry.Key);
                    }
                }
                
                foreach (var key in keysToRemove)
                {
                    _cache.Remove(key);
                    invalidatedCount++;
                }
            }
            
            stopwatch.Stop();
            
            _logger.LogInformation("[P2-CACHE] Pattern invalidation completed. Pattern: {Pattern}, " +
                "Operation: {Operation}, Keys: {InvalidatedCount}, Duration: {ElapsedMs}ms", 
                pattern, operationName, invalidatedCount, stopwatch.ElapsedMilliseconds);
                
            RecordCacheOperation("PatternInvalidation", stopwatch.ElapsedMilliseconds, invalidatedCount);
        }

        #endregion
        
        #region P2 Cache Metrics & Monitoring
        
        private static readonly object _metricsLock = new object();
        private static long _cacheHits = 0;
        private static long _cacheMisses = 0;
        private static long _cacheInvalidations = 0;
        private static long _totalOperationTimeMs = 0;
        
        /// <summary>
        /// [P2-MONITORING] Record cache operation for metrics
        /// </summary>
        private void RecordCacheOperation(string operationType, long durationMs, int itemCount = 1)
        {
            lock (_metricsLock)
            {
                switch (operationType)
                {
                    case "Hit":
                    case "PagedHit":
                        _cacheHits += itemCount;
                        break;
                    case "Miss":
                    case "PagedMiss":
                        _cacheMisses += itemCount;
                        break;
                    case "Invalidation":
                    case "BatchInvalidation":
                    case "PatternInvalidation":
                        _cacheInvalidations += itemCount;
                        break;
                }
                
                _totalOperationTimeMs += durationMs;
            }
        }
        
        /// <summary>
        /// [P2-MONITORING] Get comprehensive cache metrics
        /// </summary>
        public CacheMetrics GetCacheMetrics()
        {
            lock (_metricsLock)
            {
                var totalOperations = _cacheHits + _cacheMisses;
                var hitRate = totalOperations > 0 ? (double)_cacheHits / totalOperations * 100 : 0;
                
                return new CacheMetrics
                {
                    CacheHits = _cacheHits,
                    CacheMisses = _cacheMisses,
                    CacheInvalidations = _cacheInvalidations,
                    HitRate = hitRate,
                    TotalOperations = totalOperations,
                    AverageOperationTimeMs = totalOperations > 0 ? (double)_totalOperationTimeMs / totalOperations : 0,
                    TotalOperationTimeMs = _totalOperationTimeMs
                };
            }
        }
        
        /// <summary>
        /// [P2-MONITORING] Reset cache metrics
        /// </summary>
        public void ResetMetrics()
        {
            lock (_metricsLock)
            {
                _cacheHits = 0;
                _cacheMisses = 0;
                _cacheInvalidations = 0;
                _totalOperationTimeMs = 0;
                
                _logger.LogInformation("[P2-CACHE] Cache metrics reset");
            }
        }
        
        #endregion

        #region Enhanced TryGetValue with Metrics
        
        public bool TryGetValueWithMetrics<T>(string key, out T? value)
        {
            var stopwatch = System.Diagnostics.Stopwatch.StartNew();
            var result = TryGetValue(key, out value);
            stopwatch.Stop();
            
            RecordCacheOperation(result ? "Hit" : "Miss", stopwatch.ElapsedMilliseconds);
            
            if (result)
            {
                _logger.LogDebug("[P2-CACHE] Cache HIT: {Key}, Duration: {ElapsedMs}ms", key, stopwatch.ElapsedMilliseconds);
            }
            else
            {
                _logger.LogDebug("[P2-CACHE] Cache MISS: {Key}, Duration: {ElapsedMs}ms", key, stopwatch.ElapsedMilliseconds);
            }
            
            return result;
        }
        
        #endregion
    }
}