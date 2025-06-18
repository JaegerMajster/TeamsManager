using System;
using System.IO;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using TeamsManager.Data;
using Xunit;

namespace TeamsManager.Tests.Data
{
    /// <summary>
    /// Testy jednostkowe dla DesignTimeDbContextFactory
    /// Testuje poprawność tworzenia DbContext w czasie projektowania
    /// </summary>
    public class DesignTimeDbContextFactoryTests : IDisposable
    {
        private readonly DesignTimeDbContextFactory _factory;
        private readonly string _testAppDataPath;
        private readonly string _testDbPath;

        public DesignTimeDbContextFactoryTests()
        {
            _factory = new DesignTimeDbContextFactory();
            
            // Ustaw ścieżkę testową w katalogu tymczasowym
            _testAppDataPath = Path.Combine(Path.GetTempPath(), "TeamsManagerTests", Guid.NewGuid().ToString());
            _testDbPath = Path.Combine(_testAppDataPath, "TeamsManager", "teamsmanager.db");
        }

        [Fact]
        public void CreateDbContext_ShouldReturnValidDbContext()
        {
            // Act
            using var context = _factory.CreateDbContext(Array.Empty<string>());

            // Assert
            context.Should().NotBeNull();
            context.Should().BeOfType<TeamsManagerDbContext>();
        }

        [Fact]
        public void CreateDbContext_ShouldConfigureSqliteProvider()
        {
            // Act
            using var context = _factory.CreateDbContext(Array.Empty<string>());

            // Assert
            var connectionString = context.Database.GetConnectionString();
            connectionString.Should().NotBeNullOrEmpty();
            connectionString.Should().StartWith("Data Source=");
            connectionString.Should().Contain("teamsmanager.db");
        }

        [Fact]
        public void CreateDbContext_ShouldCreateApplicationFolder()
        {
            // Arrange - upewnij się, że folder nie istnieje
            var appDataPath = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
            var appFolderPath = Path.Combine(appDataPath, "TeamsManager");

            // Act
            using var context = _factory.CreateDbContext(Array.Empty<string>());

            // Assert
            Directory.Exists(appFolderPath).Should().BeTrue("folder aplikacji powinien być utworzony");
        }

        [Fact]
        public void CreateDbContext_ShouldUseLocalApplicationDataPath()
        {
            // Act
            using var context = _factory.CreateDbContext(Array.Empty<string>());

            // Assert
            var connectionString = context.Database.GetConnectionString();
            var appDataPath = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
            
            connectionString.Should().Contain(appDataPath);
        }

        [Fact]
        public void CreateDbContext_WithArguments_ShouldIgnoreArguments()
        {
            // Arrange
            var args = new[] { "--test", "value" };

            // Act
            using var context1 = _factory.CreateDbContext(Array.Empty<string>());
            using var context2 = _factory.CreateDbContext(args);

            // Assert
            var connectionString1 = context1.Database.GetConnectionString();
            var connectionString2 = context2.Database.GetConnectionString();
            
            connectionString1.Should().Be(connectionString2, 
                "argumenty nie powinny wpływać na konfigurację");
        }

        [Fact]
        public void CreateDbContext_ShouldCreateValidDbContextOptions()
        {
            // Act
            using var context = _factory.CreateDbContext(Array.Empty<string>());

            // Assert
            var options = context.Database.GetDbConnection();
            options.Should().NotBeNull();
            options.GetType().Name.Should().Contain("Sqlite");
        }

        [Fact]
        public void CreateDbContext_CalledMultipleTimes_ShouldReturnSeparateInstances()
        {
            // Act
            using var context1 = _factory.CreateDbContext(Array.Empty<string>());
            using var context2 = _factory.CreateDbContext(Array.Empty<string>());

            // Assert
            context1.Should().NotBeSameAs(context2, "każde wywołanie powinno zwrócić nową instancję");
            
            var connectionString1 = context1.Database.GetConnectionString();
            var connectionString2 = context2.Database.GetConnectionString();
            connectionString1.Should().Be(connectionString2, "ale connection string powinien być taki sam");
        }

        [Fact]
        public void CreateDbContext_ShouldConfigureContextForMigrations()
        {
            // Act
            using var context = _factory.CreateDbContext(Array.Empty<string>());

            // Assert
            // Sprawdź czy context może być użyty do migracji
            var model = context.Model;
            model.Should().NotBeNull();
            
            // Sprawdź czy wszystkie główne entity są skonfigurowane
            var userEntity = model.FindEntityType(typeof(Core.Models.User));
            var teamEntity = model.FindEntityType(typeof(Core.Models.Team));
            var departmentEntity = model.FindEntityType(typeof(Core.Models.Department));
            
            userEntity.Should().NotBeNull();
            teamEntity.Should().NotBeNull();
            departmentEntity.Should().NotBeNull();
        }

        [Fact]
        public void CreateDbContext_ShouldHandleDirectoryCreationGracefully()
        {
            // Arrange - symuluj sytuację gdy folder już istnieje
            var appDataPath = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
            var appFolderPath = Path.Combine(appDataPath, "TeamsManager");
            
            // Upewnij się, że folder istnieje
            Directory.CreateDirectory(appFolderPath);

            // Act & Assert - nie powinno rzucić wyjątku
            using var context = _factory.CreateDbContext(Array.Empty<string>());
            context.Should().NotBeNull();
        }

        public void Dispose()
        {
            // Cleanup - usuń testowe katalogi jeśli zostały utworzone
            if (Directory.Exists(_testAppDataPath))
            {
                try
                {
                    Directory.Delete(_testAppDataPath, true);
                }
                catch
                {
                    // Ignore cleanup errors
                }
            }
        }
    }
} 