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
using Microsoft.EntityFrameworkCore; // Może nie być potrzebne bezpośrednio tutaj, ale często jest w testach
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
        private readonly Mock<IMemoryCache> _mockMemoryCache;

        private readonly TeamService _teamService;
        private readonly User _testOwnerUser;
        private OperationHistory? _capturedOperationHistory;
        private readonly string _currentLoggedInUserUpn = "admin@example.com";

        // Klucze cache
        private const string AllActiveTeamsCacheKey = "Teams_AllActive";
        private const string ActiveTeamsSpecificCacheKey = "Teams_Active"; // Dla GetActiveTeamsAsync
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
            _mockMemoryCache = new Mock<IMemoryCache>();

            _testOwnerUser = new User { Id = "owner-guid-123", UPN = "owner@example.com", FirstName = "Test", LastName = "Owner", Role = UserRole.Nauczyciel, IsActive = true };
            _mockCurrentUserService.Setup(s => s.GetCurrentUserUpn()).Returns(_currentLoggedInUserUpn);
            _mockOperationHistoryRepository.Setup(r => r.AddAsync(It.IsAny<OperationHistory>()))
                                         .Callback<OperationHistory>(op => _capturedOperationHistory = op!)
                                         .Returns(Task.CompletedTask);

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
                _mockMemoryCache.Object
            );
        }

        private void SetupCacheTryGetValue<TItem>(string cacheKey, TItem? item, bool foundInCache)
        {
            object? outItem = item;
            _mockMemoryCache.Setup(m => m.TryGetValue(cacheKey, out outItem))
                           .Returns(foundInCache);
        }

        private void ResetCapturedOperationHistory()
        {
            _capturedOperationHistory = null;
        }

        // Metody pomocnicze AssertCacheInvalidationByReFetching...
        // W tych metodach, jeśli predykaty używały `t.IsActive && t.Status == TeamStatus.Active`,
        // teraz wystarczy `t.IsActive`, ponieważ `Team.IsActive` już odzwierciedla `Status`.
        private void AssertCacheInvalidationByReFetchingAllActive(List<Team> expectedDbItemsAfterOperation)
        {
            SetupCacheTryGetValue(AllActiveTeamsCacheKey, (IEnumerable<Team>?)null, false);
            _mockTeamRepository.Setup(r => r.FindAsync(It.Is<Expression<Func<Team, bool>>>(ex => TestExpressionHelper.IsForActiveTeamRecords(ex)))) // Zmieniona nazwa metody pomocniczej dla jasności
                                 .ReturnsAsync(expectedDbItemsAfterOperation)
                                 .Verifiable();

            var resultAfterInvalidation = _teamService.GetAllTeamsAsync().Result;

            _mockTeamRepository.Verify(r => r.FindAsync(It.Is<Expression<Func<Team, bool>>>(ex => TestExpressionHelper.IsForActiveTeamRecords(ex))), Times.Once, "GetAllTeamsAsync powinno odpytać repozytorium po unieważnieniu cache.");
            resultAfterInvalidation.Should().BeEquivalentTo(expectedDbItemsAfterOperation);
            _mockMemoryCache.Verify(m => m.CreateEntry(AllActiveTeamsCacheKey), Times.AtLeastOnce, "Dane GetAllTeamsAsync powinny zostać ponownie zcache'owane.");
        }

        private void AssertCacheInvalidationByReFetchingActiveSpecific(List<Team> expectedDbItemsAfterOperation)
        {
            SetupCacheTryGetValue(ActiveTeamsSpecificCacheKey, (IEnumerable<Team>?)null, false);
            _mockTeamRepository.Setup(r => r.GetActiveTeamsAsync()) // Ta metoda repozytorium powinna już zwracać tylko te z TeamStatus.Active i IsActive=true
                                 .ReturnsAsync(expectedDbItemsAfterOperation)
                                 .Verifiable();

            var resultAfterInvalidation = _teamService.GetActiveTeamsAsync().Result;

            _mockTeamRepository.Verify(r => r.GetActiveTeamsAsync(), Times.Once, "GetActiveTeamsAsync powinno odpytać repozytorium po unieważnieniu cache.");
            resultAfterInvalidation.Should().BeEquivalentTo(expectedDbItemsAfterOperation);
            _mockMemoryCache.Verify(m => m.CreateEntry(ActiveTeamsSpecificCacheKey), Times.AtLeastOnce, "Dane GetActiveTeamsAsync powinny zostać ponownie zcache'owane.");
        }

        private void AssertCacheInvalidationByReFetchingArchived(List<Team> expectedDbItemsAfterOperation)
        {
            SetupCacheTryGetValue(ArchivedTeamsCacheKey, (IEnumerable<Team>?)null, false);
            _mockTeamRepository.Setup(r => r.GetArchivedTeamsAsync()) // Ta metoda repozytorium powinna zwracać tylko te z TeamStatus.Archived i IsActive=true (dla rekordu, nie dla Statusu)
                                 .ReturnsAsync(expectedDbItemsAfterOperation)
                                 .Verifiable();

            var resultAfterInvalidation = _teamService.GetArchivedTeamsAsync().Result;

            _mockTeamRepository.Verify(r => r.GetArchivedTeamsAsync(), Times.Once, "GetArchivedTeamsAsync powinno odpytać repozytorium po unieważnieniu cache.");
            resultAfterInvalidation.Should().BeEquivalentTo(expectedDbItemsAfterOperation);
            _mockMemoryCache.Verify(m => m.CreateEntry(ArchivedTeamsCacheKey), Times.AtLeastOnce, "Dane GetArchivedTeamsAsync powinny zostać ponownie zcache'owane.");
        }

        private void AssertCacheInvalidationByReFetchingByOwner(string ownerUpn, List<Team> expectedDbItemsAfterOperation)
        {
            SetupCacheTryGetValue(TeamsByOwnerCacheKeyPrefix + ownerUpn, (IEnumerable<Team>?)null, false);
            // GetTeamsByOwnerAsync powinno zwracać zespoły, gdzie rekord Team jest IsActive=true,
            // a Team.Status może być dowolny (Active lub Archived), więc filtracja po Team.IsActive (obliczeniowym)
            // powinna być robiona w serwisie lub prezentacji, jeśli chcemy tylko "aktywne operacyjnie" zespoły właściciela.
            // Na razie zakładamy, że repozytorium zwraca wszystkie (rekordy IsActive=true) zespoły danego właściciela.
            _mockTeamRepository.Setup(r => r.GetTeamsByOwnerAsync(ownerUpn))
                                 .ReturnsAsync(expectedDbItemsAfterOperation)
                                 .Verifiable();

            var resultAfterInvalidation = _teamService.GetTeamsByOwnerAsync(ownerUpn).Result;

            _mockTeamRepository.Verify(r => r.GetTeamsByOwnerAsync(ownerUpn), Times.Once, $"GetTeamsByOwnerAsync({ownerUpn}) powinno odpytać repozytorium.");
            resultAfterInvalidation.Should().BeEquivalentTo(expectedDbItemsAfterOperation);
            _mockMemoryCache.Verify(m => m.CreateEntry(TeamsByOwnerCacheKeyPrefix + ownerUpn), Times.AtLeastOnce);
        }


        // --- Testy GetTeamByIdAsync ---
        [Fact]
        public async Task GetTeamByIdAsync_ExistingTeam_NotInCache_ShouldReturnAndCache()
        {
            var teamId = "team-1";
            // Dla testu, tworzymy zespół z domyślnym Status = Active, co oznacza IsActive = true
            var expectedTeam = new Team { Id = teamId, DisplayName = "Test Team Alpha", Owner = _testOwnerUser.UPN, Status = TeamStatus.Active, Members = new List<TeamMember>(), Channels = new List<Channel>() };
            string cacheKey = TeamByIdCacheKeyPrefix + teamId;
            SetupCacheTryGetValue(cacheKey, (Team?)null, false);
            _mockTeamRepository.Setup(r => r.GetByIdAsync(teamId)).ReturnsAsync(expectedTeam);

            var result = await _teamService.GetTeamByIdAsync(teamId);

            result.Should().NotBeNull();
            result!.IsActive.Should().BeTrue(); // Sprawdzenie obliczeniowego IsActive
            result.Should().BeEquivalentTo(expectedTeam, options => options.ExcludingMissingMembers());
            _mockTeamRepository.Verify(r => r.GetByIdAsync(teamId), Times.Once);
            _mockMemoryCache.Verify(m => m.CreateEntry(cacheKey), Times.Once);
        }

        [Fact]
        public async Task GetTeamByIdAsync_ExistingTeam_InCache_ShouldReturnFromCache()
        {
            var teamId = "team-cached";
            var cachedTeam = new Team { Id = teamId, DisplayName = "Cached Team", Status = TeamStatus.Active };
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
            var cachedTeam = new Team { Id = teamId, DisplayName = "Old Data", Status = TeamStatus.Archived }; // IsActive będzie false
            var dbTeam = new Team { Id = teamId, DisplayName = "New Data from DB", Status = TeamStatus.Active }; // IsActive będzie true
            string cacheKey = TeamByIdCacheKeyPrefix + teamId;

            SetupCacheTryGetValue(cacheKey, cachedTeam, true);
            _mockTeamRepository.Setup(r => r.GetByIdAsync(teamId)).ReturnsAsync(dbTeam);

            var result = await _teamService.GetTeamByIdAsync(teamId, forceRefresh: true);

            result.Should().BeEquivalentTo(dbTeam);
            result!.IsActive.Should().BeTrue();
            _mockTeamRepository.Verify(r => r.GetByIdAsync(teamId), Times.Once);
            _mockMemoryCache.Verify(m => m.CreateEntry(cacheKey), Times.Once);
        }

        // --- Testy GetAllTeamsAsync (zwraca rekordy z BaseEntity.IsActive = true) ---
        [Fact]
        public async Task GetAllTeamsAsync_NotInCache_ShouldReturnAndCache()
        {
            // W tej metodzie zakładamy, że repozytorium zwraca te zespoły, które w bazie mają IsActive = true (z BaseEntity)
            // Niezależnie od ich domenowego Team.Status. Serwis potem może filtrować dalej.
            // Obecna implementacja _teamRepository.FindAsync(t => t.IsActive) w TeamService
            // będzie teraz używać nowego, obliczeniowego Team.IsActive, więc zwróci tylko te z TeamStatus.Active.
            var activeStatusTeams = new List<Team> { new Team { Id = "all-active-1", Status = TeamStatus.Active } }; // IsActive będzie true
            SetupCacheTryGetValue(AllActiveTeamsCacheKey, (IEnumerable<Team>?)null, false);
            _mockTeamRepository.Setup(r => r.FindAsync(It.Is<Expression<Func<Team, bool>>>(ex => TestExpressionHelper.IsForActiveTeamRecords(ex))))
                               .ReturnsAsync(activeStatusTeams);

            var result = await _teamService.GetAllTeamsAsync();

            result.Should().BeEquivalentTo(activeStatusTeams);
            _mockMemoryCache.Verify(m => m.CreateEntry(AllActiveTeamsCacheKey), Times.Once);
        }

        // --- Testy GetActiveTeamsAsync ---
        [Fact]
        public async Task GetActiveTeamsAsync_NotInCache_ShouldReturnAndCache()
        {
            // Ta metoda powinna zwracać zespoły z Team.Status = Active (co implikuje Team.IsActive = true)
            var activeStatusTeams = new List<Team> { new Team { Id = "status-active-1", Status = TeamStatus.Active } }; // IsActive będzie true
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
            // Ta metoda powinna zwracać zespoły z Team.Status = Archived
            // Wcześniej GetArchivedTeamsAsync zwracało także te z IsActive = true (dla rekordu).
            // Teraz, jeśli Team.IsActive jest obliczeniowe, to GetArchivedTeamsAsync powinno zwracać
            // te, które mają Status = Archived (a więc ich Team.IsActive będzie false).
            // Jednak TeamRepository.GetArchivedTeamsAsync prawdopodobnie nadal filtruje po `IsActive = true` rekordu
            // i `Status = TeamStatus.Archived`. To jest OK, bo serwis może chcieć pokazać "logicznie zarchiwizowane"
            // które nie są "usunięte" (soft-delete przez BaseEntity.IsActive = false).
            // Po zmianie w Team.cs, repozytorium powinno nadal działać tak samo, jeśli polegało na Status.
            var archivedTeams = new List<Team> { new Team { Id = "archived-1", Status = TeamStatus.Archived, DisplayName = "ARCHIWALNY - Stary Zespół" } }; // IsActive będzie false
            SetupCacheTryGetValue(ArchivedTeamsCacheKey, (IEnumerable<Team>?)null, false);
            _mockTeamRepository.Setup(r => r.GetArchivedTeamsAsync()).ReturnsAsync(archivedTeams);

            var result = await _teamService.GetArchivedTeamsAsync();

            result.Should().BeEquivalentTo(archivedTeams);
            result.First().IsActive.Should().BeFalse(); // Sprawdzenie obliczeniowego IsActive
            _mockMemoryCache.Verify(m => m.CreateEntry(ArchivedTeamsCacheKey), Times.Once);
        }

        // --- Testy GetTeamsByOwnerAsync ---
        [Fact]
        public async Task GetTeamsByOwnerAsync_NotInCache_ShouldReturnAndCache()
        {
            var ownerUpn = "owner1@example.com";
            // Repozytorium GetTeamsByOwnerAsync zwraca zespoły gdzie rekord IsActive=true.
            // Status może być Active lub Archived.
            var teamsByOwner = new List<Team> {
                new Team { Id = "owner-team-1", Owner = ownerUpn, Status = TeamStatus.Active }, // IsActive będzie true
                new Team { Id = "owner-team-2", Owner = ownerUpn, Status = TeamStatus.Archived } // IsActive będzie false
            };
            // Jeśli chcemy tylko operacyjnie aktywne zespoły właściciela, filtracja po Team.IsActive (obliczeniowym)
            // powinna nastąpić w serwisie lub wyżej.
            // Na razie testujemy, co repozytorium powinno zwrócić (rekordy IsActive=true).
            // Zakładając, że repozytorium zwraca zespoły z IsActive=true (z BaseEntity, co teraz nie ma bezpośredniego wpływu na Team.IsActive),
            // to w wynikach możemy mieć zespoły z różnym Team.Status.
            // Dla spójności, załóżmy, że repozytorium _teamRepository.GetTeamsByOwnerAsync filtruje po `t.IsActive` z `BaseEntity`.
            // Ponieważ Team.IsActive przesłania to z BaseEntity, `t.IsActive` w repozytorium będzie odnosić się do `Team.Status == TeamStatus.Active`.
            // Więc repozytorium zwróci tylko zespoły z TeamStatus.Active.

            // Poprawka: _teamRepository.GetTeamsByOwnerAsync zwraca te, gdzie Team.IsActive (z BaseEntity) jest true.
            // Zmiana w Team.IsActive na właściwość obliczeniową oznacza, że jeśli repozytorium używa `t.IsActive`,
            // to będzie to teraz odnosić się do `t.Status == TeamStatus.Active`.
            // Jeśli repozytorium ma zwracać *wszystkie* zespoły danego właściciela (niezależnie od ich statusu),
            // to jego implementacja musi być `Where(t => t.Owner == ownerUpn)`.
            // Obecna implementacja w repozytorium: `Where(t => t.Owner == ownerUpn && t.IsActive)`
            // co teraz oznacza `Where(t => t.Owner == ownerUpn && t.Status == TeamStatus.Active)`

            var teamsByOwnerFromRepo = new List<Team> { teamsByOwner[0] }; // Tylko aktywny

            string cacheKey = TeamsByOwnerCacheKeyPrefix + ownerUpn;
            SetupCacheTryGetValue(cacheKey, (IEnumerable<Team>?)null, false);
            _mockTeamRepository.Setup(r => r.GetTeamsByOwnerAsync(ownerUpn)).ReturnsAsync(teamsByOwnerFromRepo);

            var result = await _teamService.GetTeamsByOwnerAsync(ownerUpn);

            result.Should().BeEquivalentTo(teamsByOwnerFromRepo);
            result.Should().OnlyContain(t => t.IsActive); // Sprawdzenie, czy są tylko te z obliczeniowym IsActive = true
            _mockMemoryCache.Verify(m => m.CreateEntry(cacheKey), Times.Once);
        }


        // --- Testy inwalidacji cache ---
        [Fact]
        public async Task CreateTeamAsync_ShouldInvalidateCache()
        {
            ResetCapturedOperationHistory();
            var ownerUpn = _testOwnerUser.UPN;
            var teamName = "Newly Created Team";
            // Nowo tworzony zespół będzie miał Status = Active, więc IsActive = true
            var createdTeam = new Team { Id = "new-team-id", DisplayName = teamName, Owner = ownerUpn, Status = TeamStatus.Active, CreatedBy = "test", CreatedDate = DateTime.UtcNow };

            _mockUserRepository.Setup(r => r.GetUserByUpnAsync(ownerUpn)).ReturnsAsync(_testOwnerUser);
            _mockPowerShellService.Setup(p => p.CreateTeamAsync(It.IsAny<string>(), It.IsAny<string>(), ownerUpn, It.IsAny<TeamVisibility>(), null))
                                  .ReturnsAsync("external-id-new");
            _mockTeamRepository.Setup(r => r.AddAsync(It.IsAny<Team>()))
                                 .Callback<Team>(t => t.Id = createdTeam.Id) // Symulacja nadania ID przez repozytorium
                                 .Returns(Task.CompletedTask);

            var result = await _teamService.CreateTeamAsync(teamName, "desc", ownerUpn, TeamVisibility.Private);
            result.Should().NotBeNull();
            result!.Status.Should().Be(TeamStatus.Active); // Sprawdzenie statusu
            result.IsActive.Should().BeTrue(); // Sprawdzenie obliczeniowego IsActive

            // ... (weryfikacja OperationHistory jak wcześniej) ...
            _capturedOperationHistory.Should().NotBeNull();
            _capturedOperationHistory!.Type.Should().Be(OperationType.TeamCreated);
            _capturedOperationHistory.Status.Should().Be(OperationStatus.Completed);

            // ... (weryfikacja inwalidacji cache jak wcześniej) ...
            var ownerMembership = new TeamMember { Id = "some-member-id", UserId = _testOwnerUser.Id, TeamId = result.Id, Role = _testOwnerUser.DefaultTeamRole, IsActive = true, User = _testOwnerUser, Team = result };
            result.Members.Add(ownerMembership);

            AssertCacheInvalidationByReFetchingAllActive(new List<Team> { result }); // Zwróci tylko ten jeden, bo jest aktywny
            AssertCacheInvalidationByReFetchingActiveSpecific(new List<Team> { result });
            AssertCacheInvalidationByReFetchingArchived(new List<Team>()); // Pusta lista
            AssertCacheInvalidationByReFetchingByOwner(result.Owner, new List<Team> { result }); // Zwróci tylko ten, bo jest aktywny
        }

        [Fact]
        public async Task UpdateTeamAsync_ShouldInvalidateCache()
        {
            ResetCapturedOperationHistory();
            var teamId = "team-update-cache";
            var oldOwnerUpn = "old.owner@example.com";
            var newOwnerUpn = _testOwnerUser.UPN;
            var oldStatus = TeamStatus.Active;
            var newStatus = TeamStatus.Active; // Załóżmy, że status się nie zmienia, tylko inne właściwości

            // existingTeam ma Status = Active, więc IsActive = true
            var existingTeam = new Team { Id = teamId, DisplayName = "Old", Owner = oldOwnerUpn, ExternalId = "ext", Status = oldStatus, CreatedBy = "initial", CreatedDate = DateTime.UtcNow.AddDays(-1) };
            var updatedTeamData = new Team { Id = teamId, DisplayName = "New", Owner = newOwnerUpn, Status = newStatus }; // Status nadal Active

            // Serwis używa FindAsync, a nie GetByIdAsync do szukania zespołu
            _mockTeamRepository.Setup(r => r.FindAsync(It.Is<Expression<Func<Team, bool>>>(
                expr => expr.Compile().Invoke(existingTeam) // Sprawdza, czy predykat t => t.Id == teamId pasuje do naszego zespołu
            ))).ReturnsAsync(new List<Team> { existingTeam });
            
            // Mock dla sprawdzenia nowego właściciela
            _mockUserRepository.Setup(r => r.GetUserByUpnAsync(newOwnerUpn)).ReturnsAsync(_testOwnerUser);
            
            _mockPowerShellService.Setup(p => p.UpdateTeamPropertiesAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<TeamVisibility?>()))
                                  .ReturnsAsync(true);
            _mockTeamRepository.Setup(r => r.Update(It.IsAny<Team>()));

            var updateResult = await _teamService.UpdateTeamAsync(updatedTeamData);
            updateResult.Should().BeTrue();

            // ... (weryfikacja OperationHistory jak wcześniej) ...
            _capturedOperationHistory.Should().NotBeNull();
            _capturedOperationHistory!.Type.Should().Be(OperationType.TeamUpdated);
            _capturedOperationHistory.Status.Should().Be(OperationStatus.Completed);

            // ... (weryfikacja inwalidacji cache jak wcześniej) ...
            var expectedAfterUpdate = new Team { Id = teamId, DisplayName = "New", Owner = newOwnerUpn, Status = newStatus, CreatedBy = existingTeam.CreatedBy, CreatedDate = existingTeam.CreatedDate };
            expectedAfterUpdate.IsActive.Should().BeTrue(); // Ponieważ Status = Active

            AssertCacheInvalidationByReFetchingAllActive(new List<Team> { expectedAfterUpdate });
            AssertCacheInvalidationByReFetchingActiveSpecific(new List<Team> { expectedAfterUpdate });
            AssertCacheInvalidationByReFetchingByOwner(newOwnerUpn, new List<Team> { expectedAfterUpdate });
            // Jeśli stary właściciel nie ma już żadnych aktywnych zespołów
            AssertCacheInvalidationByReFetchingByOwner(oldOwnerUpn, new List<Team>());
        }

        [Fact]
        public async Task ArchiveTeamAsync_ShouldInvalidateCache()
        {
            ResetCapturedOperationHistory();
            var teamId = "team-archive-cache";
            var ownerUpn = _testOwnerUser.UPN;
            var team = new Team { Id = teamId, DisplayName = "To Archive", Owner = ownerUpn, ExternalId = "ext-archive", Status = TeamStatus.Active, CreatedBy = "initial", CreatedDate = DateTime.UtcNow.AddDays(-1) };

            // Serwis używa FindAsync, a nie GetByIdAsync do szukania zespołu
            _mockTeamRepository.Setup(r => r.FindAsync(It.Is<Expression<Func<Team, bool>>>(
                expr => expr.Compile().Invoke(team) // Sprawdza, czy predykat t => t.Id == teamId pasuje do naszego zespołu
            ))).ReturnsAsync(new List<Team> { team });
            
            _mockPowerShellService.Setup(p => p.ArchiveTeamAsync(It.IsAny<string>())).ReturnsAsync(true);
            _mockTeamRepository.Setup(r => r.Update(It.IsAny<Team>()));

            var archiveResult = await _teamService.ArchiveTeamAsync(teamId, "reason");
            archiveResult.Should().BeTrue();

            // Weryfikacja obiektu przekazanego do Update
            _mockTeamRepository.Verify(r => r.Update(It.Is<Team>(t =>
                t.Id == teamId &&
                t.Status == TeamStatus.Archived && // Sprawdzamy Status
                !t.IsActive && // Sprawdzamy obliczeniowe IsActive
                t.DisplayName.StartsWith("ARCHIWALNY - ")
            )), Times.Once);

            _capturedOperationHistory.Should().NotBeNull();
            _capturedOperationHistory!.Type.Should().Be(OperationType.TeamArchived);
            _capturedOperationHistory.Status.Should().Be(OperationStatus.Completed);


            // Sprawdzenie inwalidacji cache
            // Zarchiwizowany zespół (Status = Archived) będzie miał IsActive = false
            var archivedTeamForCache = new Team { Id = teamId, DisplayName = "ARCHIWALNY - To Archive", Owner = ownerUpn, Status = TeamStatus.Archived, CreatedBy = team.CreatedBy, CreatedDate = team.CreatedDate };
            archivedTeamForCache.IsActive.Should().BeFalse();

            AssertCacheInvalidationByReFetchingAllActive(new List<Team>()); // Lista aktywnych będzie pusta
            AssertCacheInvalidationByReFetchingActiveSpecific(new List<Team>()); // Lista tych ze statusem Active będzie pusta
            AssertCacheInvalidationByReFetchingArchived(new List<Team> { archivedTeamForCache }); // Powinien być na liście zarchiwizowanych
            // GetTeamsByOwnerAsync z repozytorium zwraca tylko te z Team.Status = Active,
            // więc po archiwizacji nie powinno go tam być.
            AssertCacheInvalidationByReFetchingByOwner(ownerUpn, new List<Team>());
        }


        [Fact]
        public async Task AddMemberAsync_ShouldInvalidateTeamCache()
        {
            ResetCapturedOperationHistory();
            var teamId = "team-addmember-cache";
            var ownerUpn = _testOwnerUser.UPN;
            var team = new Team { Id = teamId, DisplayName = "Team Test", RequiresApproval = false, Status = TeamStatus.Active, Owner = ownerUpn, CreatedBy = "initial", CreatedDate = DateTime.UtcNow.AddDays(-1) };
            var userToAdd = new User { Id = "user-new-member", UPN = "newmember@example.com", IsActive = true };
            var newMember = new TeamMember { Id = "new-member-id", TeamId = teamId, UserId = userToAdd.Id, Role = TeamMemberRole.Member, IsActive = true, User = userToAdd, Team = team };

            _mockTeamRepository.Setup(r => r.GetByIdAsync(teamId)).ReturnsAsync(team);
            _mockUserRepository.Setup(r => r.GetUserByUpnAsync(userToAdd.UPN)).ReturnsAsync(userToAdd);
            _mockPowerShellService.Setup(p => p.AddUserToTeamAsync(It.IsAny<string>(), userToAdd.UPN, It.IsAny<string>())).ReturnsAsync(true);
            _mockTeamMemberRepository.Setup(r => r.AddAsync(It.IsAny<TeamMember>()))
                                    .Callback<TeamMember>(tm => tm.Id = newMember.Id)
                                    .Returns(Task.CompletedTask);

            var addResult = await _teamService.AddMemberAsync(teamId, userToAdd.UPN, TeamMemberRole.Member);
            addResult.Should().NotBeNull();

            // ... (weryfikacja OperationHistory jak wcześniej) ...
            _capturedOperationHistory.Should().NotBeNull();
            _capturedOperationHistory!.Type.Should().Be(OperationType.MemberAdded);
            _capturedOperationHistory.Status.Should().Be(OperationStatus.Completed);

            // ... (weryfikacja inwalidacji cache jak wcześniej) ...
            // Zespół nadal jest aktywny
            var teamAfterAddingMember = new Team
            {
                Id = team.Id,
                DisplayName = team.DisplayName,
                Owner = team.Owner,
                Status = team.Status,
                RequiresApproval = team.RequiresApproval,
                CreatedBy = team.CreatedBy,
                CreatedDate = team.CreatedDate,
                Members = new List<TeamMember>(team.Members) { newMember }
            };
            teamAfterAddingMember.IsActive.Should().BeTrue();

            AssertCacheInvalidationByReFetchingAllActive(new List<Team> { teamAfterAddingMember });
            AssertCacheInvalidationByReFetchingActiveSpecific(new List<Team> { teamAfterAddingMember });
            AssertCacheInvalidationByReFetchingByOwner(ownerUpn, new List<Team> { teamAfterAddingMember });
        }


        [Fact]
        public async Task RefreshCacheAsync_ShouldTriggerCacheInvalidationForAllTeamKeys()
        {
            await _teamService.RefreshCacheAsync();

            _mockMemoryCache.Verify(m => m.Remove(AllActiveTeamsCacheKey), Times.Once);
            _mockMemoryCache.Verify(m => m.Remove(ActiveTeamsSpecificCacheKey), Times.Once);
            _mockMemoryCache.Verify(m => m.Remove(ArchivedTeamsCacheKey), Times.Once);
            // Token unieważni klucze specyficzne (ID, Owner)

            SetupCacheTryGetValue(AllActiveTeamsCacheKey, (IEnumerable<Team>?)null, false);
            _mockTeamRepository.Setup(r => r.FindAsync(It.IsAny<Expression<Func<Team, bool>>>()))
                               .ReturnsAsync(new List<Team>())
                               .Verifiable();

            await _teamService.GetAllTeamsAsync(); // To powinno teraz odpytać repozytorium
            // Sprawdzamy, czy repozytorium zostało odpytane po predykacie dla Team.IsActive (obliczeniowego)
            _mockTeamRepository.Verify(r => r.FindAsync(It.Is<Expression<Func<Team, bool>>>(ex => TestExpressionHelper.IsForActiveTeamRecords(ex))), Times.Once);
        }

        [Fact]
        public async Task CreateTeamAsync_ValidInputWithoutTemplate_ShouldReturnNewTeamAndLogOperation()
        {
            ResetCapturedOperationHistory();
            var displayName = "Test Team Alpha";
            var description = "Description for Test Team Alpha";
            var ownerUpn = _testOwnerUser.UPN;
            var simulatedExternalId = $"sim_ext_{System.Guid.NewGuid()}";

            _mockUserRepository.Setup(r => r.GetUserByUpnAsync(ownerUpn)).ReturnsAsync(_testOwnerUser);
            _mockPowerShellService.Setup(p => p.CreateTeamAsync(displayName, description, ownerUpn, TeamVisibility.Private, null))
                                  .ReturnsAsync(simulatedExternalId);
            _mockTeamRepository.Setup(r => r.AddAsync(It.IsAny<Team>()))
                                 .Callback<Team>(t => {
                                     // Symulacja zachowania repozytorium, które może nadać ID, jeśli nie ma
                                     if (string.IsNullOrEmpty(t.Id)) t.Id = Guid.NewGuid().ToString();
                                     t.ExternalId = simulatedExternalId; // Upewniamy się, że ExternalId jest ustawiony
                                 })
                                 .Returns(Task.CompletedTask);

            var resultTeam = await _teamService.CreateTeamAsync(displayName, description, ownerUpn, TeamVisibility.Private, null, null, null, null);

            resultTeam.Should().NotBeNull();
            resultTeam!.DisplayName.Should().Be(displayName);
            resultTeam.ExternalId.Should().Be(simulatedExternalId); // Ważne sprawdzenie
            resultTeam.Status.Should().Be(TeamStatus.Active); // Domyślny status
            resultTeam.IsActive.Should().BeTrue(); // Obliczeniowe

            _capturedOperationHistory.Should().NotBeNull();
            _capturedOperationHistory!.Status.Should().Be(OperationStatus.Completed);
            _capturedOperationHistory.Type.Should().Be(OperationType.TeamCreated);
            _mockOperationHistoryRepository.Verify(r => r.AddAsync(It.IsAny<OperationHistory>()), Times.Once);
        }

        public static class TestExpressionHelper
        {
            // Zmieniamy nazwę i logikę, aby pasowała do nowego Team.IsActive
            public static bool IsForActiveTeamRecords(Expression<Func<Team, bool>> expression)
            {
                // Testujemy, czy wyrażenie przepuszcza zespół z Status = Active
                // i odrzuca zespół z Status = Archived
                var activeTeam = new Team { Status = TeamStatus.Active }; // IsActive będzie true
                var archivedTeam = new Team { Status = TeamStatus.Archived }; // IsActive będzie false
                var compiled = expression.Compile();
                return compiled(activeTeam) && !compiled(archivedTeam);
            }
        }
    }
}