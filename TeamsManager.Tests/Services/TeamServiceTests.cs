using System;
using System.Collections.Generic;
using System.Linq.Expressions;
using System.Threading.Tasks;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using Moq;
using TeamsManager.Core.Abstractions;
using TeamsManager.Core.Abstractions.Data;
using TeamsManager.Core.Abstractions.Services;
using TeamsManager.Core.Enums;
using TeamsManager.Core.Models;
using TeamsManager.Core.Services;
using Microsoft.EntityFrameworkCore;
using Xunit; // Lub atrybuty z MSTest/NUnit

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
        private readonly Mock<IGenericRepository<SchoolType>> _mockSchoolTypeRepository; // Upewnij się, że to jest właściwy typ mocka
        private readonly Mock<ISchoolYearRepository> _mockSchoolYearRepository;     // Upewnij się, że to jest właściwy typ mocka

        private readonly TeamService _teamService;
        private readonly User _testOwnerUser;
        private OperationHistory? _capturedOperationHistory;
        private readonly string _currentLoggedInUserUpn = "admin@example.com"; // Przykładowy zalogowany użytkownik

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

            _testOwnerUser = new User
            {
                Id = "owner-guid-123",
                UPN = "owner@example.com",
                FirstName = "Test",
                LastName = "Owner",
                Role = UserRole.Nauczyciel, // Ustawienie tej roli spowoduje, że DefaultTeamRole będzie Owner
                IsActive = true
            };

            _mockCurrentUserService.Setup(s => s.GetCurrentUserUpn()).Returns(_currentLoggedInUserUpn);

            // Domyślna konfiguracja dla OperationHistory - nowa operacja
            _mockOperationHistoryRepository.Setup(r => r.AddAsync(It.IsAny<OperationHistory>()))
                                         .Callback<OperationHistory>(op => _capturedOperationHistory = op!) // Dodaj '!' do op
                                         .Returns(Task.CompletedTask);
            _mockOperationHistoryRepository.Setup(r => r.AddAsync(It.IsAny<OperationHistory>()))
                                         .Callback<OperationHistory>(op => _capturedOperationHistory = op)
                                         .Returns(Task.CompletedTask);
            // Jeśli operacja mogłaby być aktualizowana zamiast dodawana w niektórych scenariuszach
            _mockOperationHistoryRepository.Setup(r => r.Update(It.IsAny<OperationHistory>()))
                                         .Callback<OperationHistory>(op => _capturedOperationHistory = op);


            _teamService = new TeamService(
                _mockTeamRepository.Object,
                _mockUserRepository.Object,
                _mockTeamMemberRepository.Object,
                _mockTeamTemplateRepository.Object,
                _mockOperationHistoryRepository.Object,
                _mockCurrentUserService.Object,
                _mockPowerShellService.Object,
                _mockLogger.Object,
                _mockSchoolTypeRepository.Object,
                _mockSchoolYearRepository.Object
            );
        }
        [Fact]
        public async Task CreateTeamAsync_OwnerNotFound_ShouldReturnNullAndLogOperationFailed()
        {
            // Arrange
            var displayName = "Team With NonExistent Owner";
            var description = "This team should not be created.";
            var nonExistentOwnerUpn = "non.existent.user@example.com";
            var visibility = TeamVisibility.Private;

            // Konfiguracja CurrentUserService (taka sama jak w poprzednim teście)
            _mockCurrentUserService.Setup(s => s.GetCurrentUserUpn()).Returns(_currentLoggedInUserUpn);

            // Konfiguracja UserRepository - symulujemy, że użytkownik o podanym UPN nie istnieje
            _mockUserRepository.Setup(r => r.GetUserByUpnAsync(nonExistentOwnerUpn))
                               .Returns(Task.FromResult<User?>(null)); // Zwracamy null

            // Konfiguracja OperationHistoryRepository (taka sama jak w poprzednim teście dla nowego logu)
            // _capturedOperationHistory będzie ustawione przez Callback w AddAsync
            _mockOperationHistoryRepository.Setup(r => r.GetByIdAsync(It.IsAny<string>()))
                                         .Returns(Task.FromResult((OperationHistory?)null));
            _mockOperationHistoryRepository.Setup(r => r.AddAsync(It.IsAny<OperationHistory>()))
                                         .Callback<OperationHistory>(op => _capturedOperationHistory = op!) // Używamy op! po ostatniej poprawce
                                         .Returns(Task.CompletedTask);

            // Act
            var resultTeam = await _teamService.CreateTeamAsync(
                displayName,
                description,
                nonExistentOwnerUpn,
                visibility,
                teamTemplateId: null,
                schoolTypeId: null,
                schoolYearId: null,
                additionalTemplateValues: null
            );

            // Assert
            // 1. Sprawdzenie, czy metoda zwróciła null
            resultTeam.Should().BeNull();

            // 2. Weryfikacja, że próbowano pobrać użytkownika
            _mockUserRepository.Verify(r => r.GetUserByUpnAsync(nonExistentOwnerUpn), Times.Once);

            // 3. Weryfikacja, że NIE próbowano wywołać PowerShellService ani TeamRepository.AddAsync
            _mockPowerShellService.Verify(p => p.CreateTeamAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<TeamVisibility>(), It.IsAny<string?>()),
                Times.Never());
            _mockTeamRepository.Verify(r => r.AddAsync(It.IsAny<Team>()),
                Times.Never());

            // 4. Weryfikacja OperationHistory
            _mockOperationHistoryRepository.Verify(r => r.AddAsync(It.IsAny<OperationHistory>()), Times.Once);
            _capturedOperationHistory.Should().NotBeNull();
            _capturedOperationHistory.Type.Should().Be(OperationType.TeamCreated);
            _capturedOperationHistory.Status.Should().Be(OperationStatus.Failed);
            _capturedOperationHistory.TargetEntityName.Should().Be(displayName); // Nazwa przekazana do CreateTeamAsync
            _capturedOperationHistory.ErrorMessage.Should().NotBeNullOrWhiteSpace();
            _capturedOperationHistory.ErrorMessage.Should().Contain($"Użytkownik właściciela '{nonExistentOwnerUpn}' nie istnieje lub jest nieaktywny.");
            _capturedOperationHistory.CreatedBy.Should().Be(_currentLoggedInUserUpn);
        }

        [Theory]
        [InlineData("")]
        [InlineData(" ")]
        [InlineData(null)]
        public async Task CreateTeamAsync_EmptyOrWhiteSpaceDisplayName_ShouldReturnNullAndLogOperationFailed(string invalidDisplayName)
        {
            // Arrange
            var description = "Description for a team that should not be created.";
            var ownerUpn = _testOwnerUser.UPN; // Używamy _testOwnerUser zdefiniowanego w konstruktorze
            var visibility = TeamVisibility.Private;

            // Konfiguracja CurrentUserService (taka sama jak w poprzednich testach)
            _mockCurrentUserService.Setup(s => s.GetCurrentUserUpn()).Returns(_currentLoggedInUserUpn);

            // Konfiguracja OperationHistoryRepository (taka sama jak w poprzednich testach dla nowego logu)
            _mockOperationHistoryRepository.Setup(r => r.GetByIdAsync(It.IsAny<string>()))
                                         .Returns(Task.FromResult((OperationHistory?)null));
            _mockOperationHistoryRepository.Setup(r => r.AddAsync(It.IsAny<OperationHistory>()))
                                         .Callback<OperationHistory>(op => _capturedOperationHistory = op!)
                                         .Returns(Task.CompletedTask);

            // Act
            var resultTeam = await _teamService.CreateTeamAsync(
                invalidDisplayName,
                description,
                ownerUpn,
                visibility,
                teamTemplateId: null,
                schoolTypeId: null,
                schoolYearId: null,
                additionalTemplateValues: null
            );

            // Assert
            // 1. Sprawdzenie, czy metoda zwróciła null
            resultTeam.Should().BeNull();

            // 2. Weryfikacja, że NIE próbowano pobrać użytkownika, wywołać PowerShellService ani TeamRepository.AddAsync
            //    ponieważ walidacja nazwy powinna być pierwszym krokiem.
            _mockUserRepository.Verify(r => r.GetUserByUpnAsync(It.IsAny<string>()),
                Times.Never());
            _mockPowerShellService.Verify(p => p.CreateTeamAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<TeamVisibility>(), It.IsAny<string?>()),
                Times.Never());
            _mockTeamRepository.Verify(r => r.AddAsync(It.IsAny<Team>()),
                Times.Never());

            // 3. Weryfikacja OperationHistory
            _mockOperationHistoryRepository.Verify(r => r.AddAsync(It.IsAny<OperationHistory>()), Times.Once);
            _capturedOperationHistory.Should().NotBeNull();
            _capturedOperationHistory.Type.Should().Be(OperationType.TeamCreated);
            _capturedOperationHistory.Status.Should().Be(OperationStatus.Failed);
            _capturedOperationHistory.TargetEntityName.Should().Be(invalidDisplayName); // Nazwa przekazana do CreateTeamAsync
            _capturedOperationHistory.ErrorMessage.Should().NotBeNullOrWhiteSpace();
            _capturedOperationHistory.ErrorMessage.Should().Be("Nazwa wyświetlana zespołu nie może być pusta.");
            _capturedOperationHistory.CreatedBy.Should().Be(_currentLoggedInUserUpn);
        }

        [Fact]
        public async Task CreateTeamAsync_PowerShellServiceFails_ShouldReturnNullAndLogOperationFailed()
        {
            // Arrange
            var displayName = "Team With Failing PowerShell";
            var description = "This team creation should fail due to PowerShell error.";
            var ownerUpn = _testOwnerUser.UPN; // Używamy _testOwnerUser zdefiniowanego w konstruktorze
            var visibility = TeamVisibility.Private;

            _mockCurrentUserService.Setup(s => s.GetCurrentUserUpn()).Returns(_currentLoggedInUserUpn);

            // Konfiguracja UserRepository - właściciel istnieje i jest aktywny
            _mockUserRepository.Setup(r => r.GetUserByUpnAsync(ownerUpn))
                               .ReturnsAsync(_testOwnerUser);

            // Konfiguracja PowerShellService - symulujemy błąd (zwraca null jako externalTeamId)
            _mockPowerShellService.Setup(p => p.CreateTeamAsync(
                                    displayName,
                                    description,
                                    ownerUpn,
                                    visibility,
                                    null)) // Zakładamy brak szablonu w tym teście
                                  .ReturnsAsync((string?)null); // Symulacja błędu PowerShell

            // Konfiguracja OperationHistoryRepository
            _mockOperationHistoryRepository.Setup(r => r.GetByIdAsync(It.IsAny<string>()))
                                         .Returns(Task.FromResult((OperationHistory?)null));
            _mockOperationHistoryRepository.Setup(r => r.AddAsync(It.IsAny<OperationHistory>()))
                                         .Callback<OperationHistory>(op => _capturedOperationHistory = op!)
                                         .Returns(Task.CompletedTask);

            // Act
            var resultTeam = await _teamService.CreateTeamAsync(
                displayName,
                description,
                ownerUpn,
                visibility,
                teamTemplateId: null,
                schoolTypeId: null,
                schoolYearId: null,
                additionalTemplateValues: null
            );

            // Assert
            // 1. Sprawdzenie, czy metoda zwróciła null
            resultTeam.Should().BeNull();

            // 2. Weryfikacja, że próbowano pobrać użytkownika i wywołać PowerShellService
            _mockUserRepository.Verify(r => r.GetUserByUpnAsync(ownerUpn), Times.Once);
            _mockPowerShellService.Verify(p => p.CreateTeamAsync(displayName, description, ownerUpn, visibility, null),
                Times.Once);

            // 3. Weryfikacja, że NIE próbowano dodać zespołu do repozytorium
            _mockTeamRepository.Verify(r => r.AddAsync(It.IsAny<Team>()),
                Times.Never());

            // 4. Weryfikacja OperationHistory
            _mockOperationHistoryRepository.Verify(r => r.AddAsync(It.IsAny<OperationHistory>()), Times.Once);
            _capturedOperationHistory.Should().NotBeNull();
            _capturedOperationHistory.Type.Should().Be(OperationType.TeamCreated);
            _capturedOperationHistory.Status.Should().Be(OperationStatus.Failed);
            _capturedOperationHistory.TargetEntityName.Should().Be(displayName); // Nazwa przekazana do CreateTeamAsync
            _capturedOperationHistory.ErrorMessage.Should().NotBeNullOrWhiteSpace();
            // Sprawdź, czy komunikat błędu jest zgodny z tym, co ustawia TeamService
            _capturedOperationHistory.ErrorMessage.Should().Be("Nie udało się utworzyć zespołu w Microsoft Teams (symulacja zwróciła błąd).");
            _capturedOperationHistory.CreatedBy.Should().Be(_currentLoggedInUserUpn);
        }

        [Fact]
        public async Task CreateTeamAsync_WithValidTemplate_ShouldUseTemplateForDisplayNameAndSetTemplateId()
        {
            // Arrange
            var initialDisplayName = "Team Base Name"; // Nazwa, która zostanie nadpisana przez szablon
            var description = "Description for team with template.";
            var ownerUpn = _testOwnerUser.UPN;
            var visibility = TeamVisibility.Private;
            var simulatedExternalId = $"sim_ext_{System.Guid.NewGuid()}";

            var templateId = "template-guid-123";
            var templateContent = "SZKOŁA-{RokSzkolny}-KLASA_{Oddzial}-{Nauczyciel}";
            var expectedFinalDisplayName = $"SZKOŁA-2023/2024-KLASA_1A-{_testOwnerUser.FullName}"; // Oczekiwana nazwa po zastosowaniu szablonu

            var schoolYear = new SchoolYear { Id = "sy-1", Name = "2023/2024", IsActive = true };
            var schoolTypeId = "st-1"; // Załóżmy, że typ szkoły jest też przekazywany
            var schoolType = new SchoolType { Id = schoolTypeId, ShortName = "LO", FullName = "Liceum Ogólnokształcące", IsActive = true };

            var teamTemplate = new TeamTemplate
            {
                Id = templateId,
                Name = "Test Template",
                Template = templateContent,
                IsActive = true
            };

            var additionalTemplateValues = new Dictionary<string, string>
        {
            { "Oddzial", "1A" }
            // "Nauczyciel", "RokSzkolny", "TypSzkoly" zostaną dodane/nadpisane w serwisie
        };

            _mockCurrentUserService.Setup(s => s.GetCurrentUserUpn()).Returns(_currentLoggedInUserUpn);
            _mockUserRepository.Setup(r => r.GetUserByUpnAsync(ownerUpn)).ReturnsAsync(_testOwnerUser);

            // Mockowanie pobrania szablonu
            _mockTeamTemplateRepository.Setup(r => r.GetByIdAsync(templateId)).ReturnsAsync(teamTemplate);

            // Mockowanie pobrania typu szkoły i roku szkolnego (jeśli są używane przez logikę szablonu w serwisie)
            _mockSchoolTypeRepository.Setup(r => r.GetByIdAsync(schoolTypeId)).ReturnsAsync(schoolType);
            _mockSchoolYearRepository.Setup(r => r.GetByIdAsync(It.IsAny<string>())) // Załóżmy, że schoolYearId może być różne
                                   .ReturnsAsync(schoolYear);


            _mockPowerShellService.Setup(p => p.CreateTeamAsync(
                                    expectedFinalDisplayName, // Oczekujemy nazwy z szablonu
                                    description,
                                    ownerUpn,
                                    visibility,
                                    templateId)) // Zakładając, że ID szablonu jest przekazywane jako templateWebUrl, lub null jeśli inaczej
                                   .ReturnsAsync(simulatedExternalId);

            Team? addedTeam = null;
            _mockTeamRepository.Setup(r => r.AddAsync(It.IsAny<Team>()))
                               .Callback<Team>(team => addedTeam = team)
                               .Returns(Task.CompletedTask);

            _mockOperationHistoryRepository.Setup(r => r.GetByIdAsync(It.IsAny<string>())).Returns(Task.FromResult((OperationHistory?)null));
            _mockOperationHistoryRepository.Setup(r => r.AddAsync(It.IsAny<OperationHistory>()))
                                         .Callback<OperationHistory>(op => _capturedOperationHistory = op!)
                                         .Returns(Task.CompletedTask);

            // Act
            var resultTeam = await _teamService.CreateTeamAsync(
                initialDisplayName,
                description,
                ownerUpn,
                visibility,
                teamTemplateId: templateId,
                schoolTypeId: schoolTypeId, // Przekazujemy ID typu szkoły
                schoolYearId: schoolYear.Id, // Przekazujemy ID roku szkolnego
                additionalTemplateValues: additionalTemplateValues
            );

            // Assert
            resultTeam.Should().NotBeNull();
            addedTeam.Should().NotBeNull();
            resultTeam.Should().BeSameAs(addedTeam);

            resultTeam!.DisplayName.Should().Be(expectedFinalDisplayName); // Kluczowa asercja - nazwa z szablonu
            resultTeam.Description.Should().Be(description);
            resultTeam.Owner.Should().Be(ownerUpn);
            resultTeam.ExternalId.Should().Be(simulatedExternalId);
            resultTeam.TemplateId.Should().Be(templateId); // Sprawdzenie, czy ID szablonu zostało zapisane
            resultTeam.SchoolTypeId.Should().Be(schoolTypeId);
            resultTeam.SchoolYearId.Should().Be(schoolYear.Id);
            resultTeam.Visibility.Should().Be(visibility);

            // Weryfikacja wywołań
            _mockTeamTemplateRepository.Verify(r => r.GetByIdAsync(templateId), Times.Once);
            _mockPowerShellService.Verify(p => p.CreateTeamAsync(expectedFinalDisplayName, description, ownerUpn, visibility, templateId), Times.Once);
            _mockTeamRepository.Verify(r => r.AddAsync(It.Is<Team>(t => t.DisplayName == expectedFinalDisplayName && t.TemplateId == templateId)), Times.Once);

            // Weryfikacja OperationHistory
            _capturedOperationHistory.Should().NotBeNull();
            _capturedOperationHistory.TargetEntityName.Should().Be(expectedFinalDisplayName); // Nazwa po przetworzeniu przez szablon
            _capturedOperationHistory.Status.Should().Be(OperationStatus.Completed);
        }

        [Fact]
        public async Task CreateTeamAsync_WithInvalidOrInactiveTemplate_ShouldUseOriginalDisplayNameAndLogWarning()
        {
            // Arrange
            var originalDisplayName = "Team With Invalid Template";
            var description = "Description for team with invalid template.";
            var ownerUpn = _testOwnerUser.UPN;
            var visibility = TeamVisibility.Private;
            var invalidTemplateId = "template-does-not-exist";
            var simulatedExternalId = $"sim_ext_{System.Guid.NewGuid()}";

            _mockCurrentUserService.Setup(s => s.GetCurrentUserUpn()).Returns(_currentLoggedInUserUpn);
            _mockUserRepository.Setup(r => r.GetUserByUpnAsync(ownerUpn)).ReturnsAsync(_testOwnerUser);

            // Konfiguracja TeamTemplateRepository - szablon o podanym ID nie istnieje (zwraca null)
            _mockTeamTemplateRepository.Setup(r => r.GetByIdAsync(invalidTemplateId))
                                       .Returns(Task.FromResult((TeamTemplate?)null));

            // Konfiguracja PowerShellService - oczekujemy, że zostanie wywołany z oryginalną nazwą wyświetlaną
            _mockPowerShellService.Setup(p => p.CreateTeamAsync(
                                    originalDisplayName, // Oczekujemy oryginalnej nazwy
                                    description,
                                    ownerUpn,
                                    visibility,
                                    null)) // Brak ID szablonu, bo nie został użyty
                                   .ReturnsAsync(simulatedExternalId);

            Team? addedTeam = null;
            _mockTeamRepository.Setup(r => r.AddAsync(It.IsAny<Team>()))
                               .Callback<Team>(team => addedTeam = team)
                               .Returns(Task.CompletedTask);

            _mockOperationHistoryRepository.Setup(r => r.GetByIdAsync(It.IsAny<string>()))
                                         .Returns(Task.FromResult<OperationHistory?>(null));
            _mockOperationHistoryRepository.Setup(r => r.AddAsync(It.IsAny<OperationHistory>()))
                                         .Callback<OperationHistory>(op => _capturedOperationHistory = op!)
                                         .Returns(Task.CompletedTask);

            // Act
            var resultTeam = await _teamService.CreateTeamAsync(
                originalDisplayName,
                description,
                ownerUpn,
                visibility,
                teamTemplateId: invalidTemplateId, // Przekazujemy ID nieistniejącego szablonu
                schoolTypeId: null,
                schoolYearId: null,
                additionalTemplateValues: null
            );

            // Assert
            resultTeam.Should().NotBeNull();
            addedTeam.Should().NotBeNull();
            resultTeam.Should().BeSameAs(addedTeam);

            resultTeam!.DisplayName.Should().Be(originalDisplayName); // Kluczowa asercja - użyto oryginalnej nazwy
            resultTeam.TemplateId.Should().BeNull(); // ID szablonu nie powinno być ustawione (lub być ID nieistniejącego szablonu, zależnie od logiki serwisu)
                                                     // W obecnej logice serwisu TeamService, jeśli szablon nie zostanie znaleziony, newTeam.TemplateId = template?.Id ustawi null.

            // Weryfikacja wywołań
            _mockTeamTemplateRepository.Verify(r => r.GetByIdAsync(invalidTemplateId), Times.Once);
            _mockPowerShellService.Verify(p => p.CreateTeamAsync(originalDisplayName, description, ownerUpn, visibility, null), Times.Once);
            _mockTeamRepository.Verify(r => r.AddAsync(It.Is<Team>(t => t.DisplayName == originalDisplayName)), Times.Once);

            // Weryfikacja logowania ostrzeżenia - sprawdzamy czy została wywołana metoda Log z odpowiednimi parametrami
            _mockLogger.Verify(
                x => x.Log(
                    LogLevel.Warning,
                    It.IsAny<EventId>(),
                    It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains($"Szablon o ID {invalidTemplateId} nie istnieje lub jest nieaktywny")),
                    It.IsAny<Exception>(),
                    It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
                Times.Once);

            // Weryfikacja OperationHistory - operacja powinna się udać, ale może zawierać informację o nieużyciu szablonu
            _capturedOperationHistory.Should().NotBeNull();
            _capturedOperationHistory.Status.Should().Be(OperationStatus.Completed); // Operacja tworzenia zespołu się udała
            _capturedOperationHistory.TargetEntityName.Should().Be(originalDisplayName);
            // Można dodać asercję sprawdzającą, czy OperationDetails zawiera informację o ostrzeżeniu (jeśli taka logika jest w serwisie)
            // np. _capturedOperationHistory.OperationDetails.Should().Contain("nie znaleziono szablonu");
        }

        [Fact]
        public async Task CreateTeamAsync_ValidInputWithoutTemplate_ShouldReturnNewTeamAndLogOperation()
        {
            // Arrange
            var displayName = "Test Team Alpha";
            var description = "Description for Test Team Alpha";
            var ownerUpn = _testOwnerUser.UPN;
            var simulatedExternalId = $"sim_ext_{System.Guid.NewGuid()}";

            // Konfiguracja mocka dla UserRepository - zwraca zdefiniowanego użytkownika testowego
            _mockUserRepository.Setup(r => r.GetUserByUpnAsync(ownerUpn))
                               .ReturnsAsync(_testOwnerUser);

            // Konfiguracja mocka dla PowerShellService - symuluje pomyślne utworzenie zespołu
            // Używamy It.IsAny dla parametrów, których dokładne wartości nie są kluczowe dla tego konkretnego mocka,
            // ale ownerUser.UPN powinien być zgodny, jeśli chcemy to weryfikować.
            // Sygnatura CreateTeamAsync w IPowerShellService to:
            // Task<string?> CreateTeamAsync(string displayName, string description, string ownerUpn, TeamVisibility visibility = TeamVisibility.Private, string? templateWebUrl = null);
            _mockPowerShellService.Setup(p => p.CreateTeamAsync(
                                    displayName, // Oczekujemy, że finalDisplayName będzie taki sam jak displayName bez szablonu
                                    description,
                                    ownerUpn,
                                    It.IsAny<TeamVisibility>(), // Domyślna widoczność lub jawnie podana
                                    null)) // Brak templateWebUrl w tym scenariuszu
                                  .ReturnsAsync(simulatedExternalId);

            // Konfiguracja mocka dla TeamRepository.AddAsync
            Team? addedTeam = null;
            _mockTeamRepository.Setup(r => r.AddAsync(It.IsAny<Team>()))
                               .Callback<Team>(team => addedTeam = team) // Przechwycenie dodanego zespołu
                               .Returns(Task.CompletedTask);

            // Act
            var resultTeam = await _teamService.CreateTeamAsync(
                displayName,
                description,
                ownerUpn,
                TeamVisibility.Private, // Argument pozycyjny dla 'visibility'
                teamTemplateId: null,   // Od teraz argumenty muszą być nazwane
                schoolTypeId: null,
                schoolYearId: null,
                additionalTemplateValues: null
            );

            // Assert
            // 1. Sprawdzenie, czy zespół został zwrócony
            resultTeam.Should().NotBeNull();
            addedTeam.Should().NotBeNull(); // Sprawdzenie, czy zespół został przekazany do AddAsync
            resultTeam.Should().BeSameAs(addedTeam); // Upewniamy się, że zwrócony obiekt to ten sam, który został dodany

            // 2. Sprawdzenie podstawowych właściwości zespołu
            resultTeam!.DisplayName.Should().Be(displayName);
            resultTeam.Description.Should().Be(description);
            resultTeam.Owner.Should().Be(ownerUpn); // Upewniamy się, że UPN właściciela jest ustawiony
            resultTeam.ExternalId.Should().Be(simulatedExternalId);
            resultTeam.Status.Should().Be(TeamStatus.Active);
            resultTeam.IsActive.Should().BeTrue();
            resultTeam.CreatedBy.Should().Be(_currentLoggedInUserUpn);
            resultTeam.TemplateId.Should().BeNull();
            resultTeam.SchoolTypeId.Should().BeNull();
            resultTeam.SchoolYearId.Should().BeNull();
            resultTeam.Visibility.Should().Be(TeamVisibility.Private);

            // 3. Sprawdzenie, czy właściciel został dodany jako członek zespołu
            resultTeam.Members.Should().NotBeNull().And.HaveCount(1);
            var ownerMember = resultTeam.Members.First();
            ownerMember.UserId.Should().Be(_testOwnerUser.Id);
            ownerMember.Role.Should().Be(_testOwnerUser.DefaultTeamRole); // Zakładając, że User ma DefaultTeamRole
            ownerMember.IsActive.Should().BeTrue();
            ownerMember.AddedBy.Should().Be(_currentLoggedInUserUpn);
            ownerMember.CreatedBy.Should().Be(_currentLoggedInUserUpn); // Encja TeamMember też ma CreatedBy
            ownerMember.TeamId.Should().Be(resultTeam.Id);
            ownerMember.User.Should().BeSameAs(_testOwnerUser); // Sprawdzenie referencji obiektu User
            ownerMember.Team.Should().BeSameAs(resultTeam);   // Sprawdzenie referencji obiektu Team

            // 4. Weryfikacja wywołań mocków
            _mockUserRepository.Verify(r => r.GetUserByUpnAsync(ownerUpn), Times.Once);
            _mockPowerShellService.Verify(p => p.CreateTeamAsync(displayName, description, ownerUpn, TeamVisibility.Private, null), Times.Once); // Możemy być bardziej szczegółowi co do parametrów
            _mockTeamRepository.Verify(r => r.AddAsync(It.Is<Team>(t => t.DisplayName == displayName && t.ExternalId == simulatedExternalId)), Times.Once);

            // 5. Weryfikacja OperationHistory
            _mockOperationHistoryRepository.Verify(r => r.AddAsync(It.IsAny<OperationHistory>()), Times.Once); // Lub Update jeśli logiką jest aktualizacja
            _capturedOperationHistory.Should().NotBeNull();
            _capturedOperationHistory.Type.Should().Be(OperationType.TeamCreated);
            _capturedOperationHistory.TargetEntityType.Should().Be(nameof(Team));
            _capturedOperationHistory.TargetEntityName.Should().Be(displayName); // Przed ewentualną zmianą przez szablon
            _capturedOperationHistory.TargetEntityId.Should().Be(resultTeam.Id);
            _capturedOperationHistory.Status.Should().Be(OperationStatus.Completed);
            _capturedOperationHistory.CreatedBy.Should().Be(_currentLoggedInUserUpn);
            _capturedOperationHistory.OperationDetails.Should().Contain($"Zespół ID: {resultTeam.Id}");
        }
    }
}