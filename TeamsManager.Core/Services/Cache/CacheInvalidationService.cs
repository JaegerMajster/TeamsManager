using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using TeamsManager.Core.Abstractions.Services.Cache;
using TeamsManager.Core.Abstractions.Services.PowerShell;
using TeamsManager.Core.Enums;
using TeamsManager.Core.Models;

namespace TeamsManager.Core.Services.Cache
{
    /// <summary>
    /// Centralny serwis do systematycznej inwalidacji cache z obsługą operacji kaskadowych
    /// ETAP 7/8: Systematyczna Inwalidacja Cache
    /// </summary>
    public class CacheInvalidationService : ICacheInvalidationService
    {
        private readonly IPowerShellCacheService _cacheService;
        private readonly ILogger<CacheInvalidationService> _logger;
        private readonly CascadeInvalidationStrategy _cascadeStrategy;
        
        public CacheInvalidationService(
            IPowerShellCacheService cacheService,
            ILogger<CacheInvalidationService> logger)
        {
            _cacheService = cacheService ?? throw new ArgumentNullException(nameof(cacheService));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _cascadeStrategy = new CascadeInvalidationStrategy();
        }
        
        // TEAM OPERATIONS
        public async Task InvalidateForTeamCreatedAsync(Team team)
        {
            var keys = new List<string>
            {
                "Teams_AllActive",
                "Teams_Active",
                $"Teams_ByOwner_{team.Owner}",
                $"Team_Id_{team.Id}",
                "PowerShell_Teams_All"
            };
            
            if (!string.IsNullOrEmpty(team.SchoolYearId))
                keys.Add($"Teams_BySchoolYear_{team.SchoolYearId}");
            
            if (!string.IsNullOrEmpty(team.SchoolTypeId))
                keys.Add($"Teams_BySchoolType_{team.SchoolTypeId}");
                
            // Dodaj klucze kaskadowe
            var cascadeKeys = _cascadeStrategy.GetCascadeKeys("Team.Created", team);
            keys.AddRange(cascadeKeys);
            
            await PerformBatchInvalidationAsync(keys, $"TeamCreated_{team.Id}");
        }
        
        public async Task InvalidateForTeamUpdatedAsync(Team team, Team? oldTeam = null)
        {
            var keys = new List<string>
            {
                $"Team_Id_{team.Id}",
                "Teams_AllActive",
                $"PowerShell_Team_{team.ExternalId}"
            };
            
            // Status change handling
            if (oldTeam != null && oldTeam.Status != team.Status)
            {
                if (oldTeam.Status == TeamStatus.Active)
                {
                    keys.Add("Teams_Active");
                    keys.Add("Teams_Archived");
                }
                else if (team.Status == TeamStatus.Active)
                {
                    keys.Add("Teams_Archived");
                    keys.Add("Teams_Active");
                }
            }
            
            // Owner change handling
            if (oldTeam != null && oldTeam.Owner != team.Owner)
            {
                keys.Add($"Teams_ByOwner_{oldTeam.Owner}");
                keys.Add($"Teams_ByOwner_{team.Owner}");
            }
            
            // School type/year changes
            if (oldTeam != null)
            {
                if (oldTeam.SchoolYearId != team.SchoolYearId)
                {
                    if (!string.IsNullOrEmpty(oldTeam.SchoolYearId))
                        keys.Add($"Teams_BySchoolYear_{oldTeam.SchoolYearId}");
                    if (!string.IsNullOrEmpty(team.SchoolYearId))
                        keys.Add($"Teams_BySchoolYear_{team.SchoolYearId}");
                }
                
                if (oldTeam.SchoolTypeId != team.SchoolTypeId)
                {
                    if (!string.IsNullOrEmpty(oldTeam.SchoolTypeId))
                        keys.Add($"Teams_BySchoolType_{oldTeam.SchoolTypeId}");
                    if (!string.IsNullOrEmpty(team.SchoolTypeId))
                        keys.Add($"Teams_BySchoolType_{team.SchoolTypeId}");
                }
            }
            
            await PerformBatchInvalidationAsync(keys, $"TeamUpdated_{team.Id}");
        }
        
