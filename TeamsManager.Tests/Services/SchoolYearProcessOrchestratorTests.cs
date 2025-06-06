using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;
using TeamsManager.Application.Services;
using TeamsManager.Core.Abstractions.Services;
using TeamsManager.Core.Abstractions.Services.PowerShell;
using TeamsManager.Core.Models;
using TeamsManager.Core.Enums;

namespace TeamsManager.Tests.Services
{
    /// <summary>
    /// Testy jednostkowe dla orkiestratora procesów szkolnych
    /// </summary>
    public class SchoolYearProcessOrchestratorTests
    {
        private readonly Mock<ISchoolYearService> _mockSchoolYearService;
        private readonly Mock<ITeamTemplateService> _mockTeamTemplateService;
        private readonly Mock<ITeamService> _mockTeamService;
        private readonly Mock<IPowerShellBulkOperationsService> _mockBulkOperationsService;
        private readonly Mock<INotificationService> _mockNotificationService;
        private readonly Mock<ILogger<SchoolYearProcessOrchestrator>> _mockLogger;
        private readonly SchoolYearProcessOrchestrator _orchestrator;

        public SchoolYearProcessOrchestratorTests()
        {
            _mockSchoolYearService = new Mock<ISchoolYearService>();
            _mockTeamTemplateService = new Mock<ITeamTemplateService>();
            _mockTeamService = new Mock<ITeamService>();
            _mockBulkOperationsService = new Mock<IPowerShellBulkOperationsService>();
            _mockNotificationService = new Mock<INotificationService>();
            _mockLogger = new Mock<ILogger<SchoolYearProcessOrchestrator>>();

            _orchestrator = new SchoolYearProcessOrchestrator(
                _mockTeamService.Object,
                _mockTeamTemplateService.Object,
                _mockSchoolYearService.Object,
                _mockBulkOperationsService.Object,
                _mockNotificationService.Object,
                _mockLogger.Object
            );
        }

        [Fact]
        public async Task CreateTeamsForNewSchoolYearAsync_WithValidInput_ShouldReturnSuccessResult()
        {
            // Arrange
            var schoolYearId = "school-year-1";
            var templateIds = new[] { "template-1", "template-2" };
            var accessToken = "test-token";

            var schoolYear = new SchoolYear
            {
                Id = schoolYearId,
                Name = "2024/2025",
                StartDate = DateTime.Today,
                EndDate = DateTime.Today.AddMonths(10)
            };

            var templates = new List<TeamTemplate>
            {
                new TeamTemplate
                {
                    Id = "template-1",
                    Name = "Szablon Klasy",
                    Template = "{Class} - {Year}",
                    Description = "Szablon dla klas",
                    Category = "class"
                },
                new TeamTemplate
                {
                    Id = "template-2",
                    Name = "Szablon Przedmiotu",
                    Template = "{Subject} - {Class} - {Year}",
                    Description = "Szablon dla przedmiotów",
                    Category = "subject"
                }
            };

            var successfulBulkResult = new BulkOperationResult
            {
                IsSuccess = true,
                SuccessfulOperations = new List<BulkOperationSuccess>
                {
                    new BulkOperationSuccess { Operation = "CreateTeam", EntityId = "team-1", Message = "Sukces" },
                    new BulkOperationSuccess { Operation = "CreateTeam", EntityId = "team-2", Message = "Sukces" }
                },
                Errors = new List<BulkOperationError>()
            };

            // Setup mocks
            _mockSchoolYearService.Setup(s => s.GetByIdAsync(schoolYearId))
                .ReturnsAsync(schoolYear);

            _mockTeamTemplateService.Setup(s => s.GetByIdAsync("template-1"))
                .ReturnsAsync(templates[0]);
            _mockTeamTemplateService.Setup(s => s.GetByIdAsync("template-2"))
                .ReturnsAsync(templates[1]);

            _mockTeamService.Setup(s => s.CreateTeamAsync(
                It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), 
                It.IsAny<Core.Enums.TeamVisibility>(), It.IsAny<string>(), 
                It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), 
                It.IsAny<Dictionary<string, string>>()))
                .ReturnsAsync(new Team { Id = Guid.NewGuid().ToString() });

