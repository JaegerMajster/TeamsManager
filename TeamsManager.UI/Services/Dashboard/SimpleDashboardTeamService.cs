using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using TeamsManager.Core.Abstractions.Services;
using TeamsManager.Core.Models;
using TeamsManager.Core.Enums;

namespace TeamsManager.UI.Services.Dashboard
{
    /// <summary>
    /// Uproszczona implementacja ITeamService tylko dla potrzeb Dashboard
    /// </summary>
    public class SimpleDashboardTeamService : ITeamService
    {
        private readonly List<Team> _mockTeams;

        public SimpleDashboardTeamService()
        {
            _mockTeams = new List<Team>
            {
                new Team 
                { 
                    Id = "1", 
                    DisplayName = "Team Alpha", 
                    Description = "Zespół projektowy Alpha",
                    Status = TeamStatus.Active
                },
                new Team 
                { 
                    Id = "2", 
                    DisplayName = "Team Beta", 
                    Description = "Zespół projektowy Beta",
                    Status = TeamStatus.Active
                },
                new Team 
                { 
                    Id = "3", 
                    DisplayName = "Team Gamma", 
                    Description = "Zespół projektowy Gamma",
                    Status = TeamStatus.Archived
                },
                new Team 
                { 
                    Id = "4", 
                    DisplayName = "Team Delta", 
                    Description = "Zespół projektowy Delta",
                    Status = TeamStatus.Active
                }
            };
        }

        public Task<IEnumerable<Team>> GetActiveTeamsAsync(bool forceRefresh = false, string? accessToken = null)
        {
            var activeTeams = _mockTeams.Where(t => t.Status == TeamStatus.Active);
            return Task.FromResult<IEnumerable<Team>>(activeTeams);
        }

        // Pozostałe metody - implementacje zaślepkowe
        public Task<Team?> CreateTeamAsync(string displayName, string description, string ownerUpn, TeamVisibility visibility, string accessToken, string? teamTemplateId = null, string? schoolTypeId = null, string? schoolYearId = null, Dictionary<string, string>? additionalTemplateValues = null)
        {
            return Task.FromResult<Team?>(null);
        }

        public Task<IEnumerable<Team>> GetAllTeamsAsync(bool forceRefresh = false, string? accessToken = null)
        {
            return Task.FromResult<IEnumerable<Team>>(_mockTeams);
        }

        public Task<IEnumerable<Team>> GetArchivedTeamsAsync(bool forceRefresh = false, string? accessToken = null)
        {
            var archivedTeams = _mockTeams.Where(t => t.Status == TeamStatus.Archived);
            return Task.FromResult<IEnumerable<Team>>(archivedTeams);
        }

        public Task<IEnumerable<Team>> GetTeamsByOwnerAsync(string ownerUpn, bool forceRefresh = false, string? accessToken = null)
        {
            return Task.FromResult<IEnumerable<Team>>(new List<Team>());
        }

        public Task<Team?> GetTeamByIdAsync(string teamId, bool includeMembers = false, bool includeChannels = false, bool forceRefresh = false, string? accessToken = null)
        {
            var team = _mockTeams.FirstOrDefault(t => t.Id == teamId);
            return Task.FromResult(team);
        }

        public Task<Team?> GetByIdAsync(string teamId)
        {
            var team = _mockTeams.FirstOrDefault(t => t.Id == teamId);
            return Task.FromResult(team);
        }

        public Task<IEnumerable<Team>> GetTeamsBySchoolYearAsync(string schoolYearId, bool forceRefresh = false, string? accessToken = null)
        {
            return Task.FromResult<IEnumerable<Team>>(new List<Team>());
        }

        public Task<bool> UpdateTeamAsync(Team team, string accessToken)
        {
            return Task.FromResult(true);
        }

        public Task<bool> ArchiveTeamAsync(string teamId, string reason, string accessToken)
        {
            return Task.FromResult(true);
        }

        public Task<bool> RestoreTeamAsync(string teamId, string accessToken)
        {
            return Task.FromResult(true);
        }

        public Task<bool> DeleteTeamAsync(string teamId, string accessToken)
        {
            return Task.FromResult(true);
        }

        public Task<TeamMember?> AddMemberAsync(string teamId, string userUpn, TeamMemberRole role, string accessToken)
        {
            return Task.FromResult<TeamMember?>(null);
        }

        public Task<bool> RemoveMemberAsync(string teamId, string userId, string accessToken)
        {
            return Task.FromResult(true);
        }

        public Task<Dictionary<string, bool>> AddUsersToTeamAsync(string teamId, List<string> userUpns, string accessToken)
        {
            return Task.FromResult(new Dictionary<string, bool>());
        }

        public Task<Dictionary<string, bool>> RemoveUsersFromTeamAsync(string teamId, List<string> userUpns, string reason, string accessToken)
        {
            return Task.FromResult(new Dictionary<string, bool>());
        }

        public Task<Dictionary<string, string>> SynchronizeAllTeamsAsync(string apiAccessToken, IProgress<int>? progress = null)
        {
            return Task.FromResult(new Dictionary<string, string>());
        }

        public Task RefreshCacheAsync()
        {
            return Task.CompletedTask;
        }
    }
} 