        public async Task InvalidateForTeamArchivedAsync(Team team)
        {
            var keys = new List<string>
            {
                $"Team_Id_{team.Id}",
                "Teams_AllActive",
                "Teams_Active",
                "Teams_Archived",
                $"Teams_ByOwner_{team.Owner}",
                $"PowerShell_Team_{team.ExternalId}",
                $"PowerShell_TeamChannels_{team.Id}",
                $"Channels_TeamId_{team.Id}" // Z Etapu 6/8
            };
            
            // Kaskadowa inwalidacja dla członków zespołu
            if (team.Members?.Any() == true)
            {
                foreach (var member in team.Members.Where(m => m.IsActive))
                {
                    keys.Add($"User_Teams_{member.UserId}");
                }
            }
            
            // Dodaj klucze kaskadowe
            var cascadeKeys = _cascadeStrategy.GetCascadeKeys("Team.Archived", team);
            keys.AddRange(cascadeKeys);
            
            await PerformBatchInvalidationAsync(keys, $"TeamArchived_{team.Id}");
        }
        
        public async Task InvalidateForTeamRestoredAsync(Team team)
        {
            var keys = new List<string>
            {
                $"Team_Id_{team.Id}",
                "Teams_AllActive",
                "Teams_Active",
                "Teams_Archived",
                $"Teams_ByOwner_{team.Owner}",
                $"PowerShell_Team_{team.ExternalId}"
            };
            
            await PerformBatchInvalidationAsync(keys, $"TeamRestored_{team.Id}");
        }
        
        public async Task InvalidateForTeamDeletedAsync(Team team)
        {
            var keys = new List<string>
            {
                $"Team_Id_{team.Id}",
                "Teams_AllActive",
                "Teams_Active",
                "Teams_Archived",
                $"Teams_ByOwner_{team.Owner}",
                $"PowerShell_Team_{team.ExternalId}",
                $"PowerShell_TeamChannels_{team.Id}",
                $"Channels_TeamId_{team.Id}"
            };
            
            // Pattern-based invalidation dla wszystkich powiązanych danych zespołu
            await InvalidateByPatternAsync($"*Team*{team.Id}*", $"TeamDeleted_{team.Id}");
            await PerformBatchInvalidationAsync(keys, $"TeamDeleted_{team.Id}");
        }
        
        public async Task InvalidateForTeamMemberAddedAsync(string teamId, string userId)
        {
            var keys = new List<string>
            {
                $"Team_Members_{teamId}",
                $"User_Teams_{userId}",
                $"Team_Id_{teamId}" // Może zawierać członków
            };
            
            await PerformBatchInvalidationAsync(keys, $"TeamMemberAdded_{teamId}_{userId}");
        }
        
        public async Task InvalidateForTeamMemberRemovedAsync(string teamId, string userId)
        {
            var keys = new List<string>
            {
                $"Team_Members_{teamId}",
                $"User_Teams_{userId}",
                $"Team_Id_{teamId}"
            };
            
            await PerformBatchInvalidationAsync(keys, $"TeamMemberRemoved_{teamId}_{userId}");
        }
        
        public async Task InvalidateForTeamMembersBulkOperationAsync(string teamId, List<string> userIds)
        {
            var keys = new List<string>
            {
                $"Team_Members_{teamId}",
                $"Team_Id_{teamId}"
            };
            
            // Dodaj klucze dla każdego użytkownika
            keys.AddRange(userIds.Select(userId => $"User_Teams_{userId}"));
            
            await PerformBatchInvalidationAsync(keys, $"TeamMembersBulk_{teamId}_{userIds.Count}");
        }
        
        // USER OPERATIONS
        public async Task InvalidateForUserCreatedAsync(User user)
        {
            var keys = new List<string>
            {
                "Users_AllActive",
                $"Users_Role_{user.Role}",
                $"User_Id_{user.Id}",
                $"User_Upn_{user.UPN}",
                $"PowerShell_UserId_{user.UPN}",
                "PowerShell_M365Users_AccountEnabled_True"
            };
            
            if (!string.IsNullOrEmpty(user.DepartmentId))
                keys.Add($"Department_UsersIn_Id_{user.DepartmentId}");
            
            await PerformBatchInvalidationAsync(keys, $"UserCreated_{user.Id}");
        }
        
