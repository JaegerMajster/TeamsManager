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
        public class TeamMemberRepositoryTests : RepositoryTestBase
    {
        private readonly GenericRepository<TeamMember> _repository;

        public TeamMemberRepositoryTests()
        {
            _repository = new GenericRepository<TeamMember>(Context);
        }

        [Fact]
        public async Task AddAsync_ShouldAddTeamMemberToDatabase()
        {
            // Arrange
            await CleanDatabaseAsync();
            var team = await CreateTeamAsync();
            var user = await CreateUserAsync("member@test.com", "Jan", "Kowalski");
            
            var teamMember = new TeamMember
            {
                Id = Guid.NewGuid().ToString(),
                TeamId = team.Id,
                UserId = user.Id,
                Role = TeamMemberRole.Member,
                AddedDate = DateTime.UtcNow,
                AddedBy = "admin@test.com",
                CreatedBy = "test_user",
                IsActive = true
            };

            // Act
            await _repository.AddAsync(teamMember);
            await SaveChangesAsync();

            // Assert
            var savedMember = await Context.TeamMembers.FirstOrDefaultAsync(tm => tm.Id == teamMember.Id);
            savedMember.Should().NotBeNull();
            savedMember!.TeamId.Should().Be(team.Id);
            savedMember.UserId.Should().Be(user.Id);
            savedMember.Role.Should().Be(TeamMemberRole.Member);
        }

        [Fact]
        public async Task GetByIdAsync_ShouldReturnCorrectTeamMember()
        {
            // Arrange
            await CleanDatabaseAsync();
            var team = await CreateTeamAsync();
            var user = await CreateUserAsync("owner@test.com", "Anna", "Nowak");
            var teamMember = await CreateTeamMemberAsync(team.Id, user.Id, TeamMemberRole.Owner);

            // Act
            var result = await _repository.GetByIdAsync(teamMember.Id);

            // Assert
            result.Should().NotBeNull();
            result!.TeamId.Should().Be(team.Id);
            result.UserId.Should().Be(user.Id);
            result.Role.Should().Be(TeamMemberRole.Owner);
        }

        [Fact]
        public async Task GetAllAsync_ShouldReturnAllTeamMembers()
        {
            // Arrange
            await CleanDatabaseAsync();
            var team1 = await CreateTeamAsync("Team A");
            var team2 = await CreateTeamAsync("Team B");
            
            var users = new List<User>
            {
                await CreateUserAsync("user1@test.com", "User", "One"),
                await CreateUserAsync("user2@test.com", "User", "Two"),
                await CreateUserAsync("user3@test.com", "User", "Three")
            };

            var teamMembers = new List<TeamMember>
            {
                await CreateTeamMemberAsync(team1.Id, users[0].Id, TeamMemberRole.Owner),
                await CreateTeamMemberAsync(team1.Id, users[1].Id, TeamMemberRole.Member),
                await CreateTeamMemberAsync(team2.Id, users[1].Id, TeamMemberRole.Member),
                await CreateTeamMemberAsync(team2.Id, users[2].Id, TeamMemberRole.Owner)
            };

            // Act
            var result = await _repository.GetAllAsync();

            // Assert
            result.Should().HaveCount(4);
            result.Where(tm => tm.TeamId == team1.Id).Should().HaveCount(2);
            result.Where(tm => tm.TeamId == team2.Id).Should().HaveCount(2);
        }

        [Fact]
        public async Task FindAsync_ShouldReturnFilteredTeamMembers()
        {
            // Arrange
            await CleanDatabaseAsync();
            var team = await CreateTeamAsync();
            
            var users = new List<User>
            {
                await CreateUserAsync("owner@test.com", "Owner", "User"),
                await CreateUserAsync("member1@test.com", "Member", "One"),
                await CreateUserAsync("member2@test.com", "Member", "Two"),
                await CreateUserAsync("member3@test.com", "Member", "Three"),
                await CreateUserAsync("inactive@test.com", "Inactive", "Member")
            };

            var teamMembers = new List<TeamMember>
            {
                await CreateTeamMemberAsync(team.Id, users[0].Id, TeamMemberRole.Owner),
                await CreateTeamMemberAsync(team.Id, users[1].Id, TeamMemberRole.Member),
                await CreateTeamMemberAsync(team.Id, users[2].Id, TeamMemberRole.Member),
                await CreateTeamMemberAsync(team.Id, users[3].Id, TeamMemberRole.Member),
                await CreateTeamMemberAsync(team.Id, users[4].Id, TeamMemberRole.Member, false) // nieaktywny
            };

            // Act - znajdź wszystkich członków (role Member)
            var members = await _repository.FindAsync(tm => 
                tm.TeamId == team.Id && 
                tm.Role == TeamMemberRole.Member && 
                tm.IsActive);

            // Assert
            members.Should().HaveCount(3);
            members.Select(tm => tm.UserId).Should().Contain(new[] { users[1].Id, users[2].Id, users[3].Id });

            // Act - znajdź wszystkich aktywnych członków zespołu
            var allActiveMembers = await _repository.FindAsync(tm => 
                tm.TeamId == team.Id && 
                tm.IsActive);

            // Assert
            allActiveMembers.Should().HaveCount(4);
        }

        [Fact]
        public async Task Update_ShouldModifyTeamMemberData()
        {
            // Arrange
            await CleanDatabaseAsync();
            var team = await CreateTeamAsync();
            var user = await CreateUserAsync("promoted@test.com", "Promoted", "User");
            var teamMember = await CreateTeamMemberAsync(team.Id, user.Id, TeamMemberRole.Member);

            // Act
            teamMember.Role = TeamMemberRole.Owner;
            teamMember.RoleChangedDate = DateTime.UtcNow;
            teamMember.RoleChangedBy = "admin@test.com";
            teamMember.Notes = "Awansowany na właściciela zespołu";
            teamMember.MarkAsModified("updater");

            _repository.Update(teamMember);
            await SaveChangesAsync();

            // Assert
            var updatedMember = await Context.TeamMembers.FirstOrDefaultAsync(tm => tm.Id == teamMember.Id);
            updatedMember.Should().NotBeNull();
            updatedMember!.Role.Should().Be(TeamMemberRole.Owner);
            updatedMember.RoleChangedDate.Should().NotBeNull();
            updatedMember.RoleChangedBy.Should().Be("admin@test.com");
            updatedMember.Notes.Should().Be("Awansowany na właściciela zespołu");
            updatedMember.ModifiedBy.Should().Be("updater");
            updatedMember.ModifiedDate.Should().NotBeNull();
        }

        [Fact]
        public async Task Delete_ShouldRemoveTeamMemberFromDatabase()
        {
            // Arrange
            await CleanDatabaseAsync();
            var team = await CreateTeamAsync();
            var user = await CreateUserAsync("removed@test.com", "Removed", "User");
            var teamMember = await CreateTeamMemberAsync(team.Id, user.Id, TeamMemberRole.Member);

            // Act
            _repository.Delete(teamMember);
            await SaveChangesAsync();

            // Assert
            var deletedMember = await Context.TeamMembers.FirstOrDefaultAsync(tm => tm.Id == teamMember.Id);
            deletedMember.Should().BeNull();
        }

        [Fact]
        public async Task ComplexScenario_TeamWithDifferentMemberRoles()
        {
            // Arrange
            await CleanDatabaseAsync();
            var team = await CreateTeamAsync("Project Team");
            
            // Utwórz użytkowników o różnych rolach
            var owner = await CreateUserAsync("owner@test.com", "Team", "Owner");
            var coOwner = await CreateUserAsync("coowner@test.com", "Co", "Owner");
            var member1 = await CreateUserAsync("member1@test.com", "Regular", "Member");
            var member2 = await CreateUserAsync("member2@test.com", "Another", "Member");
            var member3 = await CreateUserAsync("member3@test.com", "Third", "Member");

            // Dodaj ich do zespołu
            await CreateTeamMemberAsync(team.Id, owner.Id, TeamMemberRole.Owner);
            await CreateTeamMemberAsync(team.Id, coOwner.Id, TeamMemberRole.Owner);
            await CreateTeamMemberAsync(team.Id, member1.Id, TeamMemberRole.Member);
            await CreateTeamMemberAsync(team.Id, member2.Id, TeamMemberRole.Member);
            await CreateTeamMemberAsync(team.Id, member3.Id, TeamMemberRole.Member);

            // Act - znajdź właścicieli
            var owners = await _repository.FindAsync(tm => 
                tm.TeamId == team.Id && 
                tm.Role == TeamMemberRole.Owner && 
                tm.IsActive);

            // Act - znajdź członków (nie właścicieli)
            var members = await _repository.FindAsync(tm => 
                tm.TeamId == team.Id && 
                tm.Role == TeamMemberRole.Member && 
                tm.IsActive);

            // Assert
            owners.Should().HaveCount(2);
            owners.Select(tm => tm.UserId).Should().Contain(new[] { owner.Id, coOwner.Id });

            members.Should().HaveCount(3);
            members.Select(tm => tm.UserId).Should().Contain(new[] { member1.Id, member2.Id, member3.Id });
        }

        #region Helper Methods

        private async Task<Team> CreateTeamAsync(string displayName = "Test Team")
        {
            var team = new Team
            {
                Id = Guid.NewGuid().ToString(),
                DisplayName = displayName,
                Description = $"Opis zespołu {displayName}",
                Owner = "owner@test.com",
                Status = TeamStatus.Active,
                Visibility = TeamVisibility.Private,
                CreatedBy = "test_user",
                IsActive = true
            };
            await Context.Teams.AddAsync(team);
            await Context.SaveChangesAsync();
            return team;
        }

        private async Task<User> CreateUserAsync(string upn, string firstName, string lastName)
        {
            var user = new User
            {
                Id = Guid.NewGuid().ToString(),
                FirstName = firstName,
                LastName = lastName,
                UPN = upn,
                Role = UserRole.Nauczyciel,
                CreatedBy = "test_user",
                IsActive = true
            };
            await Context.Users.AddAsync(user);
            await Context.SaveChangesAsync();
            return user;
        }

        private async Task<TeamMember> CreateTeamMemberAsync(
            string teamId, 
            string userId, 
            TeamMemberRole role,
            bool isActive = true)
        {
            var teamMember = new TeamMember
            {
                Id = Guid.NewGuid().ToString(),
                TeamId = teamId,
                UserId = userId,
                Role = role,
                AddedDate = DateTime.UtcNow,
                AddedBy = "admin@test.com",
                CreatedBy = "test_user",
                IsActive = isActive
            };
            await Context.TeamMembers.AddAsync(teamMember);
            await Context.SaveChangesAsync();
            return teamMember;
        }

        #endregion
    }
  
} 