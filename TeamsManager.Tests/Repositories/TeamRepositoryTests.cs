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
            // Arrange
            await CleanDatabaseAsync();
            var team = new Team
            {
                Id = Guid.NewGuid().ToString(),
                DisplayName = "Test Team",
                Description = "Test Description",
                Owner = "owner@test.com",
                Status = TeamStatus.Active,
                Visibility = TeamVisibility.Private,
                CreatedBy = "test_user",
                IsActive = true
            };

            // Act
            await _repository.AddAsync(team);
            await SaveChangesAsync();

            // Assert
            var savedTeam = await Context.Teams.FirstOrDefaultAsync(t => t.Id == team.Id);
            savedTeam.Should().NotBeNull();
            savedTeam!.DisplayName.Should().Be("Test Team");
            savedTeam.Description.Should().Be("Test Description");
            savedTeam.Owner.Should().Be("owner@test.com");
            savedTeam.Status.Should().Be(TeamStatus.Active);
            savedTeam.Visibility.Should().Be(TeamVisibility.Private);
        }

        [Fact]
        public async Task GetByIdAsync_ShouldReturnCorrectTeam_WithIncludes()
        {
            // Arrange
            await CleanDatabaseAsync();
            
            // Tworzenie danych pomocniczych
            var schoolType = new SchoolType
            {
                Id = Guid.NewGuid().ToString(),
                ShortName = "LO",
                FullName = "Liceum Ogólnokształcące",
                Description = "Test",
                CreatedBy = "test_user",
                IsActive = true
            };

            var schoolYear = new SchoolYear
            {
                Id = Guid.NewGuid().ToString(),
                Name = "2024/2025",
                StartDate = new DateTime(2024, 9, 1),
                EndDate = new DateTime(2025, 6, 30),
                IsCurrent = true,
                Description = "Test",
                CreatedBy = "test_user",
                IsActive = true
            };

            var template = new TeamTemplate
            {
                Id = Guid.NewGuid().ToString(),
                Name = "Test Template",
                Template = "{SchoolType} {Class} - {Subject}",
                Description = "Test",
                IsUniversal = false,
                SchoolTypeId = schoolType.Id,
                CreatedBy = "test_user",
                IsActive = true
            };

            await Context.SchoolTypes.AddAsync(schoolType);
            await Context.SchoolYears.AddAsync(schoolYear);
            await Context.TeamTemplates.AddAsync(template);
            await Context.SaveChangesAsync();

            // Tworzenie zespołu
            var team = new Team
            {
                Id = Guid.NewGuid().ToString(),
                DisplayName = "LO 1A - Matematyka",
                Description = "Zespół matematyczny",
                Owner = "teacher@test.com",
                Status = TeamStatus.Active,
                SchoolTypeId = schoolType.Id,
                SchoolYearId = schoolYear.Id,
                TemplateId = template.Id,
                CreatedBy = "test_user",
                IsActive = true
            };
            await Context.Teams.AddAsync(team);
            await Context.SaveChangesAsync();

            // Dodanie użytkownika i członka zespołu
            var user = new User
            {
                Id = Guid.NewGuid().ToString(),
                FirstName = "Jan",
                LastName = "Kowalski",
                UPN = "jan.kowalski@test.com",
                Role = UserRole.Uczen,
                CreatedBy = "test_user",
                IsActive = true
            };
            await Context.Users.AddAsync(user);
            await Context.SaveChangesAsync();

            var teamMember = new TeamMember
            {
                Id = Guid.NewGuid().ToString(),
                TeamId = team.Id,
                UserId = user.Id,
                Role = TeamMemberRole.Member,
                AddedDate = DateTime.UtcNow,
                CreatedBy = "test_user",
                IsActive = true
            };
            await Context.TeamMembers.AddAsync(teamMember);
            
            // Dodanie kanału
            var channel = new Channel
            {
                Id = Guid.NewGuid().ToString(),
                DisplayName = "General",
                Description = "Kanał ogólny",
                TeamId = team.Id,
                ChannelType = "Standard",
                Status = ChannelStatus.Active,
                IsGeneral = true,
                CreatedBy = "test_user",
                IsActive = true
            };
            await Context.Channels.AddAsync(channel);
            await Context.SaveChangesAsync();

            // Act
            var result = await _repository.GetByIdAsync(team.Id);

            // Assert
            result.Should().NotBeNull();
            result!.DisplayName.Should().Be("LO 1A - Matematyka");
            
            // Sprawdzenie includes
            result.SchoolType.Should().NotBeNull();
            result.SchoolType!.ShortName.Should().Be("LO");
            
            result.SchoolYear.Should().NotBeNull();
            result.SchoolYear!.Name.Should().Be("2024/2025");
            
            result.Template.Should().NotBeNull();
            result.Template!.Name.Should().Be("Test Template");
            
            result.Members.Should().HaveCount(1);
            result.Members.First().User.Should().NotBeNull();
            result.Members.First().User!.FullName.Should().Be("Jan Kowalski");
            
            result.Channels.Should().HaveCount(1);
            result.Channels.First().DisplayName.Should().Be("General");
        }

        [Fact]
        public async Task GetTeamByNameAsync_ShouldReturnCorrectTeam()
        {
            // Arrange
            await CleanDatabaseAsync();
            var teamName = "Unique Team Name";
            var team = new Team
            {
                Id = Guid.NewGuid().ToString(),
                DisplayName = teamName,
                Description = "Test",
                Owner = "owner@test.com",
                Status = TeamStatus.Active,
                CreatedBy = "test_user",
                IsActive = true
            };
            await Context.Teams.AddAsync(team);
            await Context.SaveChangesAsync();

            // Act
            var result = await _repository.GetTeamByNameAsync(teamName);

            // Assert
            result.Should().NotBeNull();
            result!.DisplayName.Should().Be(teamName);
            result.Id.Should().Be(team.Id);
        }

        [Fact]
        public async Task GetTeamsByOwnerAsync_ShouldReturnMatchingTeams()
        {
            // Arrange
            await CleanDatabaseAsync();
            var owner1 = "owner1@test.com";
            var owner2 = "owner2@test.com";

            var teams = new List<Team>
            {
                CreateTeamWithOwner("Team 1", owner1, TeamStatus.Active, true),
                CreateTeamWithOwner("Team 2", owner1, TeamStatus.Active, true),
                CreateTeamWithOwner("Team 3", owner1, TeamStatus.Archived, true),
                CreateTeamWithOwner("Team 4", owner2, TeamStatus.Active, true),
            };

            await Context.Teams.AddRangeAsync(teams);
            await Context.SaveChangesAsync();

            // Tworzymy nieaktywny zespół dla owner1
            var inactiveTeam = CreateTeamWithOwner("Inactive Team", owner1, TeamStatus.Active, true);
            await Context.Teams.AddAsync(inactiveTeam);
            await Context.SaveChangesAsync();
            
            // Dezaktywujemy go
            inactiveTeam.IsActive = false;
            Context.Teams.Update(inactiveTeam);
            await Context.SaveChangesAsync();

            // Act
            var result = await _repository.GetTeamsByOwnerAsync(owner1);

            // Assert
            result.Should().HaveCount(3);
            result.Should().OnlyContain(t => t.Owner == owner1 && t.IsActive);
            result.Select(t => t.DisplayName).Should().Contain(new[] { "Team 1", "Team 2", "Team 3" });
        }

        [Fact]
        public async Task GetActiveTeamsAsync_ShouldReturnOnlyActiveStatusTeams()
        {
            // Arrange
            await CleanDatabaseAsync();
            
            var teams = new List<Team>
            {
                CreateTeam("Active Team 1", TeamStatus.Active, TeamVisibility.Private, true),
                CreateTeam("Active Team 2", TeamStatus.Active, TeamVisibility.Public, true),
                CreateTeam("Archived Team", TeamStatus.Archived, TeamVisibility.Private, true),
            };

            await Context.Teams.AddRangeAsync(teams);
            await Context.SaveChangesAsync();

            // Tworzymy nieaktywny zespół (BaseEntity.IsActive = false)
            var inactiveTeam = CreateTeam("Inactive Team", TeamStatus.Active, TeamVisibility.Private, true);
            await Context.Teams.AddAsync(inactiveTeam);
            await Context.SaveChangesAsync();
            
            // Dezaktywujemy go
            inactiveTeam.IsActive = false;
            Context.Teams.Update(inactiveTeam);
            await Context.SaveChangesAsync();

            // Act
            var result = await _repository.GetActiveTeamsAsync();

            // Assert
            result.Should().HaveCount(2);
            result.Should().OnlyContain(t => t.Status == TeamStatus.Active && t.IsActive);
            result.Select(t => t.DisplayName).Should().Contain(new[] { "Active Team 1", "Active Team 2" });
        }

        [Fact]
        public async Task GetArchivedTeamsAsync_ShouldReturnOnlyArchivedStatusTeams()
        {
            // Arrange
            await CleanDatabaseAsync();
            
            var teams = new List<Team>
            {
                CreateTeam("Active Team", TeamStatus.Active, TeamVisibility.Private, true),
                CreateTeam("Archived Team 1", TeamStatus.Archived, TeamVisibility.Private, true),
                CreateTeam("Archived Team 2", TeamStatus.Archived, TeamVisibility.Public, true),
            };

            await Context.Teams.AddRangeAsync(teams);
            await Context.SaveChangesAsync();

            // Tworzymy nieaktywny zarchiwizowany zespół
            var inactiveArchivedTeam = CreateTeam("Inactive Archived", TeamStatus.Archived, TeamVisibility.Private, true);
            await Context.Teams.AddAsync(inactiveArchivedTeam);
            await Context.SaveChangesAsync();
            
            // Dezaktywujemy go
            inactiveArchivedTeam.IsActive = false;
            Context.Teams.Update(inactiveArchivedTeam);
            await Context.SaveChangesAsync();

            // Act
            var result = await _repository.GetArchivedTeamsAsync();

            // Assert
            result.Should().HaveCount(2);
            result.Should().OnlyContain(t => t.Status == TeamStatus.Archived && t.IsActive);
            result.Select(t => t.DisplayName).Should().Contain(new[] { "Archived Team 1", "Archived Team 2" });
        }

        [Fact]
        public async Task Update_ShouldModifyTeamData()
        {
            // Arrange
            await CleanDatabaseAsync();
            var team = new Team
            {
                Id = Guid.NewGuid().ToString(),
                DisplayName = "Original Name",
                Description = "Original Description",
                Owner = "original.owner@test.com",
                Status = TeamStatus.Active,
                CreatedBy = "test_user",
                IsActive = true
            };
            await Context.Teams.AddAsync(team);
            await Context.SaveChangesAsync();

            // Act
            team.DisplayName = "Updated Name";
            team.Description = "Updated Description";
            team.Owner = "new.owner@test.com";
            team.Status = TeamStatus.Archived;
            team.StatusChangeDate = DateTime.UtcNow;
            team.StatusChangedBy = "admin";
            team.StatusChangeReason = "End of semester";
            team.Language = "English";
            team.MaxMembers = 100;
            team.MarkAsModified("updater");

            _repository.Update(team);
            await SaveChangesAsync();

            // Assert
            var updatedTeam = await Context.Teams.FirstOrDefaultAsync(t => t.Id == team.Id);
            updatedTeam.Should().NotBeNull();
            updatedTeam!.DisplayName.Should().Be("Updated Name");
            updatedTeam.Description.Should().Be("Updated Description");
            updatedTeam.Owner.Should().Be("new.owner@test.com");
            updatedTeam.Status.Should().Be(TeamStatus.Archived);
            updatedTeam.StatusChangeDate.Should().NotBeNull();
            updatedTeam.StatusChangedBy.Should().Be("admin");
            updatedTeam.StatusChangeReason.Should().Be("End of semester");
            updatedTeam.Language.Should().Be("English");
            updatedTeam.MaxMembers.Should().Be(100);
            updatedTeam.ModifiedBy.Should().Be("system@teamsmanager.local");
            updatedTeam.ModifiedDate.Should().NotBeNull();
        }

        #region Helper Methods

        private Team CreateTeam(string displayName, TeamStatus status, TeamVisibility visibility, bool isActive)
        {
            return new Team
            {
                Id = Guid.NewGuid().ToString(),
                DisplayName = displayName,
                Description = $"Description for {displayName}",
                Owner = "owner@test.com",
                Status = status,
                Visibility = visibility,
                CreatedBy = "test_user",
                IsActive = isActive
            };
        }

        private Team CreateTeamWithOwner(string displayName, string owner, TeamStatus status, bool isActive)
        {
            return new Team
            {
                Id = Guid.NewGuid().ToString(),
                DisplayName = displayName,
                Description = $"Description for {displayName}",
                Owner = owner,
                Status = status,
                Visibility = TeamVisibility.Private,
                CreatedBy = "test_user",
                IsActive = isActive
            };
        }

        #endregion
    }
} 