using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using TeamsManager.Core.Enums;
using TeamsManager.Core.Models;
using TeamsManager.Data.Repositories;
using Xunit;

namespace TeamsManager.Tests.Repositories
{
    [Collection("Sequential")]
    public class TeamRepositoryTests : RepositoryTestBase
    {
        private readonly TeamRepository _repository;

        public TeamRepositoryTests()
        {
            _repository = new TeamRepository(Context);
        }

        [Fact]
        public async Task AddAsync_ShouldAddTeamToDatabase()
        {
            // Przygotowanie
            await CleanDatabaseAsync();
            var team = new Team
            {
                Id = Guid.NewGuid().ToString(),
                DisplayName = "Test Team",
                Description = "Test Description",
                Owner = "owner@test.com",
                Status = TeamStatus.Active, // Ustawiamy Status
                Visibility = TeamVisibility.Private
                // IsActive jest teraz obliczane
            };

            // Działanie
            await _repository.AddAsync(team);
            await SaveChangesAsync(); // To ustawi pola audytu

            // Weryfikacja
            var savedTeam = await Context.Teams.FirstOrDefaultAsync(t => t.Id == team.Id);
            savedTeam.Should().NotBeNull();
            savedTeam!.DisplayName.Should().Be("Test Team");
            savedTeam.Status.Should().Be(TeamStatus.Active);
            savedTeam.IsActive.Should().BeTrue(); // Sprawdzenie obliczeniowego IsActive
            savedTeam.CreatedBy.Should().Be("test_user_integration_base_default"); // Oczekujemy użytkownika z TestDbContext
        }

        [Fact]
        public async Task GetByIdAsync_ShouldReturnCorrectTeam_WithIncludes()
        {
            // Przygotowanie
            await CleanDatabaseAsync();
            var schoolType = new SchoolType { Id = Guid.NewGuid().ToString(), ShortName = "LO", FullName = "Liceum Ogólnokształcące", IsActive = true };
            var schoolYear = new SchoolYear { Id = Guid.NewGuid().ToString(), Name = "2024/2025", StartDate = new DateTime(2024, 9, 1), EndDate = new DateTime(2025, 6, 30), IsCurrent = true, IsActive = true };
            var template = new TeamTemplate { Id = Guid.NewGuid().ToString(), Name = "Test Template", Template = "{SchoolType} {Class} - {Subject}", SchoolTypeId = schoolType.Id, IsActive = true };
            await Context.SchoolTypes.AddAsync(schoolType);
            await Context.SchoolYears.AddAsync(schoolYear);
            await Context.TeamTemplates.AddAsync(template);
            await SaveChangesAsync();

            var team = new Team
            {
                Id = Guid.NewGuid().ToString(),
                DisplayName = "LO 1A - Matematyka",
                Owner = "teacher@test.com",
                Status = TeamStatus.Active, // Ustawiamy Status
                SchoolTypeId = schoolType.Id,
                SchoolYearId = schoolYear.Id,
                TemplateId = template.Id
            };
            await Context.Teams.AddAsync(team);
            await SaveChangesAsync();

            var user = new User { Id = Guid.NewGuid().ToString(), FirstName = "Jan", LastName = "Kowalski", UPN = "jan.kowalski@test.com", Role = UserRole.Uczen, IsActive = true };
            var department = await Context.Departments.FirstOrDefaultAsync(d => d.Name == "Dział dla Użytkowników Testowych")
                ?? new Department { Id = Guid.NewGuid().ToString(), Name = "Dział dla Użytkowników Testowych", IsActive = true };
            if (!Context.Departments.Local.Any(d => d.Id == department.Id)) await Context.Departments.AddAsync(department); // Dodaj jeśli nie istnieje
            await SaveChangesAsync();
            user.DepartmentId = department.Id;
            await Context.Users.AddAsync(user);
            await SaveChangesAsync();


            var teamMember = new TeamMember { Id = Guid.NewGuid().ToString(), TeamId = team.Id, UserId = user.Id, Role = TeamMemberRole.Member, AddedDate = DateTime.UtcNow, IsActive = true };
            var channel = new Channel { Id = Guid.NewGuid().ToString(), DisplayName = "General", TeamId = team.Id, Status = ChannelStatus.Active, IsGeneral = true }; // Kanał też ma Status
            await Context.TeamMembers.AddAsync(teamMember);
            await Context.Channels.AddAsync(channel);
            await SaveChangesAsync();

            // Działanie
            var result = await _repository.GetByIdAsync(team.Id);

            // Weryfikacja
            result.Should().NotBeNull();
            result!.DisplayName.Should().Be("LO 1A - Matematyka");
            result.Status.Should().Be(TeamStatus.Active);
            result.IsActive.Should().BeTrue();
            result.SchoolType.Should().NotBeNull();
            result.Members.Should().HaveCount(1);
            result.Channels.Should().HaveCount(1);
            result.CreatedBy.Should().Be("test_user_integration_base_default");
        }

