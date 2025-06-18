using System;
using System.Linq;
using System.Threading.Tasks;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using TeamsManager.Core.Enums;
using TeamsManager.Core.Models;
using TeamsManager.Data;
using TeamsManager.Tests.Integration;
using Xunit;

namespace TeamsManager.Tests.Data
{
    /// <summary>
    /// Testy jednostkowe dla TestDataSeeder
    /// Testuje poprawność seedowania przykładowych danych
    /// </summary>
    public class TestDataSeederTests : IntegrationTestBase
    {
        [Fact]
        public async Task SeedAsync_WithEmptyDatabase_ShouldCreateAllTestData()
        {
            // Arrange - baza jest pusta po utworzeniu

            // Act
            await TestDataSeeder.SeedAsync(Context);

            // Assert
            var departments = await Context.Departments.ToListAsync();
            var users = await Context.Users.ToListAsync();
            var schoolTypes = await Context.SchoolTypes.ToListAsync();
            var schoolYears = await Context.SchoolYears.ToListAsync();
            var teams = await Context.Teams.ToListAsync();
            var teamMembers = await Context.TeamMembers.ToListAsync();
            var channels = await Context.Channels.ToListAsync();
            var operationHistories = await Context.OperationHistories.ToListAsync();

            departments.Should().HaveCount(2, "powinny być utworzone 2 działy");
            users.Should().HaveCount(13, "powinno być utworzonych 13 użytkowników (12 aktywnych + 1 nieaktywny)");
            schoolTypes.Should().HaveCount(2, "powinny być utworzone 2 typy szkół");
            schoolYears.Should().HaveCount(1, "powinien być utworzony 1 rok szkolny");
            teams.Should().HaveCount(1, "powinien być utworzony 1 zespół");
            teamMembers.Should().HaveCount(1, "powinien być utworzony 1 członek zespołu");
            channels.Should().HaveCount(1, "powinien być utworzony 1 kanał");
            operationHistories.Should().HaveCount(1, "powinna być utworzona 1 historia operacji");
        }

        [Fact]
        public async Task SeedAsync_WithEmptyDatabase_ShouldCreateCorrectDepartments()
        {
            // Act
            await TestDataSeeder.SeedAsync(Context);

            // Assert
            var departments = await Context.Departments.ToListAsync();
            
            var itDepartment = departments.FirstOrDefault(d => d.DepartmentCode == "IT001");
            var mathDepartment = departments.FirstOrDefault(d => d.DepartmentCode == "MATH001");

            itDepartment.Should().NotBeNull();
            itDepartment!.Name.Should().Be("IT");
            itDepartment.Description.Should().Be("Dział informatyki");
            itDepartment.Email.Should().Be("it@school.edu.pl");
            itDepartment.IsActive.Should().BeTrue();

            mathDepartment.Should().NotBeNull();
            mathDepartment!.Name.Should().Be("Matematyka");
            mathDepartment.Description.Should().Be("Dział matematyki");
            mathDepartment.Email.Should().Be("math@school.edu.pl");
            mathDepartment.IsActive.Should().BeTrue();
        }

        [Fact]
        public async Task SeedAsync_WithEmptyDatabase_ShouldCreateCorrectSchoolTypes()
        {
            // Act
            await TestDataSeeder.SeedAsync(Context);

            // Assert
            var schoolTypes = await Context.SchoolTypes.OrderBy(st => st.SortOrder).ToListAsync();
            
            var primarySchool = schoolTypes.FirstOrDefault(st => st.ShortName == "SP");
            var highSchool = schoolTypes.FirstOrDefault(st => st.ShortName == "LO");

            primarySchool.Should().NotBeNull();
            primarySchool!.FullName.Should().Be("Szkoła Podstawowa");
            primarySchool.ColorCode.Should().Be("#4CAF50");
            primarySchool.SortOrder.Should().Be(1);

            highSchool.Should().NotBeNull();
            highSchool!.FullName.Should().Be("Liceum Ogólnokształcące");
            highSchool.ColorCode.Should().Be("#2196F3");
            highSchool.SortOrder.Should().Be(2);
        }