        public async Task InvalidateForUserUpdatedAsync(User user, User? oldUser = null)
        {
            var keys = new List<string>
            {
                $"User_Id_{user.Id}",
                $"User_Upn_{user.UPN}",
                "Users_AllActive",
                $"Users_Role_{user.Role}",
                $"PowerShell_UserId_{user.UPN}",
                $"PowerShell_M365User_Id_{user.ExternalId}"
            };
            
            // Role change handling
            if (oldUser != null && oldUser.Role != user.Role)
            {
                keys.Add($"Users_Role_{oldUser.Role}");
            }
            
            // Department change handling
            if (oldUser != null && oldUser.DepartmentId != user.DepartmentId)
            {
                if (!string.IsNullOrEmpty(oldUser.DepartmentId))
                    keys.Add($"Department_UsersIn_Id_{oldUser.DepartmentId}");
                if (!string.IsNullOrEmpty(user.DepartmentId))
                    keys.Add($"Department_UsersIn_Id_{user.DepartmentId}");
            }
            
            await PerformBatchInvalidationAsync(keys, $"UserUpdated_{user.Id}");
        }
        
        public async Task InvalidateForUserActivatedAsync(User user)
        {
            var keys = new List<string>
            {
                $"User_Id_{user.Id}",
                $"User_Upn_{user.UPN}",
                "Users_AllActive",
                $"Users_Role_{user.Role}",
                $"PowerShell_UserId_{user.UPN}",
                $"PowerShell_M365User_Id_{user.ExternalId}",
                "PowerShell_M365Users_AccountEnabled_True",
                "PowerShell_M365Users_AccountEnabled_False"
            };
            
            await PerformBatchInvalidationAsync(keys, $"UserActivated_{user.Id}");
        }
        
        public async Task InvalidateForUserDeactivatedAsync(User user)
        {
            var keys = new List<string>
            {
                $"User_Id_{user.Id}",
                $"User_Upn_{user.UPN}",
                "Users_AllActive",
                $"Users_Role_{user.Role}",
                $"PowerShell_UserId_{user.UPN}",
                $"PowerShell_UserUpn_{user.UPN}",
                $"PowerShell_M365User_Id_{user.ExternalId}",
                "PowerShell_M365Users_AccountEnabled_True",
                "PowerShell_M365Users_AccountEnabled_False"
            };
            
            // Kaskadowa inwalidacja dla departamentu
            if (!string.IsNullOrEmpty(user.DepartmentId))
            {
                keys.Add($"Department_UsersIn_Id_{user.DepartmentId}");
            }
            
            // Kaskadowa inwalidacja dla przedmiotów (dla nauczycieli)
            if (user.TaughtSubjects?.Any() == true)
            {
                foreach (var subject in user.TaughtSubjects.Where(s => s.IsActive))
                {
                    keys.Add($"Subject_Teachers_Id_{subject.SubjectId}");
                }
            }
            
            // Kaskadowa inwalidacja dla zespołów gdzie użytkownik jest członkiem
            if (user.TeamMemberships?.Any() == true)
            {
                foreach (var membership in user.TeamMemberships.Where(m => m.IsActive))
                {
                    keys.Add($"Team_Members_{membership.TeamId}");
                }
            }
            
            // Dodaj klucze kaskadowe
            var cascadeKeys = _cascadeStrategy.GetCascadeKeys("User.Deactivated", user);
            keys.AddRange(cascadeKeys);
            
            await PerformBatchInvalidationAsync(keys, $"UserDeactivated_{user.Id}_WithCascade");
        }
        
        public async Task InvalidateForUserSchoolTypeChangedAsync(string userId, string? oldSchoolTypeId, string? newSchoolTypeId)
        {
            var keys = new List<string>
            {
                $"User_Id_{userId}"
            };
            
            if (!string.IsNullOrEmpty(oldSchoolTypeId))
                keys.Add($"SchoolType_Users_{oldSchoolTypeId}");
            
            if (!string.IsNullOrEmpty(newSchoolTypeId))
                keys.Add($"SchoolType_Users_{newSchoolTypeId}");
            
            await PerformBatchInvalidationAsync(keys, $"UserSchoolTypeChanged_{userId}");
        }
        
        public async Task InvalidateForUserSubjectChangedAsync(string userId, string subjectId, bool added)
        {
            var keys = new List<string>
            {
                $"User_Id_{userId}",
                $"Subject_Teachers_Id_{subjectId}",
                $"Users_Role_{UserRole.Nauczyciel}" // Zwykle tylko nauczyciele mają przedmioty
            };
            
            var operation = added ? "Added" : "Removed";
            await PerformBatchInvalidationAsync(keys, $"UserSubject{operation}_{userId}_{subjectId}");
        }
        
