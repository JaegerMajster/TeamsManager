using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using TeamsManager.Data;
using TeamsManager.Tests.Infrastructure.Services;
using Xunit;

namespace TeamsManager.Tests.Data
{
    /// <summary>
    /// Testy dla migracji Entity Framework Core
    /// Sprawdza czy migracje wykonują się poprawnie i tworzą odpowiednią strukturę bazy
    /// </summary>
    public class MigrationsTests : IDisposable
    {
        private readonly string _testDbPath;
        private readonly DbContextOptions<TeamsManagerDbContext> _options;

        public MigrationsTests()
        {
            _testDbPath = Path.Combine(Path.GetTempPath(), $"test_migrations_{Guid.NewGuid()}.db");
            
            _options = new DbContextOptionsBuilder<TeamsManagerDbContext>()
                .UseSqlite($"Data Source={_testDbPath}")
                .Options;
        }

        [Fact]
        public async Task Database_Migration_ShouldCreateAllTables()
        {
            // Act
            using var context = new TeamsManagerDbContext(_options, new TestCurrentUserService());
            await context.Database.MigrateAsync();

            // Assert
            var tableNames = new[]
            {
                "Departments",
                "Users", 
                "Teams",
                "TeamMembers",
                "Channels",
                "SchoolTypes",
                "SchoolYears",
                "Subjects",
                "TeamTemplates",
                "ApplicationSettings",
                "OperationHistories",
                "UserSchoolTypes",
                "UserSubjects",
                "OrganizationalUnits"
            };

            var connection = context.Database.GetDbConnection();
            await connection.OpenAsync();
            
            foreach (var tableName in tableNames)
            {
                using var command = connection.CreateCommand();
                command.CommandText = $"SELECT COUNT(*) FROM sqlite_master WHERE type='table' AND name='{tableName}'";
                
                var tableExists = (long)await command.ExecuteScalarAsync();
                tableExists.Should().Be(1, $"tabela {tableName} powinna istnieć");
            }
        }

        [Fact]
        public async Task Database_Migration_ShouldCreateCorrectDepartmentsStructure()
        {
            // Act
            using var context = new TeamsManagerDbContext(_options, new TestCurrentUserService());
            await context.Database.MigrateAsync();

            // Assert
            var columns = await GetTableColumnsAsync(context, "Departments");
            
            columns.Should().Contain("Id");
            columns.Should().Contain("Name");
            columns.Should().Contain("Description");
            columns.Should().Contain("DepartmentCode");
            columns.Should().Contain("Email");
            columns.Should().Contain("Phone");
            columns.Should().Contain("Location");
            columns.Should().Contain("IsActive");
            columns.Should().Contain("CreatedDate");
            columns.Should().Contain("CreatedBy");
            columns.Should().Contain("ModifiedDate");
            columns.Should().Contain("ModifiedBy");
        }

        [Fact]
        public async Task Database_Migration_ShouldCreateCorrectUsersStructure()
        {
            // Act
            using var context = new TeamsManagerDbContext(_options, new TestCurrentUserService());
            await context.Database.MigrateAsync();

            // Assert
            var columns = await GetTableColumnsAsync(context, "Users");
            
            columns.Should().Contain("Id");
            columns.Should().Contain("FirstName");
            columns.Should().Contain("LastName");
            columns.Should().Contain("UPN");
            columns.Should().Contain("Role");
            columns.Should().Contain("DepartmentId");
            columns.Should().Contain("Position");
            columns.Should().Contain("Phone");
            columns.Should().Contain("IsActive");
        }

        [Fact]
        public async Task Database_Migration_ShouldCreateCorrectTeamsStructure()
        {
            // Act
            using var context = new TeamsManagerDbContext(_options, new TestCurrentUserService());
            await context.Database.MigrateAsync();

            // Assert
            var columns = await GetTableColumnsAsync(context, "Teams");
            
            columns.Should().Contain("Id");
            columns.Should().Contain("DisplayName");
            columns.Should().Contain("Description");
            columns.Should().Contain("Owner");
            columns.Should().Contain("Visibility");
            columns.Should().Contain("Status");
            columns.Should().Contain("SchoolTypeId");
            columns.Should().Contain("SchoolYearId");
            columns.Should().Contain("DepartmentId"); // Dodane w migracji AddTeamDepartmentRelation
        }

