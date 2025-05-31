using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Threading;
using System.Threading.Tasks;
using FluentAssertions;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Primitives;
using Moq;
using TeamsManager.Core.Abstractions;
using TeamsManager.Core.Abstractions.Data;
using TeamsManager.Core.Abstractions.Services;
using TeamsManager.Core.Enums;
using TeamsManager.Core.Models;
using TeamsManager.Core.Services;
using Microsoft.EntityFrameworkCore; // Potrzebne dla niektórych mocków repozytorium
using Xunit;

namespace TeamsManager.Tests.Services
{
    public class TeamServiceTests
    {
        private readonly Mock<ITeamRepository> _mockTeamRepository;
        private readonly Mock<IUserRepository> _mockUserRepository;
        private readonly Mock<IGenericRepository<TeamMember>> _mockTeamMemberRepository;
        private readonly Mock<ITeamTemplateRepository> _mockTeamTemplateRepository;
        private readonly Mock<IOperationHistoryRepository> _mockOperationHistoryRepository;
        private readonly Mock<ICurrentUserService> _mockCurrentUserService;
        private readonly Mock<IPowerShellService> _mockPowerShellService;
        private readonly Mock<ILogger<TeamService>> _mockLogger;
        private readonly Mock<IGenericRepository<SchoolType>> _mockSchoolTypeRepository;
        private readonly Mock<ISchoolYearRepository> _mockSchoolYearRepository;
        private readonly Mock<IMemoryCache> _mockMemoryCache; // Dodano

        private readonly TeamService _teamService;
        private readonly User _testOwnerUser;
        private OperationHistory? _capturedOperationHistory;
        private readonly string _currentLoggedInUserUpn = "admin@example.com";

        // Klucze cache
        private const string AllActiveTeamsCacheKey = "Teams_AllActive";
        private const string ActiveTeamsSpecificCacheKey = "Teams_Active";
        private const string ArchivedTeamsCacheKey = "Teams_Archived";
        private const string TeamsByOwnerCacheKeyPrefix = "Teams_ByOwner_";
        private const string TeamByIdCacheKeyPrefix = "Team_Id_";


        public TeamServiceTests()
        {
            _mockTeamRepository = new Mock<ITeamRepository>();
            _mockUserRepository = new Mock<IUserRepository>();
            _mockTeamMemberRepository = new Mock<IGenericRepository<TeamMember>>();
            _mockTeamTemplateRepository = new Mock<ITeamTemplateRepository>();
            _mockOperationHistoryRepository = new Mock<IOperationHistoryRepository>();
            _mockCurrentUserService = new Mock<ICurrentUserService>();
            _mockPowerShellService = new Mock<IPowerShellService>();
            _mockLogger = new Mock<ILogger<TeamService>>();
            _mockSchoolTypeRepository = new Mock<IGenericRepository<SchoolType>>();
            _mockSchoolYearRepository = new Mock<ISchoolYearRepository>();
            _mockMemoryCache = new Mock<IMemoryCache>(); // Inicjalizacja

            _testOwnerUser = new User { Id = "owner-guid-123", UPN = "owner@example.com", FirstName = "Test", LastName = "Owner", Role = UserRole.Nauczyciel, IsActive = true };
            _mockCurrentUserService.Setup(s => s.GetCurrentUserUpn()).Returns(_currentLoggedInUserUpn);
            _mockOperationHistoryRepository.Setup(r => r.AddAsync(It.IsAny<OperationHistory>()))
                                         .Callback<OperationHistory>(op => _capturedOperationHistory = op!)
                                         .Returns(Task.CompletedTask);
            _mockOperationHistoryRepository.Setup(r => r.Update(It.IsAny<OperationHistory>()))
                                         .Callback<OperationHistory>(op => _capturedOperationHistory = op!);

            var mockCacheEntry = new Mock<ICacheEntry>();
            mockCacheEntry.SetupGet(e => e.ExpirationTokens).Returns(new List<IChangeToken>());
            mockCacheEntry.SetupGet(e => e.PostEvictionCallbacks).Returns(new List<PostEvictionCallbackRegistration>());
            mockCacheEntry.SetupProperty(e => e.Value);
            mockCacheEntry.SetupProperty(e => e.AbsoluteExpiration);
            mockCacheEntry.SetupProperty(e => e.AbsoluteExpirationRelativeToNow);
            mockCacheEntry.SetupProperty(e => e.SlidingExpiration);

            _mockMemoryCache.Setup(m => m.CreateEntry(It.IsAny<object>()))
                           .Returns(mockCacheEntry.Object);


            _teamService = new TeamService(
                _mockTeamRepository.Object, _mockUserRepository.Object, _mockTeamMemberRepository.Object,
                _mockTeamTemplateRepository.Object, _mockOperationHistoryRepository.Object,
                _mockCurrentUserService.Object, _mockPowerShellService.Object, _mockLogger.Object,
                _mockSchoolTypeRepository.Object, _mockSchoolYearRepository.Object,
                _mockMemoryCache.Object // Przekazanie mocka
            );
        }