        [Fact]
        public async Task GetTeamByNameAsync_ShouldReturnCorrectTeam_WhenActive()
        {
            // Przygotowanie
            await CleanDatabaseAsync();
            var teamName = "Unique Active Team Name";
            var team = CreateTeam(teamName, TeamStatus.Active, TeamVisibility.Private); // Pomocnik używa Status
            await Context.Teams.AddAsync(team);
            await SaveChangesAsync();

            // Działanie
            var result = await _repository.GetTeamByNameAsync(teamName);

            // Weryfikacja
            result.Should().NotBeNull();
            result!.DisplayName.Should().Be(teamName);
            result.Status.Should().Be(TeamStatus.Active);
            result.IsActive.Should().BeTrue();
        }

        [Fact]
        public async Task GetTeamByNameAsync_ShouldReturnCorrectTeam_WhenArchived()
        {
            // Przygotowanie
            await CleanDatabaseAsync();
            var teamName = "Unique Archived Team Name";
            // GetTeamByNameAsync nie filtruje po statusie
            var team = CreateTeam(teamName, TeamStatus.Archived, TeamVisibility.Private);
            await Context.Teams.AddAsync(team);
            await SaveChangesAsync();

            // Działanie
            var result = await _repository.GetTeamByNameAsync(teamName);

            // Weryfikacja
            result.Should().NotBeNull();
            result!.DisplayName.Should().Be(teamName); // Nazwa w bazie jest bez prefiksu
            result.Status.Should().Be(TeamStatus.Archived);
            result.IsActive.Should().BeFalse();
        }

        [Fact]
        public async Task GetActiveTeamByNameAsync_ShouldReturnNull_WhenTeamIsArchived()
        {
            // Przygotowanie
            await CleanDatabaseAsync();
            var teamName = "Test Archived Team For Active Method";
            var team = CreateTeam(teamName, TeamStatus.Archived, TeamVisibility.Private);
            await Context.Teams.AddAsync(team);
            await SaveChangesAsync();

            // Działanie
            var result = await _repository.GetActiveTeamByNameAsync(teamName);

            // Weryfikacja
            result.Should().BeNull("metoda powinna zwrócić null dla zespołu archiwalnego");
        }

        [Fact]
        public async Task GetActiveTeamByNameAsync_ShouldReturnTeam_WhenTeamIsActive()
        {
            // Przygotowanie
            await CleanDatabaseAsync();
            var teamName = "Test Active Team For Active Method";
            var team = CreateTeam(teamName, TeamStatus.Active, TeamVisibility.Private);
            await Context.Teams.AddAsync(team);
            await SaveChangesAsync();

            // Działanie
            var result = await _repository.GetActiveTeamByNameAsync(teamName);

            // Weryfikacja
            result.Should().NotBeNull();
            result!.DisplayName.Should().Be(teamName);
            result.Status.Should().Be(TeamStatus.Active);
            result.IsActive.Should().BeTrue();
        }

        [Fact]
        public async Task GetActiveTeamByNameAsync_ShouldIncludeAllRelations()
        {
            // Przygotowanie
            await CleanDatabaseAsync();
            var schoolType = new SchoolType { Id = Guid.NewGuid().ToString(), ShortName = "LO", FullName = "Liceum Ogólnokształcące", IsActive = true };
            var schoolYear = new SchoolYear { Id = Guid.NewGuid().ToString(), Name = "2024/2025", StartDate = new DateTime(2024, 9, 1), EndDate = new DateTime(2025, 6, 30), IsCurrent = true, IsActive = true };
            var template = new TeamTemplate { Id = Guid.NewGuid().ToString(), Name = "Test Template", Template = "{SchoolType} {Class}", SchoolTypeId = schoolType.Id, IsActive = true };
            
            await Context.SchoolTypes.AddAsync(schoolType);
            await Context.SchoolYears.AddAsync(schoolYear);
            await Context.TeamTemplates.AddAsync(template);
            await SaveChangesAsync();

            var teamName = "Active Team With Relations";
            var team = new Team
            {
                Id = Guid.NewGuid().ToString(),
                DisplayName = teamName,
                Owner = "teacher@test.com",
                Status = TeamStatus.Active,
                SchoolTypeId = schoolType.Id,
                SchoolYearId = schoolYear.Id,
                TemplateId = template.Id
            };
            await Context.Teams.AddAsync(team);
            await SaveChangesAsync();

            // Działanie
            var result = await _repository.GetActiveTeamByNameAsync(teamName);

            // Weryfikacja
            result.Should().NotBeNull();
            result!.SchoolType.Should().NotBeNull();
            result.SchoolType!.ShortName.Should().Be("LO");
            result.SchoolYear.Should().NotBeNull();
            result.Template.Should().NotBeNull();
        }