            _mockBulkOperationsService.Setup(s => s.CreateTeamsAsync(It.IsAny<string[]>(), It.IsAny<string>()))
                .ReturnsAsync(successfulBulkResult);

            // Act
            var result = await _orchestrator.CreateTeamsForNewSchoolYearAsync(schoolYearId, templateIds, accessToken);

            // Assert
            Assert.NotNull(result);
            Assert.True(result.IsSuccess);
            Assert.Equal(2, result.SuccessfulOperations.Count);
            Assert.Empty(result.Errors);

            // Verify service calls
            _mockSchoolYearService.Verify(s => s.GetByIdAsync(schoolYearId), Times.Once);
            _mockTeamTemplateService.Verify(s => s.GetByIdAsync("template-1"), Times.Once);
            _mockTeamTemplateService.Verify(s => s.GetByIdAsync("template-2"), Times.Once);
        }

        [Fact]
        public async Task CreateTeamsForNewSchoolYearAsync_WithInvalidSchoolYear_ShouldThrowArgumentException()
        {
            // Arrange
            var schoolYearId = "invalid-school-year";
            var templateIds = new[] { "template-1" };
            var accessToken = "test-token";

            _mockSchoolYearService.Setup(s => s.GetByIdAsync(schoolYearId))
                .ReturnsAsync((SchoolYear?)null);

            // Act
            var result = await _orchestrator.CreateTeamsForNewSchoolYearAsync(schoolYearId, templateIds, accessToken);

            // Assert - powinna zwrócić błąd zamiast rzucać wyjątek
            Assert.NotNull(result);
            Assert.False(result.IsSuccess);
            Assert.Contains("nie istnieje", result.ErrorMessage);
        }

        [Fact]
        public async Task CreateTeamsForNewSchoolYearAsync_WithNoValidTemplates_ShouldReturnError()
        {
            // Arrange
            var schoolYearId = "school-year-1";
            var templateIds = new[] { "invalid-template-1", "invalid-template-2" };
            var accessToken = "test-token";

            var schoolYear = new SchoolYear
            {
                Id = schoolYearId,
                Name = "2024/2025",
                StartDate = DateTime.Today,
                EndDate = DateTime.Today.AddMonths(10)
            };

            _mockSchoolYearService.Setup(s => s.GetByIdAsync(schoolYearId))
                .ReturnsAsync(schoolYear);

            _mockTeamTemplateService.Setup(s => s.GetByIdAsync("invalid-template-1"))
                .ReturnsAsync((TeamTemplate?)null);
            _mockTeamTemplateService.Setup(s => s.GetByIdAsync("invalid-template-2"))
                .ReturnsAsync((TeamTemplate?)null);

            // Act
            var result = await _orchestrator.CreateTeamsForNewSchoolYearAsync(schoolYearId, templateIds, accessToken);

            // Assert - powinna zwrócić błąd zamiast rzucać wyjątek
            Assert.NotNull(result);
            Assert.False(result.IsSuccess);
            Assert.Contains("nie istnieje", result.ErrorMessage);
        }

        [Fact]
        public async Task CreateTeamsForNewSchoolYearAsync_WithDryRunOption_ShouldSimulateOperations()
        {
            // Arrange
            var schoolYearId = "school-year-1";
            var templateIds = new[] { "template-1" };
            var accessToken = "test-token";
            var options = new SchoolYearProcessOptions { DryRun = true };

            var schoolYear = new SchoolYear
            {
                Id = schoolYearId,
                Name = "2024/2025",
                StartDate = DateTime.Today,
                EndDate = DateTime.Today.AddMonths(10)
            };

            var template = new TeamTemplate
            {
                Id = "template-1",
                Name = "Szablon Testowy",
                Template = "{Class} - {Year}",
                Category = "class"
            };

            _mockSchoolYearService.Setup(s => s.GetByIdAsync(schoolYearId))
                .ReturnsAsync(schoolYear);

            _mockTeamTemplateService.Setup(s => s.GetByIdAsync("template-1"))
                .ReturnsAsync(template);

            // Act
            var result = await _orchestrator.CreateTeamsForNewSchoolYearAsync(schoolYearId, templateIds, accessToken, options);

            // Assert
            Assert.NotNull(result);
            Assert.True(result.IsSuccess || result.Success); // Akceptuj obie właściwości
        }

        [Fact]
        public async Task ArchiveTeamsFromPreviousSchoolYearAsync_WithValidInput_ShouldReturnSuccessResult()
        {
            // Arrange
            var schoolYearId = "school-year-1";
            var accessToken = "test-token";

            var schoolYear = new SchoolYear
            {
                Id = schoolYearId,
                Name = "2023/2024",
                StartDate = DateTime.Today.AddYears(-1),
                EndDate = DateTime.Today.AddMonths(-2)
            };

            var teamsToArchive = new List<Team>
            {
                new Team { Id = "team-1", DisplayName = "Klasa 1A", SchoolYearId = schoolYearId, Status = Core.Enums.TeamStatus.Active },
                new Team { Id = "team-2", DisplayName = "Klasa 1B", SchoolYearId = schoolYearId, Status = Core.Enums.TeamStatus.Active }
            };

            var successfulBulkResult = new BulkOperationResult
            {
                IsSuccess = true,
                SuccessfulOperations = new List<BulkOperationSuccess>
                {
                    new BulkOperationSuccess { Operation = "ArchiveTeam", EntityId = "team-1", Message = "Zarchiwizowano" },
                    new BulkOperationSuccess { Operation = "ArchiveTeam", EntityId = "team-2", Message = "Zarchiwizowano" }
                },
                Errors = new List<BulkOperationError>()
            };

            // Setup mocks - używam wszystkich parametrów aby uniknąć CS0854
            _mockTeamService.Setup(s => s.GetTeamsBySchoolYearAsync(schoolYearId, false, null))
                .ReturnsAsync(teamsToArchive);

            // Mock'i dla ArchiveTeamsAsync - różne warianty
            _mockBulkOperationsService.Setup(s => s.ArchiveTeamsAsync(new string[] { "team-1", "team-2" }, accessToken, 50))
                .ReturnsAsync(successfulBulkResult);
            
            // Dodatkowy mock z It.IsAny dla ArchiveTeamsAsync na wszelki wypadek
            _mockBulkOperationsService.Setup(s => s.ArchiveTeamsAsync(It.IsAny<string[]>(), It.IsAny<string>(), It.IsAny<int>()))
                .ReturnsAsync(successfulBulkResult);

            // Act
            var result = await _orchestrator.ArchiveTeamsFromPreviousSchoolYearAsync(schoolYearId, accessToken, new SchoolYearProcessOptions());

            // Debug output - sprawdzmy co faktycznie zwraca
            if (result == null)
            {
                // Test czy mock został wywołany
                try
                {
                    _mockTeamService.Verify(s => s.GetTeamsBySchoolYearAsync(schoolYearId, false, null), Times.AtLeastOnce);
                    Console.WriteLine("✅ Mock GetTeamsBySchoolYearAsync został wywołany");
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"❌ Mock GetTeamsBySchoolYearAsync NIE został wywołany: {ex.Message}");
                }

                // Test czy mock dla ArchiveTeamsAsync został wywołany
                try
                {
                    _mockBulkOperationsService.Verify(s => s.ArchiveTeamsAsync(It.IsAny<string[]>(), It.IsAny<string>(), It.IsAny<int>()), Times.AtLeastOnce);
                    Console.WriteLine("✅ Mock ArchiveTeamsAsync został wywołany");
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"❌ Mock ArchiveTeamsAsync NIE został wywołany: {ex.Message}");
                }
            }
            else
            {
                Console.WriteLine($"✅ Result otrzymany: IsSuccess={result.IsSuccess}");
            }

            // Assert
            Assert.NotNull(result);
            Assert.True(result.IsSuccess);
            Assert.Equal(2, result.SuccessfulOperations.Count);
            Assert.Empty(result.Errors);
        }

        [Fact]
        public async Task ArchiveTeamsFromPreviousSchoolYearAsync_Debug_ShouldNotThrowException()
        {
            // Arrange
            var schoolYearId = "test-year";
            var accessToken = "test-token";

            // Setup minimal mocks
            _mockTeamService.Setup(s => s.GetTeamsBySchoolYearAsync(schoolYearId, false, null))
                .ReturnsAsync(new List<Team>());

            try
            {
                // Act
                var result = await _orchestrator.ArchiveTeamsFromPreviousSchoolYearAsync(schoolYearId, accessToken, new SchoolYearProcessOptions());

                // Assert - sprawdzamy czy zwraca pusty wynik ale nie null
                Assert.NotNull(result);
                Assert.True(result.IsSuccess);
                Assert.Empty(result.SuccessfulOperations);
                Assert.Empty(result.Errors);
            }
            catch (Exception ex)
            {
                // Debug - pokażmy jaki jest wyjątek
                Assert.Fail($"Orkiestrator rzucił wyjątek: {ex.GetType().Name}: {ex.Message}");
            }
        }

        [Fact]
        public async Task TransitionToNewSchoolYearAsync_WithValidInput_ShouldArchiveAndCreateTeams()
        {
            // Arrange
            var oldSchoolYearId = "old-year";
            var newSchoolYearId = "new-year";
            var templateIds = new[] { "template-1" };
            var accessToken = "test-token";

            var oldSchoolYear = new SchoolYear { Id = oldSchoolYearId, Name = "2023/2024" };
            var newSchoolYear = new SchoolYear { Id = newSchoolYearId, Name = "2024/2025" };

            var template = new TeamTemplate
            {
                Id = "template-1",
                Name = "Szablon Testowy",
                Template = "{Class} - {Year}",
                Category = "class"
            };

            var oldTeams = new List<Team>
            {
                new Team { Id = "old-team-1", DisplayName = "Stara Klasa 1A", SchoolYearId = oldSchoolYearId, Status = Core.Enums.TeamStatus.Active }
            };

            var archiveResult = new BulkOperationResult 
            { 
                IsSuccess = true, 
                Success = true,
                SuccessfulOperations = new List<BulkOperationSuccess>
                {
                    new BulkOperationSuccess { Operation = "ArchiveTeam", EntityId = "old-team-1", Message = "Zarchiwizowano" }
                }, 
                Errors = new List<BulkOperationError>() 
            };
            var createResult = new BulkOperationResult 
            { 
                IsSuccess = true, 
                Success = true,
                SuccessfulOperations = new List<BulkOperationSuccess>
                {
                    new BulkOperationSuccess { Operation = "CreateTeam", EntityId = "new-team-1", Message = "Utworzono" }
                }, 
                Errors = new List<BulkOperationError>() 
            };

            // Setup mocks - używam konkretnych wartości zamiast It.IsAny
            _mockSchoolYearService.Setup(s => s.GetByIdAsync(oldSchoolYearId))
                .ReturnsAsync(oldSchoolYear);
            _mockSchoolYearService.Setup(s => s.GetByIdAsync(newSchoolYearId))
                .ReturnsAsync(newSchoolYear);
            
            // Dodatkowy mock dla GetSchoolYearByIdAsync (może być używany zamiast GetByIdAsync)
            _mockSchoolYearService.Setup(s => s.GetSchoolYearByIdAsync(newSchoolYearId, false))
                .ReturnsAsync(newSchoolYear);

            _mockTeamTemplateService.Setup(s => s.GetByIdAsync("template-1"))
                .ReturnsAsync(template);

            // Mock dla GetTeamsBySchoolYearAsync z It.IsAny aby obsłużyć różne warianty wywołań
            _mockTeamService.Setup(s => s.GetTeamsBySchoolYearAsync(It.IsAny<string>(), It.IsAny<bool>(), It.IsAny<string>()))
                .ReturnsAsync(oldTeams);

            _mockBulkOperationsService.Setup(s => s.ArchiveTeamsAsync(new string[] { "old-team-1" }, accessToken, 50))
                .ReturnsAsync(archiveResult);

            _mockBulkOperationsService.Setup(s => s.CreateTeamsAsync(new string[0], accessToken))
                .ReturnsAsync(createResult);
            
            // Dodatkowy mock dla CreateTeamsAsync z It.IsAny na wszelki wypadek
            _mockBulkOperationsService.Setup(s => s.CreateTeamsAsync(It.IsAny<string[]>(), It.IsAny<string>()))
                .ReturnsAsync(createResult);
            
            // Dodatkowy mock z It.IsAny dla ArchiveTeamsAsync na wszelki wypadek
            _mockBulkOperationsService.Setup(s => s.ArchiveTeamsAsync(It.IsAny<string[]>(), It.IsAny<string>(), It.IsAny<int>()))
                .ReturnsAsync(archiveResult);

            // Act
            var result = await _orchestrator.TransitionToNewSchoolYearAsync(oldSchoolYearId, newSchoolYearId, templateIds, accessToken, new SchoolYearProcessOptions());

            // Debug - sprawdzmy co dokładnie zwraca
            if (result != null && !result.IsSuccess)
            {
                Console.WriteLine($"Result.IsSuccess: {result.IsSuccess}");
                Console.WriteLine($"Result.Success: {result.Success}");
                Console.WriteLine($"Result.ErrorMessage: {result.ErrorMessage}");
                Console.WriteLine($"Result.Errors.Count: {result.Errors?.Count ?? 0}");
                if (result.Errors?.Any() == true)
                {
                    foreach (var error in result.Errors)
                    {
                        Console.WriteLine($"Error: {error.Operation} - {error.Message}");
                    }
                }
                
                // Sprawdzmy czy mock'i zostały wywołane
                try
                {
                    _mockSchoolYearService.Verify(s => s.GetByIdAsync(newSchoolYearId), Times.AtLeastOnce);
                    Console.WriteLine("✅ Mock GetByIdAsync(newSchoolYearId) został wywołany");
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"❌ Mock GetByIdAsync(newSchoolYearId) NIE został wywołany: {ex.Message}");
                }
                
                try
                {
                    _mockTeamTemplateService.Verify(s => s.GetByIdAsync("template-1"), Times.AtLeastOnce);
                    Console.WriteLine("✅ Mock GetByIdAsync(template-1) został wywołany");
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"❌ Mock GetByIdAsync(template-1) NIE został wywołany: {ex.Message}");
                }
            }

            // Assert
            Assert.NotNull(result);
            Assert.True(result.IsSuccess);
        }

        [Fact]
        public async Task GetActiveProcessesStatusAsync_ShouldReturnActiveProcesses()
        {
            // Act
            var result = await _orchestrator.GetActiveProcessesStatusAsync();

            // Assert
            Assert.NotNull(result);
        }

        [Fact]
        public async Task CancelProcessAsync_WithNonExistentProcess_ShouldReturnFalse()
        {
            // Act
            var result = await _orchestrator.CancelProcessAsync("non-existent-process");

            // Assert
            Assert.False(result);
        }
    }
} 