        private void SetupCacheTryGetValue<TItem>(string cacheKey, TItem? item, bool foundInCache)
        {
            object? outItem = item;
            _mockMemoryCache.Setup(m => m.TryGetValue(cacheKey, out outItem))
                           .Returns(foundInCache);
        }

        // --- Testy GetTeamByIdAsync ---
        [Fact]
        public async Task GetTeamByIdAsync_ExistingTeam_NotInCache_ShouldReturnAndCache()
        {
            var teamId = "team-1";
            // TeamRepository.GetByIdAsync domyślnie dołącza Members.User i Channels
            var expectedTeam = new Team { Id = teamId, DisplayName = "Test Team Alpha", Owner = _testOwnerUser.UPN, Members = new List<TeamMember>(), Channels = new List<Channel>() };
            string cacheKey = TeamByIdCacheKeyPrefix + teamId;
            SetupCacheTryGetValue(cacheKey, (Team?)null, false);
            _mockTeamRepository.Setup(r => r.GetByIdAsync(teamId)).ReturnsAsync(expectedTeam);

            var result = await _teamService.GetTeamByIdAsync(teamId);

            result.Should().BeEquivalentTo(expectedTeam, options => options.ExcludingMissingMembers());
            _mockTeamRepository.Verify(r => r.GetByIdAsync(teamId), Times.Once);
            _mockMemoryCache.Verify(m => m.CreateEntry(cacheKey), Times.Once);
        }

        [Fact]
        public async Task GetTeamByIdAsync_ExistingTeam_InCache_ShouldReturnFromCache()
        {
            var teamId = "team-cached";
            var cachedTeam = new Team { Id = teamId, DisplayName = "Cached Team" };
            string cacheKey = TeamByIdCacheKeyPrefix + teamId;
            SetupCacheTryGetValue(cacheKey, cachedTeam, true);

            var result = await _teamService.GetTeamByIdAsync(teamId);

            result.Should().BeEquivalentTo(cachedTeam);
            _mockTeamRepository.Verify(r => r.GetByIdAsync(It.IsAny<string>()), Times.Never);
        }

        [Fact]
        public async Task GetTeamByIdAsync_WithForceRefresh_ShouldBypassCache()
        {
            var teamId = "team-force";
            var cachedTeam = new Team { Id = teamId, DisplayName = "Old Data" };
            var dbTeam = new Team { Id = teamId, DisplayName = "New Data from DB" };
            string cacheKey = TeamByIdCacheKeyPrefix + teamId;

            SetupCacheTryGetValue(cacheKey, cachedTeam, true);
            _mockTeamRepository.Setup(r => r.GetByIdAsync(teamId)).ReturnsAsync(dbTeam);

            var result = await _teamService.GetTeamByIdAsync(teamId, forceRefresh: true);

            result.Should().BeEquivalentTo(dbTeam);
            _mockTeamRepository.Verify(r => r.GetByIdAsync(teamId), Times.Once);
            _mockMemoryCache.Verify(m => m.CreateEntry(cacheKey), Times.Once);
        }

        // --- Testy GetAllTeamsAsync (zwraca aktywne rekordy) ---
        [Fact]
        public async Task GetAllTeamsAsync_NotInCache_ShouldReturnAndCache()
        {
            var activeTeams = new List<Team> { new Team { Id = "all-active-1", IsActive = true } };
            SetupCacheTryGetValue(AllActiveTeamsCacheKey, (IEnumerable<Team>?)null, false);
            _mockTeamRepository.Setup(r => r.FindAsync(It.Is<Expression<Func<Team, bool>>>(ex => TestExpressionHelper.IsForActiveTeams(ex))))
                               .ReturnsAsync(activeTeams);

            var result = await _teamService.GetAllTeamsAsync();

            result.Should().BeEquivalentTo(activeTeams);
            _mockMemoryCache.Verify(m => m.CreateEntry(AllActiveTeamsCacheKey), Times.Once);
        }