        [Fact]
        public async Task GetActiveByIdAsync_ShouldReturnNull_WhenTeamIsArchived()
        {
            // Przygotowanie
            await CleanDatabaseAsync();
            var team = CreateTeam("Archived Team By Id", TeamStatus.Archived, TeamVisibility.Private);
            await Context.Teams.AddAsync(team);
            await SaveChangesAsync();

            // Działanie
            var result = await _repository.GetActiveByIdAsync(team.Id);

            // Weryfikacja
            result.Should().BeNull("metoda powinna zwrócić null dla zespołu archiwalnego");
        }

        [Fact]
        public async Task GetActiveByIdAsync_ShouldReturnTeam_WhenTeamIsActive()
        {
            // Przygotowanie
            await CleanDatabaseAsync();
            var team = CreateTeam("Active Team By Id", TeamStatus.Active, TeamVisibility.Private);
            await Context.Teams.AddAsync(team);
            await SaveChangesAsync();

            // Działanie
            var result = await _repository.GetActiveByIdAsync(team.Id);

            // Weryfikacja
            result.Should().NotBeNull();
            result!.DisplayName.Should().Be("Active Team By Id");
            result.Status.Should().Be(TeamStatus.Active);
            result.IsActive.Should().BeTrue();
        }


        [Fact]
        public async Task GetTeamsByOwnerAsync_ShouldReturnOnlyActiveStatusTeams()
        {
            // Przygotowanie
            await CleanDatabaseAsync();
            var owner1 = "owner1@test.com";
            var owner2 = "owner2@test.com";

            var teams = new List<Team>
            {
                CreateTeamWithOwner("Team 1 Owner1 Active", owner1, TeamStatus.Active),
                CreateTeamWithOwner("Team 2 Owner1 Active", owner1, TeamStatus.Active),
                CreateTeamWithOwner("Team 3 Owner1 Archived", owner1, TeamStatus.Archived), // Ten nie powinien być zwrócony
                CreateTeamWithOwner("Team 4 Owner2 Active", owner2, TeamStatus.Active),
            };
            await Context.Teams.AddRangeAsync(teams);
            await SaveChangesAsync();

            // Działanie
            var result = await _repository.GetTeamsByOwnerAsync(owner1);

            // Weryfikacja
            result.Should().HaveCount(2); // Tylko aktywne zespoły (nie archiwalny)
            result.Should().OnlyContain(t => t.Owner == owner1 && t.Status == TeamStatus.Active);
            result.Select(t => t.DisplayName).Should().Contain(new[] { "Team 1 Owner1 Active", "Team 2 Owner1 Active" });
        }

        [Fact]
        public async Task GetActiveTeamsAsync_ShouldReturnOnlyTeamsWithStatusActive()
        {
            // Przygotowanie
            await CleanDatabaseAsync();
            var teams = new List<Team>
            {
                CreateTeam("Active Team 1", TeamStatus.Active, TeamVisibility.Private),
                CreateTeam("Active Team 2", TeamStatus.Active, TeamVisibility.Public),
                CreateTeam("Archived Team", TeamStatus.Archived, TeamVisibility.Private),
            };
            await Context.Teams.AddRangeAsync(teams);
            await SaveChangesAsync();

            // Działanie
            var result = await _repository.GetActiveTeamsAsync();

            // Weryfikacja - sprawdzamy czy wszystkie zwrócone zespoły są aktywne i czy zawierają nasze dodane aktywne zespoły
            result.Should().OnlyContain(t => t.Status == TeamStatus.Active); // Co implikuje t.IsActive == true
            result.Select(t => t.DisplayName).Should().Contain(new[] { "Active Team 1", "Active Team 2" });
            result.Count().Should().BeGreaterThanOrEqualTo(2); // Mogą być dodatkowe aktywne zespoły z innych testów
        }