        // CHANNEL OPERATIONS
        public async Task InvalidateForChannelCreatedAsync(Channel channel)
        {
            var keys = new List<string>
            {
                $"Channels_TeamId_{channel.TeamId}",
                $"Channel_Id_{channel.Id}",
                $"PowerShell_TeamChannels_{channel.TeamId}"
            };
            
            if (!string.IsNullOrEmpty(channel.Id))
                keys.Add($"Channel_GraphId_{channel.Id}");
            
            await PerformBatchInvalidationAsync(keys, $"ChannelCreated_{channel.Id}");
        }
        
        public async Task InvalidateForChannelUpdatedAsync(Channel channel)
        {
            var keys = new List<string>
            {
                $"Channel_Id_{channel.Id}",
                $"Channels_TeamId_{channel.TeamId}",
                $"PowerShell_TeamChannels_{channel.TeamId}"
            };
            
            if (!string.IsNullOrEmpty(channel.Id))
                keys.Add($"Channel_GraphId_{channel.Id}");
            
            await PerformBatchInvalidationAsync(keys, $"ChannelUpdated_{channel.Id}");
        }
        
        public async Task InvalidateForChannelDeletedAsync(Channel channel)
        {
            var keys = new List<string>
            {
                $"Channel_Id_{channel.Id}",
                $"Channels_TeamId_{channel.TeamId}",
                $"PowerShell_TeamChannels_{channel.TeamId}"
            };
            
            if (!string.IsNullOrEmpty(channel.Id))
                keys.Add($"Channel_GraphId_{channel.Id}");
            
            await PerformBatchInvalidationAsync(keys, $"ChannelDeleted_{channel.Id}");
        }
        
        // DEPARTMENT OPERATIONS
        public async Task InvalidateForDepartmentChangedAsync(Department department, Department? oldDepartment = null)
        {
            var keys = new List<string>
            {
                $"Department_Id_{department.Id}",
                "Departments_All",
                "Departments_Active",
                $"Department_Sub_ParentId_{department.ParentDepartmentId}",
                $"Department_UsersIn_Id_{department.Id}"
            };
            
            // Parent department change
            if (oldDepartment != null && oldDepartment.ParentDepartmentId != department.ParentDepartmentId)
            {
                if (!string.IsNullOrEmpty(oldDepartment.ParentDepartmentId))
                    keys.Add($"Department_Sub_ParentId_{oldDepartment.ParentDepartmentId}");
            }
            
            await PerformBatchInvalidationAsync(keys, $"DepartmentChanged_{department.Id}");
        }
        
        // SUBJECT OPERATIONS
        public async Task InvalidateForSubjectChangedAsync(Subject subject, Subject? oldSubject = null)
        {
            var keys = new List<string>
            {
                $"Subject_Id_{subject.Id}",
                "Subjects_All",
                "Subjects_Active",
                $"Subject_Teachers_Id_{subject.Id}"
            };
            
            await PerformBatchInvalidationAsync(keys, $"SubjectChanged_{subject.Id}");
        }
        
        // BATCH OPERATIONS
        public async Task InvalidateBatchAsync(Dictionary<string, List<string>> operationsMap)
        {
            var allKeys = new List<string>();
            
            foreach (var operation in operationsMap)
            {
                allKeys.AddRange(operation.Value);
            }
            
            await PerformBatchInvalidationAsync(
                allKeys, 
                $"BatchOperation_{string.Join("_", operationsMap.Keys)}");
        }
        