        // --- Testy GetActiveTeamsAsync ---
        [Fact]
        public async Task GetActiveTeamsAsync_NotInCache_ShouldReturnAndCache()
        {
            var activeStatusTeams = new List<Team> { new Team { Id = "status-active-1", Status = TeamStatus.Active, IsActive = true } };
            SetupCacheTryGetValue(ActiveTeamsSpecificCacheKey, (IEnumerable<Team>?)null, false);
            _mockTeamRepository.Setup(r => r.GetActiveTeamsAsync()).ReturnsAsync(activeStatusTeams);

            var result = await _teamService.GetActiveTeamsAsync();

            result.Should().BeEquivalentTo(activeStatusTeams);
            _mockMemoryCache.Verify(m => m.CreateEntry(ActiveTeamsSpecificCacheKey), Times.Once);
        }

        // --- Testy GetArchivedTeamsAsync ---
        [Fact]
        public async Task GetArchivedTeamsAsync_NotInCache_ShouldReturnAndCache()
        {
            var archivedTeams = new List<Team> { new Team { Id = "archived-1", Status = TeamStatus.Archived, IsActive = true } };
            SetupCacheTryGetValue(ArchivedTeamsCacheKey, (IEnumerable<Team>?)null, false);
            _mockTeamRepository.Setup(r => r.GetArchivedTeamsAsync()).ReturnsAsync(archivedTeams);

            var result = await _teamService.GetArchivedTeamsAsync();

            result.Should().BeEquivalentTo(archivedTeams);
            _mockMemoryCache.Verify(m => m.CreateEntry(ArchivedTeamsCacheKey), Times.Once);
        }

        // --- Testy GetTeamsByOwnerAsync ---
        [Fact]
        public async Task GetTeamsByOwnerAsync_NotInCache_ShouldReturnAndCache()
        {
            var ownerUpn = "owner1@example.com";
            var teamsByOwner = new List<Team> { new Team { Id = "owner-team-1", Owner = ownerUpn, IsActive = true } };
            string cacheKey = TeamsByOwnerCacheKeyPrefix + ownerUpn;
            SetupCacheTryGetValue(cacheKey, (IEnumerable<Team>?)null, false);
            _mockTeamRepository.Setup(r => r.GetTeamsByOwnerAsync(ownerUpn)).ReturnsAsync(teamsByOwner);

            var result = await _teamService.GetTeamsByOwnerAsync(ownerUpn);

            result.Should().BeEquivalentTo(teamsByOwner);
            _mockMemoryCache.Verify(m => m.CreateEntry(cacheKey), Times.Once);
        }


        // --- Testy inwalidacji cache ---
        [Fact]
        public async Task CreateTeamAsync_ShouldInvalidateCache()
        {
            var ownerUpn = _testOwnerUser.UPN;
            var teamName = "Newly Created Team";
            _mockUserRepository.Setup(r => r.GetUserByUpnAsync(ownerUpn)).ReturnsAsync(_testOwnerUser);
            _mockPowerShellService.Setup(p => p.CreateTeamAsync(It.IsAny<string>(), It.IsAny<string>(), ownerUpn, It.IsAny<TeamVisibility>(), null))
                                  .ReturnsAsync("external-id-new");
            _mockTeamRepository.Setup(r => r.AddAsync(It.IsAny<Team>())).Returns(Task.CompletedTask);

            await _teamService.CreateTeamAsync(teamName, "desc", ownerUpn, TeamVisibility.Private);

            _mockMemoryCache.Verify(m => m.Remove(It.IsAny<object>()), Times.AtLeastOnce);
        }

        [Fact]
        public async Task UpdateTeamAsync_ShouldInvalidateCache()
        {
            var teamId = "team-update-cache";
            var existingTeam = new Team { Id = teamId, DisplayName = "Old", Owner = _testOwnerUser.UPN, ExternalId = "ext", Status = TeamStatus.Active, IsActive = true };
            var updatedTeam = new Team { Id = teamId, DisplayName = "New", Owner = _testOwnerUser.UPN, Status = TeamStatus.Active };
            _mockTeamRepository.Setup(r => r.GetByIdAsync(teamId)).ReturnsAsync(existingTeam);

            await _teamService.UpdateTeamAsync(updatedTeam);

            _mockMemoryCache.Verify(m => m.Remove(TeamByIdCacheKeyPrefix + teamId), Times.AtLeastOnce);
            // Token powinien unieważnić też listy
        }