        [Fact]
        public async Task Database_Migration_ShouldCreateCorrectOrganizationalUnitsStructure()
        {
            // Act
            using var context = new TeamsManagerDbContext(_options, new TestCurrentUserService());
            await context.Database.MigrateAsync();

            // Assert
            var columns = await GetTableColumnsAsync(context, "OrganizationalUnits");
            
            columns.Should().Contain("Id");
            columns.Should().Contain("Name");
            columns.Should().Contain("Description");
            columns.Should().Contain("ParentUnitId");
            columns.Should().Contain("SortOrder");
            columns.Should().Contain("Code"); // Dodane w migracji AddOrganizationalUnitCode
        }

        [Fact]
        public async Task Database_Migration_ShouldCreateForeignKeyRelationships()
        {
            // Act
            using var context = new TeamsManagerDbContext(_options, new TestCurrentUserService());
            await context.Database.MigrateAsync();

            // Assert - sprawdź czy można utworzyć encje z relacjami
            var department = new Core.Models.Department
            {
                Id = Guid.NewGuid().ToString(),
                Name = "Test Department",
                DepartmentCode = "TEST001",
                IsActive = true,
                CreatedDate = DateTime.UtcNow,
                CreatedBy = "Test"
            };

            var user = new Core.Models.User
            {
                Id = Guid.NewGuid().ToString(),
                FirstName = "Test",
                LastName = "User",
                UPN = "test@test.com",
                Role = Core.Enums.UserRole.Nauczyciel,
                DepartmentId = department.Id,
                IsActive = true,
                CreatedDate = DateTime.UtcNow,
                CreatedBy = "Test"
            };

            context.Departments.Add(department);
            context.Users.Add(user);
            
            // Should not throw
            await context.SaveChangesAsync();
            
            var savedUser = await context.Users
                .Include(u => u.Department)
                .FirstAsync(u => u.Id == user.Id);
                
            savedUser.Department.Should().NotBeNull();
            savedUser.Department!.Id.Should().Be(department.Id);
        }

        [Fact]
        public async Task Database_Migration_ShouldHandleEnumColumns()
        {
            // Act
            using var context = new TeamsManagerDbContext(_options, new TestCurrentUserService());
            await context.Database.MigrateAsync();

            // Assert - najpierw utwórz Department
            var department = new Core.Models.Department
            {
                Id = Guid.NewGuid().ToString(),
                Name = "Test Department",
                DepartmentCode = "TEST001",
                IsActive = true,
                CreatedDate = DateTime.UtcNow,
                CreatedBy = "Test"
            };
            context.Departments.Add(department);
            await context.SaveChangesAsync();

            // Teraz utwórz User z poprawnym DepartmentId
            var user = new Core.Models.User
            {
                Id = Guid.NewGuid().ToString(),
                FirstName = "Test",
                LastName = "User", 
                UPN = "test@test.com",
                Role = Core.Enums.UserRole.PracownikAdministracyjny, // Test nowej roli z migracji
                DepartmentId = department.Id, // Ustaw poprawny Department
                IsActive = true,
                CreatedDate = DateTime.UtcNow,
                CreatedBy = "Test"
            };

            context.Users.Add(user);
            await context.SaveChangesAsync();

            var savedUser = await context.Users.FirstAsync(u => u.Id == user.Id);
            savedUser.Role.Should().Be(Core.Enums.UserRole.PracownikAdministracyjny);
        }

        [Fact]
        public async Task Database_Migration_ShouldCreateIndexes()
        {
            // Act
            using var context = new TeamsManagerDbContext(_options, new TestCurrentUserService());
            await context.Database.MigrateAsync();

            // Assert - sprawdź czy istnieją ważne indeksy
            var indexes = await context.Database.SqlQueryRaw<string>(
                "SELECT name FROM sqlite_master WHERE type='index' AND sql IS NOT NULL")
                .ToListAsync();

            // EF Core automatycznie tworzy indeksy dla kluczy obcych
            indexes.Should().Contain(idx => idx.Contains("IX_Users_DepartmentId"));
            indexes.Should().Contain(idx => idx.Contains("IX_Teams_SchoolTypeId"));
            indexes.Should().Contain(idx => idx.Contains("IX_Teams_SchoolYearId"));
        }