        // HELPER METHODS
        private async Task PerformBatchInvalidationAsync(List<string> keys, string operationName)
        {
            var stopwatch = System.Diagnostics.Stopwatch.StartNew();
            
            try
            {
                _logger.LogInformation(
                    "[CACHE-INVALIDATION] Starting batch invalidation for operation: {Operation} with {Count} keys",
                    operationName, keys.Count);
                
                // Remove duplicates
                var uniqueKeys = keys.Distinct().ToList();
                
                // Use P2 batch invalidation feature from Etap 6/8
                _cacheService.BatchInvalidateKeys(uniqueKeys, operationName);
                
                stopwatch.Stop();
                
                _logger.LogInformation(
                    "[CACHE-INVALIDATION] Completed batch invalidation for operation: {Operation}. " +
                    "Keys: {Count}, Duration: {ElapsedMs}ms",
                    operationName, uniqueKeys.Count, stopwatch.ElapsedMilliseconds);
                
                // Log detailed keys in debug mode
                if (_logger.IsEnabled(LogLevel.Debug))
                {
                    _logger.LogDebug(
                        "[CACHE-INVALIDATION] Invalidated keys for {Operation}: {Keys}",
                        operationName, string.Join(", ", uniqueKeys));
                }
            }
            catch (Exception ex)
            {
                stopwatch.Stop();
                _logger.LogError(ex,
                    "[CACHE-INVALIDATION] Failed batch invalidation for operation: {Operation}. " +
                    "Duration: {ElapsedMs}ms",
                    operationName, stopwatch.ElapsedMilliseconds);
                throw;
            }
        }
        
        private async Task InvalidateByPatternAsync(string pattern, string operationName)
        {
            _logger.LogInformation(
                "[CACHE-INVALIDATION] Starting pattern-based invalidation: {Pattern} for operation: {Operation}",
                pattern, operationName);
            
            _cacheService.InvalidateByPattern(pattern, operationName);
        }
    }
    
    /// <summary>
    /// Strategia obsługi operacji kaskadowych w inwalidacji cache
    /// </summary>
    public class CascadeInvalidationStrategy
    {
        private readonly Dictionary<string, List<CascadeRule>> _cascadeRules;
        
        public CascadeInvalidationStrategy()
        {
            _cascadeRules = new Dictionary<string, List<CascadeRule>>
            {
                ["User.Deactivated"] = new List<CascadeRule>
                {
                    new CascadeRule("Department", user => 
                        !string.IsNullOrEmpty(((User)user).DepartmentId) ? 
                        new[] { $"Department_UsersIn_Id_{((User)user).DepartmentId}" } : null),
                    new CascadeRule("Subjects", user => 
                        ((User)user).TaughtSubjects?.Where(s => s.IsActive)
                        .Select(s => $"Subject_Teachers_Id_{s.SubjectId}").ToArray()),
                    new CascadeRule("Teams", user => 
                        ((User)user).TeamMemberships?.Where(tm => tm.IsActive)
                        .Select(tm => $"Team_Members_{tm.TeamId}").ToArray())
                },
                
                ["Team.Archived"] = new List<CascadeRule>
                {
                    new CascadeRule("Channels", team => 
                        new[] { $"Channels_TeamId_{((Team)team).Id}" }),
                    new CascadeRule("Members", team => 
                        ((Team)team).Members?.Where(m => m.IsActive)
                        .Select(m => $"User_Teams_{m.UserId}").ToArray()),
                    new CascadeRule("Operations", team => 
                        new[] { $"Operations_Team_{((Team)team).Id}" })
                },
                
                ["Department.Deleted"] = new List<CascadeRule>
                {
                    new CascadeRule("SubDepartments", dept => 
                        new[] { $"Department_Sub_ParentId_{((Department)dept).Id}" }),
                    new CascadeRule("Users", dept => 
                        new[] { $"Users_InDepartment_{((Department)dept).Id}" }),
                    new CascadeRule("ParentDepartment", dept => 
                        !string.IsNullOrEmpty(((Department)dept).ParentDepartmentId) ? 
                        new[] { $"Department_Sub_ParentId_{((Department)dept).ParentDepartmentId}" } : null)
                }
            };
        }
        
        public List<string> GetCascadeKeys(string operation, object entity)
        {
            var keys = new List<string>();
            
            if (_cascadeRules.TryGetValue(operation, out var rules))
            {
                foreach (var rule in rules)
                {
                    var cascadeKeys = rule.GetKeys(entity);
                    if (cascadeKeys != null)
                    {
                        keys.AddRange(cascadeKeys);
                    }
                }
            }
            
            return keys;
        }
    }
    
    /// <summary>
    /// Reguła kaskadowej inwalidacji cache
    /// </summary>
    public class CascadeRule
    {
        public string TargetEntity { get; }
        public Func<object, string[]?> GetKeys { get; }
        
        public CascadeRule(string targetEntity, Func<object, string[]?> getKeys)
        {
            TargetEntity = targetEntity;
            GetKeys = getKeys;
        }
    }
}