using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using Moq;
using TeamsManager.Core.Abstractions.Services;
using TeamsManager.Core.Enums;
using TeamsManager.Core.Services;
using Xunit;

namespace TeamsManager.Tests.Services
{
    public class PowerShellServiceTests
    {
        private readonly Mock<ILogger<PowerShellService>> _mockLogger;

        public PowerShellServiceTests()
        {
            _mockLogger = new Mock<ILogger<PowerShellService>>();
        }

        [Fact]
        public void Constructor_ShouldInitializeRunspace_AndLogInformation()
        {
            // Arrange & Act
            // Inicjalizacja Runspace dzieje się w konstruktorze PowerShellService.
            // Tutaj testujemy głównie, czy konstruktor nie rzuca wyjątku i loguje informację.
            // Symulacja rzeczywistej inicjalizacji Runspace jest trudna w teście jednostkowym.
            var service = new PowerShellService(_mockLogger.Object);

            // Assert
            service.Should().NotBeNull();
            _mockLogger.Verify(
                x => x.Log(
                    LogLevel.Information,
                    It.IsAny<EventId>(),
                    It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("Środowisko PowerShell zostało zainicjowane poprawnie")),
                    null,
                    It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
                Times.Once);
        }

        [Fact]
        public void IsConnected_ShouldReturnFalse_ByDefault()
        {
            // Arrange
            var service = new PowerShellService(_mockLogger.Object);

            // Act & Assert
            service.IsConnected.Should().BeFalse();
        }

        // Testy dla ConnectToTeamsAsync są trudne do zrealizowania jako testy jednostkowe
        // bez rzeczywistego środowiska lub bardzo złożonego mockowania Runspace/PowerShell.
        // Można testować ścieżki błędów, np. gdy Runspace nie jest otwarty.

        [Fact]
        public async Task CreateTeamAsync_WhenNotConnected_ShouldReturnNullAndLogError()
        {
            // Arrange
            var service = new PowerShellService(_mockLogger.Object);
            // Upewniamy się, że IsConnected jest false (domyślnie jest)

            // Act
            var result = await service.CreateTeamAsync("Test Team", "Desc", "owner@test.com");

            // Assert
            result.Should().BeNull();
            _mockLogger.Verify(
                x => x.Log(
                    LogLevel.Error,
                    It.IsAny<EventId>(),
                    It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("Nie można utworzyć zespołu: Nie połączono z Teams.")),
                    null,
                    It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
                Times.Once);
        }

        [Theory]
        [InlineData("", "owner@test.com", "Nie można utworzyć zespołu: Nazwa wyświetlana (DisplayName) oraz właściciel (OwnerUpn) są wymagane.")]
        [InlineData("Test Team", "", "Nie można utworzyć zespołu: Nazwa wyświetlana (DisplayName) oraz właściciel (OwnerUpn) są wymagane.")]
        public async Task CreateTeamAsync_WithInvalidParameters_ShouldReturnNullAndLogError(string displayName, string ownerUpn, string expectedLogMessage)
        {
            // Arrange
            var service = new PowerShellService(_mockLogger.Object);

            // Act
            var result = await service.CreateTeamAsync(displayName, "Description", ownerUpn);

            // Assert
            result.Should().BeNull();
            _mockLogger.Verify(
                x => x.Log(
                    LogLevel.Error,
                    It.IsAny<EventId>(),
                    It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains(expectedLogMessage)),
                    null,
                    It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
                Times.Once);
        }

        // Podobnie testy dla innych metod (ArchiveTeamAsync, AddUserToTeamAsync etc.)
        // będą sprawdzać głównie walidację parametrów i zachowanie przy braku połączenia.

        [Fact]
        public async Task AddUserToTeamAsync_WithInvalidRole_ShouldReturnFalseAndLogError()
        {
            var service = new PowerShellService(_mockLogger.Object);
            // Zakładamy, że serwis jest "połączony" dla tego testu, aby sprawdzić logikę roli
            // W rzeczywistości bez mockowania IsConnected, najpierw zaloguje błąd braku połączenia

            var result = await service.AddUserToTeamAsync("teamId", "user@test.com", "InvalidRole");

            // W obecnej implementacji bez mockowania IsConnected, result będzie false z powodu braku połączenia.
            result.Should().BeFalse();
            _mockLogger.Verify(
               x => x.Log(
                   LogLevel.Error,
                   It.IsAny<EventId>(),
                   It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("Nie można dodać użytkownika do zespołu")), // Ogólny komunikat
                   null,
                   It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
               Times.Once);
            // Jeśli _isConnected byłoby true:
            // _mockLogger.Verify(
            //    x => x.Log(
            //        LogLevel.Error,
            //        It.IsAny<EventId>(),
            //        It.Is<It.IsAnyType>((v, t) => v.ToString().Contains("Nieprawidłowa rola 'InvalidRole'")),
            //        null,
            //        It.IsAny<Func<It.IsAnyType, Exception, string>>()),
            //    Times.Once);
        }

        [Fact]
        public void Dispose_WhenCalled_ShouldAttemptToCloseRunspaceAndLog()
        {
            // Arrange
            var service = new PowerShellService(_mockLogger.Object);
            // Symulacja, że runspace został otwarty
            // W idealnym teście jednostkowym mockowalibyśmy Runspace, ale to skomplikowane.
            // Tutaj testujemy głównie, czy Dispose nie rzuca wyjątku i loguje.

            // Act
            service.Dispose();

            // Assert
            _mockLogger.Verify(
                x => x.Log(
                    LogLevel.Information,
                    It.IsAny<EventId>(),
                    It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("Zasoby Runspace zostały zwolnione")),
                    null,
                    It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
                Times.Once);

            // Sprawdzenie czy ponowne wywołanie Dispose nie powoduje problemów
            service.Dispose(); // Powinno być bezpieczne
            _mockLogger.Verify(
               x => x.Log(
                   LogLevel.Information,
                   It.IsAny<EventId>(),
                   It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("Zasoby Runspace zostały zwolnione")),
                   null,
                   It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
                   Times.Once); // Nadal tylko raz, bo _disposed=true
        }

        // Testy dla nowo dodanych metod (CreateM365UserAsync, SetM365UserAccountStateAsync, itd.)
        // będą podobne - sprawdzenie walidacji i zachowania przy braku połączenia.

        [Fact]
        public async Task CreateM365UserAsync_WhenNotConnected_ShouldReturnNull()
        {
            var service = new PowerShellService(_mockLogger.Object);
            var result = await service.CreateM365UserAsync("Test User", "test@user.com", "P@$$wOrd");
            result.Should().BeNull();
        }

        [Fact]
        public async Task SetM365UserAccountStateAsync_WhenNotConnected_ShouldReturnFalse()
        {
            var service = new PowerShellService(_mockLogger.Object);
            var result = await service.SetM365UserAccountStateAsync("test@user.com", true);
            result.Should().BeFalse();
        }
    }
}