        [Fact]
        public async Task SeedAsync_WithEmptyDatabase_ShouldCreateCorrectUsers()
        {
            // Act
            await TestDataSeeder.SeedAsync(Context);

            // Assert
            var users = await Context.Users.Include(u => u.Department).ToListAsync();
            
            var adminUser = users.FirstOrDefault(u => u.UPN == "jan.kowalski@school.edu.pl");
            var teacherUser = users.FirstOrDefault(u => u.UPN == "anna.nowak@school.edu.pl");
            var studentUser = users.FirstOrDefault(u => u.UPN == "marek.testowy@school.edu.pl");
            var inactiveUser = users.FirstOrDefault(u => u.UPN == "nieaktywny.uzytkownik@school.edu.pl");

            // Sprawdź administratora
            adminUser.Should().NotBeNull();
            adminUser!.Role.Should().Be(UserRole.Dyrektor);
            adminUser.FirstName.Should().Be("Jan");
            adminUser.LastName.Should().Be("Kowalski");
            adminUser.IsActive.Should().BeTrue();
            adminUser.Department!.DepartmentCode.Should().Be("IT001");

            // Sprawdź nauczyciela
            teacherUser.Should().NotBeNull();
            teacherUser!.Role.Should().Be(UserRole.Nauczyciel);
            teacherUser.FirstName.Should().Be("Anna");
            teacherUser.LastName.Should().Be("Nowak");
            teacherUser.IsActive.Should().BeTrue();
            teacherUser.Department!.DepartmentCode.Should().Be("MATH001");

            // Sprawdź ucznia
            studentUser.Should().NotBeNull();
            studentUser!.Role.Should().Be(UserRole.Uczen);
            studentUser.IsActive.Should().BeTrue();

            // Sprawdź nieaktywnego użytkownika
            inactiveUser.Should().NotBeNull();
            inactiveUser!.IsActive.Should().BeFalse();
        }

        [Fact]
        public async Task SeedAsync_WithEmptyDatabase_ShouldCreateCurrentSchoolYear()
        {
            // Act
            await TestDataSeeder.SeedAsync(Context);

            // Assert
            var schoolYear = await Context.SchoolYears.FirstAsync();
            
            schoolYear.Name.Should().Be("2024/2025");
            schoolYear.IsCurrent.Should().BeTrue();
            schoolYear.StartDate.Should().Be(new DateTime(2024, 9, 1));
            schoolYear.EndDate.Should().Be(new DateTime(2025, 6, 30));
            schoolYear.IsActive.Should().BeTrue();
        }

        [Fact]
        public async Task SeedAsync_WithEmptyDatabase_ShouldCreateTeamWithCorrectStructure()
        {
            // Act
            await TestDataSeeder.SeedAsync(Context);

            // Assert
            var team = await Context.Teams
                .Include(t => t.SchoolType)
                .Include(t => t.SchoolYear)
                .FirstAsync();

            team.DisplayName.Should().Be("Matematyka - Klasa 1A - 2024/2025");
            team.Visibility.Should().Be(TeamVisibility.Public);
            team.Status.Should().Be(TeamStatus.Active);
            team.SchoolType!.ShortName.Should().Be("SP");
            team.SchoolYear!.Name.Should().Be("2024/2025");
            team.Owner.Should().Be("anna.nowak@school.edu.pl");
        }

        [Fact]
        public async Task SeedAsync_WithEmptyDatabase_ShouldCreateTeamMemberWithOwnerRole()
        {
            // Act
            await TestDataSeeder.SeedAsync(Context);

            // Assert
            var teamMember = await Context.TeamMembers
                .Include(tm => tm.Team)
                .Include(tm => tm.User)
                .FirstAsync();

            teamMember.Role.Should().Be(TeamMemberRole.Owner);
            teamMember.IsApproved.Should().BeTrue();
            teamMember.IsActive.Should().BeTrue();
            teamMember.User!.UPN.Should().Be("anna.nowak@school.edu.pl");
        }

        [Fact]
        public async Task SeedAsync_WithEmptyDatabase_ShouldCreateGeneralChannel()
        {
            // Act
            await TestDataSeeder.SeedAsync(Context);

            // Assert
            var channel = await Context.Channels
                .Include(c => c.Team)
                .FirstAsync();

            channel.DisplayName.Should().Be("Ogólny");
            channel.IsGeneral.Should().BeTrue();
            channel.IsPrivate.Should().BeFalse();
            channel.Status.Should().Be(ChannelStatus.Active);
            channel.ChannelType.Should().Be("Standard");
        }

        [Fact]
        public async Task SeedAsync_WithEmptyDatabase_ShouldCreateOperationHistory()
        {
            // Act
            await TestDataSeeder.SeedAsync(Context);

            // Assert
            var operation = await Context.OperationHistories.FirstAsync();

            operation.Type.Should().Be(OperationType.TeamCreated);
            operation.Status.Should().Be(OperationStatus.Completed);
            operation.TargetEntityType.Should().Be("Team");
            operation.OperationDetails.Should().Be("Utworzono zespół dla klasy 1A");
            operation.Duration.Should().Be(TimeSpan.FromMinutes(5));
        }

