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
            // Przygotowanie
            await CleanDatabaseAsync();
            var team = await CreateTeamAsync(); // Metoda pomocnicza zaktualizowana
            var user = await CreateUserAsync("member@test.com", "Jan", "Kowalski"); // Metoda pomocnicza zaktualizowana

            var teamMember = new TeamMember
            {
                Id = Guid.NewGuid().ToString(),
                TeamId = team.Id,
                UserId = user.Id,
                Role = TeamMemberRole.Member,
                AddedDate = DateTime.UtcNow,
                AddedBy = "admin@test.com", // To pole nie jest standardowym polem audytu BaseEntity
                // CreatedBy zostanie ustawione przez TestDbContext
                IsActive = true
            };

            // Działanie
            await _repository.AddAsync(teamMember);
            await SaveChangesAsync();

            // Weryfikacja
            var savedMember = await Context.TeamMembers.FirstOrDefaultAsync(tm => tm.Id == teamMember.Id);
            savedMember.Should().NotBeNull();
            savedMember!.TeamId.Should().Be(team.Id);
            savedMember.UserId.Should().Be(user.Id);
            savedMember.Role.Should().Be(TeamMemberRole.Member);
            savedMember.CreatedBy.Should().Be("test_user_integration_base_default");
            savedMember.CreatedDate.Should().NotBe(default(DateTime));
            savedMember.ModifiedBy.Should().BeNull();
            savedMember.ModifiedDate.Should().BeNull();
        }

        [Fact]
        public async Task GetByIdAsync_ShouldReturnCorrectTeamMember()
        {
            // Przygotowanie
            await CleanDatabaseAsync();
            var team = await CreateTeamAsync();
            var user = await CreateUserAsync("owner@test.com", "Anna", "Nowak");
            var teamMember = await CreateTeamMemberAsync(team.Id, user.Id, TeamMemberRole.Owner); // Metoda pomocnicza zaktualizowana

            // Działanie
            var result = await _repository.GetByIdAsync(teamMember.Id);

            // Weryfikacja
            result.Should().NotBeNull();
            result!.TeamId.Should().Be(team.Id);
            result.UserId.Should().Be(user.Id);
            result.Role.Should().Be(TeamMemberRole.Owner);
            result.CreatedBy.Should().Be("test_user_integration_base_default");
        }

        [Fact]
        public async Task GetAllAsync_ShouldReturnAllTeamMembers()
        {
            // Przygotowanie
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

            // Działanie
            var result = await _repository.GetAllAsync();

            // Weryfikacja
            result.Should().HaveCount(4);
            result.Where(tm => tm.TeamId == team1.Id).Should().HaveCount(2);
            result.Where(tm => tm.TeamId == team2.Id).Should().HaveCount(2);
            result.ToList().ForEach(tm => tm.CreatedBy.Should().Be("test_user_integration_base_default"));
        }

        [Fact]
        public async Task FindAsync_ShouldReturnFilteredTeamMembers()
        {
            // Przygotowanie
            await CleanDatabaseAsync();
            var team = await CreateTeamAsync();

            var users = new List<User>
            {
                await CreateUserAsync("owner@test.com", "Owner", "User"),
                await CreateUserAsync("member1@test.com", "Member", "One"),
                await CreateUserAsync("member2@test.com", "Member", "Two"),
                await CreateUserAsync("member3@test.com", "Member", "Three"),
                await CreateUserAsync("inactive@test.com", "Inactive", "Member") // Użytkownik będzie aktywny po utworzeniu, członkostwo nieaktywne
            };

            var teamMembers = new List<TeamMember>
            {
                await CreateTeamMemberAsync(team.Id, users[0].Id, TeamMemberRole.Owner),
                await CreateTeamMemberAsync(team.Id, users[1].Id, TeamMemberRole.Member),
                await CreateTeamMemberAsync(team.Id, users[2].Id, TeamMemberRole.Member),
                await CreateTeamMemberAsync(team.Id, users[3].Id, TeamMemberRole.Member),
                await CreateTeamMemberAsync(team.Id, users[4].Id, TeamMemberRole.Member, false) // nieaktywne członkostwo
            };

            // Działanie - znajdź wszystkich członków (role Member)
            var members = await _repository.FindAsync(tm =>
                tm.TeamId == team.Id &&
                tm.Role == TeamMemberRole.Member &&
                tm.IsActive);

            // Weryfikacja
            members.Should().HaveCount(3);
            members.Select(tm => tm.UserId).Should().Contain(new[] { users[1].Id, users[2].Id, users[3].Id });

            // Działanie - znajdź wszystkich aktywnych członków zespołu
            var allActiveMembers = await _repository.FindAsync(tm =>
                tm.TeamId == team.Id &&
                tm.IsActive);

            // Weryfikacja
            allActiveMembers.Should().HaveCount(4);
        }

        [Fact]
        public async Task Update_ShouldModifyTeamMemberData()
        {
            // Przygotowanie
            await CleanDatabaseAsync();
            var team = await CreateTeamAsync();
            var user = await CreateUserAsync("promoted@test.com", "Promoted", "User");
            var teamMember = await CreateTeamMemberAsync(team.Id, user.Id, TeamMemberRole.Member);
            var initialCreatedBy = teamMember.CreatedBy;
            var initialCreatedDate = teamMember.CreatedDate;
            var currentUser = "member_updater";
            SetTestUser(currentUser);

            // Działanie
            var teamMemberToUpdate = await _repository.GetByIdAsync(teamMember.Id);
            teamMemberToUpdate!.Role = TeamMemberRole.Owner;
            teamMemberToUpdate.RoleChangedDate = DateTime.UtcNow;
            teamMemberToUpdate.RoleChangedBy = "admin@test.com";
            teamMemberToUpdate.Notes = "Awansowany na właściciela zespołu";
            // teamMemberToUpdate.MarkAsModified(currentUser); // Niepotrzebne

            _repository.Update(teamMemberToUpdate);
            await SaveChangesAsync();

            // Weryfikacja
            var updatedMember = await Context.TeamMembers.FirstOrDefaultAsync(tm => tm.Id == teamMember.Id);
            updatedMember.Should().NotBeNull();
            updatedMember!.Role.Should().Be(TeamMemberRole.Owner);
            updatedMember.RoleChangedDate.Should().NotBeNull();
            updatedMember.RoleChangedBy.Should().Be("admin@test.com");
            updatedMember.Notes.Should().Be("Awansowany na właściciela zespołu");
            updatedMember.CreatedBy.Should().Be(initialCreatedBy);
            updatedMember.CreatedDate.Should().Be(initialCreatedDate);
            updatedMember.ModifiedBy.Should().Be(currentUser);
            updatedMember.ModifiedDate.Should().NotBeNull();

            ResetTestUser();
        }

        [Fact]
        public async Task Delete_ShouldRemoveTeamMemberFromDatabase()
        {
            // Przygotowanie
            await CleanDatabaseAsync();
            var team = await CreateTeamAsync();
            var user = await CreateUserAsync("removed@test.com", "Removed", "User");
            var teamMember = await CreateTeamMemberAsync(team.Id, user.Id, TeamMemberRole.Member);

            // Działanie
            _repository.Delete(teamMember); // Fizyczne usunięcie
            await SaveChangesAsync();

            // Weryfikacja
            var deletedMember = await Context.TeamMembers.FirstOrDefaultAsync(tm => tm.Id == teamMember.Id);
            deletedMember.Should().BeNull();
        }

        [Fact]
        public async Task ComplexScenario_TeamWithDifferentMemberRoles()
        {
            // Przygotowanie
            await CleanDatabaseAsync();
            var team = await CreateTeamAsync("Project Team");

            var owner = await CreateUserAsync("owner@test.com", "Team", "Owner");
            var coOwner = await CreateUserAsync("coowner@test.com", "Co", "Owner");
            var member1 = await CreateUserAsync("member1@test.com", "Regular", "Member");
            var member2 = await CreateUserAsync("member2@test.com", "Another", "Member");
            var member3 = await CreateUserAsync("member3@test.com", "Third", "Member");

            await CreateTeamMemberAsync(team.Id, owner.Id, TeamMemberRole.Owner);
            await CreateTeamMemberAsync(team.Id, coOwner.Id, TeamMemberRole.Owner);
            await CreateTeamMemberAsync(team.Id, member1.Id, TeamMemberRole.Member);
            await CreateTeamMemberAsync(team.Id, member2.Id, TeamMemberRole.Member);
            await CreateTeamMemberAsync(team.Id, member3.Id, TeamMemberRole.Member);

            // Działanie - znajdź właścicieli
            var owners = await _repository.FindAsync(tm =>
                tm.TeamId == team.Id &&
                tm.Role == TeamMemberRole.Owner &&
                tm.IsActive);

            // Działanie - znajdź członków (nie właścicieli)
            var members = await _repository.FindAsync(tm =>
                tm.TeamId == team.Id &&
                tm.Role == TeamMemberRole.Member &&
                tm.IsActive);

            // Weryfikacja
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
            };
            await Context.Teams.AddAsync(team);
            await Context.SaveChangesAsync(); // Zapis z audytem
            return team;
        }

        private async Task<User> CreateUserAsync(string upn, string firstName, string lastName)
        {
            var department = await Context.Departments.FirstOrDefaultAsync(d => d.Name == "Dział Testowy Członków");
            if (department == null)
            {
                department = new Department { Id = Guid.NewGuid().ToString(), Name = "Dział Testowy Członków", IsActive = true };
                await Context.Departments.AddAsync(department);
                await Context.SaveChangesAsync();
            }

            var user = new User
            {
                Id = Guid.NewGuid().ToString(),
                FirstName = firstName,
                LastName = lastName,
                UPN = upn,
                Role = UserRole.Nauczyciel, // Domyślnie nauczyciel dla uproszczenia
                DepartmentId = department.Id,
                // CreatedBy zostanie ustawione przez TestDbContext
                IsActive = true
            };
            await Context.Users.AddAsync(user);
            await Context.SaveChangesAsync(); // Zapis z audytem
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
                AddedBy = "admin@test.com", // To pole nie jest standardowym polem audytu
                // CreatedBy zostanie ustawione przez TestDbContext
                IsActive = isActive
            };
            await Context.TeamMembers.AddAsync(teamMember);
            await Context.SaveChangesAsync(); // Zapis z audytem
            return teamMember;
        }

        #endregion
    }
}