        [Fact]
        public async Task Database_Migration_ShouldApplyAllMigrations()
        {
            // Act
            using var context = new TeamsManagerDbContext(_options, new TestCurrentUserService());
            await context.Database.MigrateAsync();

            // Assert
            var appliedMigrations = await context.Database.GetAppliedMigrationsAsync();
            
            appliedMigrations.Should().NotBeEmpty("powinny być zastosowane migracje");
            appliedMigrations.Should().Contain(m => m.Contains("InitialCreate"));
            appliedMigrations.Should().Contain(m => m.Contains("ReplaceTeamIsVisibleWithVisibility"));
            appliedMigrations.Should().Contain(m => m.Contains("AddTeamDepartmentRelation"));
            appliedMigrations.Should().Contain(m => m.Contains("AddOrganizationalUnit"));
            appliedMigrations.Should().Contain(m => m.Contains("AddOrganizationalUnitCode"));
            appliedMigrations.Should().Contain(m => m.Contains("AddSystemDefaultFlags"));
            appliedMigrations.Should().Contain(m => m.Contains("AddPracownikAdministracyjnyRole"));
        }

        [Fact]
        public async Task Database_Migration_ShouldAllowDataOperations()
        {
            // Act
            using var context = new TeamsManagerDbContext(_options, new TestCurrentUserService());
            await context.Database.MigrateAsync();

            // Assert - sprawdź czy można wykonywać podstawowe operacje CRUD
            var department = new Core.Models.Department
            {
                Id = Guid.NewGuid().ToString(),
                Name = "CRUD Test Department",
                DepartmentCode = "CRUD001",
                IsActive = true,
                CreatedDate = DateTime.UtcNow,
                CreatedBy = "Test"
            };

            // Create
            context.Departments.Add(department);
            await context.SaveChangesAsync();

            // Read
            var saved = await context.Departments.FirstAsync(d => d.Id == department.Id);
            saved.Name.Should().Be("CRUD Test Department");

            // Update
            saved.Name = "Updated Department";
            await context.SaveChangesAsync();

            var updated = await context.Departments.FirstAsync(d => d.Id == department.Id);
            updated.Name.Should().Be("Updated Department");

            // Delete
            context.Departments.Remove(updated);
            await context.SaveChangesAsync();

            var exists = await context.Departments.AnyAsync(d => d.Id == department.Id);
            exists.Should().BeFalse();
        }

        [Fact]
        public async Task Database_Migration_ShouldHandleNullableColumns()
        {
            // Act
            using var context = new TeamsManagerDbContext(_options, new TestCurrentUserService());
            await context.Database.MigrateAsync();

            // Assert - sprawdź czy można utworzyć Department bez opcjonalnych pól
            var department = new Core.Models.Department
            {
                Id = Guid.NewGuid().ToString(),
                Name = "Test Department",
                DepartmentCode = "TEST001",
                // Email, Phone, Location są nullable - nie ustawiamy
                IsActive = true,
                CreatedDate = DateTime.UtcNow,
                CreatedBy = "Test"
            };

            context.Departments.Add(department);
            
            // Should not throw
            await context.SaveChangesAsync();

            var savedDepartment = await context.Departments.FirstAsync(d => d.Id == department.Id);
            savedDepartment.Email.Should().BeNull();
            savedDepartment.Phone.Should().BeNull();
            savedDepartment.Location.Should().BeNull();
        }

        private async Task<string[]> GetTableColumnsAsync(TeamsManagerDbContext context, string tableName)
        {
            // Użyj prostego SQL query do pobrania nazw kolumn
            var sql = $"PRAGMA table_info({tableName})";
            var connection = context.Database.GetDbConnection();
            await connection.OpenAsync();
            
            var columnNames = new List<string>();
            using var command = connection.CreateCommand();
            command.CommandText = sql;
            
            using var reader = await command.ExecuteReaderAsync();
            while (await reader.ReadAsync())
            {
                // Kolumna 'name' jest na pozycji 1 w PRAGMA table_info
                columnNames.Add(reader.GetString(1));
            }
            
            return columnNames.ToArray();
        }



        public void Dispose()
        {
            if (File.Exists(_testDbPath))
            {
                try
                {
                    File.Delete(_testDbPath);
                }
                catch
                {
                    // Ignore cleanup errors
                }
            }
        }
    }
} 