        [Fact]
        public async Task ArchiveTeamAsync_ShouldInvalidateCache()
        {
            var teamId = "team-archive-cache";
            var team = new Team { Id = teamId, DisplayName = "To Archive", Owner = _testOwnerUser.UPN, ExternalId = "ext-archive", Status = TeamStatus.Active, IsActive = true };
            _mockTeamRepository.Setup(r => r.GetByIdAsync(teamId)).ReturnsAsync(team);
            _mockPowerShellService.Setup(p => p.ArchiveTeamAsync(It.IsAny<string>())).ReturnsAsync(true);

            await _teamService.ArchiveTeamAsync(teamId, "reason");

            _mockMemoryCache.Verify(m => m.Remove(TeamByIdCacheKeyPrefix + teamId), Times.AtLeastOnce);
            _mockMemoryCache.Verify(m => m.Remove(ActiveTeamsSpecificCacheKey), Times.AtLeastOnce);
            _mockMemoryCache.Verify(m => m.Remove(ArchivedTeamsCacheKey), Times.AtLeastOnce);
        }

        [Fact]
        public async Task AddMemberAsync_ShouldInvalidateTeamCache()
        {
            var teamId = "team-addmember-cache";
            var team = new Team { Id = teamId, DisplayName = "Team Test", RequiresApproval = false, IsActive = true, Status = TeamStatus.Active, Owner = _testOwnerUser.UPN };
            var userToAdd = new User { Id = "user-new-member", UPN = "newmember@example.com", IsActive = true };

            _mockTeamRepository.Setup(r => r.GetByIdAsync(teamId)).ReturnsAsync(team);
            _mockUserRepository.Setup(r => r.GetUserByUpnAsync(userToAdd.UPN)).ReturnsAsync(userToAdd);
            _mockPowerShellService.Setup(p => p.AddUserToTeamAsync(It.IsAny<string>(), userToAdd.UPN, It.IsAny<string>())).ReturnsAsync(true);
            _mockTeamMemberRepository.Setup(r => r.AddAsync(It.IsAny<TeamMember>())).Returns(Task.CompletedTask);


            await _teamService.AddMemberAsync(teamId, userToAdd.UPN, TeamMemberRole.Member);

            _mockMemoryCache.Verify(m => m.Remove(TeamByIdCacheKeyPrefix + teamId), Times.AtLeastOnce);
        }

        [Fact]
        public async Task RefreshCacheAsync_ShouldTriggerCacheInvalidationForAllTeamKeys()
        {
            await _teamService.RefreshCacheAsync();

            SetupCacheTryGetValue(AllActiveTeamsCacheKey, (IEnumerable<Team>?)null, false);
            _mockTeamRepository.Setup(r => r.FindAsync(It.IsAny<Expression<Func<Team, bool>>>()))
                               .ReturnsAsync(new List<Team>())
                               .Verifiable();

            await _teamService.GetAllTeamsAsync();
            _mockTeamRepository.Verify();
        }

        // Istniejące testy
        [Fact]
        public async Task CreateTeamAsync_ValidInputWithoutTemplate_ShouldReturnNewTeamAndLogOperation()
        {
            var displayName = "Test Team Alpha";
            var description = "Description for Test Team Alpha";
            var ownerUpn = _testOwnerUser.UPN;
            var simulatedExternalId = $"sim_ext_{System.Guid.NewGuid()}";

            _mockUserRepository.Setup(r => r.GetUserByUpnAsync(ownerUpn)).ReturnsAsync(_testOwnerUser);
            _mockPowerShellService.Setup(p => p.CreateTeamAsync(displayName, description, ownerUpn, TeamVisibility.Private, null))
                                  .ReturnsAsync(simulatedExternalId);
            _mockTeamRepository.Setup(r => r.AddAsync(It.IsAny<Team>())).Returns(Task.CompletedTask);

            var resultTeam = await _teamService.CreateTeamAsync(displayName, description, ownerUpn, TeamVisibility.Private, null, null, null, null);

            resultTeam.Should().NotBeNull();
            resultTeam!.DisplayName.Should().Be(displayName);
            _capturedOperationHistory.Should().NotBeNull();
            _capturedOperationHistory.Status.Should().Be(OperationStatus.Completed);
        }

        // Pomocnicza klasa do testowania wyrażeń lambda przekazywanych do FindAsync
        public static class TestExpressionHelper
        {
            public static bool IsForActiveTeams(Expression<Func<Team, bool>> expression)
            {
                // Prosta implementacja, można ją rozbudować do analizy drzewa wyrażenia
                var activeTeam = new Team { IsActive = true, Status = TeamStatus.Active };
                var inactiveTeam = new Team { IsActive = false, Status = TeamStatus.Active };
                var archivedTeam = new Team { IsActive = true, Status = TeamStatus.Archived };

                var compiled = expression.Compile();
                // W GetAllTeamsAsync predykat to t => t.IsActive
                return compiled(activeTeam) && !compiled(inactiveTeam) && compiled(archivedTeam); // archivedTeam jest IsActive=true, więc przejdzie ten filtr
            }
        }

    }
}