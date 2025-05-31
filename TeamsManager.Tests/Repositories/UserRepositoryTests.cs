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
    public class UserRepositoryTests : RepositoryTestBase
    {
        private readonly UserRepository _repository;

        public UserRepositoryTests()
        {
            _repository = new UserRepository(Context);
        }

        [Fact]
        public async Task AddAsync_ShouldAddUserToDatabase()
        {
            // Arrange
            await CleanDatabaseAsync();
            var department = new Department
            {
                Id = Guid.NewGuid().ToString(),
                Name = "IT Department",
                Description = "Information Technology",
                CreatedBy = "test_user",
                IsActive = true
            };
            await Context.Departments.AddAsync(department);
            await Context.SaveChangesAsync();

            var user = new User
            {
                Id = Guid.NewGuid().ToString(),
                FirstName = "Jan",
                LastName = "Kowalski",
                UPN = "jan.kowalski@test.com",
                Role = UserRole.Nauczyciel,
                DepartmentId = department.Id,
                CreatedBy = "test_user",
                IsActive = true
            };

            // Act
            await _repository.AddAsync(user);
            await SaveChangesAsync();

            // Assert
            var savedUser = await Context.Users.FirstOrDefaultAsync(u => u.Id == user.Id);
            savedUser.Should().NotBeNull();
            savedUser!.FirstName.Should().Be("Jan");
            savedUser.LastName.Should().Be("Kowalski");
            savedUser.UPN.Should().Be("jan.kowalski@test.com");
            savedUser.Role.Should().Be(UserRole.Nauczyciel);
        }

        [Fact]
        public async Task GetByIdAsync_ShouldReturnCorrectUser_WithAllIncludes()
        {
            // Arrange
            await CleanDatabaseAsync();

            // Tworzenie danych testowych
            var department = new Department
            {
                Id = Guid.NewGuid().ToString(),
                Name = "Test Department",
                Description = "Test",
                CreatedBy = "test_user",
                IsActive = true
            };

            var schoolType = new SchoolType
            {
                Id = Guid.NewGuid().ToString(),
                ShortName = "LO",
                FullName = "Liceum Ogólnokształcące",
                Description = "Test",
                CreatedBy = "test_user",
                IsActive = true
            };

            var team = new Team
            {
                Id = Guid.NewGuid().ToString(),
                DisplayName = "Test Team",
                Description = "Test",
                Owner = "owner@test.com",
                Status = TeamStatus.Active,
                CreatedBy = "test_user",
                IsActive = true
            };

            await Context.Departments.AddAsync(department);
            await Context.SchoolTypes.AddAsync(schoolType);
            await Context.Teams.AddAsync(team);
            await Context.SaveChangesAsync();

            var user = new User
            {
                Id = Guid.NewGuid().ToString(),
                FirstName = "Test",
                LastName = "User",
                UPN = "test.user@test.com",
                Role = UserRole.Nauczyciel,
                DepartmentId = department.Id,
                CreatedBy = "test_user",
                IsActive = true
            };
            await Context.Users.AddAsync(user);
            await Context.SaveChangesAsync();

            // Dodanie powiązań
            var teamMembership = new TeamMember
            {
                Id = Guid.NewGuid().ToString(),
                TeamId = team.Id,
                UserId = user.Id,
                Role = TeamMemberRole.Member,
                AddedDate = DateTime.UtcNow,
                CreatedBy = "test_user",
                IsActive = true
            };

            var schoolTypeAssignment = new UserSchoolType
            {
                Id = Guid.NewGuid().ToString(),
                UserId = user.Id,
                SchoolTypeId = schoolType.Id,
                AssignedDate = DateTime.UtcNow,
                IsCurrentlyActive = true,
                CreatedBy = "test_user",
                IsActive = true
            };

            await Context.TeamMembers.AddAsync(teamMembership);
            await Context.UserSchoolTypes.AddAsync(schoolTypeAssignment);
            await Context.SaveChangesAsync();

            // Act
            var result = await _repository.GetByIdAsync(user.Id);

            // Assert
            result.Should().NotBeNull();
            result!.Department.Should().NotBeNull();
            result.Department!.Name.Should().Be("Test Department");
            result.TeamMemberships.Should().HaveCount(1);
            result.TeamMemberships.First().Team.Should().NotBeNull();
            result.TeamMemberships.First().Team!.DisplayName.Should().Be("Test Team");
            result.SchoolTypeAssignments.Should().HaveCount(1);
            result.SchoolTypeAssignments.First().SchoolType.Should().NotBeNull();
            result.SchoolTypeAssignments.First().SchoolType!.ShortName.Should().Be("LO");
        }

        [Fact]
        public async Task GetUserByUpnAsync_ShouldReturnCorrectUser()
        {
            // Arrange
            await CleanDatabaseAsync();
            var upn = "unique.user@test.com";
            var user = new User
            {
                Id = Guid.NewGuid().ToString(),
                FirstName = "Unique",
                LastName = "User",
                UPN = upn,
                Role = UserRole.Uczen,
                CreatedBy = "test_user",
                IsActive = true
            };
            await Context.Users.AddAsync(user);
            await Context.SaveChangesAsync();

            // Act
            var result = await _repository.GetUserByUpnAsync(upn);

            // Assert
            result.Should().NotBeNull();
            result!.UPN.Should().Be(upn);
            result.FirstName.Should().Be("Unique");
            result.LastName.Should().Be("User");
        }

        [Theory]
        [InlineData(UserRole.Nauczyciel, 3)]
        [InlineData(UserRole.Uczen, 2)]
        [InlineData(UserRole.Dyrektor, 1)]
        [InlineData(UserRole.Wicedyrektor, 0)]
        public async Task GetUsersByRoleAsync_ShouldReturnMatchingUsers(UserRole role, int expectedCount)
        {
            // Arrange
            await CleanDatabaseAsync();
            var users = new List<User>
            {
                CreateUser("teacher1@test.com", UserRole.Nauczyciel, true),
                CreateUser("teacher2@test.com", UserRole.Nauczyciel, true),
                CreateUser("teacher3@test.com", UserRole.Nauczyciel, true),
                CreateUser("student1@test.com", UserRole.Uczen, true),
                CreateUser("student2@test.com", UserRole.Uczen, true),
                CreateUser("director@test.com", UserRole.Dyrektor, true),
                CreateUser("inactive.teacher@test.com", UserRole.Nauczyciel, false), // nieaktywny
            };

            await Context.Users.AddRangeAsync(users);
            await Context.SaveChangesAsync();

            // Act
            var result = await _repository.GetUsersByRoleAsync(role);

            // Assert
            result.Should().HaveCount(expectedCount);
            result.Should().OnlyContain(u => u.Role == role && u.IsActive);
        }

        [Theory]
        [InlineData("Jan", 3)]
        [InlineData("KOWALSKI", 1)]
        [InlineData("anna", 1)]
        [InlineData("test.com", 5)]
        [InlineData("nieistniejacy", 0)]
        [InlineData("", 5)] // pusty string zwraca wszystkich aktywnych
        [InlineData(null, 5)] // null również zwraca wszystkich aktywnych
        public async Task SearchUsersAsync_ShouldReturnMatchingUsers(string searchTerm, int expectedCount)
        {
            // Arrange
            await CleanDatabaseAsync();
            var users = new List<User>
            {
                CreateUser("jan.kowalski@test.com", "Jan", "Kowalski", true),
                CreateUser("jan.nowak@test.com", "Jan", "Nowak", true),
                CreateUser("anna.kowalska@test.com", "Anna", "Kowalska", true),
                CreateUser("piotr.wisniewski@test.com", "Piotr", "Wiśniewski", true),
                CreateUser("inactive.jan@test.com", "Jan", "Nieaktywny", false), // nieaktywny
            };

            await Context.Users.AddRangeAsync(users);
            await Context.SaveChangesAsync();

            // Act
            var result = await _repository.SearchUsersAsync(searchTerm);

            // Assert
            result.Should().HaveCount(expectedCount);
            if (!string.IsNullOrWhiteSpace(searchTerm))
            {
                result.Should().OnlyContain(u => u.IsActive &&
                    (u.FirstName.ToLower().Contains(searchTerm.ToLower()) ||
                     u.LastName.ToLower().Contains(searchTerm.ToLower()) ||
                     u.UPN.ToLower().Contains(searchTerm.ToLower())));
            }
        }

        [Fact]
        public async Task Update_ShouldModifyUserData()
        {
            // Arrange
            await CleanDatabaseAsync();
            var user = new User
            {
                Id = Guid.NewGuid().ToString(),
                FirstName = "Original",
                LastName = "Name",
                UPN = "original@test.com",
                Role = UserRole.Uczen,
                Phone = "123456789",
                CreatedBy = "test_user",
                IsActive = true
            };
            await Context.Users.AddAsync(user);
            await Context.SaveChangesAsync();

            // Act
            user.FirstName = "Updated";
            user.LastName = "Surname";
            user.Phone = "987654321";
            user.Role = UserRole.Nauczyciel;
            user.MarkAsModified("updater");

            _repository.Update(user);
            await SaveChangesAsync();

            // Assert
            var updatedUser = await Context.Users.FirstOrDefaultAsync(u => u.Id == user.Id);
            updatedUser.Should().NotBeNull();
            updatedUser!.FirstName.Should().Be("Updated");
            updatedUser.LastName.Should().Be("Surname");
            updatedUser.Phone.Should().Be("987654321");
            updatedUser.Role.Should().Be(UserRole.Nauczyciel);
            updatedUser.ModifiedBy.Should().Be("system@teamsmanager.local");
            updatedUser.ModifiedDate.Should().NotBeNull();
        }

        [Fact]
        public async Task Delete_ShouldMarkUserAsInactive()
        {
            // Arrange
            await CleanDatabaseAsync();
            var user = new User
            {
                Id = Guid.NewGuid().ToString(),
                FirstName = "ToDelete",
                LastName = "User",
                UPN = "delete@test.com",
                Role = UserRole.Uczen,
                CreatedBy = "test_user",
                IsActive = true
            };
            await Context.Users.AddAsync(user);
            await Context.SaveChangesAsync();

            // Act
            user.MarkAsDeleted("deleter");
            _repository.Update(user); // używamy Update zamiast Delete dla soft delete
            await SaveChangesAsync();

            // Assert
            var deletedUser = await Context.Users.FirstOrDefaultAsync(u => u.Id == user.Id);
            deletedUser.Should().NotBeNull();
            deletedUser!.IsActive.Should().BeFalse();
            deletedUser.ModifiedBy.Should().Be("system@teamsmanager.local");
            deletedUser.ModifiedDate.Should().NotBeNull();
        }

        #region Helper Methods

        private User CreateUser(string upn, UserRole role, bool isActive)
        {
            var parts = upn.Split('@')[0].Split('.');
            return new User
            {
                Id = Guid.NewGuid().ToString(),
                FirstName = parts.Length > 0 ? parts[0] : "Test",
                LastName = parts.Length > 1 ? parts[1] : "User",
                UPN = upn,
                Role = role,
                CreatedBy = "test_user",
                IsActive = isActive
            };
        }

        private User CreateUser(string upn, string firstName, string lastName, bool isActive)
        {
            return new User
            {
                Id = Guid.NewGuid().ToString(),
                FirstName = firstName,
                LastName = lastName,
                UPN = upn,
                Role = UserRole.Uczen,
                CreatedBy = "test_user",
                IsActive = isActive
            };
        }

        #endregion
    }

}