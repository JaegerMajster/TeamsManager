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
        // Mockowane zależności
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

        // Testowany serwis
        private readonly TeamService _teamService;

        // Dane wspólne dla testów
        private readonly string _testUserUpn = "test.admin@example.com";
        private User _testOwnerUser;
        private OperationHistory _capturedOperationHistory = null!; // Użycie ! dla inicjalizacji w Setup

        public TeamServiceTests()
        {
            // Inicjalizacja mocków
            _mockTeamRepository = new Mock<ITeamRepository>();
            _mockUserRepository = new Mock<IUserRepository>();
            _mockTeamMemberRepository = new Mock<IGenericRepository<TeamMember>>();
            _mockTeamTemplateRepository = new Mock<ITeamTemplateRepository>();
            _mockOperationHistoryRepository = new Mock<IOperationHistoryRepository>();
            _mockCurrentUserService = new Mock<ICurrentUserService>();
            _mockPowerShellService = new Mock<IPowerShellService>(); // <<<--- ZMIANA TUTAJ
            _mockLogger = new Mock<ILogger<TeamService>>();
            _mockSchoolTypeRepository = new Mock<IGenericRepository<SchoolType>>();
            _mockSchoolYearRepository = new Mock<ISchoolYearRepository>();


            // Konfiguracja domyślnych zachowań mocków
            _mockCurrentUserService.Setup(s => s.GetCurrentUserUpn()).Returns(_testUserUpn);

            // Konfiguracja przechwytywania OperationHistory
            _mockOperationHistoryRepository.Setup(r => r.AddAsync(It.IsAny<OperationHistory>()))
                .Callback<OperationHistory>(op => _capturedOperationHistory = op)
                .Returns(Task.CompletedTask);
            _mockOperationHistoryRepository.Setup(r => r.Update(It.IsAny<OperationHistory>()))
                .Callback<OperationHistory>(op => _capturedOperationHistory = op);


            // Inicjalizacja testowanego serwisu
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

            // Przygotowanie wspólnych danych testowych
            _testOwnerUser = new User { Id = "owner1", UPN = _testUserUpn, FirstName = "Test", LastName = "Owner", IsActive = true, Role = UserRole.Nauczyciel };
        }

        // Tutaj będziemy dodawać metody testowe, np. dla CreateTeamAsync

        [Fact]
        public async Task CreateTeamAsync_ValidInputAndOwnerExists_ShouldReturnNewTeamAndLogOperation()
        {
            // Arrange
            var displayName = "Nowy Zespół Testowy";
            var description = "Opis nowego zespołu";
            var ownerUpn = _testOwnerUser.UPN;
            string? templateId = null;
            var expectedExternalId = $"sim_ext_{Guid.NewGuid()}"; // Oczekiwane ID z PowerShell

            _mockUserRepository.Setup(r => r.GetUserByUpnAsync(ownerUpn)).ReturnsAsync(_testOwnerUser);
            _mockTeamRepository.Setup(r => r.AddAsync(It.IsAny<Team>())).Returns(Task.CompletedTask);

            // Konfiguracja mocka IPowerShellService
            _mockPowerShellService.Setup(p => p.CreateTeamAsync(
                    It.Is<string>(s => s == displayName || s.StartsWith(displayName)), // Akceptuj finalDisplayName jeśli różni się od displayName
                    description,
                    ownerUpn,
                    It.IsAny<string>(), // visibility
                    It.IsAny<string?>())) // template
                .ReturnsAsync(expectedExternalId);


            // Act
            var result = await _teamService.CreateTeamAsync(displayName, description, ownerUpn, templateId);

            // Assert
            result.Should().NotBeNull();
            // result!.DisplayName może być różne od 'displayName' jeśli szablon był użyty
            // Lepiej sprawdzić, czy zawiera 'displayName' lub jest zgodne z logiką szablonu
            result!.Owner.Should().Be(ownerUpn);
            result.ExternalId.Should().Be(expectedExternalId); // Sprawdź, czy ExternalId zostało ustawione
            result.Members.Should().HaveCount(1);
            result.Members.First().UserId.Should().Be(_testOwnerUser.Id);
            result.Members.First().Role.Should().Be(_testOwnerUser.DefaultTeamRole);

            _mockTeamRepository.Verify(r => r.AddAsync(It.Is<Team>(t => t.Owner == ownerUpn)), Times.Once);
            _mockPowerShellService.Verify(p => p.CreateTeamAsync(
                It.IsAny<string>(), // Dokładna nazwa może zależeć od szablonu
                description,
                ownerUpn,
                It.IsAny<string>(),
                It.IsAny<string?>()), Times.Once);

            _capturedOperationHistory.Should().NotBeNull();
            _capturedOperationHistory.Type.Should().Be(OperationType.TeamCreated);
            // _capturedOperationHistory.TargetEntityName.Should().Be(result.DisplayName); // Nazwa po przetworzeniu przez szablon
            _capturedOperationHistory.Status.Should().Be(OperationStatus.Completed);
            _capturedOperationHistory.TargetEntityId.Should().Be(result.Id);
        }

        // TODO: Dodać więcej testów dla CreateTeamAsync (np. nieistniejący właściciel, pusty displayname, użycie szablonu, błąd PowerShell)
        // TODO: Dodać testy dla GetAllTeamsAsync, GetTeamByIdAsync
        // TODO: Dodać testy dla UpdateTeamAsync
        // TODO: Dodać testy dla ArchiveTeamAsync, RestoreTeamAsync, DeleteTeamAsync
        // TODO: Dodać testy dla AddMemberAsync, RemoveMemberAsync
    }
}