        [Fact]
        public async Task GetArchivedTeamsAsync_ShouldReturnOnlyTeamsWithStatusArchived()
        {
            // Przygotowanie
            await CleanDatabaseAsync();
            var teams = new List<Team>
            {
                CreateTeam("Active Team", TeamStatus.Active, TeamVisibility.Private),
                CreateTeam("Archived Team 1", TeamStatus.Archived, TeamVisibility.Private),
                CreateTeam("Archived Team 2", TeamStatus.Archived, TeamVisibility.Public),
            };
            await Context.Teams.AddRangeAsync(teams);
            await SaveChangesAsync();

            // Działanie
            var result = await _repository.GetArchivedTeamsAsync();

            // Weryfikacja
            result.Should().HaveCount(2);
            result.Should().OnlyContain(t => t.Status == TeamStatus.Archived); // Co implikuje t.IsActive == false
            result.Select(t => t.DisplayName).Should().Contain(new[] { "Archived Team 1", "Archived Team 2" });
        }

        [Fact]
        public async Task Update_ShouldModifyTeamData_AndAuditFields()
        {
            // Przygotowanie
            await CleanDatabaseAsync();
            var team = new Team
            {
                Id = Guid.NewGuid().ToString(),
                DisplayName = "Original Name",
                Description = "Original Description",
                Owner = "original.owner@test.com",
                Status = TeamStatus.Active
            };
            await Context.Teams.AddAsync(team);
            await SaveChangesAsync(); // Ustawia CreatedBy, CreatedDate

            var initialCreatedBy = team.CreatedBy;
            var initialCreatedDate = team.CreatedDate;
            var currentUser = "team_updater_repo_test";
            SetTestUser(currentUser); // Ustawiamy użytkownika dla tej operacji Update

            // Działanie
            var teamToUpdate = await _repository.GetByIdAsync(team.Id);
            teamToUpdate!.DisplayName = "Updated Name Repo";
            teamToUpdate.Description = "Updated Description Repo";
            teamToUpdate.Owner = "new.owner.repo@test.com";
            // Celowo nie zmieniamy Statusu tutaj, aby sprawdzić modyfikację innych pól
            // gdybyśmy chcieli zmienić Status, użylibyśmy teamToUpdate.Archive() lub teamToUpdate.Restore()
            // a następnie _repository.Update(teamToUpdate).

            _repository.Update(teamToUpdate);
            await SaveChangesAsync(); // To powinno ustawić ModifiedBy i ModifiedDate

            // Weryfikacja
            var updatedTeam = await Context.Teams.FirstOrDefaultAsync(t => t.Id == team.Id);
            updatedTeam.Should().NotBeNull();
            updatedTeam!.DisplayName.Should().Be("Updated Name Repo");
            updatedTeam.Description.Should().Be("Updated Description Repo");
            updatedTeam.Owner.Should().Be("new.owner.repo@test.com");
            updatedTeam.Status.Should().Be(TeamStatus.Active); // Status nie powinien się zmienić
            updatedTeam.IsActive.Should().BeTrue();

            updatedTeam.CreatedBy.Should().Be(initialCreatedBy);
            updatedTeam.CreatedDate.Should().Be(initialCreatedDate);
            updatedTeam.ModifiedBy.Should().Be(currentUser); // Oczekujemy użytkownika z SetTestUser
            updatedTeam.ModifiedDate.Should().NotBeNull();

            ResetTestUser(); // Przywracamy domyślnego użytkownika testowego
        }

        #region Helper Methods

        // Metody pomocnicze nie ustawiają już BaseEntity.IsActive bezpośrednio
        private Team CreateTeam(string displayName, TeamStatus status, TeamVisibility visibility)
        {
            return new Team
            {
                Id = Guid.NewGuid().ToString(),
                DisplayName = displayName,
                Description = $"Description for {displayName}",
                Owner = "owner@test.com",
                Status = status, // Ustawiamy Status
                Visibility = visibility
                // IsActive jest obliczane na podstawie Status
            };
        }

        private Team CreateTeamWithOwner(string displayName, string owner, TeamStatus status)
        {
            return new Team
            {
                Id = Guid.NewGuid().ToString(),
                DisplayName = displayName,
                Description = $"Description for {displayName}",
                Owner = owner,
                Status = status, // Ustawiamy Status
                Visibility = TeamVisibility.Private
            };
        }

        #endregion
    }
}