        [Fact]
        public async Task SeedAsync_WithExistingData_ShouldNotDuplicateData()
        {
            // Arrange - dodaj istniejący dział
            var existingDepartment = new Department
            {
                Id = Guid.NewGuid().ToString(),
                Name = "Existing Department",
                DepartmentCode = "EXIST001",
                IsActive = true,
                CreatedDate = DateTime.UtcNow,
                CreatedBy = "Test"
            };
            Context.Departments.Add(existingDepartment);
            await Context.SaveChangesAsync();

            // Act
            await TestDataSeeder.SeedAsync(Context);

            // Assert
            var departments = await Context.Departments.ToListAsync();
            departments.Should().HaveCount(1, "nie powinny być dodane nowe działy gdy już istnieją dane");
            departments.First().Name.Should().Be("Existing Department");
        }

        [Fact]
        public async Task SeedAsync_WithEmptyDatabase_ShouldCreateActiveAndInactiveUsers()
        {
            // Act
            await TestDataSeeder.SeedAsync(Context);

            // Assert
            var activeUsers = await Context.Users.Where(u => u.IsActive).ToListAsync();
            var inactiveUsers = await Context.Users.Where(u => !u.IsActive).ToListAsync();

            activeUsers.Should().HaveCount(12, "powinno być 12 aktywnych użytkowników");
            inactiveUsers.Should().HaveCount(1, "powinien być 1 nieaktywny użytkownik");
            
            inactiveUsers.First().FirstName.Should().Be("Nieaktywny");
        }

        [Fact]
        public async Task SeedAsync_WithEmptyDatabase_ShouldCreateUsersWithDifferentRoles()
        {
            // Act
            await TestDataSeeder.SeedAsync(Context);

            // Assert
            var users = await Context.Users.ToListAsync();
            
            var roleGroups = users.GroupBy(u => u.Role).ToList();
            
            users.Count(u => u.Role == UserRole.Dyrektor).Should().Be(1);
            users.Count(u => u.Role == UserRole.Wicedyrektor).Should().Be(1);
            users.Count(u => u.Role == UserRole.Administrator).Should().Be(1);
            users.Count(u => u.Role == UserRole.PracownikAdministracyjny).Should().Be(1);
            users.Count(u => u.Role == UserRole.Nauczyciel).Should().Be(6); // 5 aktywnych + 1 nieaktywny
            users.Count(u => u.Role == UserRole.Uczen).Should().Be(3);
        }

        [Fact]
        public async Task SeedAsync_WithEmptyDatabase_ShouldSetCorrectAuditFields()
        {
            // Act
            await TestDataSeeder.SeedAsync(Context);

            // Assert
            var allEntities = new object[]
            {
                await Context.Departments.FirstAsync(),
                await Context.Users.FirstAsync(),
                await Context.SchoolTypes.FirstAsync(),
                await Context.SchoolYears.FirstAsync(),
                await Context.Teams.FirstAsync(),
                await Context.TeamMembers.FirstAsync(),
                await Context.Channels.FirstAsync(),
                await Context.OperationHistories.FirstAsync()
            };

            foreach (var entity in allEntities.OfType<BaseEntity>())
            {
                entity.CreatedBy.Should().Be("System");
                entity.CreatedDate.Should().BeCloseTo(DateTime.Now, TimeSpan.FromMinutes(1));
                entity.IsActive.Should().BeTrue();
            }
        }

        [Fact]
        public async Task SeedAsync_WithEmptyDatabase_ShouldCreateValidRelationships()
        {
            // Act
            await TestDataSeeder.SeedAsync(Context);

            // Assert
            // Sprawdź relacje User -> Department
            var usersWithDepartments = await Context.Users
                .Include(u => u.Department)
                .Where(u => u.IsActive)
                .ToListAsync();

            usersWithDepartments.Should().OnlyContain(u => u.Department != null);

            // Sprawdź relacje Team -> SchoolType i SchoolYear
            var teamWithRelations = await Context.Teams
                .Include(t => t.SchoolType)
                .Include(t => t.SchoolYear)
                .FirstAsync();

            teamWithRelations.SchoolType.Should().NotBeNull();
            teamWithRelations.SchoolYear.Should().NotBeNull();

            // Sprawdź relacje TeamMember -> Team i User
            var teamMemberWithRelations = await Context.TeamMembers
                .Include(tm => tm.Team)
                .Include(tm => tm.User)
                .FirstAsync();

            teamMemberWithRelations.Team.Should().NotBeNull();
            teamMemberWithRelations.User.Should().NotBeNull();

            // Sprawdź relację Channel -> Team
            var channelWithTeam = await Context.Channels
                .Include(c => c.Team)
                .FirstAsync();

            channelWithTeam.Team.Should().NotBeNull();
        }
    }
} 